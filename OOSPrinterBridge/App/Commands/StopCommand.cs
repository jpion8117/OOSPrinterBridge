using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App.Commands
{
    internal class StopCommand : ICommand
    {
        private readonly IInteruptable _target;

        public StopCommand(IInteruptable target)
        {
            _target = target;
        }

        public string Trigger { get; set; } = "stop";

        public void Execute(params string[] args) => _target.Stop();
    }

    internal interface IInteruptable
    {
        public void Stop();
    }
}
