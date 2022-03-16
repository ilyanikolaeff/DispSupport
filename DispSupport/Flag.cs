using System;
using System.Collections.Generic;

namespace DispSupport
{
    class Flag
    {
        public int Num { get; set; }
        public List<Bit> Bits { get; set; }
        public Flag()
        {
            Bits = new List<Bit>();
        }

        public static int GetFlagNumber(string tagName)
        {
            var parts = tagName.Split('.');
            return Convert.ToInt32(parts[parts.Length - 1].Replace("flg", "").Replace("tor", "").Replace("mode", ""));
        }

    }

    class Bit
    {
        public int Num { get; set; }
        public string Description { get; set; }
    }
}
