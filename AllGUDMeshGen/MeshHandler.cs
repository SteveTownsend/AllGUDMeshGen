using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Bsa;
using Mutagen.Bethesda.Skyrim;
using SSEForms = Mutagen.Bethesda.FormKeys.SkyrimSE;
using nifly;

namespace AllGUD
{
    internal class MeshHandler
    {
        private static string? meshGenLocation;
        private static IDictionary<string, ModelType> targetMeshes = new Dictionary<string, ModelType>();
        private static ISet<IWeaponGetter> alternateTextureWeapons = new HashSet<IWeaponGetter>();
        private static ISet<IArmorAddonGetter> alternateTextureArmorAddons = new HashSet<IArmorAddonGetter>();

        // Check STAT records - rare case, maintain a list of plugins where it matters
        private static ISet<string> staticMods = new HashSet<string>
        {
            "Unique Uniques.esp",
            "UniqueWeaponsRedone.esp"
        };

        internal enum ModelType {
            Unknown = 0,
            Sword,
            Dagger,
            Mace,
            Axe,
            Staff,
            TwoHandMelee,
            TwoHandRange,
            Shield
        };

        internal enum WeaponType {
            Unknown = 0,
            OneHandMelee,
            TwoHandMelee,
            Shield,
            TwoHandRange,
            Staff
        };

        private static readonly IDictionary<ModelType, WeaponType> weaponTypeByModelType = new Dictionary<ModelType, WeaponType>
        {
            { ModelType.Unknown, WeaponType.Unknown },
            { ModelType.Sword, WeaponType.OneHandMelee },
            { ModelType.Dagger, WeaponType.OneHandMelee },
            { ModelType.Mace, WeaponType.OneHandMelee },
            { ModelType.Axe, WeaponType.OneHandMelee },
            { ModelType.Staff, WeaponType.Staff },
            { ModelType.TwoHandMelee, WeaponType.TwoHandMelee },
            { ModelType.TwoHandRange, WeaponType.TwoHandRange },
            { ModelType.Shield, WeaponType.Shield }
        };

        private static int countSkipped;
        private static int countPatched;
        internal static int countGenerated;
        private static int countFailed;

        private static bool AddMesh(string modelPath, ModelType modelType)
        {
            // Do not add the same model more than once - model reuse is common
            if (targetMeshes.ContainsKey(modelPath))
            {
                return false;
            }
            targetMeshes.Add(modelPath, modelType);
            return true;
        }

        // returns true iff model has alternate textures
        private static bool RecordModel(FormKey record, ModelType modelType, IModelGetter model)
        {
            // normalize the model path. Model paths always use the backslash character as separator
            string modelPath = model.File.ToLower();
            if (AddMesh(modelPath, modelType))
            {
                Console.WriteLine("Model {0} with type {1} added as normalized form {2}", model.File, modelType.ToString(), modelPath);
            }

            // record weapons with alternate textures for patching later in the workflow
            if (model.AlternateTextures != null && model.AlternateTextures.Count > 0)
            {
                Console.WriteLine("Form {0} has alternate textures", record.ToString());
                return true;
            }
            return false;
        }

        private static void CollateWeapons()
        {
            foreach (var weap in ScriptLess.PatcherState.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>())
            {
                // skip if no model
                if (weap == null || weap.Model == null)
                    continue;
                // skip non-playable
                if (weap.MajorFlags.HasFlag(Weapon.MajorFlag.NonPlayable))
                    continue;
                // skip scan if no Keywords
                ModelType modelType = ModelType.Unknown;
                if (weap.Keywords != null)
                {
                    foreach (var keyword in weap.Keywords)
                    {
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeDagger.FormKey)
                        {
                            modelType = ModelType.Dagger;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeSword.FormKey)
                        {
                            modelType = ModelType.Dagger;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeWarAxe.FormKey)
                        {
                            modelType = ModelType.Axe;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeMace.FormKey)
                        {
                            modelType = ModelType.Mace;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeBattleaxe.FormKey ||
                            keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeGreatsword.FormKey ||
                            keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeWarhammer.FormKey)
                        {
                            modelType = ModelType.TwoHandMelee;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeBow.FormKey)
                        {
                            modelType = ModelType.TwoHandRange;
                            break;
                        }
                        if (keyword.FormKey == SSEForms.Skyrim.Keyword.WeapTypeStaff.FormKey)
                        {
                            modelType = ModelType.Staff;
                            break;
                        }
                    }
                }

                // check animation if weapon type not determined yet
                if (modelType == ModelType.Unknown && weap.Data != null)
                {
                    //currently required for: SSM Spears
                    if (weap.Data.AnimationType == WeaponAnimationType.OneHandSword)
                    {
                        modelType = ModelType.Sword;
                    }
                }

                // skip records with indeterminate weapon type
                if (modelType == ModelType.Unknown)
                    continue;

                if (RecordModel(weap.FormKey, modelType, weap.Model))
                {
                    alternateTextureWeapons.Add(weap);
                }
            }
        }

        private static void CollateShields()
        {
            foreach (var armor in ScriptLess.PatcherState.LoadOrder.PriorityOrder.WinningOverrides<IArmorGetter>())
            {
                // skip if no record
                if (armor == null)
                    continue;
                // skip non-playable in main record or template
                if (armor.MajorFlags.HasFlag(Armor.MajorFlag.NonPlayable) ||
                    (armor.BodyTemplate != null && armor.BodyTemplate.Flags.HasFlag(BodyTemplate.Flag.NonPlayable)))
                    continue;

                // skip if not a Shield
                if (armor.EquipmentType != null &&
                    armor.EquipmentType.FormKey == SSEForms.Skyrim.EquipType.Shield.FormKey)
                {
                    // Armor Armature holds the model information - use the first and warn if more than one
                    if (armor.Armature != null && armor.Armature.Count >= 1)
                    {
                        if (armor.Armature.Count > 1)
                        {
                            Console.WriteLine("Armor {0} has {1) Armature models: using the first", armor.FormKey.ToString(), armor.Armature.Count);
                        }
                        var arma = armor.Armature[0];
                        if (arma != null)
                        {
                            
                            IArmorAddonGetter? armorAddon = arma.TryResolve(ScriptLess.PatcherState.LinkCache);
                            if (armorAddon != null && armorAddon.WorldModel != null)
                            {
                                // process male and female cases
                                if (armorAddon.WorldModel.Male != null)
                                {
                                    if (RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Male))
                                    {
                                        alternateTextureArmorAddons.Add(armorAddon);
                                    }
                                }
                                if (armorAddon.WorldModel.Female != null)
                                {
                                    if (RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Female))
                                    {
                                        alternateTextureArmorAddons.Add(armorAddon);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private static void CollateStatics()
        {
            foreach (var target in ScriptLess.PatcherState.LoadOrder.PriorityOrder.WinningOverrides<IStaticGetter>().
                Where(stat => staticMods.Contains(stat.FormKey.ModKey.FileName)))
            {
                // skip if no record or Model
                if (target == null || target.Model == null)
                    continue;
                string modelPath = target.Model.File.ToLower();
                if (modelPath.Contains("weapon") || modelPath.Contains("armor"))
                {
                    if (AddMesh(modelPath, ModelType.Unknown))
                    {
                        Console.WriteLine("Model {0} for STAT {1} added as normalized form {2}", target.Model.File, target.FormKey.ToString(), modelPath);
                    }
                }
            }
        }

        private static ModelType FinalizeModelType(NifFile nif, string modelPath, ModelType modelType)
        {
            bool rightHanded = modelPath.Contains("Right.nif");
            NiNode node = nif.GetHeader().GetBlockById<NiNode>(0);
            if (node == null)
            {
                Console.WriteLine("Expected NiNode at offset 0 not found");
                return ModelType.Unknown;
            }
            // analyze 'Prn' in ExtraData for first block
            var children = nif.StringExtraDataChildren(node, true);
            foreach (NiStringExtraData extraData in children)
            {
                using (extraData)
                {
                    var refs = extraData.GetStringRefList();
                    if (refs.Count != 2)
                        continue;
                    if (refs[0].get() == "Prn")
                    {
                        string tag = refs[1].get();
                        if (modelType == ModelType.Unknown)
                        {
                            if (tag == "WeaponDagger")
                            {
                                modelType = ModelType.Dagger;
                            }
                            else if (tag == "WeaponSword")
                            {
                                modelType = ModelType.Sword;
                            }
                            else if (tag == "WeaponAxe")
                            {
                                modelType = ModelType.Axe;
                            }
                            else if (tag == "WeaponMace")
                            {
                                modelType = ModelType.Mace;
                            }
                            else if (tag == "WeaponStaff" && !rightHanded)
                            {
                                //Filter out meshes using DSR file naming convention.
                                //Vanilla staves may have incorrect Prn, USP fixed Staff01
                                modelType = ModelType.Staff;
                            }
                            else if (tag == "WeaponBack")
                            {
                                modelType = ModelType.TwoHandMelee;
                            }
                            else if (tag == "WeaponBow")
                            {
                                modelType = ModelType.TwoHandRange;
                            }
                            else if (tag == "SHIELD")
                            {
                                modelType = ModelType.Shield;
                            }
                        }
                        else if (modelType != ModelType.Staff)
                        // Sword of amazement brought this up. Staves can't share with OneHand meshes since they both use '*Left.nif'
                        // So One Hand Weapon Node in the Prn overrides Keyword:WeaponTypeStaff
                        {
                            if (tag == "WeaponDagger")
                            {
                                modelType = ModelType.Dagger;
                            }
                            else if (tag == "WeaponSword")
                            {
                                modelType = ModelType.Sword;
                            }
                            else if (tag == "WeaponAxe")
                            {
                                modelType = ModelType.Axe;
                            }
                            else if (tag == "WeaponMace")
                            {
                                modelType = ModelType.Mace;
                            }
                        }
                        break;
                    }
                }
            }
            return modelType;
        }

        private static void GenerateMeshes(NifFile nif, string modelPath, ModelType modelType)
        {
            modelType = FinalizeModelType(nif, modelPath, modelType);

            WeaponType weaponType = weaponTypeByModelType[modelType];
            if (weaponType == WeaponType.Unknown)
            {
                Console.WriteLine("Skip {0}, cannot categorize {0}", modelPath);
                ++countSkipped;
            }
            else if (!ScriptLess.Configuration.IsNifValid(modelPath))
            {
                Console.WriteLine("Filters skip {0}", modelPath);
                ++countSkipped;
            }
            else
            {
                // TODO selective patching by weapon type would need a filter here
                ++countPatched;
                Console.WriteLine("\tTemplate: Special Edition");
                new NifTransformer(nif, modelPath, modelType, weaponType).Generate();
            }
        }

        private static void TransformMeshes()
        {
            // no op if empty
            if (targetMeshes.Count == 0)
            {
                Console.WriteLine("No meshes require transformation");
                return;
            }
            IDictionary<string, string> bsaFiles = new Dictionary<string, string>();
            int totalMeshes = targetMeshes.Count;

            ISet<string> looseDone = new HashSet<string>();
            foreach (var kv in targetMeshes)
            {
                // loose file wins over BSA contents
                string originalFile = ScriptLess.Configuration.meshGenInputFolder + kv.Key;
                if (File.Exists(originalFile))
                {
                    using (NifFile nif = new NifFile())
                    {
                        Console.WriteLine("Transform mesh from loose file {0}", originalFile);
                        nif.Load(originalFile);
                        GenerateMeshes(nif, kv.Key, kv.Value);
                        looseDone.Add(kv.Key);
                    }
                }
                else
                {
                    // check for this file in archives
                    bsaFiles.Add("meshes\\" + kv.Key, kv.Key);
                }
            }

            IDictionary<string, string> bsaDone = new Dictionary<string, string>();
            if (bsaFiles.Count > 0)
            {
                // Introspect BSAs to locate meshes not found as loose files. Dups are ignored - first find wins.
                // ModKey parameter appears immaterial.
                foreach (var bsaFile in Archive.GetApplicableArchivePaths(GameRelease.SkyrimSE, ScriptLess.PatcherState.DataFolderPath, new ModKey()))
                {
                    var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, bsaFile);
                    foreach (var bsaMesh in bsaReader.Files.Where(candidate => bsaFiles.ContainsKey(candidate.Path)))
                    {
                        string rawPath = bsaFiles[bsaMesh.Path];
                        if (bsaDone.ContainsKey(rawPath))
                        {
                            Console.WriteLine("Mesh {0} from BSA {1} already processed from BSA {2}", bsaMesh.Path, bsaFile, bsaDone[rawPath]);
                            continue;
                        }

                        bsaDone.Add(rawPath, bsaFile);
                        using (MemoryStream meshStream = new MemoryStream((int)bsaMesh.Size))
                        {
                            bsaMesh.CopyDataTo(meshStream);

                            // Load NIF from stream via String - must rewind first
                            byte[] bsaData = meshStream.ToArray();
                            meshStream.Seek(0, SeekOrigin.Begin);
                            using (StreamReader reader = new StreamReader(meshStream))
                            {
                                using (NifFile nif = new NifFile(new vectoruchar(bsaData)))
                                {
                                    Console.WriteLine("Transform mesh {0} from BSA {1}", bsaMesh.Path, bsaFile);
                                    GenerateMeshes(nif, rawPath, targetMeshes[rawPath]);
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Total meshes {0} - {1} Loose, {2} in BSA", targetMeshes.Count, looseDone.Count, bsaDone.Count);
            foreach (var mesh in targetMeshes.Where(kv => !looseDone.Contains(kv.Key) && !bsaDone.ContainsKey(kv.Key)))
            {
                Console.WriteLine("Mesh {0} not found loose or in BSA", mesh.Key);
            }
        }

        // Mesh Generation logic from AllGUD Weapon Mesh Generator.pas
        public static void Generate()
        {
            // determine the file path for meshes
            meshGenLocation = String.IsNullOrEmpty(ScriptLess.Configuration!.meshGenInputFolder) ?
                ScriptLess.PatcherState!.DataFolderPath : ScriptLess.Configuration.meshGenInputFolder;
            Console.WriteLine("Process meshes relative to {0}", meshGenLocation);

            // inventory the meshes to be transformed
            CollateWeapons();
            CollateShields();
            CollateStatics();

            // transform the meshes
            TransformMeshes();
        }
    }
}
