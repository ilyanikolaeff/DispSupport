using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace DispSupportConsole
{
    #region Trash
    public static class MainLogic
    {
        //private static Logger _logger = LogManager.GetCurrentClassLogger();
        //private static List<Protection> _protsBuffer = new List<Protection>();
        //private static System.Timers.Timer _timer = new System.Timers.Timer()
        //{
        //    Interval = ProjectSettings.StateValueWaitingTimeout,
        //    Enabled = true
        //};

        //public static void OnFlagDataChanged(string tagName, object tagValue, bool isSourceFlag)
        //{
        //    var flgValue = Convert.ToInt32(tagValue);
        //    var flgNum = GetFlgNum(tagName);
        //    var bits = CheckFlagForBits(flgValue);

        //    foreach (var bit in bits)
        //    {
        //        var protNum = flgNum * 32 + bit.Key;
        //        // Проверяем буффер на наличие защиты
        //        var prot = _protsBuffer.FirstOrDefault(p => p.Number == protNum);

        //        // если в буфере нет такой защиты, то создаем новую защиту и добавляем ее в буферок :) 
        //        if (prot == null && bit.Value)
        //        {
        //            prot = new Protection(protNum);
        //            OnAddProt(prot);
        //        }

        //        if (prot != null)
        //        {
        //            OnFlagValueChanged(prot, bit.Value, isSourceFlag);
        //        }
        //    }
        //}

        //// Обработка изменения флага
        //private static void OnFlagValueChanged(Protection prot, bool flagValue, bool isSourceFlag)
        //{
        //    // Если это источник
        //    if (isSourceFlag)
        //    {
        //        // Взвелся флаг
        //        if (!prot.SourceFlag && flagValue)
        //        {
        //            OnSourceFlagChanged(prot, true);
        //        }
        //        // Снялся флаг
        //        else
        //        {
        //            OnSourceFlagChanged(prot, false);
        //        }
        //        prot.SourceFlag = flagValue;
        //    }
        //    // Значит это флаг стейта
        //    else
        //    {
        //        // Флаг стейта снялся
        //        if (prot.StateFlag && !flagValue)
        //        {
        //            OnStateFlagChanged(prot);
        //        }
        //        prot.StateFlag = flagValue;
        //    }
        //}
        //private static void OnAddProt(Protection prot)
        //{
        //    _logger.Debug($"Сработала защита. Параметры защиты --> " +
        //        $"Name = {prot.Name}, " +
        //        $"Num = {prot.Number}, " +
        //        $"TargetMode = {prot.TargetMode}, " +
        //        $"Timeout = {prot.Timeout}");
        //    _protsBuffer.Add(prot);
        //}
        //private static void OnSourceFlagChanged(Protection prot, bool raise)
        //{
        //    // Флаг взвелся
        //    if (raise)
        //    {
        //        // если целевой режим задан
        //        if (prot.TargetMode > 0)
        //        {
        //            var currentModeIndex = GetCurrentModeIndex();
        //            Mode currentMode = new Mode(currentModeIndex);
        //            var targetModeIndex = GetTargetModeIndex(prot.TargetMode);
        //            Mode targetMode = new Mode(targetModeIndex);
        //            _logger.Debug($"Индекс текущего режима = {currentModeIndex} ({ProjectSettings.Modes.Where(p => p.Key == currentModeIndex).Select(s => s.Value).First()}). " +
        //                $"Индекс целевого режима = {targetModeIndex} ({ProjectSettings.Modes.Where(p => p.Key == targetModeIndex).Select(s => s.Value).First()})");

        //            var suppTable = GetDispSuppTable(currentMode, targetMode);
        //            _logger.Debug("Записываем таблицу поддержки диспетчера...");
        //            CommAdapter.OpcClient.WriteData(suppTable);
        //            _logger.Debug("Таблица поддержки диспетчера записана");
        //        }

        //        // если целевой режим не задан, но выдержка времени больше нуля, то значит будет стоп ту
        //        if (prot.TargetMode == -1 && prot.Timeout >= 0)
        //        {
        //            _logger.Debug("Целевой режим отсутствует, но выдержка больше 0 => СТОП ТУ...");
        //            var tagsValues = new Dictionary<string, object>();
        //            tagsValues.Add($"{ProjectSettings.DispSuppNodePreffix}.ShowSupportTable", true);
        //            tagsValues.Add($"{ProjectSettings.DispSuppNodePreffix}.TargetMode", "-");
        //            CommAdapter.OpcClient.WriteData(tagsValues);
        //        }
        //    }
        //    // Флаг снялся
        //    else
        //    {
        //        _logger.Debug("Скрытие таблицы поддержки диспетчера");
        //        _protsBuffer.Remove(prot);
        //        HideSupportTable();
        //    }
        //}

        //private static void OnStateFlagChanged(Protection prot)
        //{
        //    // Удаляем из буфера защиту и хайдим таблицу
        //    _logger.Debug("Скрытие таблицы поддержки диспетчера");
        //    _protsBuffer.Remove(prot);
        //    HideSupportTable();
        //}
        //private static int GetCurrentModeIndex()
        //{
        //    var tagName = "TZ_OUT_VU.dMODE";
        //    var result = CommAdapter.PlcClient.ReadSync(tagName);
        //    return Convert.ToInt32(result.Result);
        //}
        //private static int GetTargetModeIndex(int protTargetMode)
        //{
        //    return protTargetMode >= 100 ? protTargetMode - ProjectSettings.AddTargetModeNumberOffset : protTargetMode + ProjectSettings.MainTargetModeNumberOffset;
        //}
        //public static Dictionary<string, object> GetDispSuppTable(Mode currentMode, Mode targetMode)
        //{
        //    var stationsCount = ProjectSettings.PumpStationsCount;
        //    var dispSuppPreffix = ProjectSettings.DispSuppNodePreffix;
        //    Dictionary<string, object> tagsValues = new Dictionary<string, object>();
        //    // diff
        //    for (int i = 0; i < stationsCount - 1; i++)
        //    {
        //        // Compare count pumps, ust pin and ust pout
        //        if (currentMode.ModeObjects[i] is PumpStation && targetMode.ModeObjects[i] is PumpStation)
        //        {
        //            if (((PumpStation)currentMode.ModeObjects[i]).MPUCount == ((PumpStation)targetMode.ModeObjects[i]).MPUCount)
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA.IsDiff", false);
        //            }
        //            else
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA.IsDiff", true);
        //            }

        //            if (((PumpStation)currentMode.ModeObjects[i]).UstPin == ((PumpStation)targetMode.ModeObjects[i]).UstPin)
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin.IsDiff", false);
        //            }
        //            else
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin.IsDiff", true);
        //            }

        //            if (((PumpStation)currentMode.ModeObjects[i]).UstPout == ((PumpStation)targetMode.ModeObjects[i]).UstPout)
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout.IsDiff", false);
        //            }
        //            else
        //            {
        //                tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout.IsDiff", true);
        //            }
        //        }

        //        // add tags values
        //        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.CountMNA", ((PumpStation)targetMode.ModeObjects[i]).MPUCount);
        //        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPin", ((PumpStation)targetMode.ModeObjects[i]).UstPin);
        //        tagsValues.Add($"{dispSuppPreffix}.NPS_{i + 1}.UstPout", ((PumpStation)targetMode.ModeObjects[i]).UstPout);
        //    }

        //    // regs (compare reg 10 (21))
        //    if (((PressureRegulator)currentMode.ModeObjects[stationsCount]).UstPin == ((PressureRegulator)targetMode.ModeObjects[stationsCount]).UstPin)
        //    {
        //        tagsValues.Add($"{dispSuppPreffix}.URD_NPS{stationsCount}.UstPin.IsDiff", false);
        //    }
        //    else
        //    {
        //        tagsValues.Add($"{dispSuppPreffix}.URD_NPS{stationsCount}.UstPin.IsDiff", true);
        //    }

        //    tagsValues.Add($"{dispSuppPreffix}.URD_NPS{stationsCount}.UstPin", ((PressureRegulator)targetMode.ModeObjects[stationsCount]).UstPin);

        //    // targetMode
        //    tagsValues.Add($"{dispSuppPreffix}.ShowSupportTable", true);
        //    tagsValues.Add($"{dispSuppPreffix}.TargetMode", targetMode.Name);

        //    return tagsValues;
        //}
        ////public static void SubscribeOnFlags()
        ////{
        ////    // Формируем флаги для подписывания через OPCDA
        ////    var flgTags = new List<string>();
        ////    foreach (var flg in ProjectSettings.Flags)
        ////    {
        ////        flgTags.Add($"{ProjectSettings.TZPreffix}.LCK_SECOND_LEVEL.source.{flg}");
        ////        flgTags.Add($"{ProjectSettings.TZPreffix}.LCK_SECOND_LEVEL.level.{flg}");
        ////    }

        ////    CommAdapter.OpcClient.Subscribe(flgTags.Where(s => s.Contains("source")),
        ////        "FlgSourceGroup",
        ////        1000,
        ////        new Opc.Da.DataChangedEventHandler(OPCClient.OnSourceFlagValueChanged));
        ////    CommAdapter.OpcClient.Subscribe(flgTags.Where(s => s.Contains("level")),
        ////        "FlgLevelGroup",
        ////        1000,
        ////        new Opc.Da.DataChangedEventHandler(OPCClient.OnStateFlagValueChanged));

        ////    _logger.Debug($"Подписан на {flgTags.Count} сигнала (ов)");
        ////}
        //private static void HideSupportTable()
        //{
        //    var tagsValues = new Dictionary<string, object>();
        //    tagsValues.Add($"{ProjectSettings.DispSuppNodePreffix}.ShowSupportTable", false);
        //    CommAdapter.OpcClient.WriteData(tagsValues);
        //}
    }
    #endregion
}
