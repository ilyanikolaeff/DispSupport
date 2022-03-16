using NLog;
using System;
using System.Threading;

namespace DispSupport
{
    internal class Project
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public ProjectSettings ProjSettings { get; set; }

        public bool IsStarted { get; set; }

        private CommAdapter _commAdapter;

        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private ProtectionsBuffer _protectionsBuffer;

        public void Start()
        {
            _logger.Debug($"[{Name}] Запускаю проект...");

            // Create and ini communication adapter
            _logger.Debug($"[{Name}] 1/3 Создаю и инициализирую коммуникационный адаптер");
            try
            {
                _commAdapter = new CommAdapter();
                _commAdapter.Initialize(ProjSettings.OPCDAConnection, ProjSettings.PLCConnection);
                _logger.Debug($"[{Name}] 1/3 Выдержка на установление соединения коммуникационного адаптера ({AppSettings.OPC_CONN_WAITING_TIMEOUT / 1000} секунд)");
                Thread.Sleep(AppSettings.OPC_CONN_WAITING_TIMEOUT); // для того, чтобы дать время на подключение или в ResultID = E_FAIL
                _logger.Debug($"[{Name}] 1/3 Коммуникационный адаптер инициализирован");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] Коммуникационный адаптер не инициализирован из-за ошибки: {ex}");
                IsStarted = false;
                return;
            }

            // Creating prots buffer 
            _logger.Debug($"[{Name}] 2/3 Инициализация буфера защит");
            try
            {
                _protectionsBuffer = new ProtectionsBuffer(this, _commAdapter);
                _logger.Debug($"[{Name}] 2/3 Инициализация буфера защит завершена");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] Буфер защит не инициализирован: {ex}");
                IsStarted = false;
                return;
            }

            // Subscribe on flags
            // Get flags and create tags to subscribe
            _logger.Debug($"[{Name}] 3/3 Подписываюсь на флаги");
            try
            {
                var tagNames = ProjSettings.GetTagNamesOfFlags();
                tagNames.AddRange(ProjSettings.GetTagNamesOfNonNominalProtection());
                _commAdapter.OpcClient.Subscribe(tagNames,
                    Name + "_SUB",
                    ProjSettings.OpcDaSubscriptionUpdateRate,
                    new Opc.Da.DataChangedEventHandler(_commAdapter.OpcClient.OnItemValueChanged),
                    _protectionsBuffer.OnFlagsChanged);
                _logger.Debug($"[{Name}] 3/3 Успешно подписан на {tagNames.Count} флаг(ов)");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] Подписка на флаги не удалась: {ex}");
                IsStarted = false;
                return;
            }


            IsStarted = true;
            _logger.Debug($"[{Name}] Проект запущен");
        }

        public void Stop()
        {
            // stop subscribe
            _commAdapter.OpcClient.Unsubscribe();

            // disc
            _commAdapter.PlcClient.Dispose();
            _commAdapter.OpcClient.Disconnect();
        }
    }
}
