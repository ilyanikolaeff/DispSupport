using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DispSupportConsole
{
    public class Logger
    {
        private object obj = new object();
        private string _logFileName;
        private bool _isConsoleWrite;
        private bool _isClearLog;

        private static readonly Lazy<Logger> _instance =
            new Lazy<Logger>(() => new Logger($"Log_{DateTime.Now:yyyy-MM-dd}.txt"));
        public static Logger GetInstance()
        {
            return _instance.Value;
        }
        private Logger(string logFileName)
        {
            _logFileName = logFileName;
            _isConsoleWrite = true;
            _isClearLog = true;
            if (_isClearLog && File.Exists(_logFileName))
                File.Delete(_logFileName);
        }
        public void Log(string logEntry, Severity severity = Severity.Debug, [CallerMemberName]string callerMemberName = "")
        {
            lock (obj)
            {
                using (StreamWriter logWriter = new StreamWriter(_logFileName, true))
                {
                    if (_isConsoleWrite)
                        Console.WriteLine($"{logEntry}");
                    
                    logWriter.WriteLine($"{DateTime.Now}\t{severity}\t{callerMemberName}\t{logEntry}");
                    logWriter.Flush();
                }
            }
        }
    }

    public enum Severity
    {
        Debug,
        Error,
        Info,
        Trace
    }
}
