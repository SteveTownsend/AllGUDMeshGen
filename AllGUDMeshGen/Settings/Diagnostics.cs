using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public record Diagnostics
    {
        [SynthesisSettingName("Log File")]
        [SynthesisTooltip("This must be a valid filename on a valid path for your PC. Use / separators between path components. Leave blank to just get Console output.")]
        [SynthesisDescription("Name of log file for diagnostic output.")]
        public string logFile { get; set; } = "";
        [SynthesisSettingName("Detailed Output")]
        [SynthesisTooltip("Set true to include detailed output on transformed mesh block handling. Typically, false is adequate and preferable.")]
        [SynthesisDescription("Flag to trigger more verbose output to console and file.")]
        public bool detailedLog { get; set; } = false;
    }
}
