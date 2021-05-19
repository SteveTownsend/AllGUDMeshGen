using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Meshes
    {
        private string _inputFolder = "";
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup. Use / separators between path components.")]
        [SynthesisDescription("Path to search for Weapon and Armour meshes.")]
        public string inputFolder
        {
            get { return _inputFolder; }
            set { _inputFolder = Helper.EnsureInputPathIsValid(value); }
        }

        private string _outputFolder = "";
        
        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup - usually something like 'mods/AllGUD Output/meshes'. Use / separators between path components.")]
        [SynthesisDescription("Path where transformed Weapon and Armour meshes are written.")]
        public string outputFolder
        {
            get { return _outputFolder; }
            set { _outputFolder = Helper.EnsureOutputPathIsValid(value); }
        }
        private IList<string[]> _skipNifs = new List<string[]>();
        [SynthesisSettingName("Meshes to skip")]
        [SynthesisTooltip("This must be a valid path in your game setup - usually something like 'mods/AllGUD Output/meshes'. Use / separators between path components.")]
        [SynthesisDescription("Path where transformed Weapon and Armour meshes are written.")]
        public string skipNifs
        {
            get { return ""; }
            set { _skipNifs = ParseNifFilter(value); }
        }

        private IList<string[]> _nameFilters = new List<string[]>();
        [SynthesisSettingName("Meshes to skip")]
        [SynthesisTooltip("This must be a valid path in your game setup - usually something like 'mods/AllGUD Output/meshes'. Use / separators between path components.")]
        [SynthesisDescription("Path where transformed Weapon and Armour meshes are written.")]
        public string nameFilters
        {
            get { return ""; }
            set { _nameFilters = ParseNifFilter(value); }
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

        public bool IsNifValid(string nifPath)
        {
            // check blacklist, exclude NIF if all substrings in an entry match
            foreach (string[] filterElements in _skipNifs)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            // if not blacklisted, check whitelist if present
            if (_nameFilters.Count == 0)
            {
                // allow all iff no filters
                return true;
            }
            foreach (string[] filterElements in _nameFilters)
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
