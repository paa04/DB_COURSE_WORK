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

    // [Fact]
    // public async Task TagPersonsTest()
    // {
    //     GraphService graphService = new GraphService(Driver);
    //
    //     await graphService.TagPersons(2);
    // }

    [Fact]
    public async Task GlobalTesting()
    {
        var intraDensities = new double[] { 0.2 };
        var interDensities = new double[] { 0.00};

        for (var i = 0; i < 1; i++)
        for(int j = 0; j < 1; j++)
        {
            var intra = intraDensities[i];
            var inter = interDensities[j];
            
            using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
            var graphService = new GraphService(driver);
            var param = new GenParams
            {
                NumberOfCommunities = 10,
                NodesPerCommunity = 10,
                IntraCommunityDensity = intra,
                InterCommunityDensity = inter
            };

            await GenGraph(driver, param);

            var alg = new LouvainAlg(graphService);
            await alg.AlgInit();
            await alg.ExecuteLeiden();

            Console.WriteLine(alg.GetCommunityCount());
            await graphService.DeleteGraph();
            await driver.CloseAsync();
        }
    }
    
    private static async Task GenGraph(IDriver driver, GenParams param)
    {
        GraphGenerator graphGenerator = new GraphGenerator(driver, param);

        await graphGenerator.GenerateGraph();
    }

}