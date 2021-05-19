using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Skeleton
    {
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("Must be a valid readable path on your computer. Leave blank to use Game Data location in your Mod Manager VFS + 'meshes/actors/character/'. Relative or absolute path is allowed.")]
        [SynthesisDescription("Path to search for Skeleton meshes.")]
        public string InputFolder { get; set; } = "";

        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("Must be a valid writable path on your computer. Typically this points to a new mod directory in your Mod Manager VFS, e.g. 'D:/ModdedSkyrim/mods/AllGUD Output'. Patcher appends 'meshes/actors/character/' to allow the game to find the output meshes. Relative or absolute path is allowed.")]
        [SynthesisDescription("Path where transformed Skeleton meshes are written.")]
        public string OutputFolder { get; set; } = "";
        //public string OutputFolder { get; set; } = "j:/omegalotd/tools/mods/AllGUD Patcher";

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
                errors.Add(String.Format("Skeleton Patcher cannot use {0} as both Input and Output Folder", InputFolder));
            }
            return errors;
        }
    }
}
