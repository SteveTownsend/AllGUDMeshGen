using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using nifly;

namespace AllGUD
{
    public class MeshGen
    {
        private static readonly ModKey AllGUDModKey = ModKey.FromNameAndExtension("All Geared Up Derivative.esp");
        private static readonly string skeletonMeshFolder = "meshes\\actors\\character\\";
        private static readonly string skeletonMeshFilter = "*skeleton*.nif";

        private static Config? configuration;
        private static string? meshRoot;

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
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance.SetTypicalOpen(GameRelease.SkyrimSE, "AllGUDMeshGen.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        // save options set to simplify binary comparison of output vs Edit Script output
        private static nifly.NifSaveOptions saveOptions = new NifSaveOptions()
        {
            optimize = false,
            sortBlocks = false
        };

        private static void CheckDestinationExists(string destDir)
        {
            try
            {
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    Console.WriteLine("Destination directory {0} created", destDir);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Destination directory {0} inaccessible: {1}", destDir, e.ToString());
                throw;
            }
        }

        private static void PatchWeaponNodes(NifFile nif, NiNode node, ISet<String> weapons)
        {
            var childNodes = node.GetChildren().GetRefs();
            IDictionary<int, NiAVObject> patchTargets = new Dictionary<int, NiAVObject>();
            foreach (var childNode in childNodes)
            {
                var nodeBlock = nif.GetHeader().NiAVObjectBlock(childNode.GetIndex());
                if (weapons.Contains(nodeBlock.GetName()))
                {
                    // Mark for patching and remove from target list - if we patch here, the loop
                    // range gets nuked
                    weapons.Remove(nodeBlock.GetName());
                    patchTargets[childNode.GetIndex()] = nodeBlock;

                }
                else
                {
                    PatchWeaponNodes(nif, nif.GetHeader().NiNodeBlock(childNode.GetIndex()), weapons);
                }
                foreach (var patchTarget in patchTargets)
                {
                    NiAVObject newBlock = patchTarget.Value.Clone();
                    newBlock.SetName(patchTarget.Value.GetName() + "Armor");

                    // record new block and add as a sibling of existing
                    int newID = nif.GetHeader().AddBlock(newBlock);
                    node.GetChildren().AddBlockRef(newID);

                    Console.WriteLine("Patched Weapon at Node {0}/{1} as new Block {2}/{3}",
                        patchTarget.Key, patchTarget.Value.GetName(), newID, newBlock.GetName());
                }
                if (weapons.Count == 0)
                    break;
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
                            NiNode node = nif.GetHeader().NiNodeBlock(blockID);
                            // scan block refs checking for Extra Data with species=human
                            var children = nif.StringExtraDataChildren(node, true);
                            foreach (NiStringExtraData extraData in children)
                            {
                                var refs = extraData.GetStringRefList();
                                if (refs.Count != 2)
                                    continue;
                                if (refs[0].GetString() == "species" && refs[1].GetString() == "Human")
                                {
                                    Console.WriteLine("This Skeleton is confirmed to be Human");
                                    confirmedHuman = true;
                                    break;
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
                                string relativePath = Path.GetRelativePath(meshRoot!, Path.GetDirectoryName(nifName)!);
                                string destFolder = configuration?.skeletonOutputFolder + relativePath;
                                CheckDestinationExists(destFolder);
                                newNif = Path.Join(destFolder, newNif);
                                Console.WriteLine("All Weapon nodes patched for Skeleton, saving to {0}", newNif);
                                nif.Save(newNif, saveOptions);
                            }
                            break;
                        }
                    }
                }
            }
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");
            configuration = new Config(configFilePath);

            // determine the file path for meshes
            meshRoot = String.IsNullOrEmpty(configuration.skeletonInputFolder) ? state.DataFolderPath : configuration.skeletonInputFolder;
            Console.WriteLine("Process meshes relative to {0}", meshRoot);
            string meshLocation = meshRoot + '/' + skeletonMeshFolder;

            // Skeleton Patcher logic from AllGUD Skeleton Patcher.pas
            EnumerationOptions scanRule = new EnumerationOptions();
            scanRule.RecurseSubdirectories = true;

            foreach (string candidate in Directory.EnumerateFiles(meshLocation, skeletonMeshFilter, scanRule))
            {
                PatchSkeleton(candidate);
            }
        }
    }
}
