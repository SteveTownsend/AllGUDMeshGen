using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class ItemPosition
    {
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("Must be a valid readable path on your computer. Leave blank to use Game Data location in your Mod Manager VFS + 'meshes/'. Relative or absolute path is allowed.")]
        [SynthesisDescription("Path to search for Item Back/Center meshes using the pattern '*BC.nif'.")]
        public string InputFolder { get; set; } = "";

        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("Must be a valid writable path on your computer. Allow patch-provided Item Position meshes to load after this, to override default Item Positioning with Custom Meshes. Typically this points to a new mod directory in your Mod Manager VFS, e.g. 'D:/ModdedSkyrim/mods/AllGUD Output'. Patcher appends 'meshes/' to allow the game to find the output meshes. Relative or absolute path is allowed.")]
        [SynthesisDescription("Path where Item Positioning meshes are written.")]
        public string OutputFolder { get; set; } = "";
        //public string OutputFolder { get; set; } = "j:/omegalotd/tools/mods/AllGUD Patcher";

        private List<string[]> _nifBlackList = new List<string[]>();
        private static List<string> DefaultBlackList()
        {
            var defaults = new List<string>();
            // Facegen NIF matches if FormID is xxxxBC
            defaults.Add("facegeom,facegendata");
            // Instruments excluded in xEdit script
            defaults.Add("instruments");
            return defaults;
        }
        private static readonly List<string[]> _defaultNifBlackList = NifFilters.ParseNifFilters(DefaultBlackList());
        [SynthesisSettingName("BlackList Patterns")]
        [SynthesisTooltip("Each entry is a comma-separated list of strings. Every string must match for a mesh to be excluded.")]
        [SynthesisDescription("List of patterns for excluded mesh names.")]
        public List<string> NifBlackList
        {
            get { return NifFilters.BuildNifFilters(_nifBlackList); }
            set { _nifBlackList = NifFilters.ParseNifFilters(value); }
        }
        private List<string[]>? _fullBlackList;
        private List<string[]> fullBlackList
        {
            get
            {
                if (_fullBlackList is null)
                    _fullBlackList = new List<string[]>(_nifBlackList.Concat(_defaultNifBlackList));
                return _fullBlackList;
            }
        }

        [SynthesisSettingName("Male")]
        [SynthesisTooltip("TODO")]
        [SynthesisDescription("Settings specific to Male Model")]
        public ItemPositionM Male { get; set; } = new();

        [SynthesisSettingName("Female")]
        [SynthesisTooltip("TODO")]
        [SynthesisDescription("Settings specific to Female Model")]
        public ItemPositionF Female { get; set; } = new();

        public bool IsNifValid(string nifPath)
        {
            // check blacklist, exclude NIF if all substrings in an entry match
            foreach (string[] filterElements in fullBlackList)
            {
                if (filterElements
                    .Where(x => !string.IsNullOrEmpty(x))
                    .All(v => nifPath.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            return true;
        }

        public List<string> GetConfigErrors()
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
            if (InputFolder == OutputFolder)
            {
                errors.Add(String.Format("Item Positioner cannot use {0} as both Input and Output Folder", InputFolder));
            }
            return errors;
        }
    }
}
