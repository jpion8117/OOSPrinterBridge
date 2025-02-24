using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App.Commands
{
    internal class SetIntervalCommand : ICommand
    {
        private readonly string _key;
        private readonly IIntervalable _target;

        public SetIntervalCommand(string key, IIntervalable target, string trigger)
        {
            _key = key;
            _target = target;
            Trigger = trigger;
        }

        public string Trigger { get; set; }

        public void Execute(params string[] args)
        {
            if (args.Length == 0 || !double.TryParse(args[0], out var interval))
            {
                Logger.LogError($"Invalid Command Format: [{Trigger}] [interval] where interval is the number " +
                    $"of seconds between requests to the server.");
                return;
            }

            _target.Intervals[_key] = interval * 1000;
            Logger.LogInfo($"Interval updated to: {interval}");
        }
    }

    internal interface IIntervalable
    {
        public Dictionary<string, double> Intervals { get; }

        public void SetInterval(string key, double value) => Intervals[key] = value;
    }
}
