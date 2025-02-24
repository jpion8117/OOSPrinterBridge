using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OOSPrinterBridge.App
{
    internal static class Logger
    {
        public static List<LogMessage> History { get; private set; } = new List<LogMessage>();

        public static void RefreshLog()
        {
            foreach (var log in History)
            {
                var color = ConsoleColor.Green;
                var tag = "[Info]   ";

                switch (log.Level)
                {
                    case LogLevel.Warn:
                        color = ConsoleColor.Yellow;
                        tag = "[Warning]";
                        break;
                    case LogLevel.Error:
                        color = ConsoleColor.Red;
                        tag = "[Error]  ";
                        break;
                    case LogLevel.Statement:
                        Console.WriteLine(log.Message);
                        continue;
                }

                Console.ForegroundColor = color;
                Console.Write(tag);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" {log.Timestamp.ToLocalTime()}: {log.Message}");
            }
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Info, bool preserve = true)
        {
            switch (logLevel)
            {
                case LogLevel.Info:
                    LogInfo(message, preserve);
                    break;

                case LogLevel.Warn:
                    LogWarning(message, preserve);
                    break;

                case LogLevel.Error:
                    LogError(message, preserve);
                    break;
                case LogLevel.Statement:
                    LogStatement(message, preserve);
                    break;
            }
        }

        public static void LogStatement(string message, bool preserve = true)
        {
            Console.WriteLine(message);

            if (preserve) 
                History.Add(new LogMessage
                {
                    Message = message,
                    Level = LogLevel.Statement
                });
        }

        public static void LogInfo(string message, bool preserve = true)
        {
            var time = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[Info]   ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" {time.ToLocalTime()}: {message}");

            if (preserve)
                History.Add(new LogMessage
                {
                    Message = message,
                    Level = LogLevel.Info,
                    Timestamp = time
                });
        }
        public static void LogWarning(string message, bool preserve = true)
        {
            var time = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Warning]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" {time.ToLocalTime()}: {message}");

            if (preserve)
                History.Add(new LogMessage
                {
                    Message = message,
                    Level = LogLevel.Warn,
                    Timestamp = time
                });
        }
        public static void LogError(string message, bool preserve = true)
        {
            var time = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[Error]  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" {time.ToLocalTime()}: {message}");

            if (preserve)
                History.Add(new LogMessage
                {
                    Message = message,
                    Level = LogLevel.Error,
                    Timestamp = time
                });
        }
    }

    public struct LogMessage
    {
        public LogLevel Level;
        public string Message;
        public DateTime Timestamp;
    }

    public enum LogLevel
    {
        Info,
        Warn,
        Error,
        Statement
    }
}
