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
            // Fix ' issues.
            name = name.Replace("&#x27;", "'");
            name = name.Replace("''", "'");

            switch (name)
            {
                case "Mitchell Marner":
                    name = "Mitch Marner";
                    break;
            }

            return name.Trim();
        }
    }
}
