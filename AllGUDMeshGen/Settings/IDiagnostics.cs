using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllGUD
{
    public interface IDiagnostics
    {
        public string LogFile { get; set; }
        public bool DetailedLog { get; set; }
        public Logger logger { get; }
    }
}
