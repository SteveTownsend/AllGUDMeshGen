using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class ItemPositionM
    {
        [SynthesisSettingName("BCR & BCL : Transform/X")]
        public float BCTransformX { get; set; } = 4.0f;
        [SynthesisSettingName("Transform/Y")]
        public float BCTransformY { get; set; } = 0.75f;
        [SynthesisSettingName("Rotation/R")]
        public float BCRotationR { get; set; } = 17.5f;

        [SynthesisSettingName("BR & BL : Transform/X")]
        public float BTransformX { get; set; } = 5.5f;
        [SynthesisSettingName("Transform/Y")]
        public float BTransformY { get; set; } = 2.0f;
        [SynthesisSettingName("Rotation/R")]
        public float BRotationR { get; set; } = 50.0f;

        [SynthesisSettingName("FR & FL : Transform/X")]
        public float FTransformX { get; set; } = 4.0f;
        [SynthesisSettingName("Transform/Y")]
        public float FTransformY { get; set; } = 7.5f;
        [SynthesisSettingName("Transform/Z")]
        public float FTransformZ { get; set; } = 0.0f;
        [SynthesisSettingName("Rotation/Y")]
        public float FRotationY { get; set; } = 0.0f;
        [SynthesisSettingName("Rotation/P")]
        public float FRotationP { get; set; } = 0.0f;
        [SynthesisSettingName("Rotation/R")]
        public float FRotationR { get; set; } = 140.0f;

        public List<string> GetConfigErrors()
        {
            List<string> errors = new List<string>();
            // TODO
            return errors;
        }
    }
}
