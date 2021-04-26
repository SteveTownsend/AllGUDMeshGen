using System;
using System.IO;
using System.Collections.Generic;
using nifly;

namespace AllGUD
{
    internal class SkeletonHandler
    {
        private static string? skeletonMeshLocation;
        private static readonly string skeletonMeshFolder = "meshes/actors/character/";
        private static readonly string skeletonMeshFilter = "*skeleton*.nif";

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

        private static void PatchWeaponNodes(NifFile nif, NiNode node, ISet<String> weapons)
        {
            if (node == null)
                return;

            var childNodes = node.GetChildren().GetRefs();
            IDictionary<int, NiAVObject> patchTargets = new Dictionary<int, NiAVObject>();
            foreach (var childNode in childNodes)
            {
                var nodeBlock = nif.GetHeader().GetBlockById<NiAVObject>(childNode.index);
                if (nodeBlock != null)
                {
                    if (weapons.Contains(nodeBlock.name.get()))
                    {
                        // Mark for patching and remove from target list - if we patch here, the loop
                        // range gets nuked
                        weapons.Remove(nodeBlock.name.get());
                        patchTargets[childNode.index] = nodeBlock;

                    }
                    else
                    {
                        PatchWeaponNodes(nif, nif.GetHeader().GetBlockById<NiNode>(childNode.index), weapons);
                    }
                }
                if (weapons.Count == 0)
                    break;
            }
            foreach (var patchTarget in patchTargets)
            {
                NiAVObject newBlock = patchTarget.Value.Clone();
                newBlock.name = new NiStringRef(patchTarget.Value.name.get() + "Armor");

                // record new block and add as a sibling of existing
                int newID = nif.GetHeader().AddBlock(newBlock);
                node.GetChildren().AddBlockRef(newID);

                Console.WriteLine("Patched Weapon at Node {0}/{1} as new Block {2}/{3}",
                    patchTarget.Key, patchTarget.Value.name.get(), newID, newBlock.name.get());
            }
        }
        private static void PatchSkeleton(string nifName)
        {
            Console.WriteLine("Skeleton Mesh {0}", nifName);
            using (NifFile nif = new NifFile())
            {
                nif.Load(nifName);
                ISet<string> headerStrings = new HashSet<string>();
                for (int strId = 0; strId < nif.GetHeader().GetStringCount(); ++strId)
                {
                    headerStrings.Add(nif.GetHeader().GetStringById(strId));
                }
                ISet<string> patchedNodes = new HashSet<string>(skeletonPatches);
                patchedNodes.IntersectWith(headerStrings);
                if (skeletonPatches.Count == patchedNodes.Count)
                {
                    Console.WriteLine("This Skeleton already has the required AllGUD Armor Nodes", nifName);
                    return;
                }
                if (!skeletonValid.IsSubsetOf(headerStrings))
                {
                    Console.WriteLine("This skeleton is missing one or more required Nodes ", nifName);
                }
                // Add missing required strings from patch node list
                ISet<string> nodesToAdd = new HashSet<string>(skeletonPatches);
                nodesToAdd.ExceptWith(patchedNodes);
                foreach (string patchNode in nodesToAdd)
                {
                    Console.WriteLine("This Skeleton needs required AllGUD Armor Node {0}", patchNode);
                    nif.GetHeader().AddOrFindStringId(patchNode);
                }
                // iterate blocks in the NIF
                bool confirmedHuman = false;
                for (int blockID = 0; blockID < nif.GetHeader().GetNumBlocks(); ++blockID)
                {
                    string blockType = nif.GetHeader().GetBlockTypeStringById(blockID);
                    if (blockType == "NiNode")
                    {
                        if (!confirmedHuman)
                        {
                            NiNode node = nif.GetHeader().GetBlockById<NiNode>(blockID);
                            if (node != null)
                            {
                                // scan block refs checking for Extra Data with species=human
                                var children = nif.StringExtraDataChildren(node, true);
                                foreach (NiStringExtraData extraData in children)
                                {
                                    using (extraData)
                                    {
                                        var refs = extraData.GetStringRefList();
                                        if (refs.Count != 2)
                                            continue;
                                        if (refs[0].get() == "species" && refs[1].get() == "Human")
                                        {
                                            Console.WriteLine("This Skeleton is confirmed to be Human");
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
                                    string destFolder = ScriptLess.Configuration?.skeletonOutputFolder + skeletonMeshFolder + relativePath;
                                    ScriptLess.CheckDestinationExists(destFolder);
                                    newNif = Path.Join(destFolder, newNif);
                                    Console.WriteLine("All Weapon nodes patched for Skeleton, saving to {0}", newNif);
                                    nif.Save(newNif, ScriptLess.saveOptions);
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Skeleton Patcher logic from AllGUD Skeleton Patcher.pas
        public static void PatchAllHuman()
        {
            // determine the file path for meshes
            skeletonMeshLocation = String.IsNullOrEmpty(ScriptLess.Configuration!.skeletonInputFolder) ?
                (ScriptLess.PatcherState!.DataFolderPath + '/' + skeletonMeshFolder) : ScriptLess.Configuration.skeletonInputFolder;
            Console.WriteLine("Process meshes relative to {0}", skeletonMeshLocation);

            EnumerationOptions scanRule = new EnumerationOptions();
            scanRule.RecurseSubdirectories = true;

            foreach (string candidate in Directory.EnumerateFiles(skeletonMeshLocation, skeletonMeshFilter, scanRule))
            {
                PatchSkeleton(candidate);
            }
        }
    }
}
