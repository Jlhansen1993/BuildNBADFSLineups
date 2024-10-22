using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildNBADFSLineups.BOL
{
    public class NBAEvent
    {
        public string EntryId { get; set; }
        public string ContestId { get; set; }
        public string ContestName { get; set; }
        public string PG1 { get; set; }
        public string PG2 { get; set; }
        public string SG1{ get; set; }
        public string SG2 { get; set; }
        public string SF1 { get; set; }
        public string SF2 { get; set; }
        public string PF1 { get; set; }
        public string PF2 { get; set; }
        public string C { get; set; }
    }

    public class NBAEventList : List<NBAEvent> { }
}
