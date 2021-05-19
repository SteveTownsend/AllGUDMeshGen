using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllGUD
{
    public interface ISettings
    {
        public string allGUDVersion { get; }
        public IDiagnostics diagnostics { get; set; }
        public ISkeleton skeleton { get; set; }
        public IMeshes meshes { get; set; }
    }
}
