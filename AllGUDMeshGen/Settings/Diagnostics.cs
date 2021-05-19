using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public record Diagnostics
    {
        [SynthesisSettingName("Log File")]
        public string logFile { get; set; } = "";
        [SynthesisSettingName("Detailed Output")]
        public bool detailedLog { get; set; } = false;
    }
}
