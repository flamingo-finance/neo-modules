using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo;

namespace Neo.Plugins
{
    public class PerpSettings
    {

        public static PerpSettings Default { get; set; }

        public UInt160 PerpContract { get; set; }
    }
}
