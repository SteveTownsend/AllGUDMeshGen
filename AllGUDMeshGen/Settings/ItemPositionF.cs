using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class ItemPositionF
    {
        [SynthesisSettingName("BCR & BCL : Transform/X")]
        public float BCTransformX { get; set; } = 1.0f;
        [SynthesisSettingName("Transform/Y")]
        public float BCTransformY { get; set; } = 0.5f;
        [SynthesisSettingName("Rotation/R")]
        public float BCRotationR { get; set; } = 25.0f;

        [SynthesisSettingName("BR & BL : Transform/X")]
        public float BTransformX { get; set; } = 2.0f;
        [SynthesisSettingName("Transform/Y")]
        public float BTransformY { get; set; } = 1.0f;
        [SynthesisSettingName("Rotation/R")]
        public float BRotationR { get; set; } = 50.0f;

        [SynthesisSettingName("FR & FL : Transform/X")]
        public float FTransformX { get; set; } = 1.0f;
        [SynthesisSettingName("Transform/Y")]
        public float FTransformY { get; set; } = -4.5f;
        [SynthesisSettingName("Transform/Z")]
        public float FTransformZ { get; set; } = 2.0f;
        [SynthesisSettingName("Rotation/Y")]
        public float FRotationY { get; set; } = -10.0f;
        [SynthesisSettingName("Rotation/P")]
        public float FRotationP { get; set; } = 5.0f;
        [SynthesisSettingName("Rotation/R")]
        public float FRotationR { get; set; } = 145.0f;
        [SynthesisSettingName("FL Rotation/R")]
        public float FLRotationR { get; set; } = 135.0f;

        public List<string> GetConfigErrors()
        {
            List<string> errors = new List<string>();
            // TODO
            return errors;
        }
    }
}
