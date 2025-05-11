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
        var alg = new LeidenAlg(graphService);

        await alg.AlgInit();

        await alg.ExecuteLeiden();
        
        // await GenGraph();

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
    }

    private static async Task GenGraph()
    {
        GraphGenerator graphGenerator = new GraphGenerator(Driver);

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