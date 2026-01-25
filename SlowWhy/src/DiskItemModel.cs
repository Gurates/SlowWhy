using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlowWhy
{
    public class DiskItemModel
    {
        public string Name { get; set; }
        public string DisplaySize { get; set; }
        public long RawSize { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
    }
}
