using System;
using System.Collections.Generic;
using System.Linq;

namespace DispSupport
{
    class Mode
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public List<ModeObject> ModeObjects { get; set; }

        private List<OperationResult> _results = new List<OperationResult>();
        public Mode(int index)
        {
            Index = index;
        }
        public override string ToString()
        {
            return $"Номер режима = {Index}, название режима = {Name}";
        }

        public void ReadParameters(CommAdapter commAdapter, int stationsCount, out bool isSuccessfullyRead)
        {
            // Костыль из-за того, что режимы по разному хранятся в ТУ-1 и ТУ-2
            bool tu2 = stationsCount == 12;
            int modeParIndex;
            if (tu2)
                modeParIndex = (stationsCount - 1) * 2;
            else
                modeParIndex = stationsCount * 2;

            // Формируем теги для считывания
            var tagRequest = new List<string>();
            for (int i = 0; i < stationsCount - 1; i++)
            {
                // mpu, spu
                tagRequest.Add($"MODE_SET[{Index}].MPU[{i}]");
                tagRequest.Add($"MODE_SET[{Index}].SPU[{i}]");

                // ust

                tagRequest.Add($"MODE_SET[{Index}].P[{i * 2}]");
                tagRequest.Add($"MODE_SET[{Index}].P[{i * 2 + 1}]");


                // pu
                tagRequest.Add($"MODE_SET[{Index}].PU[{i}]");
            }
            // regulators
            tagRequest.Add($"MODE_SET[{Index}].P[{modeParIndex}]");
            tagRequest.Add($"MODE_SET[{Index}].P[{modeParIndex + 1}]");

            // READ FROM PLC
            _results = commAdapter.PlcClient.ReadSync(tagRequest);
            if (_results == null)
            {
                isSuccessfullyRead = false;
                return;
            }

            // TO-DO Generic method
            ModeObjects = new List<ModeObject>();
            bool isSuccessConvert;
            string queryString;
            int intValue;
            double doubleValue;
            for (int i = 0; i < stationsCount - 1; i++)
            {
                var currentPumpStation = new PumpStation();

                // Количество МНА
                queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].MPU[{i}]").Select(s => s.Result).FirstOrDefault().ToString();
                isSuccessConvert = int.TryParse(queryString, out intValue);
                if (!isSuccessConvert)
                    intValue = int.MinValue;
                currentPumpStation.MPUCount = intValue;

                // Количество ПНА
                queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].SPU[{i}]").Select(s => s.Result).FirstOrDefault().ToString();
                isSuccessConvert = int.TryParse(queryString, out intValue);
                if (!isSuccessConvert)
                    intValue = int.MinValue;
                currentPumpStation.SPUCount = intValue;

                // Уставка по давлению ВХОД
                queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].P[{i * 2}]").Select(s => s.Result).FirstOrDefault().ToString().Replace(".", ",");
                isSuccessConvert = double.TryParse(queryString, out doubleValue);
                if (!isSuccessConvert)
                    doubleValue = int.MinValue;
                currentPumpStation.UstPin = doubleValue;

                // Уставка по давлению ВЫХОД
                queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].P[{i * 2 + 1}]").Select(s => s.Result).FirstOrDefault().ToString().Replace(".", ",");
                isSuccessConvert = double.TryParse(queryString, out doubleValue);
                if (!isSuccessConvert)
                    doubleValue = int.MinValue;
                currentPumpStation.UstPout = doubleValue;

                // Состояние узлов ПУ
                queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].PU[{i}]").Select(s => s.Result).FirstOrDefault().ToString();
                isSuccessConvert = int.TryParse(queryString, out intValue);
                if (!isSuccessConvert)
                    intValue = int.MinValue;
                currentPumpStation.PUStatus = intValue;

                ModeObjects.Add(currentPumpStation);
            }

            // 8 (19)
            queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].P[{modeParIndex}]").Select(s => s.Result).FirstOrDefault().ToString().Replace(".", ",");
            isSuccessConvert = Double.TryParse(queryString, out doubleValue);
            if (!isSuccessConvert)
                doubleValue = int.MinValue;
            ModeObjects.Add(new PressureRegulator() { UstPin = doubleValue });

            // 10 (21)
            queryString = _results.Where(p => p.Tag == $"MODE_SET[{Index}].P[{modeParIndex + 1}]").Select(s => s.Result).FirstOrDefault().ToString().Replace(".", ",");
            isSuccessConvert = Double.TryParse(queryString, out doubleValue);
            if (!isSuccessConvert)
                doubleValue = int.MinValue;
            ModeObjects.Add(new PressureRegulator() { UstPin = doubleValue });

            isSuccessfullyRead = true;
        }
    }
}
