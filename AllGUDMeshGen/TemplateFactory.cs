using System.Collections.Generic;
using nifly;
using ModelType = AllGUD.MeshHandler.ModelType;

namespace AllGUD
{
    class TemplateFactory
    {
        // Reverse-engineered from the AllGUD file using NifSkope to introspect. Format also verified vs DawnBreaker NIF.
        // [The AllGUD file gets the Prn ExtraData wrong.]
        public static NifFile CreateSSE(ModelType modelType, bool mirror)
        {
            NifFile sseTemplate = new NifFile();
            sseTemplate.CreateNamedBSFadeNode(NiVersion.getSSE(), "AllGUDTemplate");

            // we know there is only one NiNode - add the required NiStringExtraData to it
            sseTemplate.AddStringExtraDataToNode(0, "Prn", WeaponPrnName(modelType, mirror));

            return sseTemplate;
        }

        private static readonly IDictionary<ModelType, string> weaponTypeNames = new Dictionary<ModelType, string>
        {
            { ModelType.Unknown, "" },
            { ModelType.Sword, "WeaponSwordArmor" },
            { ModelType.Dagger, "WeaponDaggerArmor" },
            { ModelType.Mace, "WeaponMaceArmor" },
            { ModelType.Axe, "WeaponAxeArmor" },
            { ModelType.Staff, "WeaponStaff" },
            { ModelType.TwoHandMelee, "WeaponBackArmor" },
            { ModelType.TwoHandRange, "WeaponBowArmor" },
            { ModelType.Shield, "ShieldBack" }
        };

        private static readonly IDictionary<ModelType, string> weaponTypeNamesMirror = new Dictionary<ModelType, string>
        {
            { ModelType.Unknown, "" },
            { ModelType.Sword, "WeaponSwordLeft" },
            { ModelType.Dagger, "WeaponDaggerLeft" },
            { ModelType.Mace, "WeaponMaceLeft" },
            { ModelType.Axe, "WeaponAxeLeft" },
            { ModelType.Staff, "WeaponStaffLeft" },
            { ModelType.TwoHandMelee, "WeaponBackArmor" },
            { ModelType.TwoHandRange, "WeaponBowArmor" },
            { ModelType.Shield, "ShieldBack" }
        };

        private static string WeaponPrnName(ModelType modelType, bool mirror)
        {
            if (mirror)
                return weaponTypeNamesMirror[modelType];
            else
                return weaponTypeNames[modelType];
        }
    }
}
