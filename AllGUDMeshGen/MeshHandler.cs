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
    public class MeshHandler
    {
        private class TargetMeshInfo
        {
            public readonly string originalName;
            public readonly ModelType modelType;
            public TargetMeshInfo(string name, ModelType model)
            {
                originalName = name;
                modelType = model;
            }
        }
        public ISettings _settings { get; }
        private IDictionary<string, TargetMeshInfo> targetMeshes = new Dictionary<string, TargetMeshInfo>();
        private IDictionary<string, IList<IWeaponGetter>> alternateTextureWeapons = new Dictionary<string, IList<IWeaponGetter>>();
        private IDictionary<string, IList<IArmorAddonGetter>> alternateTextureArmorAddons = new Dictionary<string, IList<IArmorAddonGetter>>();

        private class AlternateTextureInfo
        {
            public string nifPath { get; }
            public ModelType modelType { get; }

            public AlternateTextureInfo(string path, ModelType model)
            {
                nifPath = path;
                modelType = model;
            }
        };

        // Check STAT records - rare case, maintain a list of plugins where it matters
        private static ISet<string> staticMods = new HashSet<string>
        {
            "Unique Uniques.esp",
            "UniqueWeaponsRedone.esp"
        };

        public enum ModelType {
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

        private int countSkipped;
        private int countPatched;
        internal int countGenerated;
        private int countFailed;

        private int alternateTextureModels;

        public MeshHandler(ISettings settings)
        {
            _settings = settings;
        }

        private bool AddMesh(string modelPath, ModelType modelType)
        {
            if (!_settings.meshes.IsNifValid(modelPath))
            {
                _settings.diagnostics.logger.WriteLine("Filters skip {0}", modelPath);
                ++countSkipped;
                return false;
            }
            // Do not add the same model more than once - model reuse is common
            string normalizedPath = modelPath.ToLower();
            if (targetMeshes.ContainsKey(normalizedPath))
            {
                return false;
            }
            targetMeshes.Add(normalizedPath, new TargetMeshInfo(modelPath, modelType));
            return true;
        }

        // returns true iff model has alternate textures
        private bool RecordModel(FormKey record, ModelType modelType, IModelGetter model)
        {
            string modelPath = model.File;
            if (AddMesh(modelPath, modelType))
            {
                _settings.diagnostics.logger.WriteLine("Model {0}/{1} with type {2} added", record, modelPath, modelType.ToString());
            }

            // record weapons with alternate textures for patching later in the workflow
            if (model.AlternateTextures != null && model.AlternateTextures.Count > 0)
            {
                return true;
            }
            return false;
        }

        private void CollateWeapons()
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
                            modelType = ModelType.Sword;
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
                    _settings.diagnostics.logger.WriteLine("WEAP {0} has alternate textures", weap.FormKey);
                    if (!alternateTextureWeapons.ContainsKey(weap.Model!.File))
                    {
                        alternateTextureWeapons[weap.Model.File] = new List<IWeaponGetter>();
                    }
                    alternateTextureWeapons[weap.Model.File].Add(weap);
                    AddMesh(weap.Model!.File, modelType);
                }
            }
        }

        private void CollateShields()
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
                            _settings.diagnostics.logger.WriteLine("Armour {0} has {1} Armature models: using the first", armor.FormKey.ToString(), armor.Armature.Count);
                        }
                        var arma = armor.Armature[0];
                        if (arma != null)
                        {
                            
                            IArmorAddonGetter? armorAddon = arma.TryResolve(ScriptLess.PatcherState.LinkCache);
                            if (armorAddon != null && armorAddon.WorldModel != null)
                            {
                                // process male and female cases
                                string currentPath = "";
                                if (armorAddon.WorldModel.Male != null)
                                {
                                    currentPath = armorAddon.WorldModel.Male.File;
                                    if (RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Male))
                                    {
                                        _settings.diagnostics.logger.WriteLine("ARMA {0} has alternate textures in Male Model", armorAddon.FormKey);
                                        if (!alternateTextureArmorAddons.ContainsKey(armorAddon.WorldModel.Male.File))
                                        {
                                            alternateTextureArmorAddons[armorAddon.WorldModel.Male.File] = new List<IArmorAddonGetter>();
                                        }
                                        alternateTextureArmorAddons[armorAddon.WorldModel.Male.File].Add(armorAddon);
                                        AddMesh(armorAddon.WorldModel.Male.File, ModelType.Shield);
                                    }
                                }
                                if (armorAddon.WorldModel.Female != null && armorAddon.WorldModel.Female.File != currentPath)
                                {
                                    if (RecordModel(armorAddon.FormKey, ModelType.Shield, armorAddon.WorldModel.Female))
                                    {
                                        _settings.diagnostics.logger.WriteLine("ARMA {0} has alternate textures in Female Model", armorAddon.FormKey);
                                        if (!alternateTextureArmorAddons.ContainsKey(armorAddon.WorldModel.Female.File))
                                        {
                                            alternateTextureArmorAddons[armorAddon.WorldModel.Female.File] = new List<IArmorAddonGetter>();
                                        }
                                        alternateTextureArmorAddons[armorAddon.WorldModel.Female.File].Add(armorAddon);
                                        AddMesh(armorAddon.WorldModel.Female.File, ModelType.Shield);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void CollateStatics()
        {
            foreach (var target in ScriptLess.PatcherState.LoadOrder.PriorityOrder.WinningOverrides<IStaticGetter>().
                Where(stat => staticMods.Contains(stat.FormKey.ModKey.FileName)))
            {
                // skip if no record or Model
                if (target == null || target.Model == null)
                    continue;
                string modelPath = target.Model.File.ToLower();
                if (modelPath.Contains("weapon", StringComparison.OrdinalIgnoreCase) ||
                    modelPath.Contains("armor", StringComparison.OrdinalIgnoreCase))
                {
                    if (AddMesh(modelPath, ModelType.Unknown))
                    {
                        _settings.diagnostics.logger.WriteLine("Model {0} for STAT {1} added as normalized form {2}",
                            target.Model.File, target.FormKey.ToString(), modelPath);
                    }
                }
            }
        }

        private ModelType FinalizeModelType(NifFile nif, string modelPath, ModelType modelType)
        {
            bool rightHanded = modelPath.Contains("Right.nif", StringComparison.OrdinalIgnoreCase);
            using var header = nif.GetHeader();
            NiNode? node = header.GetBlockById(0) as NiNode;
            if (node == null)
            {
                _settings.diagnostics.logger.WriteLine("Expected NiNode at offset 0 not found");
                return ModelType.Unknown;
            }
            // analyze 'Prn' in ExtraData for first block
            using var children = nif.StringExtraDataChildren(node, true);
            foreach (NiStringExtraData extraData in children)
            {
                using (extraData)
                {
                    var refs = extraData.GetStringRefList();
                    if (refs.Count != 2)
                        continue;
                    using NiStringRef refKey = refs[0];
                    if (refKey.get() == "Prn")
                    {
                        using NiStringRef refValue = refs[1];
                        string tag = refValue.get();
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
                        else if (modelType == ModelType.Staff)
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

        public void GenerateMeshes(NifFile nif, string originalPath, ModelType modelType)
        {
            try
            {
                modelType = FinalizeModelType(nif, originalPath, modelType);

                WeaponType weaponType = weaponTypeByModelType[modelType];
                if (weaponType == WeaponType.Unknown)
                {
                    _settings.diagnostics.logger.WriteLine("Skip {0}, cannot categorize {0}", originalPath);
                    ++countSkipped;
                }
                else
                {
                    // TODO selective patching by weapon type would need a filter here
                    ++countPatched;
                    if (_settings.diagnostics.DetailedLog)
                        _settings.diagnostics.logger.WriteLine("\tTemplate: Special Edition");
                    using NifTransformer transformer = new NifTransformer(this, nif, originalPath, modelType, weaponType);
                    transformer.Generate();
                }
            }
            catch (Exception e)
            {
                ++countFailed;
                _settings.diagnostics.logger.WriteLine("Exception processing {0}: {1}", originalPath, e.GetBaseException());
            }
        }

        // ALternate Textures processing first generates a brand-new base mesh that embeds the alternate textures for this record. This
        // new mesh is referenced in a record override in Synthesis.esp. It is named using the EditorID of the record to distinguish from the
        // original mesh. Once this new AllGUD-friendly mesh is on disk, it is used as a source for the regular mesh generation process to
        // complete the picture for this record. The original mesh is also processed: there may be records that use it without Alternate Textures.
        private string AlternateTextureMeshName(string nifPath, ISkyrimMajorRecordGetter record, bool isMale)
        {
            string newNif = Path.GetDirectoryName(nifPath) + '\\' + record.EditorID;
            if (!isMale)
                newNif += "Female";
            newNif += ".nif";
            _settings.diagnostics.logger.WriteLine("Alternate Texture set found for {0} for model {1}, create Alt Textures mesh {2}",
                record.FormKey, nifPath, newNif);
            return newNif;
        }

        private IDictionary<string, NifFile> GenerateAlternateWeaponMeshes(
            NifFile originalNif, string modelPath, IList<IWeaponGetter> weapons, ModelType modelType)
        {
            IDictionary<string, NifFile> result = new Dictionary<string, NifFile>();
            foreach (IWeaponGetter weapon in weapons)
            {
                if (weapon.Model != null && weapon.Model.AlternateTextures != null)
                {
                    string model = weapon.Model.File;
                    bool isMale = true;
                    string newPath = AlternateTextureMeshName(model, weapon, isMale);
                    var newWeapon = ScriptLess.PatcherState.PatchMod.Weapons.GetOrAddAsOverride(weapon);
                    newWeapon.Model!.File = newPath;
                    using AlternateTextureRemover alternateTextureRemover = new AlternateTextureRemover(
                        this, originalNif, weapon.Model.AlternateTextures, modelPath, newPath);
                    NifFile newNif = alternateTextureRemover.Execute();
                    newWeapon.Model!.File = newPath;
                    newWeapon.Model!.AlternateTextures = null;
                    result[newPath] = newNif;
                }
            }
            // Original model also needs AllGUD-friendly variants
            result[modelPath] = originalNif;
            return result;
        }

        private IDictionary<string, NifFile> GenerateAlternateArmorAddonMeshes(
            NifFile originalNif, string modelPath, IList<IArmorAddonGetter> armorAddons, ModelType modelType)
        {
            IDictionary<string, NifFile> result = new Dictionary<string, NifFile>();
            foreach (IArmorAddonGetter armorAddon in armorAddons)
            {
                if (armorAddon.WorldModel != null)
                {
                    string maleNif = "";
                    bool isMale = true;
                    if (armorAddon.WorldModel.Male != null)
                    {
                        maleNif = armorAddon.WorldModel.Male.File;
                        if (!String.IsNullOrEmpty(maleNif) && armorAddon.WorldModel.Male.AlternateTextures != null)
                        {
                            string newPath = AlternateTextureMeshName(maleNif, armorAddon, isMale);
                            using AlternateTextureRemover alternateTextureRemover = new AlternateTextureRemover(
                                this, originalNif, armorAddon.WorldModel.Male.AlternateTextures, modelPath, newPath);
                            NifFile newNif = alternateTextureRemover.Execute();

                            var newArmorAddon = ScriptLess.PatcherState.PatchMod.ArmorAddons.GetOrAddAsOverride(armorAddon);
                            newArmorAddon.WorldModel!.Male!.File = newPath;
                            newArmorAddon.WorldModel!.Male!.AlternateTextures = null;

                            result[newPath] = newNif;
                        }
                    }
                    isMale = false;
                    if (armorAddon.WorldModel.Female != null && armorAddon.WorldModel.Female.AlternateTextures != null)
                    {
                        string femaleNif = armorAddon.WorldModel.Female.File;
                        if (!String.IsNullOrEmpty(femaleNif) && maleNif != femaleNif)
                        {
                            string newPath = AlternateTextureMeshName(femaleNif, armorAddon, isMale);
                            using AlternateTextureRemover alternateTextureRemover = new AlternateTextureRemover(
                                this, originalNif, armorAddon.WorldModel.Female.AlternateTextures, modelPath, newPath);
                            NifFile newNif = alternateTextureRemover.Execute();

                            var newArmorAddon = ScriptLess.PatcherState.PatchMod.ArmorAddons.GetOrAddAsOverride(armorAddon);
                            newArmorAddon.WorldModel!.Female!.File = newPath;
                            newArmorAddon.WorldModel!.Female!.AlternateTextures = null;

                            result[newPath] = newNif;
                        }
                    }
                }
            }
            // Original model also needs AllGUD-friendly variants
            result[modelPath] = originalNif;
            return result;
        }

        private IDictionary<string, NifFile> GenerateAlternateTextureMeshes(NifFile originalNif, string nifPath, ModelType modelType)
        {
            IList<IWeaponGetter>? weapons;
            if (alternateTextureWeapons.TryGetValue(nifPath, out weapons) && weapons != null)
            {
                var mapped = GenerateAlternateWeaponMeshes(originalNif, nifPath, weapons, modelType);
                if (mapped != null && mapped.Count > 0)
                {
                    alternateTextureModels += mapped.Count - 1;     // exclude the input
                    return mapped;
                }
            }
            IList<IArmorAddonGetter>? armorAddons;
            if (alternateTextureArmorAddons.TryGetValue(nifPath, out armorAddons) && armorAddons != null)
            {
                var mapped = GenerateAlternateArmorAddonMeshes(originalNif, nifPath, armorAddons, modelType);
                if (mapped != null && mapped.Count > 0)
                {
                    alternateTextureModels += mapped.Count - 1;     // exclude the input
                    return mapped;
                }
            }
            // If no alternates found, we must still process the input mesh
            IDictionary<string, NifFile> result = new Dictionary<string, NifFile>();
            result[nifPath] = originalNif;
            return result;
        }

        private IDictionary<string, NifFile> CheckLooseFileAlternateTextures(string nifOriginalPath, ModelType modelType, string originalFile)
        {
            NifFile originalNif = new NifFile();
            originalNif.Load(originalFile);
            return GenerateAlternateTextureMeshes(originalNif, nifOriginalPath, modelType);
        }

        private IDictionary<string, NifFile> CheckBSABytesAlternateTextures(string nifOriginalPath, ModelType modelType, vectoruchar bsaBytes)
        {
            NifFile originalNif = new NifFile(bsaBytes);
            return GenerateAlternateTextureMeshes(originalNif, nifOriginalPath, modelType);
        }

        internal void TransformMeshes()
        {
            // no op if empty
            if (targetMeshes.Count == 0)
            {
                _settings.diagnostics.logger.WriteLine("No meshes require transformation");
                return;
            }
            IDictionary<string, string> bsaFiles = new Dictionary<string, string>();
            int totalMeshes = targetMeshes.Count;

            ISet<string> looseDone = new HashSet<string>();
            foreach (var kv in targetMeshes)
            {
                // loose file wins over BSA contents
                string originalFile = _settings.meshes.InputFolder + kv.Key;
                if (File.Exists(originalFile))
                {
                    _settings.diagnostics.logger.WriteLine("Transform mesh from loose file {0}", originalFile);

                    IDictionary<string, NifFile> nifs = CheckLooseFileAlternateTextures(kv.Value.originalName, kv.Value.modelType, originalFile);
                    foreach (var pathNif in nifs)
                    {
                        using (pathNif.Value)
                        {
                            GenerateMeshes(pathNif.Value, pathNif.Key, kv.Value.modelType);
                        }
                    }
                    looseDone.Add(kv.Key);
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
                    foreach (var bsaMesh in bsaReader.Files.Where(candidate => bsaFiles.ContainsKey(candidate.Path.ToLower())))
                    {
                        string rawPath = bsaFiles[bsaMesh.Path.ToLower()];
                        TargetMeshInfo meshInfo = targetMeshes[rawPath];
                        if (bsaDone.ContainsKey(rawPath))
                        {
                            _settings.diagnostics.logger.WriteLine("Mesh {0} from BSA {1} already processed from BSA {2}", bsaMesh.Path, bsaFile, bsaDone[rawPath]);
                            continue;
                        }

                        using MemoryStream meshStream = new MemoryStream((int)bsaMesh.Size);
                        bsaMesh.CopyDataTo(meshStream);

                        // Load NIF from stream via String - must rewind first
                        byte[] bsaData = meshStream.ToArray();
                        using vectoruchar bsaBytes = new vectoruchar(bsaData);

                        IDictionary<string, NifFile> nifs = CheckBSABytesAlternateTextures(meshInfo.originalName, meshInfo.modelType, bsaBytes);
                        foreach (var pathNif in nifs)
                        {
                            using (pathNif.Value)
                            {
                                _settings.diagnostics.logger.WriteLine("Transform mesh {0} from BSA {1}", bsaMesh.Path, bsaFile);
                                GenerateMeshes(pathNif.Value, pathNif.Key, targetMeshes[rawPath].modelType);
                            }
                        }
                        bsaDone.Add(rawPath, bsaFile);
                    }
                }
            }

            var missingFiles = targetMeshes.Where(kv => !looseDone.Contains(kv.Key) && !bsaDone.ContainsKey(kv.Key)).ToList();
            foreach (var mesh in missingFiles)
            {
                _settings.diagnostics.logger.WriteLine("Referenced Mesh {0} not found loose or in BSA", mesh.Key);
            }
            _settings.diagnostics.logger.WriteLine("{0} total meshes: found {1} Loose, {2} in BSA, {3} missing files",
                targetMeshes.Count, looseDone.Count, bsaDone.Count, missingFiles.Count);
            _settings.diagnostics.logger.WriteLine("Generated {0} with {1} for Alternate Textures, Patched {2}, Skipped {3}, Failed {4}",
                countGenerated, alternateTextureModels, countPatched, countSkipped, countFailed);
        }

        // Mesh Generation logic from AllGUD Weapon Mesh Generator.pas
        internal void Analyze()
        {
            // inventory the meshes to be transformed
            CollateWeapons();
            CollateShields();
            CollateStatics();
        }
    }
}
