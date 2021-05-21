using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using nifly;
using System.Threading;

namespace AllGUD
{
    internal class ItemPositioner
    {
        public Settings _settings { get; }
        private static string? itemMeshLocation;
        private static readonly string itemMeshFolder = "meshes/";
        private static readonly string itemMeshFilter = "*BC.nif";
        private int items;
        private int generated;

        private static readonly float RADIANS_PER_DEGREE = (float)(180.0 / Math.PI);

        public ItemPositioner(Settings settings)
        {
            _settings = settings;
        }

        private void PatchItem(string fullPath, string relativePath)
        {
            _settings.diagnostics.logger.WriteLine("Item Mesh {0}", fullPath);
            using (NifFile nif = new NifFile())
            {
                nif.Load(fullPath);

                niflycpp.BlockCache blockCache = new niflycpp.BlockCache(nif.GetHeader());

                // find direct children of root node that are NiNode
                ISet<uint> rootChildIds = new SortedSet<uint>();
                using (NiNode rootNode = nif.GetRootNode())
                {
                    if (rootNode == null)
                        return;

                    var childNodes = rootNode.GetChildren().GetRefs();
                    foreach (var childNode in childNodes)
                    {
                        if (rootChildIds.Contains(childNode.index))
                            continue;
                        var block = blockCache.EditableBlockById<NiNode>(childNode.index);
                        if (block == null)
                            continue;
                        rootChildIds.Add(childNode.index);

                        using var transform = block.transform;
                        using var translation = transform.translation;
                        if (!translation.IsZero())
                        {
                            _settings.diagnostics.logger.WriteLine("\tWarning: Translation in block {0} is not Zero-d: {1},{2},{3}",
                                childNode.index, translation.x, translation.y, translation.z);
                            _settings.diagnostics.logger.WriteLine("\tApply Transform to NiNode");
                        }
                        using var rotation = transform.rotation;
                        if (!rotation.IsIdentity())
                        {
                            _settings.diagnostics.logger.WriteLine("\tWarning: Rotation in block {0} is not the Identity Matrix", childNode.index);
                            _settings.diagnostics.logger.WriteLine("\tApply Transform to NiNode");
                        }
                    }
                }

                GenerateMeshes(rootChildIds, relativePath, nif, blockCache);
            }
            Interlocked.Increment(ref items);
        }

        private void GenerateMeshes(ISet<uint> rootChildIds, string relativePath, NifFile nif, niflycpp.BlockCache blockCache)
        {
            bool isFemale = relativePath.Contains("Female", StringComparison.OrdinalIgnoreCase);
            // Get filename without the BC that marked it for processing
            string destPathRoot = _settings.itemPosition.OutputFolder + itemMeshFolder + Path.ChangeExtension(relativePath, null);
            destPathRoot = destPathRoot.Substring(0, destPathRoot.Length - 2);

            //BCL BCR Positions
            float transX;
            float transY;
            float rotR;
            if (isFemale)
            {
                transX = _settings.itemPosition.Female.BCTransformX;
                transY = _settings.itemPosition.Female.BCTransformY;
                rotR = _settings.itemPosition.Female.BCRotationR;
            }
            else
            {
                transX = _settings.itemPosition.Male.BCTransformX;
                transY = _settings.itemPosition.Male.BCTransformY;
                rotR = _settings.itemPosition.Male.BCRotationR;
            }

            // MESH #1 - BCL
            string newPath = destPathRoot + "BCL.nif"; 
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = -transX;
                    translation.y = transY;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(0.0f, 0.0f, rotR / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }

            // MESH #2 - BCR
            newPath = destPathRoot + "BCR.nif";
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = transX;
                    translation.y = transY;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(0.0f, 0.0f, -rotR / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }

            // BL BR Positions
            if (isFemale)
            {
                transX = _settings.itemPosition.Female.BTransformX;
                transY = _settings.itemPosition.Female.BTransformY;
                rotR = _settings.itemPosition.Female.BRotationR;
            }
            else
            {
                transX = _settings.itemPosition.Male.BTransformX;
                transY = _settings.itemPosition.Male.BTransformY;
                rotR = _settings.itemPosition.Male.BRotationR;
            }

            // MESH #3 - BL
            newPath = destPathRoot + "BL.nif";
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = -transX;
                    translation.y = transY;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(0.0f, 0.0f, rotR / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }

            // MESH #4 - BR
            newPath = destPathRoot + "BR.nif";
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = transX;
                    translation.y = transY;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(0.0f, 0.0f, -rotR / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }

            // FL FR Positions
            float transZ, rotY, rotP, rotRLeft;
            if (isFemale)
            {
                transX = _settings.itemPosition.Female.FTransformX;
                transY = _settings.itemPosition.Female.FTransformY;
                transZ = _settings.itemPosition.Female.FTransformZ;
                rotY = _settings.itemPosition.Female.FRotationY;
                rotP = _settings.itemPosition.Female.FRotationP;
                rotR = _settings.itemPosition.Female.FRotationR;
                //Female idle stance has the left leg much further forward. Looks like 10deg less than Right leg to avoid potion strap going through the crotch.
                rotRLeft = _settings.itemPosition.Female.FLRotationR;
            }
            else
            {
                transX = _settings.itemPosition.Male.FTransformX;
                transY = _settings.itemPosition.Male.FTransformY;
                transZ = _settings.itemPosition.Male.FTransformZ;
                rotY = _settings.itemPosition.Male.FRotationY;
                rotP = _settings.itemPosition.Male.FRotationP;
                rotR = _settings.itemPosition.Male.FRotationR;
                rotRLeft = rotR;
            }
            // Potions should have??? different positions due to the Y Rotation difference
            if (relativePath.Contains("potion", StringComparison.OrdinalIgnoreCase))
            {
                transZ = 0;
                rotY = 0;
                rotP = 0;
            }

            // MESH #5 - FL
            newPath = destPathRoot + "FL.nif";
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = -transX;
                    translation.y = transY;
                    translation.z = transZ;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(-rotY / RADIANS_PER_DEGREE, rotP / RADIANS_PER_DEGREE, rotRLeft / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }

            // MESH #6 - FR
            newPath = destPathRoot + "FR.nif";
            using (NifFile newNif = new NifFile(nif))
            {
                // Edit Blocks
                foreach (uint index in rootChildIds!)
                {
                    var block = niflycpp.BlockCache.SafeClone<NiNode>(blockCache.EditableBlockById<NiNode>(index));
                    using NiHeader newHeader = newNif.GetHeader();

                    using var transform = block.transform;
                    using var translation = transform.translation;
                    translation.x = transX;
                    translation.y = transY;
                    translation.z = transZ;
                    transform.translation = translation;

                    // signs reversed from script
                    transform.rotation = Matrix3.MakeRotation(-rotY / RADIANS_PER_DEGREE, -rotP / RADIANS_PER_DEGREE, -rotR / RADIANS_PER_DEGREE);
                    block.transform = transform;

                    newHeader.ReplaceBlock(index, block);
                }
                newNif.SafeSave(newPath, ScriptLess.saveOptions);
                Interlocked.Increment(ref generated);
            }
        }

        // Item Positioning from 'AllGUD Item Position Generator.pas'
        public void Execute()
        {
            // determine the file path for meshes
            itemMeshLocation = String.IsNullOrEmpty(ScriptLess.settings.itemPosition.InputFolder) ?
                (ScriptLess.PatcherState!.DataFolderPath + '/' + itemMeshFolder) : ScriptLess.settings.itemPosition.InputFolder;
            _settings.diagnostics.logger.WriteLine("Process meshes relative to {0}", itemMeshLocation);

            EnumerationOptions scanRule = new EnumerationOptions();
            scanRule.RecurseSubdirectories = true;

            // assumes all target NIFs are loose files
            Parallel.ForEach(Directory.EnumerateFiles(itemMeshLocation, itemMeshFilter, scanRule), candidate =>
            {
                if (!_settings.itemPosition.IsNifValid(candidate))
                {
                    _settings.diagnostics.logger.WriteLine("Filters skip {0}", candidate);
                }
                else
                {
                    PatchItem(candidate, candidate.Substring(itemMeshLocation.Length));
                }
            });
            _settings.diagnostics.logger.WriteLine("Processed {0} Item meshes, generated {1} new meshes", items, generated);
        }
    }
}
