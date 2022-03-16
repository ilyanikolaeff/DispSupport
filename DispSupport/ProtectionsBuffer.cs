using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Timers;

namespace DispSupport
{
    // Реализация буфера защит
    class ProtectionsBuffer
    {
        // Коллекция защит (буфер)
        private ObservableCollection<Protection> _protections;

        // variable for block (parallel)
        private static object _parallelLock = new object();

        // Инстанс текущего логгера
        private Logger _logger = LogManager.GetCurrentClassLogger();

        // Инстанс коммуникационного адаптера
        private readonly CommAdapter _commAdapter;

        // Приватные свойства для кеширования текущей активной защиты
        private int _currentActiveProtectionNumber = -1;
        private int _currentModeIndex = 1;
        private Mode _currentMode;
        private int _targetModeIndex = -1;
        private Mode _targetMode;

        // Инстанс проекта
        private Project _project;
        private int _pumpStationsCount;

        // Приватные свойства для защиты выход ту на нештатный режим работы
        private bool _notNominalSource = false;
        private bool _notNominalState = false;

        // зафиксированная защита для удаления и ее номер (для запроса из буфера защит)
        private Protection _fixedProtection = null;
        private int _fixedProtectionNumber = -1;

        public ProtectionsBuffer(Project proj, CommAdapter commAdapter)
        {
            _protections = new ObservableCollection<Protection>();
            _commAdapter = commAdapter;
            _project = proj;
            _pumpStationsCount = _project.ProjSettings.PumpStationsCount;
            _protections.CollectionChanged += Protections_CollectionChanged;

            ColdStart();
        }

        /// <summary>
        /// Обработка event изменение коллекции защит
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Protections_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Если коллекция изменилась в результате добавления новой защиты
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Debug($"[{_project.Name}] Буфер изменился в результате добавления защиты");
                var currentProtection = GetLastActiveProtection();
                if (currentProtection != null)
                    ShowSupportTable(currentProtection);
            }
            // Если коллекция изменилась в результате удаления защиты
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                _logger.Debug($"[{_project.Name}] Буфер изменился в результате удаления защиты");
                // проверяем есть ли еще защиты в буфере. Если нет, то скрываем таблицу 
                var currentProtection = GetLastActiveProtection();
                if (currentProtection == null)
                {
                    HideSupportTable();
                }
                else
                {
                    ShowSupportTable(currentProtection);
                }
            }
        }

        /// <summary>
        /// Процедура "холодного запуска" буфера защит (выполняет скрытие таблицы)
        /// </summary>
        public void ColdStart()
        {
            // При первой инициализации буфера защит - всегда холодный старт. На всякий случай скрываем таблицу поддержки диспетчера
            _logger.Debug($"[{_project.Name}] Холодный старт буфера защит...");
            HideSupportTable();
        }

        /// <summary>
        /// Скрытие таблицы поддержки диспетчера
        /// </summary>
        private void HideSupportTable()
        {
            _logger.Debug($"[{_project.Name}] Скрытие таблицы поддержки");
            var dispSuppPreffix = _project.ProjSettings.TZPreffix + ".DispSupport";
            var tagsValues = new Dictionary<string, object>();
            for (int i = 0; i < _pumpStationsCount - 1; i++)
            {
                // add target mode tags values
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA", 0);
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA.IsDiff", false);
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin", 0);
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin.IsDiff", false);
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout", 0);
                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout.IsDiff", false);
            }
            // regs 8 (19(
            //if (_project.ProjSettings.UseNotTZEndPressureRegulator)
            //{
            //    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount - 1}.UstPin.IsDiff",
            //        ((PressureRegulator)currentMode.ModeObjects[_pumpStationsCount - 1]).UstPin == ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount - 1]).UstPin);
            //    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount - 1}.UstPin", ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount - 1]).UstPin);
            //}
            // regs (compare reg 10 (21))

            tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount}.UstPin.IsDiff", false);
            tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount}.UstPin", 0);
            // targetMode
            tagsValues.Add($"{dispSuppPreffix}.ShowSupportTable", false);
            tagsValues.Add($"{dispSuppPreffix}.TargetMode", 0);
            tagsValues.Add($"{dispSuppPreffix}.Masked", false);

            WriteTagsValues(tagsValues);

            _logger.Debug($"[{_project.Name}] Скрытие таблицы поддержки выполнено");
        }

        /// <summary>
        /// Показать таблицу поддержки диспетчера для защиты currentProtection
        /// </summary>
        /// <param name="currentProtection">Защита, для которой необходимо показать таблицу диспетчера</param>
        private void ShowSupportTable(Protection currentProtection)
        {

            _logger.Debug($"[{_project.Name}] Отображение для следующей защиты: <{currentProtection}>");
            _currentActiveProtectionNumber = currentProtection.Number;

            // Если задан целевой режим
            var dispSupport = new Dictionary<string, object>();
            if (currentProtection.TargetMode > 0)
            {
                // Получаем параметры текущего (currentModeIndex) и целевого (targetModeIndex) режима
                // current
                var readResult = _commAdapter.PlcClient.ReadSync("TZ_OUT_VU.dMODE");
                if (!readResult.IsOK)
                {
                    _logger.Error($"[{_project.Name}] Не удалось считать индекс текущего режима");
                    return;
                }
                int.TryParse(readResult.Result.ToString(), out int currentModeIndex);
                if (currentModeIndex == 0 || currentModeIndex == 1)
                {
                    string tzStatus = "";
                    if (currentModeIndex == 0)
                        tzStatus = "(состояние не определено)";
                    if (currentModeIndex == 1)
                        tzStatus = "(ТУ остановлен)";
                    _logger.Error($"[{_project.Name}] Не найден текущий режим {tzStatus}");
                    return;
                }
                // target
                var targetModeIndex = GetInxdexOfTargetMode(currentProtection.TargetMode);
                if (targetModeIndex == 0)
                {
                    _logger.Error($"[{_project.Name}] Не найден целевой режим для защиты <{currentProtection.Name}>");
                    return;
                }

                // пробуем првоерить полученные индексы и если они в данный момент не кэшированы, то считать параметры режимов
                // считывание параметров обеспечивается методом CheckMode
                if (!CheckMode(currentModeIndex, ref _currentModeIndex, ref _currentMode) || !CheckMode(targetModeIndex, ref _targetModeIndex, ref _targetMode))
                {
                    _logger.Error($"[{_project.Name}] Не удалось прочитать настройки текущего режима №{currentModeIndex} или целевого режима №{targetModeIndex}");
                    return;
                }

                _logger.Debug($"[{_project.Name}] Настройки текущего режима прочитаны: <{_currentMode}>");
                _logger.Debug($"[{_project.Name}] Настройки целевоого режима прочитаны: <{_targetMode}>");

                // Получаем таблицу с параметрами
                dispSupport = GetDispSuppTable(_currentMode, _targetMode);
                if (dispSupport == null)
                    return;
            }
            // Если целевой режм не задан, но выдержка >= 0 => Будет противоаварийная остановка ТУ
            if (currentProtection.TargetMode == -1 && currentProtection.Timeout >= 0)
            {
                dispSupport = GetDispSuppTable(null, null);
                if (dispSupport == null)
                    return;
                _logger.Debug($"[{_project.Name}] Будет выполнена противоаварийная остановка");
            }

            // Проверяем защиту на наличие маски
            dispSupport.Add($"{_project.ProjSettings.TZPreffix}.DispSupport.Masked", currentProtection.Masked);

            // Записываем все
            WriteTagsValues(dispSupport);

            _logger.Debug($"[{_project.Name}] Отображение для защиты: <{currentProtection.Name}> выполнено");
        }

        /// <summary>
        /// Записать словарь со значениями тегов с проверкой на успешность записи
        /// </summary>
        /// <param name="tagsValues"></param>
        private void WriteTagsValues(Dictionary<string, object> tagsValues)
        {
            var writeResults = _commAdapter.OpcClient.WriteData(tagsValues);
            if (writeResults.ToList().Where(p => p.ResultID != Opc.ResultID.S_OK).Count() != 0)
            {
                _logger.Error($"[{_project.Name}] Часть значений записана в OPC Server с ошибками");
            }
        }

        /// <summary>
        /// Метод обрабатывающий изменение значения на который подписан текущий проект
        /// </summary>
        /// <param name="flagsValues">Входящий словарь ИМЯ ТЕГА - Значение (object)</param>
        public void OnFlagsChanged(IDictionary<string, object> flagsValues)
        {
            try
            {

                _logger.Debug($"[{_project.Name}] Обработка {flagsValues.Count} флага(ов)");
                foreach (var flag in flagsValues)
                {
                    if (flagsValues.Count <= AppSettings.MAX_FLAGS_COUNT_TO_LOG)
                    {
                        _logger.Debug($"[{_project.Name}] Обработка флага: {flag.Key} - {flag.Value}");
                    }

                    // Защита 1 уровня (ВЫХОД ТУ НА НЕШТАТНЫЙ РЕЖИМ РАБОТЫ) (работа будет производиться с зафиксированной защитой 2 уровня)
                    if (flag.Key.ToLower().Contains(".lck_first_level."))
                    {
                        var flagValue = Convert.ToBoolean(flag.Value);
                        // если это source флаг
                        if (flag.Key.ToLower().Contains(".source."))
                        {
                            _notNominalSource = flagValue;
                            if (!flagValue)
                                StartStateValueWaitingTimer(int.MaxValue); // Source снимаем через Timer, при этом int.MaxValue говорит нам о том, что это защита - нештатка
                        }
                        // значит это state
                        else
                        {
                            _notNominalState = flagValue;
                            if (!flagValue) // Снимаем state и удаляем защиту
                            {
                                if (_fixedProtection != null)
                                {
                                    RemoveProtection(_fixedProtection);
                                }
                            }
                        }
                    }
                    // Защита 2 уровня (ВСЕ ФЛАГИ) (работа будет производиться со всеми защитами 2 уровня)
                    else
                    {
                        // определяем номер флага и конвертим его значение
                        int flagValue = Convert.ToInt32(flag.Value);
                        int flagNumber = Flag.GetFlagNumber(flag.Key);

                        // проверяем флаг на биты
                        var bits = Helper.CheckBits(flagValue);

                        // Определяем какой это флаг
                        bool isMaskedFlag = flag.Key.ToLower().Contains(".mode.") || flag.Key.ToLower().Contains(".tor.");
                        bool isSourceFlag = flag.Key.ToLower().Contains(".source.");
                        // буферизируем все биты сработавшего флага
                        // Если это флаг для маски
                        if (isMaskedFlag)
                        {
                            CheckBitsAndSetIsProtectionMasked(flagNumber, bits);
                        }
                        // Значит это Source (isSourcFlag) или State флаг (!isSourcFlag)
                        else
                        {
                            CheckBitsAndBufferizeProtection(isSourceFlag, flagNumber, bits);
                        }
                    }
                }
                _logger.Debug($"[{_project.Name}] Обработка {flagsValues.Count} флага(ов) выполнена");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{_project.Name}] Обработка {flagsValues.Count} флаг(ов) не выполнена из-за ошибки: {ex}");
            }
        }

        /// <summary>
        /// Проверить биты флага с номером flgaNumber и буферизовать их
        /// </summary>
        /// <param name="isSourceFlag">Является ли флаг SOURCE</param>
        /// <param name="flagNumber">Номер флага</param>
        /// <param name="bits">Словарь из битов</param>
        public void CheckBitsAndBufferizeProtection(bool isSourceFlag, int flagNumber, Dictionary<int, bool> bits)
        {
            foreach (var bit in bits)
            {
                // Буферизиурем все защиты
                var protectionNumber = flagNumber * 32 + bit.Key;
                BufferizeProt(protectionNumber, isSourceFlag, bit.Value);
            }
        }

        /// <summary>
        /// Проверка битов изменившегося флага TOR или MODE и если это активная защита, то устанавливается/скинмается маска
        /// </summary>
        /// <param name="flagNumber"></param>
        /// <param name="bits"></param>
        public void CheckBitsAndSetIsProtectionMasked(int flagNumber, Dictionary<int, bool> bits)
        {
            foreach (var bit in bits)
            {
                var protectionNumber = flagNumber * 32 + bit.Key;
                if (protectionNumber == _currentActiveProtectionNumber)
                {
                    Protection protection = GetProtection(protectionNumber);
                    if (protection != null)
                        protection.Masked = bit.Value;
                    Dictionary<string, object> tagsValues = new Dictionary<string, object>();
                    tagsValues.Add(_project.ProjSettings.TZPreffix + ".DispSupport.Masked", bit.Value);
                    _commAdapter.OpcClient.WriteData(tagsValues);
                }
            }
        }

        /// <summary>
        /// Буферизация защиты
        /// </summary>
        /// <param name="protectionNumber">Номер защиты</param>
        /// <param name="isSourceFlag">Является ли взвденный флаг SOURCE</param>
        /// <param name="bitValue">Значение бита флага</param>
        public void BufferizeProt(int protectionNumber, bool isSourceFlag, bool bitValue)
        {
            // Реализация буферизации

            // Проверяем буфер 
            var protection = GetProtection(protectionNumber);
            // в буфере такую защиту не нашли и у добавляемой защиты взведен флаг - значит создаем такую защиту и добавляем в буфер
            if (protection == null && bitValue)
            {
                if (_project.ProjSettings.MaxQ_ProtsRange.Contains(protectionNumber))
                {
                    _logger.Debug($"[{_project.Name}] Защита с номером {protectionNumber} не обработана по причине попадания в диапазон исключения MaxQProtsRange");
                    return;
                }

                protection = new Protection(protectionNumber)
                {
                    FearTime = DateTime.Now,
                    Name = GetProtectionName(protectionNumber)
                };
                // Получаем параметры этой защиты и добавляем в буфер
                protection.ReadParameters(_commAdapter, out bool readResult);
                if (!readResult) // не удалось прочитать параметры
                {
                    _logger.Error($"[{_project.Name}] Не удалось прочитать параметры защиты с номером <{protection.Name}>");
                    return;
                }
                else if (protection.Timeout < 0) // Выдержка меньше 0, значит защита не имеет алгоритма
                {
                    _logger.Debug($"[{_project.Name}] Защита: <{protection.Name}> не имеет алгоритма ");
                    return;
                }
                else // устанавливаем какой это флаг, добавляем защиту и выбрасываемся из метода
                {
                    if (isSourceFlag)
                        protection.SourceFlag = bitValue;
                    else
                        protection.StateFlag = bitValue;
                    _protections.Add(protection);
                }
            }
            // если такую защиту нашли, то устанавливаем ей флаги
            else if (protection != null)
            {
                // если source флаг
                if (isSourceFlag)
                {
                    protection.SourceFlag = bitValue;
                    // снимаем source флаг через таймер из-за того, что есть задержка между снятием Source и установкой State
                    if (!bitValue)
                    {
                        StartStateValueWaitingTimer(protectionNumber);
                    }
                }
                // значит это State флаг
                else
                {
                    protection.StateFlag = bitValue;
                    // удаляем если до этого State был TRUE
                    if (!bitValue)
                        RemoveProtection(protection);
                }
            }
        }

        /// <summary>
        /// Получить последнюю активную защиту (первую с взведенным флагом STATE) или защиту с наименьшей выдержкой
        /// </summary>
        /// <returns></returns>
        private Protection GetLastActiveProtection()
        {

            if (_protections.Count > 0)
            {
                lock (_parallelLock)
                {
                    // Есть ли взведенные защиты (сработавшие)
                    Protection activeProt = _protections.LastOrDefault(p => p.StateFlag);
                    // Если сработавших защит нету, то возможно есть защиты у которых идет выдержка. Выбираем защиту с наименьшей выдержкой
                    if (activeProt == null)
                    {
                        activeProt = _protections[0];
                        foreach (var prot in _protections)
                        {
                            if (prot.FearTime.AddSeconds(prot.Timeout) < activeProt.FearTime.AddSeconds(activeProt.Timeout))
                            {
                                activeProt = prot;
                            }
                        }
                    }
                    return activeProt;
                }
            }
            else
                return null;
        }

        /// <summary>
        /// Получить защиту из буфера по ее номеру
        /// </summary>
        /// <param name="protectionNumber">Номер защиты</param>
        /// <returns></returns>
        private Protection GetProtection(int protectionNumber)
        {
            Protection protection;

            lock (_parallelLock)
            {
                protection = _protections.FirstOrDefault(p => p.Number == protectionNumber);
            }
            return protection;

        }

        /// <summary>
        /// Получить данные для таблицы поддержки диспетчера по результатам сравнения двух режимов
        /// </summary>
        /// <param name="currentMode">Текущий режим ТУ</param>
        /// <param name="targetMode">Целевой режим из параметров защиты</param>
        /// <returns></returns>
        public Dictionary<string, object> GetDispSuppTable(Mode currentMode, Mode targetMode)
        {
            _logger.Debug($"[{_project.Name}] Попытка получения таблицы");
            try
            {
                var dispSuppPreffix = _project.ProjSettings.TZPreffix + ".DispSupport";
                Dictionary<string, object> tagsValues = new Dictionary<string, object>();
                if (targetMode != null && currentMode != null)
                {
                    // diff
                    for (int i = 0; i < _pumpStationsCount - 1; i++)
                    {
                        // Compare count pumps, ust pin and ust pout
                        if (currentMode.ModeObjects[i] is PumpStation currentModeStation
                            && targetMode.ModeObjects[i] is PumpStation targetModeStation)
                        {
                            tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA.IsDiff",
                                currentModeStation.MPUCount != targetModeStation.MPUCount);


                            tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin.IsDiff",
                                currentModeStation.UstPin != targetModeStation.UstPin);


                            tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout.IsDiff",
                                currentModeStation.UstPout != targetModeStation.UstPout);
                        }

                        // add target mode tags values
                        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA", ((PumpStation)targetMode.ModeObjects[i]).MPUCount);
                        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin", ((PumpStation)targetMode.ModeObjects[i]).UstPin);
                        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout", ((PumpStation)targetMode.ModeObjects[i]).UstPout);
                    }

                    // regs 8 (19(
                    //if (_project.ProjSettings.UseNotTZEndPressureRegulator)
                    //{
                    //    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount - 1}.UstPin.IsDiff",
                    //        ((PressureRegulator)currentMode.ModeObjects[_pumpStationsCount - 1]).UstPin == ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount - 1]).UstPin);
                    //    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount - 1}.UstPin", ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount - 1]).UstPin);
                    //}
                    // regs (compare reg 10 (21))

                    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount}.UstPin.IsDiff",
                        ((PressureRegulator)currentMode.ModeObjects[_pumpStationsCount]).UstPin != ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount]).UstPin);
                    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{_pumpStationsCount}.UstPin", ((PressureRegulator)targetMode.ModeObjects[_pumpStationsCount]).UstPin);

                    // targetMode
                    tagsValues.Add($"{dispSuppPreffix}.ShowSupportTable", true);
                    tagsValues.Add($"{dispSuppPreffix}.TargetMode", targetMode.Name);

                    _logger.Debug($"[{_project.Name}] Попытка сравнения текущего и целевого режимов успешно выполнена");
                }
                else
                {
                    tagsValues.Add($"{dispSuppPreffix}.ShowSupportTable", true);
                    tagsValues.Add($"{dispSuppPreffix}.TargetMode", "-");
                }
                return tagsValues;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{_project.Name}] Попытка получения таблицы не выполнена из-за ошибки: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Запуск таймера ожидания флага STATE для защиты с номером protectionNumber
        /// </summary>
        /// <param name="protectionNumber">Номер защиты для которой запускается таймер</param>
        private void StartStateValueWaitingTimer(int protectionNumber)
        {
            var stateValueWaitingTimer = new Timer(_project.ProjSettings.StateValueWaitingTimeout);
            stateValueWaitingTimer.Elapsed += (sender, e) => Timer_Elapsed(protectionNumber);
            stateValueWaitingTimer.AutoReset = false; // start only once
            stateValueWaitingTimer.Start();
            _logger.Debug($"[{_project.Name}] Таймер выдержки флага STATE для защиты <{GetProtectionName(protectionNumber)}> запущен");
        }

        /// <summary>
        /// Событие для обработки истечения таймера ожидания флага STATE для защиты с номером protectionNumber
        /// </summary>
        /// <param name="protectionNumber">Номер защиты, для которой обрабатывается истечение таймера</param>
        private void Timer_Elapsed(int protectionNumber)
        {
            _logger.Debug($"[{_project.Name}] Таймер выдержки флага STATE для защиты <{GetProtectionName(protectionNumber)}> выполнился");

            var bgWorker = new BackgroundWorker();
            bgWorker.DoWork += BgWorker_DoWork;
            if (!bgWorker.IsBusy)
                bgWorker.RunWorkerAsync(protectionNumber);
        }

        /// <summary>
        /// Background worker выполняющий проверку на необходимость удаления защиты если не пришел STATE флаг
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // номер защиты для которой выполняется BGWorker
                var protectionNumber = (int)e.Argument;
                var protectionName = GetProtectionName(protectionNumber);
                _logger.Debug($"[{_project.Name}] BGWorker для защиты <{protectionName}> запущен");

                Protection protection = protectionNumber != int.MaxValue ? GetProtection(protectionNumber) : GetProtection(_fixedProtectionNumber);

                if (protection == null)
                {
                    _logger.Debug($"[{_project.Name}] BGWorker защита <{protectionName}> не найдена в буфере");
                    return;
                }

                // Удаляем защиту с проверкой на наличие флагов
                RemoveProtection(protection);

                _logger.Debug($"[{_project.Name}] BGWorker для защиты <{protectionName}> выполнился");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{_project.Name}] BGWorker для защиты не выполнился из-за ошибки: {ex}");
            }

        }

        /// <summary>
        /// Удалить защиту protection
        /// </summary>
        /// <param name="protection">Удаляемая защита</param>
        private void RemoveProtection(Protection protection)
        {
            // если нет сработавшей защиты "Выход ТУ на нештатный режим работы"
            if (!_notNominalSource && !_notNominalState)
            {
                if (!protection.SourceFlag && !protection.StateFlag)
                {
                    _logger.Debug($"[{_project.Name}] ОТСУТСТВУЮТ флаг защиты <Выход ТУ на нештатный режим работы>, флаги State и Source => удаляю защиту: <{protection.Name}>");
                    // удаляем зафиксированную защиту
                    if (protection.Number == _fixedProtectionNumber)
                    {
                        _fixedProtection = null;
                        _fixedProtectionNumber = -1;
                    }
                    _protections.Remove(protection);
                }
            }
            // иначе фиксируем защиту для удаления 
            else
            {
                _logger.Debug($"[{_project.Name}] ЕСТЬ флаг защиты <Выход ТУ на нештатный режим работы> (Source = {_notNominalSource}, State = {_notNominalState}) => фиксирую защиту: <{protection.Name}>");
                _fixedProtection = protection;
                _fixedProtectionNumber = protection.Number;
            }
        }

        /// <summary>
        /// Проверить новый индекс режима на необходимость создания и считывания параметров нового режима
        /// </summary>
        /// <param name="newIndex">Новый индекс режима</param>
        /// <param name="oldIndex">Старый индекс режима</param>
        /// <param name="oldMode">Экземпляр класса MODE старого режима</param>
        /// <returns></returns>
        private bool CheckMode(int newIndex, ref int oldIndex, ref Mode oldMode)
        {
            if (newIndex != oldIndex)
            {
                var newMode = new Mode(newIndex);
                newMode.Name = _project.ProjSettings.Modes.Where(p => p.Index == newIndex).Select(s => s.Name).FirstOrDefault();
                newMode.ReadParameters(_commAdapter, _pumpStationsCount, out bool readResult);
                oldIndex = newIndex;
                oldMode = newMode;
                return readResult;
            }
            return true;
        }

        /// <summary>
        /// Получить нормальный индекс целевого режима из конфигурации для целевого режима protectionTargetModeIndex
        /// </summary>
        /// <param name="protectionTargetModeIndex">Номер целевого режима из параметров защиты</param>
        /// <returns></returns>
        private int GetInxdexOfTargetMode(int protectionTargetModeIndex)
        {
            var modeIndex = _project.ProjSettings.Modes.Where(p => p.TargetIndex == protectionTargetModeIndex).Select(s => s.Index).FirstOrDefault();
            return modeIndex;
        }

        /// <summary>
        /// Получить имя защиты из файла конфигурации
        /// </summary>
        /// <param name="protectionNumber">Номер защиты для которой необходимо получить имя</param>
        /// <returns></returns>
        private string GetProtectionName(int protectionNumber)
        {
            string protName = "";
            if (protectionNumber == int.MaxValue)
            {
                protName = "Выход ТУ на нештатный режим работы";
                protectionNumber = _fixedProtectionNumber;
                if (protectionNumber > 0)
                {
                    protName += ". Зафиксированная защита -> ";
                }
            }
            protName += _project.ProjSettings.Flags
                         .Where(p => p.Num == protectionNumber / 32)
                         .Select(s => s.Bits)
                         .FirstOrDefault().Where(p => p.Num == protectionNumber % 32)
                         .Select(s => s.Description)
                         .FirstOrDefault();
            if (string.IsNullOrEmpty(protName))
                return "<Не найдено название защиты>";
            else
                return protName;

        }
    }
}
