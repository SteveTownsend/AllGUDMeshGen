using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Settings
    {
        [SynthesisSettingName("For AllGUD version (display only):")]
        public string allGUDVersion { get; } = "1.5.6";

        [SynthesisSettingName("Diagnostics")]
        public Diagnostics diagnostics { get; set; } = new Diagnostics();

        [SynthesisSettingName("Skeleton Patching")]
        public Skeleton skeleton { get; set; } = new Skeleton();

        [SynthesisSettingName("Meshes for Weapons and Armour")]
        public Meshes meshes { get; set; } = new Meshes();

        public List<string> GetConfigErrors()
        {
            var errors = diagnostics.GetConfigErrors();
            errors.AddRange(skeleton.GetConfigErrors());
            errors.AddRange(meshes.GetConfigErrors());
            return errors;
        }
    }
}
