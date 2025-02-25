using OOSPrinterBridge.App;
using OOSPrinterBridge.Menu;
using System.Text;


const string majorVersion = "0";
const string minorVersion = "1";
const string patch = "7";
const string copyYear = "2025";

AppSettings.Version = $"{majorVersion}.{minorVersion}.{patch}";
AppSettings.CopyYear = copyYear;

var home = Environment.GetEnvironmentVariable("HomePath") ?? "";
AppSettings.DataDirectory = Path.Combine(home, "AppData", "Roaming", "Open Order System");

if (!Directory.Exists(AppSettings.DataDirectory))
    Directory.CreateDirectory(AppSettings.DataDirectory);

Console.OutputEncoding = Encoding.Unicode;
Console.Title = $"Open Order System - Printer Bridge {AppSettings.Version}";
Logger.LogStatement($"Open Order System - Printer Bridge {AppSettings.Version}");
Logger.LogStatement($"© JPion Software Solutions - {AppSettings.CopyYear}\n");

Logger.Log("Checking for exsiting configuraton.");

if (File.Exists("appsettings.json"))
{
    Logger.Log("Configuration located.");

    Console.Write("\nPress any key within ");
    var cursor = Console.GetCursorPosition();
    var delay = 10;
    var clearBuffer = "";
    for (int i = 0; i < Console.BufferWidth; ++i) clearBuffer += " ";
    for (int timePassed = 0; timePassed < delay; ++timePassed)
    {
        Console.SetCursorPosition(cursor.Left, cursor.Top);
        Console.Write($"{delay - timePassed}s to modify settings.");

        if (Console.KeyAvailable)
        {
            File.Delete("appsettings.json");

            Console.SetCursorPosition(0, cursor.Top - 1);
            Console.WriteLine(clearBuffer);
            Console.WriteLine(clearBuffer);
            Console.SetCursorPosition(0, cursor.Top - 1);
            Logger.LogWarning("Configuration reset. Loading configuration utility.");
            
            var configMenu = new InitialConfigMenu();
            configMenu.Run();
        }

        Thread.Sleep(1000);
    }

    //clear the message
    Console.SetCursorPosition(0, cursor.Top - 1);
    Console.WriteLine(clearBuffer);
    Console.WriteLine(clearBuffer);
    Console.SetCursorPosition(0, cursor.Top - 1);

    Logger.Log("Loading with printer bridge with existing configuration.");

    var appMain = new PrinterBridgeClientApp();
    appMain.Run();
}
else
{
    Logger.Log("Configuration not found. Beginning initial setup now.", LogLevel.Warn);
    var setupMenu = new InitialConfigMenu();
    setupMenu.Run();
}