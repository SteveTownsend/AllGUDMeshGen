using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Skeleton
    {
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("Must be a valid readable path in your game setup. Use / separators between path components. Can be relative to Game Data location e.g. 'mods/AllGUD Output/meshes'. Leave blank to use 'meshes/actors/character/'. Absolute path is allowed.")]
        [SynthesisDescription("Path to search for Skeleton meshes.")]
        public string InputFolder { get; set; } = "";

        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("Must be a valid writable path in your game setup. e.g. 'mods/AllGUD Output'. Use / separators between path components. Absolute path is allowed.")]
        [SynthesisDescription("Path where transformed Skeleton meshes are written.")]
        public string OutputFolder { get; set; } = "";

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
            return errors;
        }
    }
}
