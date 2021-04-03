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
        private static ISet<FormKey> alternateTextureForms = new HashSet<FormKey>();

        // Check STAT records - rare case, maintain a list of plugins where it matters
        private static ISet<string> staticMods = new HashSet<string>
        {
            "Unique Uniques.esp",
            "UniqueWeaponsRedone.esp"
        };

        enum ModelType {
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

        private static void RecordModel(FormKey record, ModelType modelType, IModelGetter model)
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
                alternateTextureForms.Add(record);
            }
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

                RecordModel(weap.FormKey, modelType, weap.Model);
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
                                    RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Male);
                                }
                                if (armorAddon.WorldModel.Female != null)
                                {
                                    RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Female);
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
                        Console.WriteLine("Process mesh from loose file {0}", originalFile);
                        nif.Load(originalFile);
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
                        using (Stream meshStream = new MemoryStream((int)bsaMesh.Size))
                        {
                            bsaMesh.CopyDataTo(meshStream);
                            using (NifFile nif = new NifFile())
                            {
                                // TODO load NIF from MemoryStream
                                // TODO transform mesh
                                Console.WriteLine("Process mesh {0} from BSA {1}", bsaMesh.Path, bsaFile);
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
