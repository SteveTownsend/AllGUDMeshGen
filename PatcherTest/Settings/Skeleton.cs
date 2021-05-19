using System.Collections.Generic;
using AllGUD;

namespace PatcherTest
{
    public class Skeleton : ISkeleton, IConfigErrors
    {
        public string InputFolder { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public List<string> GetConfigErrors()
        {
            return new List<string>();
        }
    }
}
