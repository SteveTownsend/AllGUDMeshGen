using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Skeleton
    {
        private string _inputFolder = "";
        [SynthesisSettingName("Input Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup. Use / separators between path components.")]
        [SynthesisDescription("Path to search for Skeleton meshes.")]
        public string inputFolder
        {
            get { return _inputFolder; }
            set { _inputFolder = Helper.EnsureInputPathIsValid(value); }
        }
        private string _outputFolder = "";
        [SynthesisSettingName("Output Folder")]
        [SynthesisTooltip("This must be a valid path in your game setup - usually something like 'mods/AllGUD Output'. Use / separators between path components.")]
        [SynthesisDescription("Path where transformed Skeleton meshes are written.")]
        public string outputFolder
        {
            get { return _outputFolder; }
            set { _outputFolder = Helper.EnsureOutputPathIsValid(value); }
        }
    }
}
