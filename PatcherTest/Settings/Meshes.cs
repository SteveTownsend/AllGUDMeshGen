using System.Collections.Generic;
using AllGUD;

namespace PatcherTest
{
    public class Meshes : IMeshes, IConfigErrors
    {
        public string InputFolder { get; set; } = "../../../Data/";
        public string OutputFolder { get; set; } = "../../../Data/Output/";
        public bool MirrorStaves { get; set; } = true;
        public List<string> NifBlackList { get; set; } = new List<string>();
        public List<string> NifWhiteList { get; set; } = new List<string>();
        public bool IsNifValid(string nifPath)
        {
            return true;
        }
        public List<string> GetConfigErrors()
        {
            return new List<string>();
        }
    }
}
