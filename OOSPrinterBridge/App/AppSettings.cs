using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App
{
    internal class AppSettings
    {
        private readonly string _filename;

        [JsonIgnore]
        public static string Version { get; set; } = string.Empty;

        [JsonIgnore]
        public static string CopyYear { get; set; } = string.Empty;

        [JsonIgnore]
        public static string DataDirectory { get; set; } = string.Empty;

        [JsonConstructor]
        public AppSettings() 
        {
            _filename = string.Empty;
        }

        private AppSettings(string filename)
        {
            _filename = Path.Combine(DataDirectory, filename);
        }

        public static AppSettings Initialize(string filename = "OOSPrintBridge.ooscfg")
        {
            var settings = new AppSettings(Path.Combine(DataDirectory, filename));
            settings.Load();

            return settings;
        }

        public string ClientId { get; set; } = string.Empty;

        public string SiteUrl { get; set; } = string.Empty;

        public PrinterSettings Printer { get; set; } = new PrinterSettings();

        public void Save() => SaveAs(_filename);

        public void SaveAs(string filename)
        {
            File.WriteAllText(filename, JsonSerializer.Serialize(this));
        }

        public void Load()
        {
            if (File.Exists(_filename))
            {
                var fileLines = File.ReadAllLines(_filename);
                var file = string.Join(' ', fileLines);

                if (string.IsNullOrEmpty(file)) return;

                var settings = JsonSerializer.Deserialize<AppSettings>(file);

                ClientId = settings?.ClientId ?? string.Empty;
                SiteUrl = settings?.SiteUrl ?? string.Empty;
                Printer.Ip = settings?.Printer?.Ip ?? string.Empty;
                Printer.Id = settings?.Printer?.Id ?? string.Empty;
                Printer.Port = settings?.Printer?.Port ?? string.Empty;
                Printer.Name = settings?.Printer?.Name ?? string.Empty;
            }
        }
    }
}
