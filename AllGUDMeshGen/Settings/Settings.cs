using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public record Settings
    {
        [SynthesisSettingName("For AllGUD version (display only):")]
        public string allGUDVersion { get; } = "1.5.6";

        [SynthesisSettingName("Diagnostics")]
        public Diagnostics diagnostics { get; set; } = new();

        [SynthesisSettingName("Skeleton")]
        public Skeleton skeleton { get; set; } = new();

        [SynthesisSettingName("Meshes")]
        public Meshes meshes { get; set; } = new();
    }
}
