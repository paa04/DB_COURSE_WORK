using DB_COURSE_WORK;
using Neo4j.Driver;

namespace TestProject1;

using Antlr4.Runtime.Misc;

public class GraphServiceTests
{
    // Neo4j connection settings
    private static readonly string uri = "bolt://localhost:7687";
    private static readonly string user = "neo4j";
    private static readonly string password = "qweasdzxc";

    private static readonly IDriver Driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));

    [Fact]
    public async Task CreateSuperNodeSaveTest()
    {
        var vertices = new List<int>() { 1, 2, 3, 4, 5 };
        var edges = new List<Pair<int, int>>()
            { new Pair<int, int>(1, 1), new Pair<int, int>(2, 2), new Pair<int, int>(3, 3), new Pair<int, int>(4, 4) };

        GraphService graphService = new GraphService(Driver);

        await graphService.CreateSuperNodeSave(0, vertices, edges, 1);

        var edges2 = new List<Pair<int, int>>() { new Pair<int, int>(0, 1), new Pair<int, int>(2, 2) };

        await graphService.CreateSuperNodeSave(1, vertices, edges2, 1);

        //await graphService.DeleteLevel(1);
    }

    [Fact]
    public async Task TagPersonsTest()
    {
        GraphService graphService = new GraphService(Driver);

        await graphService.TagPersons(2);
    }
}