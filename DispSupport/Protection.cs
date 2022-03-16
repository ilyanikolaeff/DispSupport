using System;
using System.Collections.Generic;
using System.Linq;

namespace DispSupport
{
    class Protection
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public int Timeout { get; set; }
        public int TargetMode { get; set; }
        public bool SourceFlag { get; set; }
        public bool StateFlag { get; set; }
        public DateTime FearTime { get; set; }
        public bool Masked { get; set; }

        public Protection(int protNum)
        {
            Number = protNum;
        }

        public override string ToString()
        {
            return $"Номер защиты = {Number}, название защиты = {Name}, время срабатывания = {FearTime}, " +
                $"выдержка = {Timeout}, номер целевого режима = {TargetMode}, замаскирована = {Masked}";
        }

        public void ReadParameters(CommAdapter commAdapter, out bool isSuccessfullyRead)
        {
            var flgNum = Number / 32;
            var bitNum = Number % 32;

            var plcReadResults = commAdapter.PlcClient.ReadSync(new List<string>()
            {
                $"SET_LCK_SECOND_LEVEL[{Number}].dTARGET_MODE",
                $"SET_LCK_SECOND_LEVEL[{Number}].dTIMEOUT",
                $"LCK_OUT_VU_SECOND_LEVEL.dMASK[{flgNum}]",
                $"LCK_OUT_VU_SECOND_LEVEL.dTor[{flgNum}]"
            });

            if (plcReadResults == null)
            {
                isSuccessfullyRead = false;
                return;
            }

            int.TryParse(plcReadResults[0].Result.ToString(), out int targetMode);
            TargetMode = targetMode;

            int.TryParse(plcReadResults[1].Result.ToString(), out int timeout);
            Timeout = timeout;

            // IsMasked (NEW)
            int.TryParse(plcReadResults[2].Result.ToString(), out int flgMaskValue);
            int.TryParse(plcReadResults[2].Result.ToString(), out int flgTorValue);
            var modeBits = Helper.CheckBits(flgMaskValue);
            var torBits = Helper.CheckBits(flgTorValue);

            // IsMasked (OLD)
            //var tagNames = new List<string>();
            //tagNames.Add($"{tzPreffix}.LCK_SECOND_LEVEL.mode.mode{flgNum}");
            //tagNames.Add($"{tzPreffix}.LCK_SECOND_LEVEL.tor.tor{flgNum}");

            //var opcReadResults = commAdapter.OpcClient.ReadData(tagNames);
            //if (opcReadResults.ToList().Where(p => p.ResultID != Opc.ResultID.S_OK).Count() != 0)
            //{
            //    isSuccessfullyRead = false;
            //    return;
            //}

            //var modeBits = Helper.CheckBits(Convert.ToInt32(opcReadResults[0].Value));
            //var torBits = Helper.CheckBits(Convert.ToInt32(opcReadResults[1].Value));
            Masked = modeBits.Where(p => p.Key == bitNum).Select(s => s.Value).FirstOrDefault() || torBits.Where(p => p.Key == bitNum).Select(s => s.Value).FirstOrDefault();
            isSuccessfullyRead = true;
        }
    }
}
