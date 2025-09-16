using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DB_COURSE_WORK;
using Neo4j.Driver;

class Program
{
    // Neo4j connection settings
    private const string Uri      = "bolt://localhost:7687";
    private const string User     = "neo4j";
    private const string Password = "qweasdzxc";

    private static IDriver _driver;
    private static GraphService _graphService;

    static async Task Main(string[] args)
    {
        try
        {
            _driver = GraphDatabase.Driver(Uri, AuthTokens.Basic(User, Password));
            _graphService = new GraphService(_driver);

            bool exit = false;
            while (!exit)
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("=== Меню приложения ===");
                    Console.WriteLine("1. Сгенерировать граф");
                    Console.WriteLine("2. Запустить поиск сообществ (Louvain)");
                    Console.WriteLine("3. Удалить граф из БД");
                    Console.WriteLine("0. Выход");
                    Console.Write("Выберите пункт: ");

                    switch (Console.ReadLine()?.Trim())
                    {
                        case "1":
                            await MenuGenerateGraph();
                            break;
                        case "2":
                            await MenuRunLouvain();
                            break;
                        case "3":
                            await MenuDeleteGraph();
                            break;
                        case "0":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("Неверный выбор. Нажмите любую клавишу...");
                            Console.ReadKey();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n Что-то пошло не так: {ex.Message}");
                    Console.WriteLine("Нажмите любую клавишу для продолжения...");
                    Console.ReadKey();
                }
            }

            await _driver.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nКритическая ошибка при запуске: {ex.Message}");
        }
    }

    
private static async Task MenuGenerateGraph()
{
    Console.Clear();
    Console.WriteLine("=== Генерация графа ===");

    Console.Write("Число сообществ (например, 10): ");
    var nc = int.Parse(Console.ReadLine()!);

    Console.Write("Узлов в сообществе (например, 100): ");
    var npc = int.Parse(Console.ReadLine()!);

    Console.Write("Плотность Intra (0.0–1.0): ");
    var intraInput = Console.ReadLine()!;
    var intra = double.Parse(intraInput, CultureInfo.InvariantCulture);

    Console.Write("Плотность Inter (0.0–1.0): ");
    var interInput = Console.ReadLine()!;
    var inter = double.Parse(interInput, CultureInfo.InvariantCulture);

    var param = new GenParams
    {
        NumberOfCommunities   = nc,
        NodesPerCommunity     = npc,
        IntraCommunityDensity = intra,
        InterCommunityDensity = inter
    };

    Console.WriteLine("\nГенерация графа...");
    await GenGraph(param);
    Console.WriteLine("Граф успешно сгенерирован. Нажмите любую клавишу...");
    Console.ReadKey();
}


private static async Task MenuRunLouvain()
{
    Console.Clear();
    Console.WriteLine("=== Запуск алгоритма Louvain ===");

    await using var driver = GraphDatabase.Driver(Uri, AuthTokens.Basic(User, Password));
    var graphService = new GraphService(driver);

    var alg = new LouvainAlg(graphService);
    Console.WriteLine("Инициализация...");
    await alg.AlgInit();
    Console.WriteLine("Выполнение...");
    await alg.ExecuteLouvain();

    Console.WriteLine($"Алгоритм завершён. Найдено сообществ: {alg.GetCommunityCount()}");
    Console.WriteLine("Нажмите любую клавишу...");
    Console.ReadKey();

    await driver.CloseAsync();
}

    private static void MenuShowCount()
    {
        Console.Clear();
        Console.WriteLine("=== Количество сообществ ===");

        var alg = new LouvainAlg(_graphService);
        int count = alg.GetCommunityCount();
        Console.WriteLine($"Найдено сообществ: {count}");

        Console.WriteLine("\nНажмите любую клавишу...");
        Console.ReadKey();
    }

    private static async Task MenuDeleteGraph()
    {
        Console.Clear();
        Console.WriteLine("=== Удаление графа из БД ===");
        await _graphService.DeleteGraph();
        Console.WriteLine("Граф удалён. Нажмите любую клавишу...");
        Console.ReadKey();
    }

    private static async Task GenGraph(GenParams param)
    {
        var generator = new GraphGenerator(_driver, param);
        await generator.GenerateGraph();
    }
}
