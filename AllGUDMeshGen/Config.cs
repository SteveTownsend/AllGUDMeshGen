using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace AllGUD
{
    internal class Config
    {
        public string skeletonInputFolder { get; }
        public string skeletonOutputFolder { get; }
        public string meshGenInputFolder { get; }
        public string meshGenOutputFolder { get; }
        public string[] nameFilters { get; }

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

                var meshGenKeys = configJson["meshGen"]!;
                meshGenInputFolder = (string)meshGenKeys["inputFolder"]!;
                meshGenOutputFolder = (string)meshGenKeys["outputFolder"]!;
                Console.WriteLine(String.Format("MeshGen input folder='{0}' output folder = '{1}'", meshGenInputFolder, meshGenOutputFolder));
                string nameFilter = (string)meshGenKeys["nameFilter"]!;
                if (!String.IsNullOrEmpty(nameFilter))
                {
                    nameFilters = Array.ConvertAll(nameFilter.Split(','), d => d.ToLower());
                }
                else
                {
                    nameFilters = new string[0];
                }
            }
        }

        public bool IsNifValid(string nifPath)
        {
            foreach (string filter in nameFilters)
            {
                if (!nifPath.Contains(filter))
                    return false;
            }
            return true;
        }
    }
}
