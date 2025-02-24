using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App.Commands
{
    internal class CommandManager
    {
        private List<ICommand> _commands = new List<ICommand>();

        public void RegisterCommand(ICommand command)
        {
            var conflict = _commands.FirstOrDefault(c => c.Trigger == command.Trigger);
            if (conflict != null)
            {
                throw new InvalidOperationException($"There is already an existing " +
                    $"command registered with the trigger '{command.Trigger}' please use a different trigger.");
            }

            _commands.Add(command);
        }

        public bool CheckCommandTrigger(string trigger)
        {
            var args = trigger.Split(' ').ToList();

            if (args.Count == 0) return false;

            var cmd = _commands.FirstOrDefault(c => c.Trigger.ToLower() == args[0].ToLower());
            if (cmd != null)
            {
                args.Remove(args[0]);

                cmd.Execute(args.ToArray());
                return true;
            }

            return false;
        }
    }
}
