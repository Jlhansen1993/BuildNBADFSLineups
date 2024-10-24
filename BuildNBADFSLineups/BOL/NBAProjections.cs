using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildNBADFSLineups.BOL
{
    public class NBAProjection
    {
        public string Id { get; set; }
        public string DFFId { get; set; }
        public string Name { get; set; }   
        public string Team { get; set; }
        public int Salary { get; set; }
        public string Position { get; set; }
        public double FantasyPointsPerGame { get; set; }
        public double DFFFProjectedFantasyPoints { get; set; }
        public double NFProjectedFantasyPoints { get; set; }
        public double RWProjectedFantasyPoints { get; set; }
        public double FinalFantasyPoints { get; set; }
    }

    public class NBAProjectionList : List<NBAProjection> { }
}
