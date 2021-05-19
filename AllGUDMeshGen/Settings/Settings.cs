using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;
using AllGUD;

namespace AllGUD
{
    public class Settings : ISettings, IConfigErrors
    {
        [SynthesisSettingName("For AllGUD version (display only):")]
        public string allGUDVersion { get; } = "1.5.6";

        [SynthesisSettingName("Diagnostics")]
        public IDiagnostics diagnostics { get; set; } = new Diagnostics();

        [SynthesisSettingName("Skeleton Patching")]
        public ISkeleton skeleton { get; set; } = new Skeleton();

        [SynthesisSettingName("Meshes for Weapons and Armour")]
        public IMeshes meshes { get; set; } = new Meshes();

        public List<string> GetConfigErrors()
        {
            var errors = (diagnostics as IConfigErrors)!.GetConfigErrors();
            errors.AddRange((skeleton as IConfigErrors)!.GetConfigErrors());
            errors.AddRange((meshes as IConfigErrors)!.GetConfigErrors());
            return errors;
        }
    }
}
