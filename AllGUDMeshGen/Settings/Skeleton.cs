using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Skeleton
    {
        private string _inputFolder = "";
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("Must be a valid readable path in your game setup. Use / separators between path components. Can be relative to Game Data location e.g. 'mods/AllGUD Output/meshes'. Leave blank to use 'meshes/actors/character/'. Absolute path is allowed.")]
        [SynthesisDescription("Path to search for Skeleton meshes.")]
        public string inputFolder
        {
            get { return _inputFolder; }
            set { _inputFolder = Helper.EnsureInputPathIsValid(value); }
        }
        private string _outputFolder = "";
        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("Must be a valid writable path in your game setup. e.g. 'mods/AllGUD Output'. Use / separators between path components. Absolute path is allowed.")]
        [SynthesisDescription("Path where transformed Skeleton meshes are written.")]
        public string outputFolder
        {
            get { return _outputFolder; }
            set { _outputFolder = Helper.EnsureOutputPathIsValid(value); }
        }
    }
}
