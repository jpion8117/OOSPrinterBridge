using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using OOSPrinterBridge.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OOSPrinterBridge.Menu
{
    internal class InitialConfigMenu : MenuBase
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;

        public InitialConfigMenu()
        {
            var buffer = string.Empty;
            for (int j = 0; j < Console.BufferWidth; j++) buffer += '*';
            Console.WriteLine($"\n{buffer}\n");
            _settings = AppSettings.Initialize();
            _settings.ClientId = Guid.NewGuid().ToString();

            _httpClient = new HttpClient();
        }

        public override void Run()
        {
            Console.WriteLine("Welcome to the Open Order System Printer Bridge!");
            Console.WriteLine("This program is used as a digital bridge connecting a local reciept printer to your Open Order " +
                "System cloud installation! The following wizzard will guide you through the setup process. \n");
            Console.Write("Press any key to continue...");
            Console.ReadKey();

            ConfigurePrinter();
            ConfigureSiteConnection().Wait();
            
        }

        private void ConfigurePrinter()
        {
            Clear();
            Console.WriteLine("Printer Info\n");
            var printerIp = Prompt("Printer IP Address");
            var printerPort = Prompt("Printer Port (leave blank for default)");
            printerPort = printerPort == string.Empty ? "9100" : printerPort;
            var printerName = Prompt("Printer name (used to identify this printer)");

            Clear();
            Console.WriteLine("Before continuing, is the following printer information correct?\n");
            Console.WriteLine($"\tIP ADDRESS: {printerIp}");
            Console.WriteLine($"\t      PORT: {printerPort}");
            Console.WriteLine($"\t      NAME: {printerName}\n");
            var confirmation = Prompt("Enter yes to confirm").ToLower();

            if (confirmation != "yes" && confirmation != "y")
                ConfigurePrinter();

            Clear();
            Console.WriteLine("YOUR PRINTER SETTINGS:\n");
            Console.WriteLine($"\tIP ADDRESS: {printerIp}");
            Console.WriteLine($"\t      PORT: {printerPort}");
            Console.WriteLine($"\t      NAME: {printerName}\n");
            confirmation = Prompt("Would you like to test printer before continuing?").ToLower();

            if (confirmation == "yes" || confirmation == "y")
            {
                var printer = new ImmediateNetworkPrinter(new ImmediateNetworkPrinterSettings()
                {
                    ConnectionString = $"{printerIp}:{printerPort}",
                    PrinterName = printerName
                });

                var e = new EPSON();

                printer.WriteAsync(
                    ByteSplicer.Combine(
                        e.CenterAlign(),
                        e.SetStyles(PrintStyle.DoubleHeight),
                        e.PrintLine("Open Order System - Printer Bridge"),
                        e.PrintLine($"VERSION: {AppSettings.Version}"),
                        e.PrintLine($"© JPion Software Solutions - {AppSettings.CopyYear}"),
                        e.LeftAlign(),
                        e.SetStyles(PrintStyle.None),
                        e.FeedLines(1),
                        e.PrintLine($"PRINTER CONFIGURED SUCCESSFULLY!"),
                        e.PrintLine($"\nSettings:"),
                        e.PrintLine($"     IP: {printerIp}"),
                        e.PrintLine($"   PORT: {printerPort}"),
                        e.PrintLine($"   NAME: {printerName}"),
                        e.PartialCutAfterFeed(6)

                    )).Wait();

                Clear();
                confirmation = Prompt("Success?");
                if (confirmation != "yes" && confirmation != "y")
                    ConfigurePrinter();

                Logger.LogInfo("Printer successfully connected!");
                Clear();
            }

            _settings.Printer = new PrinterSettings
            {
                Id = "___NOT_SET___",
                Ip = printerIp,
                Port = printerPort,
                Name = printerName
            };
            _settings.Save();
        }

        private async Task ConfigureSiteConnection()
        {
            Clear();
            Console.WriteLine("Great! Now that we have a printer connected, let's get the printer " +
                "registered on your Open Order System server! How would you like to continue?");
            Console.WriteLine("\nHow would you like to proceed?");
            Console.WriteLine("\t1. Sign in and create a new printer registration.");
            Console.WriteLine("\t2. Use an existing printer registration key.\n");
            var response = Prompt("Your Choice");

            Clear();
            var site = Prompt("OOS Server URL");
            Logger.Log($"Attempting to connect to OOS Server at {site}");

            _httpClient.BaseAddress = new Uri(site);
            var netResp = await _httpClient.GetAsync("API/Print/Ping");
            if (netResp.IsSuccessStatusCode)
            {
                Logger.LogInfo($"Successfully connected to {site}");
                _settings.SiteUrl = site;
                _settings.Save();
            }
            else
            {
                Logger.LogError("Failed to connect to {site} please check the URL and try again.");
                await ConfigureSiteConnection();
            }

            switch (response)
            {
                case "1":
                default:
                    await ConfigureWithLogin(site);
                    break;
                case "2":
                    ConfigureWithKey(site).Wait();
                    break;
            }
        }

        private async Task ConfigureWithLogin(string site)
        {
            Clear();
            var username = Prompt("Username");
            var password = SecurePrompt("Password");

            Logger.LogInfo($"Attempting to log in to {site} using {username}");

            var response = await _httpClient.PostAsync("API/Identity/Login", JsonContent.Create(new
            {
                username,
                password
            }));

            if (response.IsSuccessStatusCode)
            {
                Logger.LogInfo($"Successfully authenticated to {site}");
            }
            else
            {
                Logger.LogError("Login failed!");
                Logger.LogError($"Status: {response.StatusCode} - {response.ReasonPhrase ?? "Unknown error"}");
                await ConfigureWithLogin(site);
            }


            var match = false;
            string pin = "";

            while (!match)
            {
                Clear();

                Console.WriteLine("Before we continue, we need to set a password for the printer incase you ever need to move it to a new bridge. By moving a printer to a new bridge you can avoid causing any disruptions for clients using the printer.");

                pin = SecurePrompt("\nPrinter Password");
                var con = SecurePrompt("\nConfirm Password");

                match = pin == con;
                if (!match) Logger.LogError("Passwords do not match! Please try again.");
            }

            var attempts = 0;
            do
            {
                Logger.LogInfo($"Printer registration begin: attempt {attempts + 1}");
                response = await _httpClient.PostAsync("API/Print/Register", JsonContent.Create(new
                {
                    printerName = _settings.Printer.Name,
                    pin,
                    clientId = _settings.ClientId
                }));

                attempts++;
            } while (!response.IsSuccessStatusCode && attempts < 10);

            if (response.IsSuccessStatusCode)
            {
                var resMsg = await response.Content.ReadAsStringAsync();

                try
                {
                    using (var jsonDoc = JsonDocument.Parse(resMsg))
                    {
                        var root = jsonDoc.RootElement;

                        string msg = root.TryGetProperty("message", out var msgElem)
                            ? msgElem.ToString() : string.Empty;

                        string printerId = root.TryGetProperty("printerId", out var printerIdElem)
                            ? printerIdElem.ToString() : string.Empty;

                        Logger.LogInfo(msg);

                        _settings.Printer.Id = printerId;
                        _settings.Save();

                        Logger.LogInfo("Site configured successfully!");
                    }
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex.Message);
                }
            }
            else
            {
                Logger.LogError("Login failed!");
                Logger.LogError($"Status: {response.StatusCode} - {response.ReasonPhrase ?? "Unknown error"}");
                Logger.LogError("Setup aborted!");
                if (File.Exists("appsettings.json"))
                    File.Delete("appsettings.json");
            }
        }

        private async Task ConfigureWithKey(string site)
        {
            Clear();
            var id = Prompt("Printer Key");
            var pin = SecurePrompt("Pin");

            Logger.LogInfo($"Attempting to locate printer key at {site} and validate with pin");

            var client = new HttpClient();
            client.BaseAddress = new Uri(site);

            var attempts = 0;
            HttpResponseMessage? response = null;
            while (attempts < 5 && !(response?.IsSuccessStatusCode ?? false))
            {
                Logger.LogInfo($"Attempting to locate printer key at {site} and validate with pin. Attempt #{attempts + 1}");

                response = await client.PutAsync("API/Print/Register", JsonContent.Create(new
                {
                    printerId = id,
                    pin = pin,
                    printerName = _settings.Printer.Name,
                    clientId = _settings.ClientId
                }));

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError($"Error response from OOS server {response.StatusCode}: {response.RequestMessage}");
                }

                attempts++;
            }

            if (attempts > 5)
            {
                Logger.LogError("Failed to link printer. Aborting setup.");
                Clear();
                return;
            }

            _settings.Printer.Id = id;
            _settings.Save();

            Logger.LogInfo("Successfully linked printer!");
            Clear();
        }
    }
}
