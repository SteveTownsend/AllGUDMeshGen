using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Settings : ISettings
    {
        [SynthesisSettingName("For AllGUD version (display only):")]
        public string allGUDVersion { get; } = "1.5.6";

        [NotNull]
        [SynthesisSettingName("Diagnostics")]
        override public IDiagnostics? diagnostics { get; set; } = new Diagnostics();

        [NotNull]
        [SynthesisSettingName("Skeleton Patching")]
        override public ISkeleton? skeleton { get; set; } = new Skeleton();

        [NotNull]
        [SynthesisSettingName("Meshes for Weapons and Armour")]
        override public IMeshes? meshes { get; set; } = new Meshes();

        override public List<string> GetConfigErrors()
        {
            var errors = (diagnostics as IConfigErrors)!.GetConfigErrors();
            errors.AddRange((skeleton as IConfigErrors)!.GetConfigErrors());
            errors.AddRange((meshes as IConfigErrors)!.GetConfigErrors());
            return errors;
        }
    }
}
