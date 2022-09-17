using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedMapTool
{
    public class Loc
    {
        public float x;
        public float y;
        public float z;
    }
    public class ParseInfo
    {
        public List<Loc> locations = new List<Loc>();
        public string displayedName;
        public string zoneName;
        public string filePath;
        public string fileName;
    }
}
