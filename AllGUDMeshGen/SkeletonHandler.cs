using System;
using System.IO;
using System.Collections.Generic;
using nifly;

namespace AllGUD
{
    internal class SkeletonHandler
    {
        public Settings _settings { get; }
        private static string? skeletonMeshLocation;
        private static readonly string skeletonMeshFolder = "meshes/actors/character/";
        private static readonly string skeletonMeshFilter = "*skeleton*.nif";
        private static niflycpp.BlockCache? blockCache;
        private static NiHeader? header;

        // patchable Nodes
        private static ISet<string> weaponNodes = new HashSet<string>
        {
            "WeaponSword",
            "WeaponDagger",
            "WeaponAxe",
            "WeaponMace",
            "WeaponBack",
            "WeaponBow"
        };
        // patched skeleton HeaderStrings
        private static ISet<string> skeletonPatches = new HashSet<string>
        {
            "WeaponSwordArmor",
            "WeaponDaggerArmor",
            "WeaponAxeArmor",
            "WeaponMaceArmor",
            "WeaponBackArmor",
            "WeaponBowArmor"
        };
        // skeleton HeaderStrings used in validity checking
        private static ISet<string> skeletonValid = new HashSet<string>
        {
            "WeaponSwordLeft",
            "WeaponDaggerLeft",
            "WeaponAxeLeft",
            "WeaponMaceLeft",
            "WeaponStaff",
            "WeaponStaffLeft",
            "ShieldBack"
        };

        public SkeletonHandler(Settings settings)
        {
            _settings = settings;
        }

        private void PatchWeaponNodes(NifFile nif, NiNode node, ISet<String> weapons)
        {
            if (node == null)
                return;

            using var children = node.GetChildren();
            using var childNodes = children.GetRefs();
            IDictionary<uint, NiAVObject> patchTargets = new Dictionary<uint, NiAVObject>();
            foreach (var childNode in childNodes)
            {
                using (childNode)
                {
                    NiAVObject nodeBlock = blockCache!.EditableBlockById<NiAVObject>(childNode.index);
                    if (nodeBlock != null)
                    {
                        using var blockName = nodeBlock.name;
                        if (weapons.Contains(blockName.get()))
                        {
                            // Mark for patching and remove from target list - if we patch here, the loop
                            // range gets nuked
                            weapons.Remove(blockName.get());
                            patchTargets[childNode.index] = niflycpp.BlockCache.SafeClone<NiAVObject>(nodeBlock);

                        }
                        else
                        {
                            PatchWeaponNodes(nif, (NiNode)nodeBlock, weapons);
                        }
                    }
                }
                if (weapons.Count == 0)
                    break;
            }
            foreach (var patchTarget in patchTargets)
            {
                using var oldName = patchTarget.Value.name;
                string newName = oldName.get() + "Armor";

                uint newId = header!.AddOrFindStringId(newName);
                NiStringRef newRef = new NiStringRef(newName);
                newRef.SetIndex(newId);
                patchTarget.Value.name = newRef;

                // record new block
                //Brief attempt at setting new node to child of the weapon node didn't work with XPMSE
                uint blockId = header.AddBlock(patchTarget.Value);
                nif.SetParentNode(patchTarget.Value, node);

                if (_settings.diagnostics.DetailedLog)
                    _settings.diagnostics.logger.WriteLine("Patched Weapon at Node {0}/{1} as {2}/{3}",
                        patchTarget.Key, oldName.get(), blockId, newName);
            }
        }
        private void PatchSkeleton(string nifName)
        {
            _settings.diagnostics.logger.WriteLine("Skeleton Mesh {0}", nifName);
            using (NifFile nif = new NifFile())
            {
                nif.Load(nifName);
                using (blockCache = new niflycpp.BlockCache(nif.GetHeader()))
                {
                    header = blockCache.Header;
                    if (header == null)
                        return;
                    ISet<string> headerStrings = new HashSet<string>();
                    for (uint strId = 0; strId < header.GetStringCount(); ++strId)
                    {
                        headerStrings.Add(header.GetStringById(strId));
                    }
                    ISet<string> patchedNodes = new HashSet<string>(skeletonPatches);
                    patchedNodes.IntersectWith(headerStrings);
                    if (skeletonPatches.Count == patchedNodes.Count)
                    {
                        _settings.diagnostics.logger.WriteLine("This Skeleton already has the required AllGUD Armour Nodes", nifName);
                        return;
                    }
                    if (!skeletonValid.IsSubsetOf(headerStrings))
                    {
                        _settings.diagnostics.logger.WriteLine("This skeleton is missing one or more required Nodes ", nifName);
                    }
                    // Add missing required strings from patch node list
                    ISet<string> nodesToAdd = new HashSet<string>(skeletonPatches);
                    nodesToAdd.ExceptWith(patchedNodes);
                    foreach (string patchNode in nodesToAdd)
                    {
                        if (ScriptLess.settings.diagnostics.DetailedLog)
                            _settings.diagnostics.logger.WriteLine("This Skeleton needs required AllGUD Armour Node {0}", patchNode);
                        header.AddOrFindStringId(patchNode);
                    }
                    // iterate blocks in the NIF
                    bool confirmedHuman = false;
                    for (uint blockID = 0; blockID < header.GetNumBlocks(); ++blockID)
                    {
                        string blockType = header.GetBlockTypeStringById(blockID);
                        if (blockType == "NiNode")
                        {
                            if (!confirmedHuman)
                            {
                                NiNode node = blockCache.EditableBlockById<NiNode>(blockID);
                                if (node != null)
                                {
                                    // scan block refs checking for Extra Data with species=human
                                    using var children = nif.StringExtraDataChildren(node, true);
                                    foreach (NiStringExtraData extraData in children)
                                    {
                                        using (extraData)
                                        {
                                            using var refs = extraData.GetStringRefList();
                                            if (refs.Count != 2)
                                                continue;
                                            using var refKey = refs[0];
                                            using var refValue = refs[1];
                                            if (refKey.get() == "species" && refValue.get() == "Human")
                                            {
                                                _settings.diagnostics.logger.WriteLine("This Skeleton is confirmed to be Human");
                                                confirmedHuman = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (!confirmedHuman)
                                        break;

                                    // find child weapon nodes in the graph
                                    ISet<string> targets = new HashSet<string>(weaponNodes);
                                    PatchWeaponNodes(nif, node, targets);
                                    if (targets.Count < weaponNodes.Count)
                                    {
                                        string newNif = Path.GetFileName(nifName);
                                        string relativePath = Path.GetRelativePath(skeletonMeshLocation!, Path.GetDirectoryName(nifName)!);
                                        string destFolder = ScriptLess.settings.skeleton.OutputFolder + skeletonMeshFolder + relativePath;
                                        newNif = Path.Join(destFolder, newNif);
                                        ScriptLess.settings.diagnostics.logger.WriteLine("All Weapon nodes patched for Skeleton, saving to {0}", newNif);
                                        nif.SafeSave(newNif, ScriptLess.saveOptions);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Skeleton Patcher logic from 'AllGUD Skeleton Patcher.pas'
        public void PatchIfHuman()
        {
            // determine the file path for meshes
            skeletonMeshLocation = String.IsNullOrEmpty(ScriptLess.settings.skeleton.InputFolder) ?
                (ScriptLess.PatcherState!.DataFolderPath + '/' + skeletonMeshFolder) : ScriptLess.settings.skeleton.InputFolder;
            _settings.diagnostics.logger.WriteLine("Process meshes relative to {0}", skeletonMeshLocation);

            EnumerationOptions scanRule = new EnumerationOptions();
            scanRule.RecurseSubdirectories = true;

            foreach (string candidate in Directory.EnumerateFiles(skeletonMeshLocation, skeletonMeshFilter, scanRule))
            {
                PatchSkeleton(candidate);
            }
        }
    }
}
