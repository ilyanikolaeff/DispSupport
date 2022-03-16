using NLog;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace DispSupport
{
    public static partial class Program
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();


        private static AppSettings _appSettings;
        public static void Main(string[] args)
        {

            if (!Environment.UserInteractive)
            {
                _logger.Debug("======= Запуск приложения в режиме службы =======");
                using (var dispSuppService = new DispSupportService())
                    ServiceBase.Run(dispSuppService);
            }
            else
            {
                _logger.Debug("======= Запуск приложения в режиме десктопного приложения =======");
                Start(args);
                Console.ReadKey(true);
                Stop();
            }
        }

        internal static void Start(string[] args)
        {
            // set current direcrtory
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            _appSettings = new AppSettings();
            foreach (var project in _appSettings.Projects)
            {
                var thread = new Thread(() =>
                    project.Start());
                thread.Name = project.Name;
                thread.Start();
            }
            _logger.Debug($"Количество запускаемых проектов = [{_appSettings.Projects.Count}]");
        }

        internal static void Stop()
        {
            foreach (var project in _appSettings.Projects)
            {
                project.Stop();
                _logger.Debug($"[{project.Name}] Проект остановлен");
            }
            _logger.Debug("======= Приложение остановлено =======");
        }
    }
}