using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App.Commands
{
    internal interface ICommand
    {
        public string Trigger { get; }

        public void Execute(params string[] args);
    }
}
