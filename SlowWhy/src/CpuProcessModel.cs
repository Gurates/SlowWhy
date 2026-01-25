using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlowWhy
{
    public class CpuProcessModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Usage { get; set; }
        public double UsageRaw { get; set; }
    }
}
