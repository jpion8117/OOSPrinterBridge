using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using OOSPrinterBridge.App.Commands;
using OOSPrinterBridge.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace OOSPrinterBridge.App
{
    internal class PrinterBridgeClientApp : MenuBase, IInteruptable, IIntervalable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private readonly CommandManager _commandManager;
        private readonly NetworkPrinter? _printer;
        private PrinterStatusEventArgs _printerStatus = new PrinterStatusEventArgs();
        private string _input = "";
        private bool _ready = false;
        private bool _running = true;
        private DateTime _lastCheck;
        private Dictionary<string, int> _connectFailCounter = new Dictionary<string, int>
        {
            { "Server", 0 },
            { "Printer", 0 }
        };

        public PrinterBridgeClientApp()
        {
            _settings = AppSettings.Initialize();
            _httpClient = new HttpClient();
            _commandManager = new CommandManager();

            _commandManager.RegisterCommand(new StopCommand(this));
            _commandManager.RegisterCommand(new SetIntervalCommand(nameof(RefreshInterval), this, "set-refresh"));

            _ready = CheckPrinterConfig() && CheckValidClientId() && CheckValidSiteUrl();

            if (_ready)
            {
                _httpClient.BaseAddress = new Uri(_settings.SiteUrl);
                _printer = new NetworkPrinter(new NetworkPrinterSettings
                {
                    ConnectionString = $"{_settings.Printer.Ip}:{_settings.Printer.Port}",
                    PrinterName = _settings.Printer.Name
                });

                _printer.StatusChanged += (sender, args) =>
                {
                    _printerStatus = (PrinterStatusEventArgs)args;
                };

                _printer.Connected += (sender, args) =>
                {
                    _printerStatus.IsPrinterOnline = true;
                };

                _printer.Disconnected += (sender, args) =>
                {
                    _printerStatus.IsPrinterOnline = false;
                };
            }
        }

        public double RefreshInterval => Intervals.ContainsKey(nameof(RefreshInterval)) ?
            Intervals[nameof(RefreshInterval)] : 10;

        public Dictionary<string, double> Intervals { get; protected set; } = new Dictionary<string, double>();

        public override void Run()
        {
            if (!_ready) Logger.LogError("System was unable to start.");

            while (_ready && _running)
            {
                if (Console.KeyAvailable)
                {
                    var keyPress = Console.ReadKey();

                    if (keyPress.Key == ConsoleKey.Backspace)
                    {
                        if (_input.Length > 0)
                        {
                            _input = _input.Remove(_input.Length - 1);
                        }
                    }
                    else if (keyPress.Key == ConsoleKey.Enter)
                    {
                        if (!_commandManager.CheckCommandTrigger(_input))
                            Logger.LogError($"Invalid command: {_input}");

                        _input = "";
                    }
                    else
                        _input += keyPress.KeyChar;

                    Clear();
                }

                if (_lastCheck.AddSeconds(RefreshInterval) < DateTime.UtcNow)
                {
                    _lastCheck = DateTime.UtcNow;
                    CheckPrintQueue().Wait();
                }
            }

            Clear();
            Logger.LogInfo("System is shutting down.");
            Console.WriteLine("Printer bridge offline. Press any key to close...");
            Console.ReadKey();
        }

        public void Stop() => _running = false;

        protected override void Clear()
        {
            Console.Clear();
            Logger.RefreshLog();
            Console.Write($"> {_input}");
        }

        private async Task<PrinterStatusEventArgs?> GetPrinterStatusAsync()
        {
            var status = new PrinterStatusEventArgs();

            try
            {
                using (var client = new TcpClient())
                {
                    // Try to connect within 3 seconds
                    var connectTask = client.ConnectAsync(_settings.Printer.Ip, int.Parse(_settings.Printer.Port));
                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    {
                        throw new TimeoutException("Printer connection timed out.");
                    }

                    using (var stream = client.GetStream())
                    {
                        // Send status request command (DLE EOT 1)
                        byte[] statusCommand = new byte[] { 0x10, 0x04, 0x01 };
                        await stream.WriteAsync(statusCommand, 0, statusCommand.Length);

                        // Read response
                        byte[] response = new byte[1];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);

                        if (bytesRead > 0)
                        {
                            Console.WriteLine("Printer Status: " + BitConverter.ToString(response));
                        }
                        else
                        {
                            Console.WriteLine("No response from printer.");
                        }
                    }
                }
            }
            catch (SocketException)
            {
                Logger.LogError("Error: Could not reach the printer. Check network connection.");
                return null;
            }
            catch (TimeoutException)
            {
                Logger.LogError("Error: Printer connection timed out.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error: " + ex.Message);
                return null;
            }

            return status;
        }

        private bool CheckPrinterConfig()
        {
            Logger.LogInfo("Verifying valid printer configuration.");

            var printerLoaded = 
                !string.IsNullOrEmpty(_settings.Printer.Id) && 
                !string.IsNullOrEmpty(_settings.Printer.Ip) &&
                !string.IsNullOrEmpty(_settings.Printer.Port) &&
                !string.IsNullOrEmpty(_settings.Printer.Name);
            
            if (!printerLoaded)
            {
                Logger.LogError($"Invalid printer settings");
                return false;
            }

            Logger.LogInfo("Printer configuration valid.");
            return true;
        }

        private async Task CheckPrintQueue()
        {
            HttpResponseMessage? result = null;

            try
            {
                result = await _httpClient.PutAsync("API/Print/CheckIn", JsonContent.Create(new
                {
                    clientId = _settings.ClientId,
                    printerId = _settings.Printer.Id,
                    status = _printerStatus
                }));
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"Attempt {_connectFailCounter["Server"]} - Unable to reach OOS Server: {ex.Message}");
                _connectFailCounter["Server"]++;
                return;
            }
            catch (TimeoutException)
            {
                Logger.LogError("Error: Printer did not respond in time.");
            }
            catch (IOException ex)
            {
                Logger.LogError("Error: IO issue - " + ex.Message);
            }
            catch (SocketException ex)
            {
                Logger.LogError("Network error: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogError("Permission error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error: " + ex.Message);
            }

            if (result != null && result.IsSuccessStatusCode)
            {
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var payload = await result.Content.ReadAsStringAsync();
                    var jobs = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
                    Logger.LogInfo($"Retrieved {jobs?.Count} print jobs from {_settings.SiteUrl}.");
                    foreach (var job in jobs ?? new Dictionary<string, string>())
                    {
                        Logger.LogInfo($"Processing job: {job.Key} on printer {_settings.Printer.Name}");

                        var instructions = Convert.FromHexString(job.Value);

                        //var printer = new ImmediateNetworkPrinter(new ImmediateNetworkPrinterSettings()
                        //{
                        //    ConnectionString = $"{_settings.Printer.Ip}:{_settings.Printer.Port}",
                        //    PrinterName = _settings.Printer.Name
                        //});

                        _printer?.Write(instructions);
                        

                        await _httpClient.PutAsync("API/Print/CompleteJob", JsonContent.Create(new
                        {
                            jobId = job.Key,
                            clientId = _settings.ClientId,
                            printerId = _settings.Printer.Id
                        }));
                    }
                }
            }
            else
            {
                Logger.LogError($"Failed to retrieve print queue from server.");
                Logger.LogError($"Status Code: {result?.StatusCode} - {result?.ReasonPhrase}");
            }
        }

        public void PrinterStatusChecker(object sender, EventArgs ps)
        {

        }

        private bool CheckValidClientId() => !string.IsNullOrEmpty(_settings.ClientId);

        private bool CheckValidSiteUrl() => !string.IsNullOrEmpty(_settings.SiteUrl);
    }
}
