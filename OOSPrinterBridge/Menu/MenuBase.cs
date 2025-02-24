using OOSPrinterBridge.App;

namespace OOSPrinterBridge.Menu
{
    internal abstract class MenuBase
    {
        public abstract void Run();

        protected virtual void Clear()
        {
            Console.Clear();
            Logger.RefreshLog();

            var buffer = string.Empty;
            for (int j = 0; j < Console.BufferWidth; j++) buffer += '*';
            Console.WriteLine($"\n{buffer}\n");
        }

        protected virtual string Prompt(string message)
        {
            Console.Write($"{message}: ");
            return Console.ReadLine() ?? string.Empty;
        }

        protected virtual string SecurePrompt(string message)
        {
            string response = "";
            Console.Write($"{message}: ");
            var promptStart = Console.GetCursorPosition();
            var keyPress = Console.ReadKey();

            while (keyPress.Key != ConsoleKey.Enter)
            {
                var clearBuffer = "";
                for (int j = 0; j < response.Length; j++)
                    clearBuffer += ' ';

                Console.SetCursorPosition(promptStart.Left, promptStart.Top);
                Console.Write(clearBuffer);
                Console.SetCursorPosition(promptStart.Left, promptStart.Top);

                if (keyPress.Key == ConsoleKey.Delete)
                    response = "";
                else if (keyPress.Key == ConsoleKey.Backspace)
                {
                    if (response.Length > 0)
                    {
                        response = response.Remove(response.Length - 1);
                    }
                }
                else
                    response += keyPress.KeyChar;

                var mask = "";
                for (int j = 0; j < response.Length; j++) mask += "*";

                Console.Write(mask);
                keyPress = Console.ReadKey();
            }

            return response;
        }
    }
}