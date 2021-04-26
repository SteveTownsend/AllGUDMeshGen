using System;
using System.IO;
using System.Collections.Generic;
using nifly;
using ModelType = AllGUD.MeshHandler.ModelType;
using WeaponType = AllGUD.MeshHandler.WeaponType;

namespace AllGUD
{
    class NifTransformer
    {
        private static readonly string ScbTag = "scb";
        private static readonly string NonStickScbTag = "NonStickScb";
        private static readonly string MidBoneTag = "_MidBone";

        NifFile nif;
        NifFile? destNif;
        NiHeader header;
        NiHeader? destHeader;
        WeaponType nifWeapon;
        ModelType nifModel;
        string nifPath;

        bool meshHasController;

        IDictionary<int, NiAVObject> rootChildren = new SortedDictionary<int, NiAVObject>();
        ISet<int> deletedBlocks = new HashSet<int>();

        internal NifTransformer(NifFile source, string modelPath, ModelType modelType, WeaponType weaponType)
        {
            nif = source;
            header = nif.GetHeader();
            nifPath = modelPath;
            nifModel = modelType;
            nifWeapon = weaponType;
        }

        private void UnskinShader(NiBlockRefNiShader shaderRef)
        {
            NiShader shader = header.GetBlockById<NiShader>(shaderRef.index);
            if (shader == null)
            {
                Console.WriteLine("Expected NiShader at offset {0} not found", shaderRef.index);
                return;
            }
            shader.SetSkinned(false);
        }

        private void ApplyTransformToChild(NiAVObject parent, NiAVObject child, int childId, bool isBow)
        {
            MatTransform transform = new MatTransform();
            MatTransform cTransform = child.transform;
            MatTransform pTransform = parent.transform;
            transform.scale = cTransform.scale * pTransform.scale;
            Vector3 cTranslation = cTransform.translation;
            Vector3 translation = new Vector3();

            transform.translation = pTransform.rotation.opMult(cTransform.translation).opMult(pTransform.scale).opAdd(pTransform.translation);
            Matrix3 rotation = pTransform.rotation.opMult(cTransform.rotation);
            rotation.SetPrecision(4);
            transform.rotation = rotation;

            child.transform = transform;

            // Don't do this for shapes, Don't remove Transforms of Shapes in case they need to be mirrored
            if (child.controllerRef != null && !child.controllerRef.IsEmpty())
            {
                NiTransformController controller = header.GetBlockById<NiTransformController>(child.controllerRef.index);
                if (controller != null)
                {
                    meshHasController = true;
                    // TODO requires enhancement for dynamic display
                    //				if not bUseTemplates then
                    //					exit;
                }
                else
                {
                    Console.WriteLine("Expected NiTransformController at offset {0} not found", child.controllerRef.index);
                }
            }
            TransformChildren(child, childId, isBow);
        }

        private void TransformChildren(NiAVObject blockObj, int blockId, bool isBow)
        {
            ISet<int> childDone = new HashSet<int>();
            var childNodes = blockObj.CopyChildRefs();
            foreach (var childNode in childNodes)
            {
                if (childDone.Contains(childNode.index))
                    continue;
                childDone.Add(childNode.index);
                var subBlock = header.GetBlockById<NiAVObject>(childNode.index);
                if (subBlock == null)
                    continue;
                Console.WriteLine("\tApplying Transform of Block:{0} to its Child:{1}", blockId, childNode.index);
                ApplyTransformToChild(blockObj, subBlock, childNode.index, isBow);
                if (!isBow)
                {
                    MatTransform transform = new MatTransform();
                    Matrix3 rotation = new Matrix3();
                    // TODO check this - do we really clear only the first row?
                    //		SrcBlock.EditValues['Transform\Rotation']    := '0.000000 0.000000 0.000000';
                    rotation.Zero();
                    transform.rotation = rotation;
                    Vector3 translation = new Vector3();
                    translation.Zero();
                    transform.translation = translation;
                    transform.scale = 1.0f;
                    blockObj.transform = transform;
                }
            }
        }

        private void TransformRootChild(NiAVObject blockObj, int blockId)
        {
            // Apply Transforms for all non-shapes. EXCEPT BONES
            if (!RemoveSkin(new HashSet<int>(), blockObj))
            {
                // Don't do this for shapes, Don't remove Transforms of Shapes in case they need to be mirrored
                if (!meshHasController && blockObj.controllerRef != null && !blockObj.controllerRef.IsEmpty())
                {
                    NiTransformController controller = header.GetBlockById<NiTransformController>(blockObj.controllerRef.index);
                    if (controller != null)
                    {
                        meshHasController = true;
                        // TODO requires enhancement for dynamic display
                    }
                    else
                    {
                        Console.WriteLine("Expected NiTransformController at offset {0} not found", blockObj.controllerRef.index);
                    }
                }
                bool isBow = blockObj.name.get().Contains(MidBoneTag);

                // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                TransformChildren(blockObj, blockId, isBow);
            }
        }

        private void TransferVertexData(NiSkinPartition skinPartition, BSTriShape? bsTriShape)
        {
            if (bsTriShape == null)
                return;
            // Copy Vertex Data from NiSkinPartition
            bsTriShape.SetVertexData(skinPartition.vertData);

            // Get the first partition, where Triangles and the rest is stored
            // Haven't seen any with multiple partitions.
            // Not sure how that would work, revisit if there's a problem.
            var partition = skinPartition.partitions[0];
            bsTriShape.SetTriangles(partition.triangles);

            bsTriShape.UpdateBounds();
        }

        private void TransformScale(NiShape parent, NiSkinInstance skinInstance)
        {
            // On the first bone, hope all bones have the same scale here! cause seriously, what the heck could you do if they weren't?
            foreach (var boneRef in skinInstance.boneRefs.GetRefs())
            {
                var bone = header.GetBlockById<NiNode>(boneRef.index);
                if (bone != null)
                {
                    MatTransform rootTransform = parent.transform;
                    rootTransform.scale *= bone.transform.scale;
                    parent.transform = rootTransform;
                    break;
                }
            }

            if (!skinInstance.dataRef.IsEmpty())
            {
                NiSkinData skinData = header.GetBlockById<NiSkinData>(skinInstance.dataRef.index);
                if (skinData != null)
                {
                    MatTransform rootTransform = parent.transform;
                    rootTransform.scale *= skinData.skinTransform.scale;
                    parent.transform = rootTransform;
                    if (skinData.bones.Count > 0)
                    {
                        rootTransform.scale *= skinData.bones[0].boneTransform.scale;
                    }
                    deletedBlocks.Add(skinInstance.dataRef.index);
                }
                else
                {
                    Console.WriteLine("Expected NiSkinData at offset {0} not found", skinInstance.dataRef.index);
                }
            }
            deletedBlocks.Add(parent.SkinInstanceRef().index);
        }

        private bool RemoveSkin(ISet<int> skinDone, NiAVObject blockObj)
        {
            if (!(blockObj is BSTriShape) && !(blockObj is NiTriShape) && !(blockObj is NiTriStrips))
            {
                // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                var childNodes = blockObj.CopyChildRefs();
                foreach (var childNode in childNodes)
                {
                    if (skinDone.Contains(childNode.index))
                        continue;
                    skinDone.Add(childNode.index);
                    var block = header.GetBlockById<NiAVObject>(childNode.index);
                    if (block == null)
                        continue;
                    RemoveSkin(skinDone, block);
                }
                return false;
            }

            // Basically just remove anything related to skin
            NiShape? niShape = blockObj as NiShape;
            if (niShape != null)
            {
                // Remove skin flag from shader		
                if (niShape.HasShaderProperty())
                {
                    // remove unnecessary skinning on bows.
                    UnskinShader(niShape.ShaderPropertyRef());
                }
                // Remove skin from BSTriShape
                if (niShape.HasSkinInstance())
                {
                    niShape.SetSkinned(false);
                    NiSkinInstance skinInstance = header.GetBlockById<NiSkinInstance>(niShape.SkinInstanceRef().index);
                    if (skinInstance != null)
                    {
                        if (!skinInstance.skinPartitionRef.IsEmpty())
                        {
                            NiSkinPartition skinPartition = header.GetBlockById<NiSkinPartition>(skinInstance.skinPartitionRef.index);
                            if (skinPartition != null)
                            {
                                TransferVertexData(skinPartition, niShape as BSTriShape);
                            }
                            deletedBlocks.Add(skinInstance.skinPartitionRef.index);
                        }
                        else
                        {
                            Console.WriteLine("Expected NiSkinPartition at offset {0} not found", skinInstance.skinPartitionRef.index);
                        }

                        // Check for all scale transforms.
                        TransformScale(niShape, skinInstance);
                        deletedBlocks.Add(niShape.SkinInstanceRef().index);
                    }
                    else
                    {
                        Console.WriteLine("Expected NiSkinInstance at offset {0} not found", niShape.SkinInstanceRef().index);
                    }

                }
            }
            return true;
        }

        private void RenameScabbard(ISet<int> alreadyDone, NiAVObject scabbard)
        {
            if (scabbard == null)
                return;
            string newName = scabbard.name.get().Replace(ScbTag, NonStickScbTag, StringComparison.OrdinalIgnoreCase);
            int newId = header.AddOrFindStringId(newName);
            NiStringRef newRef = new NiStringRef(newName);
            newRef.SetIndex(newId);
            scabbard.name = newRef;

            var childNodes = scabbard.CopyChildRefs();
            foreach (var childNode in childNodes)
            {
                if (alreadyDone.Contains(childNode.index))
                    continue;
                alreadyDone.Add(childNode.index);
                RenameScabbard(alreadyDone, header.GetBlockById<NiAVObject>(childNode.index));
            }
        }

        private bool IsBloodMesh(NiShape? shape)
        {
            // Check if the Shape is a bloodmesh. Shapes can be treated polymorphically.
            // Blood meshes don't get used for the armor and just take up space.
            // Let's just scan the textures for 'BloodEdge'??? That's like the only commonality I can find.
            // Especially since there's a mod with a SE Mesh that has improper data that makes it cause CTD and this is the only thing I can use to catch it.
            if (shape == null || !shape.HasShaderProperty())
                return false;
            NiBlockRefNiShader shaderPropertyRef = shape.ShaderPropertyRef();
            if (shaderPropertyRef == null || shaderPropertyRef.IsEmpty())
                return false;
            BSShaderProperty shaderProperty = header.GetBlockById<BSShaderProperty>(shaderPropertyRef.index);
            if (shaderProperty != null)
            {
                if (shaderProperty.HasWeaponBlood())
                    return true;
                if (shaderProperty.HasTextureSet())
                {
                    var textureSetRef = shaderProperty.TextureSetRef();
                    if (!textureSetRef.IsEmpty())
                    {
                        BSShaderTextureSet textureSet = header.GetBlockById<BSShaderTextureSet>(textureSetRef.index);
                        if (textureSet != null)
                        {
                            string texturePath = textureSet.textures.items()[0].get().ToLower();
                            // Skullcrusher users bloodhit
                            if (texturePath.Contains("blood\\bloodedge") || texturePath.Contains("blood\bloodhit"))
                                return true;
                        }
                        else
                        {
                            Console.WriteLine("Expected BSShaderTextureSet at offset {0} not found", textureSetRef.index);
                        }
                    }
                }

                // NiTriShape blood has a NiStringExtraData sub-block named 'Keep' and 'NiHide' as its data.
                // This was the original, dunno if Kesta needed it for something specific or not?
                // Saw some meshes that couldn't keep this straight, and had NiHide/Keep reversed.
                foreach (NiBlockRefNiExtraData extraDataRef in shape.extraDataRefs.GetRefs())
                {
                    if (extraDataRef.IsEmpty())
                        continue;
                    NiStringExtraData stringExtraData = header.GetBlockById<NiStringExtraData>(extraDataRef.index);
                    if (stringExtraData != null)
                    {
                        if (stringExtraData.name.get() == "Keep" && stringExtraData.stringData.get() == "NiHide")
                            return true;
                    }
                    else
                    {
                        Console.WriteLine("Expected NiStringExtraData at offset {0} not found", extraDataRef.index);
                    }
                }
            }
            else
            {
                Console.WriteLine("Expected BSShaderProperty at offset {0} not found", shaderPropertyRef.index);
            }
            return false;
        }

        private void CopyBlockAsChildOf(NiAVObject source, NiNode parent)
        {
            // METHOD 1 -- DUPLICATE A TEMPLATE AND ADD BLOCKS TO IT
            if (source == null)
                return;

            if (source is NiShape)
            {
                NiShape? shape = source as NiShape;
                if (shape == null)
                    return;
                if (IsBloodMesh(shape))
                    return;

                // Get a copy of the source block for dest NIF. Don't add to NIF yet, we must update it first.
                NiShape destShape = shape.Clone();
                if (destShape.HasData() && !destShape.DataRef().IsEmpty())
                {
                    // Retrieves a copy of the source NIF block containing any required edits, and push to dest
                    NiGeometryData data = header.GetBlockById<NiGeometryData>(destShape.DataRef().index);
                    if (data != null)
                    {
                        int dataId = destHeader!.AddBlock(data);
                        destShape.SetDataRef(dataId);
                    }
                    else
                    {
                        Console.WriteLine("Expected NiGeometryData at offset {0} not found", destShape.DataRef().index);
                    }
                }

                if (destShape.HasShaderProperty() && !destShape.ShaderPropertyRef().IsEmpty())
                {
                    BSShaderProperty shaderProperty = header.GetBlockById<BSShaderProperty>(destShape.ShaderPropertyRef().index);
                    if (shaderProperty != null)
                    {
                        // remove unnecessary skinning on bows.
                        shaderProperty.SetSkinned(false);
                        // remove controllers and do them manually if needed
                        shaderProperty.controllerRef.Clear();
                        if (shaderProperty.HasTextureSet() && !shaderProperty.TextureSetRef().IsEmpty())
                        {
                            BSShaderTextureSet textureSet = header.GetBlockById<BSShaderTextureSet>(shaderProperty.TextureSetRef().index);
                            if (textureSet != null)
                            {
                                int textureSetId = destHeader!.AddBlock(textureSet);
                                shaderProperty.SetTextureSetRef(textureSetId);
                            }
                            else
                            {
                                Console.WriteLine("Expected BSShaderTextureSet at offset {0} not found", shaderProperty.TextureSetRef().index);
                            }
                        }
                        int shaderId = destHeader!.AddBlock(shaderProperty);
                        destShape.SetShaderPropertyRef(shaderId);
                    }
                    else
                    {
                        Console.WriteLine("Expected BSShaderProperty at offset {0} not found", destShape.ShaderPropertyRef().index);
                    }
                }

                if (destShape.HasAlphaProperty() && !destShape.AlphaPropertyRef().IsEmpty())
                {
                    NiAlphaProperty alphaProperty = header.GetBlockById<NiAlphaProperty>(destShape.AlphaPropertyRef().index);
                    if (alphaProperty != null)
                    {
                        int alphaId = destHeader!.AddBlock(alphaProperty);
                        destShape.SetAlphaPropertyRef(alphaId);
                    }
                    else
                    {
                        Console.WriteLine("Expected NiAlphaProperty at offset {0} not found", destShape.AlphaPropertyRef().index);
                    }
                }

                // Can cause CTD if a blood mesh got through
                // TODO verify this does the right thing
                destShape.extraDataRefs = new NiBlockRefArrayNiExtraData();
                //		Element := BlockDest.Elements['Extra Data List'];
                //		if Assigned(Element) then
                //			Element.SetToDefault();

                // Replace the main node in the dest once all once all editing due to child content is complete
                int newId = destHeader!.AddBlock(destShape);
                destNif!.SetParentNode(destShape, parent);
                Console.WriteLine("Block {0}/{1} copied to dest ", newId, source.GetBlockName());
            }
            else
            {
                // Scabbard or non-trishape
                // For Non-scabbard, non-trishape do not copy the block in hand to the dest NIF, only its relevant children

                NiNode blockDest = parent;
                bool isScabbard = false;
                if (source.name.get() == NonStickScbTag && source is NiNode)
                {
                    // Multipart scabbard, Apply transform down and attach and child tri-shapes to the scabbard block
                    isScabbard = true;
                    blockDest = (source as NiNode)!.Clone();
                    blockDest.childRefs = new NiBlockRefArrayNiAVObject();
                }

                // Copy Blocks all the way down until a trishape is reached
                ISet<int> alreadyDone = new HashSet<int>();
                var childNodes = source.CopyChildRefs();
                foreach (var childNode in childNodes)
                {
                    var block = header.GetBlockById<NiAVObject>(childNode.index);
                    if (block == null)
                        continue;
                    if (alreadyDone.Contains(childNode.index))
                        continue;
                    alreadyDone.Add(childNode.index);
                    Console.WriteLine("\t\tProcessing Block: {0}", childNode.index);
                    CopyBlockAsChildOf(block, blockDest);
                }

                if (isScabbard)
                {
                    // Insert scabbard in the dest once all once all editing due to child content is complete
                    destHeader!.AddBlock(blockDest);
                    destNif!.SetParentNode(blockDest, parent);
                }
            }
        }

        // We treat the loaded source NIF data as a writable scratchpad, to ease mirroring of script logic
        internal void Generate()
        {
            // Populate the list of child blocks, have to use these to Apply Transforms from non-trishapes to their kids
            NiAVObject? scabbard = null;
            NiNode rootNode = nif.GetRootNode();
            if (rootNode == null)
                return;

            var childNodes = rootNode.GetChildren().GetRefs();
            foreach (var childNode in childNodes)
            {
                if (rootChildren.ContainsKey(childNode.index))
                    continue;
                var block = header.GetBlockById<NiAVObject>(childNode.index);
                if (block == null)
                    continue;
                TransformRootChild(block, childNode.index);
                rootChildren.Add(childNode.index, block);
                if (block.name.get().ToLower().Contains(ScbTag, StringComparison.OrdinalIgnoreCase))
                {
                    scabbard = block;
                }
            }

            //Rename Scabbard if present
            if (scabbard != null)
            {
                RenameScabbard(new HashSet<int>(), scabbard);
            }

            if (meshHasController)
            {
                Console.WriteLine("\tNotification: {0}", nifPath, " contains a NiTransformController block.");
                Console.WriteLine("\t\tIt will not be transfered to a Static Display. Use Dynamic Display if this " +
                    "is meant to be animated while sheathed. Crossbows are not typically animated while sheathed.");
                // TODO update required for support of Dynamic Display (bUseTemplates false)
            }

            // MESH #1
            Console.WriteLine("\tAttempting to generate AllGUD Mesh for {0}", nifPath);

            // Create Mesh
            // Base display mesh, using DSR & AllGUD naming conventions.
            string destPath = ScriptLess.Configuration.meshGenOutputFolder + Path.ChangeExtension(nifPath, null);
            //	AllGUDMesh := DestinationPath + SubFolder + BaseFile + 'OnBack.nif'
            if (nifWeapon == WeaponType.Shield)
                destPath += "OnBack.nif";
            else if (nifWeapon == WeaponType.Staff)
                destPath += "Right.nif";
            else
                destPath += "Armor.nif";

            // static display only at present - start from template
            using (destNif = TemplateFactory.CreateSSE(nifModel, false))
            {
                destHeader = destNif.GetHeader();
                NiNode rootDest = destNif.GetRootNode();
                if (rootDest == null)
                    return;
                // Copy edited Source Blocks into the target NIF
                // TODO update required for support of Dynamic Display (bUseTemplates false)
                foreach (var idBlock in rootChildren)
                {
                    Console.WriteLine("\t\tProcessing Block: {0}", idBlock.Key);
                    CopyBlockAsChildOf(idBlock.Value, rootDest);
                }
                //if bUseTemplates then begin//TEMPLATE
                //	//Copy the relevant Blocks
                //	for i := 0 to Pred(ListRootChildren.Count) do begin
                //		DetailedLog(#9#9'Processing Block:'+inttostr(ListRootChildren[i]));
                //		SrcBlock := aNifSourceFile.Blocks[ListRootChildren[i]];
                //		CopyBlockAsChildOf(SrcBlock, Nif.Blocks[0]);
                //	end;
                //end;

                //Save and finish
                ScriptLess.CheckDestinationExists(Path.GetDirectoryName(destPath)!);
                destNif.Save(destPath, ScriptLess.saveOptions);

                Console.WriteLine("\tSuccessfully generated {0}", destPath);
                ++MeshHandler.countGenerated;
            }
        }
    }
}
