using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedMapTool
{
    [Serializable]
    public class Loc
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }
    [Serializable]
    public class ParseInfo
    {
        public List<Loc> locations { get; set; } = new List<Loc>();
        public string displayedName { get; set; }
        public string zoneName { get; set; }
        public string filePath { get; set; }
        public string fileName { get; set; }
        public string race { get; set; }
        public string className { get; set; }
        public int level { get; set; }
    }
}
