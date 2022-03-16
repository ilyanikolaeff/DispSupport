using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DispSupport
{
    class ProjectSettings
    {
        public List<Flag> Flags { get; set; }
        public List<ModeIndex> Modes { get; set; }
        public OPCDAConnectionSettings OPCDAConnection { get; set; }
        public PLCConnectionSettings PLCConnection { get; set; }
        public int PumpStationsCount { get; set; }
        public string TZPreffix { get; set; }
        public int StateValueWaitingTimeout { get; set; } //= 5000;
        public int OpcDaSubscriptionUpdateRate { get; set; }
        public int NonNominalProtectionNumber { get; set; }
        public Range MaxQ_ProtsRange { get; set; }

        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private string _projectFolder;
        private string _projectName;

        public bool IsInitialized { get; private set; }

        public ProjectSettings(string projectName, string projectFolder)
        {
            _projectFolder = projectFolder + "\\";
            _projectName = projectName;
            try
            {
                Initialize();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                IsInitialized = false;
            }
        }

        public void Initialize()
        {
            LoadConfiguration();
            LoadFlags();
            LoadModes();
            LoadConnectionSettings();
        }

        private void LoadFlags()
        {
            if (!File.Exists(_projectFolder + "Flags.xml"))
                throw new FileNotFoundException("Файл не найден", _projectFolder + "Flags.xml");

            var xRoot = XDocument.Load(_projectFolder + "Flags.xml").Root;
            Flags = new List<Flag>();
            var xFlgs = xRoot.Element("Flags").Elements("Flg");
            foreach (var xFlg in xFlgs)
            {
                var currentFlag = new Flag();
                int.TryParse(xFlg.Attribute("Num").Value, out int flgNum);
                currentFlag.Num = flgNum;
                foreach (var xBit in xFlg.Elements("Bit"))
                {
                    int.TryParse(xBit.Attribute("Num").Value, out int bitNum);
                    var bitDescription = xBit.Attribute("Description").Value;
                    currentFlag.Bits.Add(new Bit()
                    {
                        Num = bitNum,
                        Description = bitDescription
                    });
                    _ = (flgNum * 32) + bitNum;
                }
                Flags.Add(currentFlag);
            }
        }

        private void LoadModes()
        {
            if (!File.Exists(_projectFolder + "Modes.xml"))
                throw new FileNotFoundException("Файл не найден", _projectFolder + "Modes.xml");

            var xRoot = XDocument.Load(_projectFolder + "Modes.xml").Root;
            Modes = new List<ModeIndex>();
            var xModes = xRoot.Element("Modes").Elements();
            foreach (var xMode in xModes)
            {
                int.TryParse(xMode.Attribute("Index").Value, out int modeIndex);
                int.TryParse(xMode.Attribute("TargetIndex").Value, out int modeTargetIndex);
                var modeName = xMode.Attribute("Name").Value;
                Modes.Add(new ModeIndex() { Index = modeIndex, TargetIndex = modeTargetIndex, Name = modeName });
            }
        }

        private void LoadConnectionSettings()
        {
            if (!File.Exists(_projectFolder + "Connection.xml"))
                throw new FileNotFoundException("Файл не найден", _projectFolder + "Connection.xml");

            var xRoot = XDocument.Load(_projectFolder + "Connection.xml").Root;

            OPCDAConnection = new OPCDAConnectionSettings(xRoot.Element("OPCDA").Attribute("IP").Value,
                                                        xRoot.Element("OPCDA").Attribute("Name").Value);
            OPCDAConnection.Name = _projectName;

            int.TryParse(xRoot.Element("PLC").Attribute("Backplane").Value, out int backplane);
            int.TryParse(xRoot.Element("PLC").Attribute("CPU").Value, out int cpu);
            PLCConnection = new PLCConnectionSettings(xRoot.Element("PLC").Attribute("IP").Value,
                                                     backplane,
                                                     cpu);
            PLCConnection.Name = _projectName;
        }

        private void LoadConfiguration()
        {
            if (!File.Exists(_projectFolder + "Configuration.xml"))
                throw new FileNotFoundException("Файл не найден", _projectFolder + "Configuration.xml");

            var xSettings = XDocument.Load(_projectFolder + "Configuration.xml").Root.Elements();

            TZPreffix = $"AK.VSMN.UCS.TZ{xSettings.Where(p => p.Name == "TechnologyZoneNumber").Select(s => s.Value).FirstOrDefault()}";

            int.TryParse(xSettings.Where(p => p.Name == "PumpStationsCount").Select(s => s.Value).FirstOrDefault(), out int npsCount);
            int.TryParse(xSettings.Where(p => p.Name == "StateValueWaitingTimeout").Select(s => s.Value).FirstOrDefault(), out int stateValueWaitingTimeout);
            int.TryParse(xSettings.Where(p => p.Name == "OPCDASubscriptionUpdateRate").Select(s => s.Value).FirstOrDefault(), out int subscriptionUpdateRate);
            int.TryParse(xSettings.Where(p => p.Name == "NonNominalProtectionNumber").Select(s => s.Value).FirstOrDefault(), out int nonNominalProtNum);

            PumpStationsCount = npsCount;
            StateValueWaitingTimeout = stateValueWaitingTimeout;
            OpcDaSubscriptionUpdateRate = subscriptionUpdateRate;
            NonNominalProtectionNumber = nonNominalProtNum;
            MaxQ_ProtsRange = Range.Parse(xSettings.Where(p => p.Name == "MaxQSecondLevelProtNumbersRange").Select(s => s.Value).FirstOrDefault());
        }

        public List<string> GetTagNamesOfFlags()
        {
            var tagNames = new List<string>();
            foreach (var flg in Flags)
            {
                tagNames.Add(TZPreffix + $".LCK_SECOND_LEVEL.source.flg{flg.Num}"); // source
                tagNames.Add(TZPreffix + $".LCK_SECOND_LEVEL.level.flg{flg.Num}"); // state
                tagNames.Add(TZPreffix + $".LCK_SECOND_LEVEL.mode.mode{flg.Num}"); // mask
                tagNames.Add(TZPreffix + $".LCK_SECOND_LEVEL.tor.tor{flg.Num}"); // tor mask
            }
            return tagNames;
        }

        public List<string> GetTagNamesOfNonNominalProtection()
        {
            var tagNames = new List<string>();
            tagNames.Add(TZPreffix + $".LCK_FIRST_LEVEL.source.N{NonNominalProtectionNumber:000}");
            tagNames.Add(TZPreffix + $".LCK_FIRST_LEVEL.state.N{NonNominalProtectionNumber:000}");
            return tagNames;
        }

        public override string ToString()
        {
            string projSettingsString = "";

            projSettingsString += $"TZPreffix = [{TZPreffix}]\n";
            projSettingsString += $"PumpStationsCount = [{PumpStationsCount}]\n";
            projSettingsString += $"StateValueWaitingTimeout = [{StateValueWaitingTimeout}]\n";
            projSettingsString += $"OpcDaSubscriptionUpdateRate = [{OpcDaSubscriptionUpdateRate}]\n";

            projSettingsString += $"{OPCDAConnection}\n";
            projSettingsString += $"{PLCConnection}";

            return projSettingsString;
        }

        public void PrintConfigInLog(string projectName)
        {
            _logger.Debug($"[{projectName}] Загруженная конфигурация проекта:");
            _logger.Debug($"[{projectName}] TZPreffix = [{TZPreffix}]");
            _logger.Debug($"[{projectName}] PumpStationsCount = [{PumpStationsCount}]");
            _logger.Debug($"[{projectName}] StateValueWaitingTimeout = [{StateValueWaitingTimeout}]");
            _logger.Debug($"[{projectName}] OpcDaSubscriptionUpdateRate = [{OpcDaSubscriptionUpdateRate}]");
            _logger.Debug($"[{projectName}] NonNominalProtectionNumber = [{NonNominalProtectionNumber}]");
            _logger.Debug($"[{projectName}] MaxQProtsRange = [{MaxQ_ProtsRange}]");
            _logger.Debug($"[{projectName}] {OPCDAConnection}");
            _logger.Debug($"[{projectName}] {PLCConnection}");
        }

    }

    public class ConnectionSettings
    {
        public string Name { get; set; }
    }

    public class OPCDAConnectionSettings : ConnectionSettings
    {
        public string IP { get; set; }
        public string ServerName { get; set; }
        public OPCDAConnectionSettings(string ip, string serverName)
        {
            IP = ip;
            ServerName = serverName;
        }

        public override string ToString()
        {
            return $"OPCDA connection settings: IP = {IP}, Name = {ServerName}";
        }
    }

    public class PLCConnectionSettings : ConnectionSettings
    {
        public string IP { get; set; }
        public int Backplane { get; set; }
        public int CPU { get; set; }

        public PLCConnectionSettings(string ip, int backplane, int cpu)
        {
            IP = ip;
            Backplane = backplane;
            CPU = cpu;
        }

        public override string ToString()
        {
            return $"PLC connection settings: IP = {IP}, Backplane = {Backplane}, CPU = {CPU}";
        }
    }

    class Range
    {
        public int StartValue { get; private set; }
        public int EndValue { get; private set; }

        public static Range Parse(string parsingString)
        {
            var range = new Range();

            var parts = parsingString.Split('-');
            var startValue = int.Parse(parts[0].Trim());
            var endValue = int.Parse(parts[1].Trim());

            range.StartValue = startValue;
            range.EndValue = endValue;

            return range;
        }

        public bool Contains(int value)
        {
            return value >= StartValue && value <= EndValue;
        }

        public override string ToString()
        {
            return $"Range => StartValue = {StartValue} : EndValue = {EndValue}";
        }
    }
    
    internal class ModeIndex
    {
        public int Index { get; set; }
        public int TargetIndex { get; set; }
        public string Name { get; set; }
    }

}
