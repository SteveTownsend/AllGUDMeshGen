using System;
using System.IO;
using Xunit;
using AllGUD;
using nifly;

namespace PatcherTest
{
    public class UnitTest1
    {
        private readonly string ConfigLocation = "../../../Data/config.json";

        [Fact]
        public void TransformTestNifs()
        {
            var config = new Config(ConfigLocation);
            MeshHandler meshHandler = new MeshHandler(config);
            foreach (string nifFile in Directory.EnumerateFiles(config.meshGenInputFolder, "*.nif"))
            {
                using NifFile originalNif = new NifFile();
                originalNif.Load(nifFile);
                meshHandler.GenerateMeshes(originalNif, Path.GetFileName(nifFile), MeshHandler.ModelType.Unknown);
            }
        }
    }
}
