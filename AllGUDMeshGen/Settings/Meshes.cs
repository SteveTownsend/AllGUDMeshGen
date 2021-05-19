using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Meshes : IMeshes
    {
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup. Use / separators between path components. Can use relative path to Game Data location e.g. 'mods/AllGUD Output/meshes'. Leave blank to use 'meshes/actors/character/'. Absolute path is allowed. Use / separators between path components.")]
        [SynthesisDescription("Path to search for Weapon and Armour meshes.")]
        override public string InputFolder { get; set; } = "";

        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup. Use / separators between path components. Can use relative path to Game Data location e.g. 'mods/AllGUD Output/meshes'. Absolute path is allowed.")]
        [SynthesisDescription("Path where transformed Weapon and Armour meshes are written.")]
        override public string OutputFolder { get; set; } = "";


        [SynthesisSettingName("Mirror Staves")]
        [SynthesisTooltip("If unchecked, meshes used in Staff Weapon records will not be included in left-handed mesh generation.")]
        [SynthesisDescription("Generate left hand mesh for Staves.")]
        override public bool MirrorStaves { get; set; } = true;

        private List<string[]> _nifBlackList = new List<string[]>();
        private static List<string> DefaultBlackList()
        {
            var defaults = new List<string>();
            // For Animated Armoury:
            // - download Animated Armoury All Geared Up Derivative from https://www.nexusmods.com/skyrimspecialedition/mods/15394?tab=files
            // - install to load later than all Animated Armoury meshes
            // - delete all the meshes except Fists
            defaults.Add("NewArmoury,Fists,Claw");
            // SkyRe_Main.esp has farmers gloves defined as a sword, crashes during transform
            defaults.Add("SkyRe_Main,farmclothes03,farmerglovesm_0");
            return defaults;
        }
        private static readonly List<string[]> _defaultNifBlackList = ParseNifFilters(DefaultBlackList());
        [SynthesisSettingName("BlackList Patterns")]
        [SynthesisTooltip("Each entry is a comma-separated list of strings. Every string must match for a mesh to be excluded. A mesh that matches a BlackList entry cannot be WhiteListed.")]
        [SynthesisDescription("List of patterns for excluded mesh names.")]
        override public List<string> NifBlackList
        {
            get { return BuildNifFilters(_nifBlackList); }
            set { _nifBlackList = ParseNifFilters(value); }
        }
        private List<string[]>? _fullBlackList;
        private List<string[]> fullBlackList
        {
            get {
                if (_fullBlackList is null)
                    _fullBlackList = new List<string[]>(_nifBlackList.Concat(_defaultNifBlackList));
                return _fullBlackList;
            }
        }

        private List<string[]> _nifWhiteList = new List<string[]>();
        [SynthesisSettingName("WhiteList Patterns")]
        [SynthesisTooltip("Each entry is a comma-separated list of strings. Every string must match for a non-BlackListed mesh to be included.")]
        [SynthesisDescription("List of patterns for included mesh names.")]
        override public List<string> NifWhiteList
        {
            get { return BuildNifFilters(_nifWhiteList); }
            set { _nifWhiteList = ParseNifFilters(value); }
        }

        private static List<string[]> ParseNifFilters(IList<string> filterData)
        {
            List<string[]> nifFilter = new List<string[]>();
            foreach (string filter in filterData)
            {
                if (!String.IsNullOrEmpty(filter))
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

        private static List<string> BuildNifFilters(IList<string[]> filters)
        {
            List<string> nifFilters = new List<string>();
            foreach (string[] filter in filters)
            {
                nifFilters.Add(String.Join(',', filter));
            }
            return nifFilters;
        }

        override public bool IsNifValid(string nifPath)
        {
            // check blacklist, exclude NIF if all substrings in an entry match
            foreach (string[] filterElements in fullBlackList)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            // if not blacklisted, check whitelist if present
            if (_nifWhiteList.Count == 0)
            {
                // allow all iff no filters
                return true;
            }
            foreach (string[] filterElements in _nifWhiteList)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;  // disallow all if none of the >= 1 whitelist filters matched
        }

        override public List<string> GetConfigErrors()
        {
            List<string> errors = new List<string>();
            try
            {
                InputFolder = Helper.EnsureInputPathIsValid(InputFolder);
            }
            catch (Exception e)
            {
                errors.Add(e.GetBaseException().ToString());
            }
            try
            {
                OutputFolder = Helper.EnsureOutputPathIsValid(OutputFolder);
            }
            catch (Exception e)
            {
                errors.Add(e.GetBaseException().ToString());
            }
            return errors;
        }
    }
}
