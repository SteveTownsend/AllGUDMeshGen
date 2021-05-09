using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AllGUD
{
    public class Config : IDisposable
    {
        public string skeletonInputFolder { get; }
        public string skeletonOutputFolder { get; }
        public string meshGenInputFolder { get; }
        public string meshGenOutputFolder { get; }
        
        public bool mirrorStaves { get; }
        public IList<string[]> skipNifs { get; }
        public IList<string[]> nameFilters { get; }

        public bool detailedLog { get; }
        public string logFile { get; }

        public Logger logger { get; }

        private string AsAbsolutePath(string path)
        {
            if (String.IsNullOrEmpty(path))
                return String.Empty;
            return Path.GetFullPath(path);
        }

        private IList<string[]> ParseNifFilter(string filterData)
        {
            IList<string[]> nifFilter = new List<string[]>();
            if (!String.IsNullOrEmpty(filterData))
            {
                foreach (string filter in filterData.Split('|'))
                {
                    string[] filterElements = filter.Split(',');
                    if (filterElements.Length > 0)
                    {
                        nifFilter.Add(filterElements);
                    }
                }
            }
            return nifFilter;
        }

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
                var generalKeys = configJson["general"]!;
                detailedLog = (bool)generalKeys["detailedLog"]!;
                logFile = (string)generalKeys["logFile"]!;
                logger = new Logger(logFile);
                if (!string.IsNullOrEmpty(logFile))
                {
                    logger.WriteLine("Recording progress in log file {0} as well as to console", logFile);
                }
                logger.WriteLine("Use detailed logging output to console? {0}", detailedLog);

                var skeletonKeys = configJson["skeleton"]!;
                skeletonInputFolder = AsAbsolutePath((string)skeletonKeys["inputFolder"]!);
                skeletonOutputFolder = AsAbsolutePath((string)skeletonKeys["outputFolder"]!);
                logger.WriteLine("Skeleton input folder='{0}' output folder = '{1}'", skeletonInputFolder, skeletonOutputFolder);

                var meshGenKeys = configJson["meshGen"]!;
                meshGenInputFolder = AsAbsolutePath((string)meshGenKeys["inputFolder"]!);
                meshGenOutputFolder = AsAbsolutePath((string)meshGenKeys["outputFolder"]!);
                logger.WriteLine("MeshGen input folder='{0}' output folder = '{1}'", meshGenInputFolder, meshGenOutputFolder);

                mirrorStaves = (bool)meshGenKeys["mirrorStaves"]!;
                logger.WriteLine("Mirror left staff meshes ? '{0}'", mirrorStaves);
                skipNifs = ParseNifFilter((string)meshGenKeys["skipNifs"]!);
                nameFilters = ParseNifFilter((string)meshGenKeys["nameFilter"]!);
            }
        }

        public void Dispose()
        {
            if (logger != null)
            {
                logger.Dispose();
            }
        }

        public bool IsNifValid(string nifPath)
        {
            // check blacklist, exclude NIF if all substrings in an entry match
            foreach (string[] filterElements in skipNifs)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            // if not blacklisted, check whitelist if present
            if (nameFilters.Count == 0)
            {
                // allow all iff no filters
                return true;
            }
            foreach (string[] filterElements in nameFilters)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;  // disallow all if none of the >= 1 whitelist filters matched
        }
    }
}
