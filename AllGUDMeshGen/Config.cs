using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AllGUD
{
    internal class Config
    {
        public string skeletonInputFolder { get; }
        public string skeletonOutputFolder { get; }
        public string meshGenInputFolder { get; }
        public string meshGenOutputFolder { get; }
        
        public bool mirrorStaves { get; }
        public IList<string[]> nameFilters { get; }

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

                mirrorStaves = (bool)meshGenKeys["mirrorStaves"]!;
                Console.WriteLine(String.Format("Mirror left staff meshes ? '{0}'", mirrorStaves));
                string nameFilter = (string)meshGenKeys["nameFilter"]!;
                nameFilters = new List<string[]>();
                if (!String.IsNullOrEmpty(nameFilter))
                {
                    foreach (string filter in nameFilter.Split('|'))
                    {
                        string[] filterElements = filter.Split(',');
                        if (filterElements.Length > 0)
                        {
                            nameFilters.Add(filterElements);
                        }
                    }
                }
            }
        }

        public bool IsNifValid(string nifPath)
        {
            foreach (string[] filterElements in nameFilters)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return nameFilters.Count == 0;  // allow all iff no filters
        }
    }
}
