using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdaptiveExpressions;
using Antlr4.Runtime.Misc;

namespace DB_COURSE_WORK;

public class GraphService
{
    private readonly IDriver _driver;

    private readonly LRUCache<Pair<int, int>, List<Pair<int, int>>> _neighborCache =
        new LRUCache<Pair<int, int>, List<Pair<int, int>>>(1000);

    public GraphService(IDriver driver)
    {
        _driver = driver;
    }


    public async Task<(Dictionary<int, List<int>> adjacencyList, Dictionary<int, double> nodeWeights)> GetSubgraph(
        string label = "Person", int? limit = null)
    {
        var adjacencyList = new Dictionary<int, List<int>>();
        var nodeWeights = new Dictionary<int, double>();

        await using var session = _driver.AsyncSession();

        var query = $"MATCH (n:{label}) ";
        if (limit.HasValue)
            query += $"LIMIT {limit} ";
        query += "RETURN id(n) AS id";

        var nodeResult = await session.RunAsync(query);
        await foreach (var record in nodeResult)
        {
            var nodeId = record["id"].As<int>();
            adjacencyList[nodeId] = new List<int>();
            nodeWeights[nodeId] = 1.0; // Базовый вес
        }

        query = $"MATCH (n:{label})-[r]->(m:{label}) ";
        if (limit.HasValue)
            query += $"WHERE id(n) IN $nodeIds AND id(m) IN $nodeIds ";
        query += "RETURN id(n) AS source, id(m) AS target, type(r) AS type";

        var edgeResult = await session.RunAsync(query,
            limit.HasValue ? new { nodeIds = adjacencyList.Keys.ToArray() } : null);

        await foreach (var record in edgeResult)
        {
            var source = record["source"].As<int>();
            var target = record["target"].As<int>();

            if (adjacencyList.ContainsKey(source))
                adjacencyList[source].Add(target);
        }

        return (adjacencyList, nodeWeights);
    }

    public async Task SaveCommunities(Dictionary<int, int> nodeCommunities)
    {
        await using var session = _driver.AsyncSession();

        var communities = nodeCommunities.Values.Distinct().ToList();
        foreach (var communityId in communities)
        {
            await session.RunAsync(@"
                MERGE (c:Community {id: $communityId})
                SET c.size = $size, c.lastUpdated = datetime()",
                new
                {
                    communityId,
                    size = nodeCommunities.Count(nc => nc.Value == communityId)
                }
            );
        }

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                UNWIND $assignments AS assignment
                MATCH (p) WHERE id(p) = assignment.nodeId
                MATCH (c:Community {id: assignment.communityId})
                MERGE (p)-[:BELONGS_TO]->(c)",
                new
                {
                    assignments = nodeCommunities.Select(nc =>
                        new
                        {
                            nodeId = nc.Key,
                            communityId = nc.Value
                        }).ToList()
                }
            );
        });
    }

    public async Task<Dictionary<int, int>> GetExistingCommunities(string nodeLabel = "Person")
    {
        var nodeCommunities = new Dictionary<int, int>();

        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(@"
            MATCH (n:" + nodeLabel + ")-[:BELONGS_TO]->(c:Community) RETURN id(n) AS nodeId, c.id AS communityId");

        await foreach (var record in result)
        {
            var nodeId = record["nodeId"].As<int>();
            var communityId = record["communityId"].As<int>();
            nodeCommunities[nodeId] = communityId;
        }

        return nodeCommunities;
    }

    public async Task CreateIndexes()
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync("CREATE INDEX IF NOT EXISTS FOR (n:Person) ON (n.id)");
        await session.RunAsync("CREATE INDEX IF NOT EXISTS FOR (c:Community) ON (c.id)");
    }

    public async Task<List<Pair<int, int>>> GetAllNeighboursIdByLevel(int NodeId, int level)
    {
        if (_neighborCache.TryGet(new Pair<int, int>(NodeId, level), out var cachedNeighbors))
        {
            return cachedNeighbors;
        }

        var resultList = new List<Pair<int, int>>();

        await using var session = _driver.AsyncSession();

        var query =
            "MATCH (p:Person {id: $id, level: $level})-[r:FRIENDS_WITH]->(f:Person {level: $level}) RETURN f.id, r.weight";
        var parameters = new { id = NodeId, level };

        try
        {
            var result = await session.RunAsync(query, parameters);
            var records = await result.ToListAsync();

            foreach (var record in records)
            {
                var id = record[0].As<int>();
                var weight = record[1].As<int>();
                resultList.Add(new Pair<int, int>(id, weight));
            }

            _neighborCache.Set(new Pair<int, int>(NodeId, level), resultList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }

        return resultList;
    }

    public async Task<List<int>> GetAllPersonsIdByLevel(int level)
    {
        var resultList = new List<int>();

        await using var session = _driver.AsyncSession();

        try
        {
            var query = @"MATCH (n:Person {level: $level}) RETURN n.id";
            var param = new { level };
            var result = await session.RunAsync(query, param);
            var records = await result.ToListAsync();


            foreach (var record in records)
            {
                var value = record[0].As<int>();
                resultList.Add(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }

        return resultList;
    }

    public async Task<int> GetVCountForLevel(int level)
    {
        await using var session = _driver.AsyncSession();

        var query = @"MATCH (p:Person {level: $level}) WITH count(1) AS cnt return cnt";
        var parameters = new { level };

        var result = await session.RunAsync(query, parameters);
        var res = await result.SingleAsync();

        return res["cnt"].As<int>();
    }

    public async Task<int> GetPowerOfNodeByLevel(int NodeId, int level)
    {
        if (_neighborCache.TryGet(new Pair<int, int>(NodeId, level), out var cashedList))
            return cashedList.Count;

        await using var session = _driver.AsyncSession();
        var query =
            @"MATCH (p:Person {id: $id, level: $level})-[r:FRIENDS_WITH]->(:Person {level: $level}) WITH sum(r.weight) as dev  RETURN dev";
        var parameters = new { id = NodeId, level };

        var result = await session.RunAsync(query, parameters);
        var record = await result.SingleAsync();

        return record["dev"].As<int>();
    }

    public async Task<int> GetGraphWeightByLevel(int level)
    {
        await using var session = _driver.AsyncSession();

        var query =
            @"MATCH (p:Person {level: $level})-[r]-(z:Person) WITH sum(r.weight) as sum_weight return sum_weight";
        var parameters = new { level };

        var result = await session.RunAsync(query, parameters);
        var record = await result.SingleAsync();

        return record["sum_weight"].As<int>();
    }

    public async Task CreateSuperNodeSave(int superNodeId, List<int> vertices, List<Pair<int, int>> edges, int level)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            var query = @"
            MERGE (sn: Person { id: $superNodeId, level: $level})
                ON CREATE SET
                sn.members = $vertices
                ON MATCH SET
                sn.members = $vertices
        ";
            var parameters = new
            {
                superNodeId,
                level,
                vertices
            };
            await tx.RunAsync(query, parameters);

            foreach (var edge in edges)
            {
                query = @"MERGE (sn: Person {id: $superNodeId, level: $level})";
                var parameters2 = new { superNodeId = edge.a, level };
                await tx.RunAsync(query, parameters2);
            }

            foreach (var edge in edges)
            {
                query = @"MATCH (p1:Person {id: $id1, level : $level})
                        MATCH (p2: Person {id: $id2, level : $level})
                        MERGE (p1) - [:FRIENDS_WITH {weight : $weight}] -> (p2)
                        MERGE (p2) - [:FRIENDS_WITH {weight : $weight}] -> (p1)";
                var parameters3 = new { id1 = superNodeId, id2 = edge.a, level, weight = edge.b };

                await tx.RunAsync(query, parameters3);
            }
        });
    }

    public async Task DeleteLevel(int level)
    {
        await using var session = _driver.AsyncSession();
        var query = @"MATCH (n: Person {level: $level}) DETACH DELETE (n)";
        var param = new { level };
        await session.RunAsync(query, param);
    }

    public async Task FormCommunityNodes(int maxLevel)
    {
        await using var session = _driver.AsyncSession();
        for (int level = maxLevel; level > 0; --level)
        {
            var query = @"MATCH (p: Person {level: $level})
                          REMOVE p:Person
                          SET p:Community";
            var param = new { level };

            await session.RunAsync(query, param);
        }
    }

    public async Task TagPersons(int maxLevel)
    {
        TagStrategy tagStrategy = new TagStrategy(_driver);

        await using var session = _driver.AsyncSession();

        var query = @"MATCH (c: Community {level: $level}) return c.id as id";
        var param = new { level = maxLevel };

        var topCommunities = await session.RunAsync(query, param);

        var topLst = await topCommunities.ToListAsync();

        var topIds = topLst.Select(r => r["id"].As<int>()).ToList();

        TagAlg tagAlg = new TagAlg(tagStrategy, topIds, maxLevel);

        await tagAlg.DoTag();
    }
}

public class TagStrategy : IGetChildrens
{
    private readonly IDriver _driver;

    public TagStrategy(IDriver driver)
    {
        _driver = driver;
    }

    public async Task<List<int>> GetChild(int id, int level)
    {
        await using var session = _driver.AsyncSession();

        var query = @"MATCH (c:Community {id:$id, level:$level}) RETURN c.members as members";
        var parameters = new { id, level };

        var result = await session.RunAsync(query, parameters);

        var resLst = await result.ToListAsync();

        return resLst
            .SelectMany(r => r["members"].As<List<object>>())
            .Select(x => Convert.ToInt32(x))
            .ToList();
    }

    public async Task Tag(int id, List<int> tags)
    {
        await using var session = _driver.AsyncSession();

        var query = @"MATCH (p:Person {id:$id}) SET p.communityId = $tags";
        var parameters = new { id, tags = tags[0] };

        await session.RunAsync(query, parameters);
    }
}