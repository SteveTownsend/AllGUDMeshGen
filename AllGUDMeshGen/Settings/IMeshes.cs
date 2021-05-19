using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllGUD
{
    public interface IMeshes
    {
        public string InputFolder { get; set; }
        public string OutputFolder { get; set; }
        public bool MirrorStaves { get; set; }
        public List<string> NifBlackList { get; set; }
        public List<string> NifWhiteList { get; set; }
        public bool IsNifValid(string nifPath);
    }
}
