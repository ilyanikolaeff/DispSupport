using NLog;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;

namespace DispSupport
{
    internal class AppSettings
    {
        // Общие настройки для всего приложения
        public static bool DEBUG_OPC = true; // дебажить опс
        public static bool DEBUG_PLC = true; // дебажить плк

        public static int CHECK_OPC_TIMEOUT = 600000;
        public static int CHECK_PLC_TIMEOUT = 600000;

        public static int OPC_CONN_WAITING_TIMEOUT = 5000;

        public static int MAX_FLAGS_COUNT_TO_LOG = 10;

        public List<Project> Projects { get; set; }
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public AppSettings()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                _logger.Debug($"Инициализация настроек приложения");
                // Projects
                if (!File.Exists("Settings.xml"))
                    throw new FileNotFoundException("Файл с настройками не найден", "Settings.xml");
                var xRoot = XDocument.Load("Settings.xml").Root;
                Projects = new List<Project>();
                foreach (var project in xRoot.Element("Projects").Elements())
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var projFolder = project.Attribute("Folder").Value.Replace("{$baseDir}", baseDir);
                    var projName = project.Attribute("Name").Value;

                    // добавляем только проекты, у которых настройки инициализировались
                    _logger.Debug($"[{projName}] Попытка инициализации конфигурации проекта");
                    var projSettings = new ProjectSettings(projName, projFolder);
                    if (projSettings.IsInitialized)
                    {
                        _logger.Debug($"[{projName}] Конфигурация проекта инициализирована");
                        Projects.Add(new Project()
                        {
                            Name = projName,
                            Folder = projFolder,
                            ProjSettings = projSettings
                        });
                        projSettings.PrintConfigInLog(projName);
                    }
                    else
                    {
                        _logger.Error($"[{projName}] Конфигурация проекта НЕ инициализирована");
                    }
                }

                // global settings
                bool parseResult;
                // Debug 
                parseResult = bool.TryParse(xRoot.Element("Debug")?.Element("Opc")?.Value, out bool debugOpc);
                if (parseResult)
                    DEBUG_OPC = debugOpc;
                _logger.Debug($"DEBUG_OPC = [{DEBUG_OPC}] (default = {!parseResult})");

                parseResult = bool.TryParse(xRoot.Element("Debug")?.Element("Plc")?.Value, out bool debugPlc);
                if (parseResult)
                    DEBUG_PLC = debugPlc;
                _logger.Debug($"DEBUG_PLC = [{DEBUG_PLC}] (default = {!parseResult})");

                // check timeout
                // opc
                parseResult = int.TryParse(xRoot.Element("Settings")?.Element("CheckOpcTimeout")?.Value, out int checkOpcTimeout);
                if (parseResult)
                    CHECK_OPC_TIMEOUT = checkOpcTimeout;
                _logger.Debug($"CHECK_OPC_TIMEOUT = [{CHECK_OPC_TIMEOUT}] (default = {!parseResult})");
                // plc
                parseResult = int.TryParse(xRoot.Element("Settings")?.Element("CheckPlcTimeout")?.Value, out int checkPlcTimeout);
                if (parseResult)
                    CHECK_PLC_TIMEOUT = checkPlcTimeout;
                _logger.Debug($"CHECK_PLC_TIMEOUT = [{CHECK_PLC_TIMEOUT}] (default = {!parseResult})");

                // timeout to stable opc connection
                parseResult = int.TryParse(xRoot.Element("Settings")?.Element("OpcConnectionWaitingTimeout")?.Value, out int opcConnWaitingTimeout);
                if (parseResult)
                    OPC_CONN_WAITING_TIMEOUT = opcConnWaitingTimeout;
                _logger.Debug($"OPC_CONN_WAITING_TIMEOUT = [{OPC_CONN_WAITING_TIMEOUT}] (default = {!parseResult})");

                // max falgs count
                parseResult = int.TryParse(xRoot.Element("Settings")?.Element("MaxFlagsCountToLog")?.Value, out int maxFlagsCountToLog);
                if (parseResult)
                    MAX_FLAGS_COUNT_TO_LOG = maxFlagsCountToLog;
                _logger.Debug($"MAX_FLAGS_COUNT_TO_LOG = [{MAX_FLAGS_COUNT_TO_LOG}] (default = {!parseResult})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка инициализации настроек приложени: {ex}");        
            }
        }
    }

}
