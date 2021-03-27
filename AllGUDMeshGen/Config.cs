using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace AllGUD
{
    public class Config
    {
        public string skeletonInputFolder { get; set; }
        public string skeletonOutputFolder { get; set; }

        public Config(string configFilePath)
        {
            // override if config is well-formed
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("\"config.json\" cannot be found in the users Data folder, aborting.");
                throw new InvalidDataException("\"config.json\" cannot be found in the users Data folder, aborting.");
            }
            else
            {
                JObject configJson = JObject.Parse(File.ReadAllText(configFilePath));
                var skeletonKeys = configJson["skeleton"]!;
                skeletonInputFolder = (string)skeletonKeys["inputFolder"]!;
                skeletonOutputFolder = (string)skeletonKeys["outputFolder"]!;
                Console.WriteLine(String.Format("Skeleton input folder='{0}' output folder = '{1}'", skeletonInputFolder, skeletonOutputFolder));
            }
        }
    }
}
