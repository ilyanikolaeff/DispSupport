using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DispSupport
{
    class Helper
    {
        #region Helper tools (converters)
        public static void ConvertProtNames(string fileName)
        {
            var lines = File.ReadAllLines(fileName);
            var flgs = new Dictionary<int, Dictionary<int, string>>();
            foreach (var line in lines)
            {
                var lineParts = line.Split('\t');
                var protNum = Convert.ToInt32(lineParts[0]);
                var protDescription = lineParts[1];
                var flgNum = protNum / 32;
                var bitNum = protNum % 32;

                if (flgs.ContainsKey(flgNum))
                {
                    if (flgs[flgNum].ContainsKey(bitNum))
                        continue;
                    else
                        flgs[flgNum].Add(bitNum, protDescription);
                }
                else
                {
                    flgs.Add(flgNum, new Dictionary<int, string>());
                    flgs[flgNum].Add(bitNum, protDescription);
                }
            }

            // to xml
            var xRoot = new XElement("root");
            var xFlags = new XElement("Flags");
            foreach (var flg in flgs)
            {
                var xFlg = new XElement("Flg");
                xFlg.SetAttributeValue("Num", flg.Key);
                foreach (var bit in flg.Value)
                {
                    var xBit = new XElement("Bit");
                    xBit.SetAttributeValue("Num", bit.Key);
                    xBit.SetAttributeValue("Description", bit.Value);
                    xFlg.Add(xBit);
                }
                xFlags.Add(xFlg);
            }
            xRoot.Add(xFlags);
            xRoot.Save("ConvertedProtNames.xml");
        }
        // modes.cvs 101, 2 свойства 
        public static void ConvertModeNames(string fileName)
        {
            var lines = File.ReadAllLines(fileName, Encoding.GetEncoding(1251));
            var modes = new Dictionary<string, List<Property>>();
            foreach (var line in lines)
            {
                var lineParts = line.Split(',');
                var tagName = lineParts[0];
                var propNum = lineParts[1];
                var propValue = lineParts[3];

                if (modes.ContainsKey(tagName))
                {
                    modes[tagName].Add(new Property() { Num = Convert.ToInt32(propNum), Value = propValue });
                }
                else
                {
                    modes.Add(tagName, new List<Property>());
                    modes[tagName].Add(new Property() { Num = Convert.ToInt32(propNum), Value = propValue });
                }
            }

            // export to xml
            var xRoot = new XElement("root");
            var xModes = new XElement("Modes");
            foreach (var mode in modes)
            {
                var modeName = mode.Value.Where(p => p.Num == 101).Select(s => s.Value).FirstOrDefault();
                var modeNum = mode.Value.Where(p => p.Num == 2).Select(s => s.Value).FirstOrDefault();
                var xMode = new XElement("Mode");
                xMode.SetAttributeValue("Num", modeNum.ToString().Trim());
                xMode.SetAttributeValue("Name", modeName);
                xModes.Add(xMode);
            }
            xRoot.Add(xModes);
            xRoot.Save("modes.xml");
        }
        #endregion
        public static Dictionary<int, bool> CheckBits(int flgValue)
        {
            var bits = new Dictionary<int, bool>();
            // Конвертим в строку, слева добавляем нулевые биты до 32 битов
            var flgBinary = Convert.ToString(flgValue, 2).PadLeft(32, '0');
            // Читаем с конца (от младщего к старшему биту)
            int bitsCount = 0;
            for (int i = flgBinary.Length - 1; i >= 0; i--)
            {
                bits.Add(bitsCount, flgBinary[i] == '1');
                bitsCount++;
            }
            return bits;
        }

        //public static T ConvertValue<T>(object value)
        //{
        //    try
        //    {
        //        T convertedValue = (T)Convert.ChangeType(value, typeof(T));
        //        return convertedValue;
        //    }
        //    catch (Exception ex)
        //    {
        //        T convertedValue = (T)Convert.ChangeType(Int32.MinValue, typeof(T));
        //        return convertedValue;
        //    }
        //}
    }
    internal class Property
    {
        public int Num { get; set; }
        public object Value { get; set; }
    }
}
