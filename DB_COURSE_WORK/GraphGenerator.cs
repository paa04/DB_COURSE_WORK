using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdaptiveExpressions.TriggerTrees;

namespace DB_COURSE_WORK;

public class GenParams
{
    public int NumberOfCommunities { get; set; } = 5;
    public int NodesPerCommunity { get; set; } = 20;
    public double IntraCommunityDensity { get; set; } = 0.7; 
    public double InterCommunityDensity { get; set; } = 0.05;
}

public class GraphGenerator
{
    private readonly IDriver _driver;
    private readonly Random _random;

    // Настройки генерации графа
    public int NumberOfCommunities { get; set; } = 5;
    public int NodesPerCommunity { get; set; } = 20;
    public double IntraCommunityDensity { get; set; } = 0.7; // Вероятность связи внутри сообщества
    public double InterCommunityDensity { get; set; } = 0.05; // Вероятность связи между сообществами

    public GraphGenerator(IDriver driver, GenParams param)
    {
        _driver = driver;
        _random = new Random();
        
        NumberOfCommunities = param.NumberOfCommunities;
        NodesPerCommunity = param.NodesPerCommunity;
        IntraCommunityDensity = param.IntraCommunityDensity;
        InterCommunityDensity = param.InterCommunityDensity;
    }

    /// <summary>
    /// Генерирует граф с явным разбиением на сообщества и сохраняет его в Neo4j
    /// </summary>
    public async Task GenerateGraph()
    {
        // Console.WriteLine(
            // $"Начало генерации графа с {NumberOfCommunities} сообществами, по {NodesPerCommunity} узлов в каждом...");

        // Создаем структуру сообществ и узлов
        var communities = new Dictionary<int, List<int>>();
        var nodeToComm = new Dictionary<int, int>();

        int nodeIdCounter = 0;

        // Создаем узлы и распределяем их по сообществам
        for (int commId = 0; commId < NumberOfCommunities; commId++)
        {
            communities[commId] = new List<int>();

            for (int i = 0; i < NodesPerCommunity; i++)
            {
                int nodeId = nodeIdCounter++;
                communities[commId].Add(nodeId);
                nodeToComm[nodeId] = commId;
            }
        }

        // Очищаем существующие данные в БД
        await ClearDatabase();

        // Создаем узлы в Neo4j
        await using (var session = _driver.AsyncSession())
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                for (int nodeId = 0; nodeId < nodeIdCounter; nodeId++)
                {
                    int commId = nodeToComm[nodeId];
                    await tx.RunAsync(
                        "CREATE (p:Person {id: $nodeId, name: $name, level: 0})",
                        new { nodeId, name = $"Person_{nodeId}", commId }
                    );
                }
            });
        }

        // Console.WriteLine($"Создано {nodeIdCounter} узлов в Neo4j.");

        // Создаем связи между узлами
        var edgesToCreate = new List<(int source, int target)>();

        // Связи внутри сообществ (высокая плотность)
        foreach (var commId in communities.Keys)
        {
            var nodes = communities[commId];

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (_random.NextDouble() < IntraCommunityDensity)
                    {
                        edgesToCreate.Add((nodes[i], nodes[j]));
                    }
                }
            }
        }

        // Связи между сообществами (низкая плотность)
        for (int commId1 = 0; commId1 < NumberOfCommunities; commId1++)
        {
            for (int commId2 = commId1 + 1; commId2 < NumberOfCommunities; commId2++)
            {
                foreach (var node1 in communities[commId1])
                {
                    foreach (var node2 in communities[commId2])
                    {
                        if (_random.NextDouble() < InterCommunityDensity)
                        {
                            edgesToCreate.Add((node1, node2));
                        }
                    }
                }
            }
        }

        // Console.WriteLine($"Сгенерировано {edgesToCreate.Count} связей.");

        // Создаем связи пакетами для оптимизации
        const int batchSize = 1000;
        for (int i = 0; i < edgesToCreate.Count; i += batchSize)
        {
            var batch = edgesToCreate.Skip(i).Take(batchSize).ToList();
            await CreateEdgeBatch(batch);
        }

        // await CreateCommunities(communities);

        // Console.WriteLine("Граф успешно сгенерирован в Neo4j!");
        PrintGraphStatistics(communities, edgesToCreate);
    }

    /// <summary>
    /// Создает пакет связей между узлами
    /// </summary>
    private async Task CreateEdgeBatch(List<(int source, int target)> edges)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            var parameters = new
            {
                edges = edges.Select(e => new { source = e.source, target = e.target }).ToList()
            };

            await tx.RunAsync(@"
                    UNWIND $edges AS edge
                    MATCH (a:Person {id: edge.source})
                    MATCH (b:Person {id: edge.target})
                    CREATE (a)-[:FRIENDS_WITH {weight: 1}]->(b)
                    CREATE (b)-[:FRIENDS_WITH {weight: 1}]->(a)", parameters);
        });
    }

    /// <summary>
    /// Создает узлы сообществ и связывает их с узлами
    /// </summary>
    private async Task CreateCommunities(Dictionary<int, List<int>> communities)
    {
        await using var session = _driver.AsyncSession();

        // Создаем узлы сообществ
        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var commId in communities.Keys)
            {
                await tx.RunAsync(
                    "CREATE (c:Community {id: $commId, name: $name, size: $size})",
                    new
                    {
                        commId,
                        name = $"Community_{commId}",
                        size = communities[commId].Count
                    }
                );
            }
        });

        // Создаем связи между узлами и их сообществами
        foreach (var commId in communities.Keys)
        {
            var nodeIds = communities[commId];

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(@"
                        MATCH (c:Community {id: $commId})
                        MATCH (p:Person) WHERE p.id IN $nodeIds
                        CREATE (p)-[:BELONGS_TO]->(c)",
                    new { commId, nodeIds }
                );
            });
        }
    }

    /// <summary>
    /// Очищает базу данных от существующих данных
    /// </summary>
    private async Task ClearDatabase()
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx => { await tx.RunAsync("MATCH (n) DETACH DELETE n"); });
    }

    /// <summary>
    /// Создает индексы для оптимизации производительности
    /// </summary>
    public async Task CreateIndexes()
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("CREATE INDEX IF NOT EXISTS FOR (p:Person) ON (p.id)");
            await tx.RunAsync("CREATE INDEX IF NOT EXISTS FOR (c:Community) ON (c.id)");
        });
    }

    /// <summary>
    /// Выводит статистику сгенерированного графа
    /// </summary>
    private void PrintGraphStatistics(Dictionary<int, List<int>> communities, List<(int, int)> edges)
    {
        int totalNodes = communities.Values.Sum(c => c.Count);
        int intraCommunityEdges = 0;
        int interCommunityEdges = 0;

        foreach (var (source, target) in edges)
        {
            if (nodeToComm[source] == nodeToComm[target])
                intraCommunityEdges++;
            else
                interCommunityEdges++;
        }

        // Console.WriteLine("\nСтатистика сгенерированного графа:");
        // Console.WriteLine($"Всего узлов: {totalNodes}");
        // Console.WriteLine($"Всего рёбер: {edges.Count}");
        // Console.WriteLine($"Рёбер внутри сообществ: {intraCommunityEdges}");
        // Console.WriteLine($"Рёбер между сообществами: {interCommunityEdges}");
        // Console.WriteLine($"Процент рёбер внутри сообществ: {(double)intraCommunityEdges / edges.Count:P2}");
        // Console.WriteLine($"Процент рёбер между сообществами: {(double)interCommunityEdges / edges.Count:P2}");

        // Console.WriteLine("\nРазмеры сообществ:");
        // foreach (var commId in communities.Keys)
        // {
            // Console.WriteLine($"Сообщество {commId}: {communities[commId].Count} узлов");
        // }
    }

    private Dictionary<int, int> nodeToComm => Enumerable.Range(0, NumberOfCommunities * NodesPerCommunity)
        .ToDictionary(
            nodeId => nodeId,
            nodeId => nodeId / NodesPerCommunity
        );
}