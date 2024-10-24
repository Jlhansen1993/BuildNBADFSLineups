using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildNBADFSLineups.Utilities
{
    public static class DataCleanup
    {
        public static string FixNames(string name)
        {
            // Adjust player name.
            if (name.EndsWith("DTD"))
            {
                name = name.Substring(0, name.Length - 3);
            }

            if (name.EndsWith("Q"))
            {
                name = name.Substring(0, name.Length - 1);
            }

            // Fix ' issues.
            name = name.Replace("&#x27;", "'");
            name = name.Replace("''", "'");
            name = name.Replace("ü", "u");
            name = name.Replace("é", "e");
            name = name.Replace("ö", "o");
            name = name.Replace("Ş", "S");
            name = name.Replace("ć", "c");
            name = name.Replace("č", "c");
            name = name.Replace("ū", "u");
            name = name.Replace("Š", "S");

            // Remove suffixes.
            if(name.EndsWith(" Jr."))
            {
                name = name.Substring(0, name.Length - 4);
            }
            else if(name.EndsWith(" II"))
            {
                name = name.Substring(0, name.Length - 3);
            }
            else if(name.EndsWith(" III"))
            {
                name = name.Substring(0, name.Length - 4);
            }
            else if(name.EndsWith(" IV"))
            {
                name = name.Substring(0, name.Length - 3);
            }

            switch (name)
            {
                case "Cameron Thomas":
                    name = "Cam Thomas";
                    break;
                case "Nicolas Claxton":
                    name = "Nic Claxton";
                    break;
                case "C.J. McCollum":
                    name = "CJ McCollum";
                    break;
                case "A.J. Green":
                    name = "AJ Green";
                    break;
                case "Kenyon Martin":
                    name = "KJ Martin";
                    break;
                case "PJ Washington":
                    name = "P.J. Washington";
                    break;
            }

            return name.Trim();
        }
    }
}
