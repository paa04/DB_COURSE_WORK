// See https://aka.ms/new-console-template for more information

using DB_COURSE_WORK;
using Neo4j.Driver;


class Program
{
    // Neo4j connection settings
    private static readonly string uri = "bolt://localhost:7687";
    private static readonly string user = "neo4j";
    private static readonly string password = "qweasdzxc"; // Изменить на свой пароль

    private static readonly IDriver Driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));

    private static readonly GraphService graphService = new GraphService(Driver);

    static async Task Main(string[] args)
    {
        // var alg = new LeidenAlg(graphService);

        // await alg.AlgInit();

        // await alg.ExecuteLeiden();

        // var param = new GenParams();

        // await GenGraph(param);

        // var result = await graphService.GetAllNeighboursId(5);
        //
        // foreach (var res in result)
        // {
        //     Console.WriteLine(res);
        // }

        // Console.WriteLine(await graphService.GetPowerOfNode(5));

        // var alg = new LeidenAlg(graphService);
        //
        // await alg.LoadGraph();
        //
        // await alg.ExecuteLeiden();

        await RunSparsityStudy();
    }

    public static async Task RunSparsityStudy()
    {
        var intraDensities = new double[] { 0.2, 0.3, 0.4, 0.5, 0.6 };
        var interDensities = new double[] { 0.005, 0.01, 0.05, 0.1, 0.15 };

        for (var i = 0; i < 5; i++)
            for(int j = 0; j < 5; j++)
        {
            var intra = intraDensities[i];
            var inter = interDensities[j];

            Console.WriteLine($"\n🔹 Тест: Intra = {intra}, Inter = {inter}");

            using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
            var graphService = new GraphService(driver);
            var param = new GenParams
            {
                NumberOfCommunities = 10,
                NodesPerCommunity = 100,
                IntraCommunityDensity = intra,
                InterCommunityDensity = inter
            };

            await GenGraph(driver, param);

            var alg = new LeidenAlg(graphService);
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

    private static async Task TestConnection(IDriver driver)
    {
        await using var session = driver.AsyncSession();
        var result = await session.RunAsync("RETURN 'Соединение с Neo4j установлено!' AS message");
        var record = await result.SingleAsync();
        Console.WriteLine(record["message"].As<string>());
        Console.WriteLine();
    }
}