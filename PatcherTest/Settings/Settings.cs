using System.Collections.Generic;
using AllGUD;

namespace PatcherTest
{
    public class Settings : ISettings, IConfigErrors
    {
        public string allGUDVersion { get; } = "1.5.6";

        public IDiagnostics diagnostics { get; set; } = new Diagnostics();

        public ISkeleton skeleton { get; set; } = new Skeleton();

        public IMeshes meshes { get; set; } = new Meshes();
        public List<string> GetConfigErrors()
        {
            return new List<string>();
        }
    }
}
