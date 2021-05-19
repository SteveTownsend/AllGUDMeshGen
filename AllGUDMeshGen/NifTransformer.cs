using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using nifly;
using ModelType = AllGUD.MeshHandler.ModelType;
using WeaponType = AllGUD.MeshHandler.WeaponType;

namespace AllGUD
{
    class NifTransformer : IDisposable
    {
        private static readonly string ScbTag = "scb";
        private static readonly string NonStickScbTag = "NonStickScb";
        private static readonly string MidBoneTag = "_MidBone";

        MeshHandler meshHandler;
        NifFile nif;
        NifFile? destNif;
        niflycpp.BlockCache blockCache;
        NiHeader header;
        NiHeader? destHeader;
        WeaponType nifWeapon;
        ModelType nifModel;
        string nifPath;

        bool meshHasController;

        ISet<int> rootChildIds = new SortedSet<int>();

        internal NifTransformer(MeshHandler handler, NifFile source, string modelPath, ModelType modelType, WeaponType weaponType)
        {
            meshHandler = handler;
            nif = source;
            blockCache = new niflycpp.BlockCache(nif.GetHeader());
            header = blockCache.Header;
            nifPath = modelPath;
            nifModel = modelType;
            nifWeapon = weaponType;
        }

        public void Dispose()
        {
            blockCache.Dispose();
        }

        private void UnskinShader(NiBlockRefNiShader shaderRef)
        {
            NiShader shader = blockCache.EditableBlockById<NiShader>(shaderRef.index);
            if (shader == null)
            {
                meshHandler._settings.diagnostics.logger.WriteLine("Expected NiShader at offset {0} not found", shaderRef.index);
                return;
            }
            shader.SetSkinned(false);
        }

        private void ApplyTransformToChild(NiAVObject parent, NiAVObject child, int childId, bool isBow)
        {
            using MatTransform cTransform = child.transform;
            using MatTransform pTransform = parent.transform;
            using MatTransform transform = pTransform.ComposeTransforms(cTransform);
            child.transform = transform;

            // Don't do this for shapes, Don't remove Transforms of Shapes in case they need to be mirrored
            if (child.controllerRef != null && !child.controllerRef.IsEmpty())
            {
                NiTransformController controller = blockCache.EditableBlockById<NiTransformController>(child.controllerRef.index);
                if (controller != null)
                {
                    meshHasController = true;
                    // TODO requires enhancement for dynamic display
                    //				if not bUseTemplates then
                    //					exit;
                }
                else
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Expected NiTransformController at offset {0} not found", child.controllerRef.index);
                }
            }
            TransformChildren(child, childId, isBow);
        }

        private void TransformChildren(NiAVObject blockObj, int blockId, bool isBow)
        {
            ISet<int> childDone = new HashSet<int>();
            using var childNodes = blockObj.CopyChildRefs();
            foreach (var childNode in childNodes)
            {
                using (childNode)
                {
                    if (childDone.Contains(childNode.index))
                        continue;
                    childDone.Add(childNode.index);
                    var subBlock = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                    if (subBlock == null)
                        continue;
                    if (meshHandler._settings.diagnostics.DetailedLog)
                        meshHandler._settings.diagnostics.logger.WriteLine("\tApplying Transform of Block:{0} to its Child:{1}", blockId, childNode.index);
                    ApplyTransformToChild(blockObj, subBlock, childNode.index, isBow);
                }
                if (!isBow)
                {
                    using MatTransform transform = new MatTransform();
                    using Matrix3 rotation = Matrix3.MakeRotation(0.0f, 0.0f, 0.0f);  // yaw, pitch, roll
                    transform.rotation = rotation;
                    using Vector3 translation = new Vector3();
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
            if (meshHandler._settings.diagnostics.DetailedLog)
                meshHandler._settings.diagnostics.logger.WriteLine("\t\tRemoving Skin @ Block: {0}", blockId);
            if (!RemoveSkin(new HashSet<int>(), blockObj))
            {
                // Don't do this for shapes, Don't remove Transforms of Shapes in case they need to be mirrored
                if (!meshHasController && blockObj.controllerRef != null)
                {
                    using var controllerRef = blockObj.controllerRef;
                    if (!controllerRef.IsEmpty())
                    {
                        NiTransformController controller = blockCache.EditableBlockById<NiTransformController>(controllerRef.index);
                        if (controller != null)
                        {
                            meshHasController = true;
                            // TODO requires enhancement for dynamic display
                        }
                        else
                        {
                            meshHandler._settings.diagnostics.logger.WriteLine("Expected NiTransformController at offset {0} not found", controllerRef.index);
                        }
                    }
                }
                using NiStringRef blockName = blockObj.name;
                bool isBow = blockName.get().Contains(MidBoneTag);

                // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                TransformChildren(blockObj, blockId, isBow);
            }
        }

        private void TransferVertexData(NiSkinPartition skinPartition, BSTriShape? bsTriShape)
        {
            if (bsTriShape == null)
                return;
            // Copy Vertex Data from NiSkinPartition
            using var vertData = skinPartition.vertData;
            bsTriShape.SetVertexData(vertData);

            // Get the first partition, where Triangles and the rest is stored
            // Haven't seen any with multiple partitions.
            // Not sure how that would work, revisit if there's a problem.
            using var partitions = skinPartition.partitions;
            using var thePartition = partitions[0];
            using var triangles = thePartition.triangles;
            bsTriShape.SetTriangles(triangles);

            bsTriShape.UpdateBounds();
        }

        private void TransformScale(NiShape parent, NiSkinInstance skinInstance)
        {
            // On the first bone, hope all bones have the same scale here! cause seriously, what the heck could you do if they weren't?
            using var skinBoneRefs = skinInstance.boneRefs;
            using var boneRefs = skinBoneRefs.GetRefs();
            foreach (var boneRef in boneRefs)
            {
                using (boneRef)
                {
                    var bone = blockCache.EditableBlockById<NiNode>(boneRef.index);
                    if (bone != null)
                    {
                        MatTransform rootTransform = parent.transform;
                        rootTransform.scale *= bone.transform.scale;
                        parent.transform = rootTransform;
                        break;
                    }
                }
            }

            using var dataRef = skinInstance.dataRef;
            if (!dataRef.IsEmpty())
            {
                NiSkinData skinData = blockCache.EditableBlockById<NiSkinData>(dataRef.index);
                if (skinData != null)
                {
                    using MatTransform rootTransform = parent.transform;
                    rootTransform.scale *= skinData.skinTransform.scale;
                    parent.transform = rootTransform;
                    if (skinData.bones.Count > 0)
                    {
                        rootTransform.scale *= skinData.bones[0].boneTransform.scale;
                    }
                }
                else
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Expected NiSkinData at offset {0} not found", skinInstance.dataRef.index);
                }
            }
        }

        private bool RemoveSkin(ISet<int> skinDone, NiAVObject blockObj)
        {
            if (!(blockObj is BSTriShape) && !(blockObj is NiTriShape) && !(blockObj is NiTriStrips))
            {
                // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                using var childNodes = blockObj.CopyChildRefs();
                foreach (var childNode in childNodes)
                {
                    using (childNode)
                    {
                        if (skinDone.Contains(childNode.index))
                            continue;
                        skinDone.Add(childNode.index);
                        var block = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                        if (block == null)
                            continue;
                        if (meshHandler._settings.diagnostics.DetailedLog)
                            meshHandler._settings.diagnostics.logger.WriteLine("\t\tRemoving Skin @ Block: {0}", childNode.index);
                        RemoveSkin(skinDone, block);
                    }
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
                    using var shaderRef = niShape.ShaderPropertyRef();
                    UnskinShader(shaderRef);
                }
                // Remove skin from BSTriShape
                if (niShape.HasSkinInstance())
                {
                    niShape.SetSkinned(false);
                    using var skinRef = niShape.SkinInstanceRef();
                    NiSkinInstance skinInstance = blockCache.EditableBlockById<NiSkinInstance>(skinRef.index);
                    if (skinInstance != null)
                    {
                        using var partitionRef = skinInstance.skinPartitionRef;
                        if (!partitionRef.IsEmpty())
                        {
                            NiSkinPartition skinPartition = blockCache.EditableBlockById<NiSkinPartition>(partitionRef.index);
                            if (skinPartition != null)
                            {
                                TransferVertexData(skinPartition, niShape as BSTriShape);
                            }
                        }
                        else
                        {
                            meshHandler._settings.diagnostics.logger.WriteLine("Expected NiSkinPartition at offset {0} not found", partitionRef.index);
                        }

                        // Check for all scale transforms.
                        TransformScale(niShape, skinInstance);
                        // Remove the entire SkinInstance from the dest NIF
                        niShape.SetSkinInstanceRef((int)niflycpp.NIF_NPOS);
                    }
                    else
                    {
                        meshHandler._settings.diagnostics.logger.WriteLine("Expected NiSkinInstance at offset {0} not found", skinRef.index);
                    }

                }
            }
            return true;
        }

        private void RenameScabbard(ISet<int> alreadyDone, NiAVObject scabbard)
        {
            if (scabbard == null)
                return;
            using var blockName = scabbard.name;
            string newName = blockName.get().Replace(ScbTag, NonStickScbTag, StringComparison.OrdinalIgnoreCase);
            int newId = header.AddOrFindStringId(newName);
            NiStringRef newRef = new NiStringRef(newName);
            newRef.SetIndex(newId);
            scabbard.name = newRef;

            using var childNodes = scabbard.CopyChildRefs();
            foreach (var childNode in childNodes)
            {
                using (childNode)
                {
                    if (alreadyDone.Contains(childNode.index))
                        continue;
                    alreadyDone.Add(childNode.index);
                    var childBlock = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                    RenameScabbard(alreadyDone, childBlock);
                }
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
            using NiBlockRefNiShader shaderPropertyRef = shape.ShaderPropertyRef();
            if (shaderPropertyRef == null || shaderPropertyRef.IsEmpty())
                return false;
            BSShaderProperty shaderProperty = blockCache.EditableBlockById<BSShaderProperty>(shaderPropertyRef.index);
            if (shaderProperty != null)
            {
                if (shaderProperty.HasWeaponBlood())
                    return true;
                if (shaderProperty.HasTextureSet())
                {
                    using var textureSetRef = shaderProperty.TextureSetRef();
                    if (!textureSetRef.IsEmpty())
                    {
                        BSShaderTextureSet textureSet = blockCache.EditableBlockById<BSShaderTextureSet>(textureSetRef.index);
                        if (textureSet != null)
                        {
                            using var textures = textureSet.textures;
                            using var texturePaths = textures.items();
                            using var firstPath = texturePaths[0];
                            string texturePath = firstPath.get();
                            // Skullcrusher users bloodhit
                            if (texturePath.Contains("blood\\bloodedge", StringComparison.OrdinalIgnoreCase) ||
                                texturePath.Contains("blood\\bloodhit", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                        else
                        {
                            meshHandler._settings.diagnostics.logger.WriteLine("Expected BSShaderTextureSet at offset {0} not found", textureSetRef.index);
                        }
                    }
                }

                // NiTriShape blood has a NiStringExtraData sub-block named 'Keep' and 'NiHide' as its data.
                // This was the original, dunno if Kesta needed it for something specific or not?
                // Saw some meshes that couldn't keep this straight, and had NiHide/Keep reversed.
                using var extraDataRefs = shape.extraDataRefs;
                using var refList = extraDataRefs.GetRefs();
                foreach (NiBlockRefNiExtraData extraDataRef in refList)
                {
                    using (extraDataRef)
                    {
                        if (extraDataRef.IsEmpty())
                            continue;
                        NiStringExtraData stringExtraData = blockCache.EditableBlockById<NiStringExtraData>(extraDataRef.index);
                        if (stringExtraData != null)
                        {
                            using var name = stringExtraData.name;
                            using var stringData = stringExtraData.stringData;
                            if (name.get() == "Keep" && stringData.get() == "NiHide")
                                return true;
                        }
                        else
                        {
                            meshHandler._settings.diagnostics.logger.WriteLine("Expected NiStringExtraData at offset {0} not found", extraDataRef.index);
                        }
                    }
                }
            }
            else
            {
                meshHandler._settings.diagnostics.logger.WriteLine("Expected BSShaderProperty at offset {0} not found", shaderPropertyRef.index);
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
                using NiShape destShape = niflycpp.BlockCache.SafeClone<NiShape>(shape);
                using var dataRef = destShape.DataRef();
                if (destShape.HasData() && !dataRef.IsEmpty())
                {
                    // Retrieves a copy of the source NIF block containing any required edits, and push to dest
                    NiGeometryData data = niflycpp.BlockCache.SafeClone<NiGeometryData>(blockCache.EditableBlockById<NiGeometryData>(dataRef.index));
                    if (data != null)
                    {
                        int dataId = destHeader!.AddBlock(data);
                        destShape.SetDataRef(dataId);
                    }
                    else
                    {
                        meshHandler._settings.diagnostics.logger.WriteLine("Expected NiGeometryData at offset {0} not found", dataRef.index);
                    }
                }

                using var shaderRef = destShape.ShaderPropertyRef();
                if (destShape.HasShaderProperty() && !shaderRef.IsEmpty())
                {
                    using BSShaderProperty shaderProperty =
                        niflycpp.BlockCache.SafeClone<BSShaderProperty>(blockCache.EditableBlockById<BSShaderProperty>(shaderRef.index));
                    if (shaderProperty != null)
                    {
                        // remove unnecessary skinning on bows.
                        shaderProperty.SetSkinned(false);
                        // remove controllers and do them manually if needed
                        shaderProperty.SetControllerRef((int)niflycpp.NIF_NPOS);
                        using var textureSetRef = shaderProperty.TextureSetRef();
                        if (shaderProperty.HasTextureSet() && !textureSetRef.IsEmpty())
                        {
                            BSShaderTextureSet textureSet = niflycpp.BlockCache.SafeClone<BSShaderTextureSet>(
                                blockCache.EditableBlockById<BSShaderTextureSet>(textureSetRef.index));
                            if (textureSet != null)
                            {
                                int textureSetId = destHeader!.AddBlock(textureSet);
                                shaderProperty.SetTextureSetRef(textureSetId);
                            }
                            else
                            {
                                meshHandler._settings.diagnostics.logger.WriteLine("Expected BSShaderTextureSet at offset {0} not found", textureSetRef.index);
                            }
                        }
                        int shaderId = destHeader!.AddBlock(shaderProperty);
                        destShape.SetShaderPropertyRef(shaderId);
                    }
                    else
                    {
                        meshHandler._settings.diagnostics.logger.WriteLine("Expected BSShaderProperty at offset {0} not found", destShape.ShaderPropertyRef().index);
                    }
                }

                using var alphaRef = destShape.AlphaPropertyRef();
                if (destShape.HasAlphaProperty() && !alphaRef.IsEmpty())
                {
                    NiAlphaProperty alphaProperty = blockCache.EditableBlockById<NiAlphaProperty>(alphaRef.index);
                    if (alphaProperty != null)
                    {
                        using NiAlphaProperty newAlpha = niflycpp.BlockCache.SafeClone<NiAlphaProperty>(alphaProperty);
                        if (newAlpha != null)
                        {
                            int alphaId = destHeader!.AddBlock(newAlpha);
                            destShape.SetAlphaPropertyRef(alphaId);
                        }
                    }
                    else
                    {
                        meshHandler._settings.diagnostics.logger.WriteLine("Expected NiAlphaProperty at offset {0} not found", alphaRef.index);
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
                if (meshHandler._settings.diagnostics.DetailedLog)
                    meshHandler._settings.diagnostics.logger.WriteLine("Block {0}/{1} copied to dest ", newId, source.GetBlockName());
            }
            else
            {
                // Scabbard or non-trishape
                // For Non-scabbard, non-trishape do not copy the block in hand to the dest NIF, only its relevant children

                NiNode blockDest;
                bool isScabbard = false;
                using var sourceName = source.name;
                if (sourceName.get() == NonStickScbTag && source is NiNode)
                {
                    // Multipart scabbard, Apply transform down and attach and child tri-shapes to the scabbard block
                    isScabbard = true;
                    blockDest = niflycpp.BlockCache.SafeClone<NiNode>(source);
                    blockDest.childRefs = new NiBlockRefArrayNiAVObject();
                }
                else
                {
                    blockDest = niflycpp.BlockCache.SafeClone<NiNode>(parent);
                }

                using (blockDest)
                {
                    // Copy Blocks all the way down until a trishape is reached
                    ISet<int> alreadyDone = new HashSet<int>();
                    using var childNodes = source.CopyChildRefs();
                    foreach (var childNode in childNodes)
                    {
                        using (childNode)
                        {
                            var block = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                            if (block == null)
                                continue;
                            if (alreadyDone.Contains(childNode.index))
                                continue;
                            alreadyDone.Add(childNode.index);
                            if (meshHandler._settings.diagnostics.DetailedLog)
                                meshHandler._settings.diagnostics.logger.WriteLine("\t\tCopy-as-child-of @ Child {0}", childNode.index);
                            CopyBlockAsChildOf(block, blockDest);
                        }
                    }

                    if (isScabbard)
                    {
                        // Insert scabbard in the dest once all editing due to child content is complete
                        destHeader!.AddBlock(blockDest);
                        destNif!.SetParentNode(blockDest, parent);
                        blockDest.Dispose();
                    }
                }
            }
        }

        private void MirrorBlock(int id, NiAVObject block)
        {
            if (block == null)
                return;
            if (meshHandler._settings.diagnostics.DetailedLog)
                meshHandler._settings.diagnostics.logger.WriteLine("\t\tMirroring Block: {0}", id);
            if (block is BSTriShape || block is NiTriShape || block is NiTriStrips)
            {
                if (IsBloodMesh(block as NiShape))
                    return;

                // TODO it appears these functions could be combined. Stick with script flow for safety, at least initially.
                ApplyTransform(id, block); //In case things are at an angle where flipping x would produce incorrect results.
                FlipAlongX(id, block);
            }
            else
            {
                ISet<int> childDone = new HashSet<int>();
                using var childNodes = block.CopyChildRefs();
                foreach (var childNode in childNodes)
                {
                    using (childNode)
                    {
                        if (childDone.Contains(childNode.index))
                            continue;
                        childDone.Add(childNode.index);
                        var subBlock = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                        if (subBlock == null)
                            continue;
                        MirrorBlock(childNode.index, subBlock);
                    }
                }
            }
        }
		
		// Avoid Access Violation in C++ code
        private void CheckSetNormals(int id, BSTriShape shape, vectorVector3 rawNormals, int vertexCount)
        {
            if (shape.GetNumVertices() != rawNormals.Count)
            {
                throw new InvalidOperationException(String.Format("Shape @ {0} in {1} has NumVertices {2}: trying to update with {3} raw Normals",
                    id, nifPath, shape.GetNumVertices(), rawNormals.Count));
            }
            if (shape.GetNumVertices() != vertexCount)
            {
                throw new InvalidOperationException(String.Format("Shape @ {0} in {1} has NumVertices {2}: trying to update with {3} VertexData",
                    id, nifPath, shape.GetNumVertices(), rawNormals.Count));
            }
            shape.SetNormals(rawNormals);
        }

        private void FlipAlongX(int id, NiAVObject block)
        {
            if (block is BSTriShape)
            {
                BSTriShape? bsTriShape = block as BSTriShape;
                if (bsTriShape == null)
                    return;
                try
                {
                    using vectorBSVertexData vertexDataList = new vectorBSVertexData();
                    using var vertData = bsTriShape.vertData;
                    using var rawNormals = bsTriShape.UpdateRawNormals();
                    using var newRawNormals = new vectorVector3();
                    foreach (var vertexNormal in vertData.Zip(rawNormals, Tuple.Create))
                    {
                        using BSVertexData vertexData = vertexNormal.Item1;
                        using Vector3 rawNormal = vertexNormal.Item2;
                        using Vector3 newVertex = new Vector3(vertexData.vert);
                        newVertex.x = -newVertex.x;

                        using BSVertexData newVertexData = new BSVertexData(vertexData);
                        newVertexData.vert = newVertex;
                        vertexDataList.Add(newVertexData);

                        rawNormal.x = -rawNormal.x;
                        newRawNormals.Add(rawNormal);
                    }
                    bsTriShape.vertData = vertexDataList;
                    CheckSetNormals(id, bsTriShape, newRawNormals, vertexDataList.Count);

                    using  vectorTriangle newTriangles = new vectorTriangle();
                    using var oldTriangles = bsTriShape.triangles;
                    foreach (Triangle triangle in oldTriangles)
                    {
                        using (triangle)
                        {
                            using Triangle newTriangle = new Triangle(triangle.p2, triangle.p1, triangle.p3);
                            newTriangles.Add(newTriangle);
                        }
                    }
                    bsTriShape.triangles = newTriangles;
                }
                catch (Exception e)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Exception for Block Data in BSTriShape {0} in {1} : {2}", id, nifPath, e.GetBaseException());
                }
                bsTriShape.UpdateBounds();
                try // Non-vital
                {
                    // TODO is this the right mapping?
                    // aTriShape.UpdateTangents;
                    bsTriShape.CalcTangentSpace();
                }
                catch (Exception e)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Exception updating Tangents in left-hand variant(s) for: {0} in {1} : {2}",
                        id, nifPath, e.GetBaseException());
                }
            }
            else if (block is NiTriStrips || block is NiTriShape)
            {
                NiGeometry? niGeometry = block as NiGeometry;
                if (niGeometry == null)
                    return;
                using var dataRef = niGeometry.DataRef();
                if (!dataRef.IsEmpty())
                {
                    NiGeometryData geometryData = blockCache.EditableBlockById<NiGeometryData>(dataRef.index);
                    if (geometryData != null)
                    {
                        using vectorVector3 newVertices = new vectorVector3();
                        using var vertices = geometryData.vertices;
                        foreach (Vector3 vertex in vertices)
                        {
                            using Vector3 newVertex = new Vector3(vertex);
                            newVertex.x = -newVertex.x;
                            newVertices.Add(newVertex);
                        }
                        geometryData.vertices = newVertices;

                        using vectorVector3 normals = geometryData.normals;
                        if (normals != null)
                        {
                            using vectorVector3 newNormals = new vectorVector3();
                            using var dataNormals = geometryData.normals;
                            foreach (Vector3 normal in dataNormals)
                            {
                                using Vector3 newNormal = new Vector3(normal);
                                newNormal.x = -newNormal.x;
                                newNormals.Add(newNormal);
                            }
                            geometryData.normals = newNormals;

                            using vectorTriangle newTriangles = new vectorTriangle();
                            using var triangles = geometryData.Triangles();
                            foreach (Triangle triangle in triangles)
                            {
                                using (triangle)
                                {
                                    newTriangles.Add(new Triangle(triangle.p2, triangle.p1, triangle.p3));
                                }
                            }
                            geometryData.SetTriangles(newTriangles);
                        }
                        geometryData.UpdateBounds();
                        try // Non-vital
                        {
                            // TODO is this the right mapping?
                            geometryData.CalcTangentSpace();
                            // TriShapeData.UpdateTangents;
                        }
                        catch (Exception e)
                        {
                            meshHandler._settings.diagnostics.logger.WriteLine("Exception when updating the Tangent for the left-hand variant(s) for NiGeometry {0} in {1} : {2}",
                                id, nifPath, e.GetBaseException());
                        }
                    }
                }
            }
        }

        private void ApplyTransform(int id, NiAVObject block)
        {
            using MatTransform transform = block.transform;
            float scale = transform.scale;
            using Vector3 translation = transform.translation;
            using Matrix3 rotation = transform.rotation;
            rotation.SetPrecision(4);

            // Check if anything is transformed
            if (scale == 1 && translation.IsZero() && rotation.IsIdentity())
                return;

            if (block is BSTriShape)
            {
                BSTriShape? bsTriShape = block as BSTriShape;
                if (bsTriShape == null)
                    return;
                try
                {
                    using vectorBSVertexData vertexDataList = new vectorBSVertexData();
                    using var vertData = bsTriShape.vertData;
                    using var rawNormals = bsTriShape.UpdateRawNormals();
                    using var newRawNormals = new vectorVector3();
                    foreach (var vertexNormal in vertData.Zip(rawNormals, Tuple.Create))
                    {
                        using BSVertexData vertexData = vertexNormal.Item1;
                        using Vector3 rawNormal = vertexNormal.Item2;
                        using var vert = vertexData.vert;
                        using var rMultV = rotation.opMult(vert);
                        using var rMultVMultS = rMultV.opMult(scale);
                        using Vector3 newVertex = rMultVMultS.opAdd(translation);
                        using BSVertexData newVertexData = new BSVertexData(vertexData);
                        newVertexData.vert = newVertex;
                        vertexDataList.Add(newVertexData);

                        using Vector3 newRawNormal = rotation.opMult(rawNormal);
                        newRawNormals.Add(newRawNormal);
                    }
                    bsTriShape.vertData = vertexDataList;
                    CheckSetNormals(id, bsTriShape, newRawNormals, vertexDataList.Count);
                }
                catch (Exception e)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Exception for Block Data for BSTriShape {0} in {1} : {2}", id, nifPath, e.GetBaseException());
                }
                bsTriShape.UpdateBounds();
                try // Non-vital
                {
                    // TODO is this the right mapping?
                    // aTriShape.UpdateTangents;
                    bsTriShape.CalcTangentSpace();
                }
                catch (Exception e)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Exception when updating the Tangent for the left-hand variant(s) for: {0} in {1} : {2}",
                        id, nifPath, e.GetBaseException());
                }
            }
            else
            {
                if (block is NiTriStrips || block is NiTriShape)
                {
                    NiGeometry? niGeometry = block as NiGeometry;
                    if (niGeometry == null)
                        return;
                    using var dataRef = niGeometry.DataRef();
                    if (!dataRef.IsEmpty())
                    {
                        NiGeometryData geometryData = blockCache.EditableBlockById<NiGeometryData>(dataRef.index);
                        if (geometryData != null)
                        {
                            using vectorVector3 newVertices = new vectorVector3();
                            using var vertices = geometryData.vertices;
                            foreach (Vector3 vertex in vertices)
                            {
                                using (vertex)
                                {
                                    using var rMultV = rotation.opMult(vertex);
                                    using var rMultVMultS = rMultV.opMult(scale);
                                    using Vector3 newVertex = rMultVMultS.opAdd(translation);
                                    newVertices.Add(newVertex);

                                }
                            }
                            geometryData.vertices = newVertices;

                            using vectorVector3 normals = geometryData.normals;
                            if (normals != null)
                            {
                                using vectorVector3 newNormals = new vectorVector3();
                                using var dataNormals = geometryData.normals;
                                foreach (Vector3 normal in dataNormals)
                                {
                                    using (normal)
                                    {
                                        using Vector3 newNormal = rotation.opMult(normal);
                                        newNormals.Add(newNormal);
                                    }
                                }
                                geometryData.normals = newNormals;
                            }
                            geometryData.UpdateBounds();
                            try // Non-vital
                            {
                                // TODO is this the right mapping?
                                geometryData.CalcTangentSpace();
                                // TriShapeData.UpdateTangents;
                            }
                            catch (Exception e)
                            {
                                meshHandler._settings.diagnostics.logger.WriteLine("Exception when updating the Tangent for the left-hand variant(s) for NiGeometry {0} in {1} : {2}",
                                    id, nifPath, e.GetBaseException());
                            }
                        }
                    }
                }
            }

            // Clear the new transform of elements applied above
            using MatTransform newTransform = new MatTransform();
            Matrix3 newRotation = Matrix3.MakeRotation(0.0f, 0.0f, 0.0f);   // yaw, pitch, roll
            newTransform.rotation = newRotation;
            using Vector3 newTranslation = new Vector3();
            newTranslation.Zero();
            newTransform.translation = newTranslation;
            newTransform.scale = 1.0f;
            block.transform = newTransform;
        }

        // We treat the loaded source NIF data as a writable scratchpad, to ease mirroring of script logic
        internal void Generate()
        {
            // Populate the list of child blocks, have to use these to Apply Transforms from non-trishapes to their kids
            NiAVObject? scabbard = null;
            int scabbardId = -1;
            using (NiNode rootNode = nif.GetRootNode())
            {
                if (rootNode == null)
                    return;

                var childNodes = rootNode.GetChildren().GetRefs();
                foreach (var childNode in childNodes)
                {
                    if (rootChildIds.Contains(childNode.index))
                        continue;
                    var block = blockCache.EditableBlockById<NiAVObject>(childNode.index);
                    if (block == null)
                        continue;
                    TransformRootChild(block, childNode.index);
                    rootChildIds.Add(childNode.index);
                    using var blockName = block.name;
                    if (blockName.get().ToLower().Contains(ScbTag, StringComparison.OrdinalIgnoreCase))
                    {
                        scabbard = block;
                        scabbardId = childNode.index;
                    }
                }
            }

            //Rename Scabbard if present
            if (scabbard != null)
            {
                RenameScabbard(new HashSet<int>(), scabbard);
            }

            if (meshHasController)
            {
                meshHandler._settings.diagnostics.logger.WriteLine("\tNotification: {0}", nifPath, " contains a NiTransformController block.");
                meshHandler._settings.diagnostics.logger.WriteLine("\t\tIt will not be transfered to a Static Display. Use Dynamic Display if this " +
                    "is meant to be animated while sheathed. Crossbows are not typically animated while sheathed.");
                // TODO update required for support of Dynamic Display (bUseTemplates false)
            }

            // MESH #1
            meshHandler._settings.diagnostics.logger.WriteLine("\tAttempting to generate AllGUD Mesh for {0}", nifPath);

            // Create Mesh
            // Base display mesh, using DSR & AllGUD naming conventions.
            string destPath = meshHandler._settings.meshes.OutputFolder + Path.ChangeExtension(nifPath, null);
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
                using (destHeader = destNif.GetHeader())
                {
                    using (NiNode rootDest = destNif.GetRootNode())
                    {
                        if (rootDest == null)
                            return;
                        // Copy edited Source Blocks into the target NIF
                        // TODO update required for support of Dynamic Display (bUseTemplates false)
                        foreach (var id in rootChildIds)
                        {
                            if (meshHandler._settings.diagnostics.DetailedLog)
                                meshHandler._settings.diagnostics.logger.WriteLine("\t\tCopy-as-child-of @ AllGUD Mesh Root Child {0}", id);
                            CopyBlockAsChildOf(blockCache.EditableBlockById<NiAVObject>(id), rootDest);
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
                        destNif.SafeSave(destPath, ScriptLess.saveOptions);

                        meshHandler._settings.diagnostics.logger.WriteLine("\tSuccessfully generated AllGUD Mesh {0}", destPath);
                        ++meshHandler.countGenerated;
                    }
                }
            }

            // MESH #2 Left-hand DSR-style one-hand melee and staff
            if (nifWeapon == WeaponType.OneHandMelee || nifWeapon == WeaponType.Staff)
            {
                destPath = meshHandler._settings.meshes.OutputFolder + Path.ChangeExtension(nifPath, null);
                destPath += "Left.nif";

                // Mirror the shapes
                if (nifWeapon != WeaponType.Staff || meshHandler._settings.meshes.MirrorStaves)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("\tAttempting to generate Left-Hand mesh to mirror Weapon: {0}", destPath);
                    // TODO requires enhancement for dynamic displays
                    //		if not (bUseTemplates) and bMeshHasController then begin
                    //			Log(#9'Warning: ' +FileSrc+ ' contains a NiTransformController and is attempting to mirror into a left-hand mesh. This may not go well. Post to AllGUD if you encounter one of these as it will probably need a custom patch.');
                    //		end;
                    foreach (var id in rootChildIds)
                    {
                        MirrorBlock(id, blockCache.EditableBlockById<NiAVObject>(id));
                    }
                    // aNifSourceFile.SpellFaceNormals; //currently bugged in xEdit 4.0.3, will be needed in the future.
                    // TODO: Wait for this to be fixed.
                }
                else
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("\tAttempting to generate unmirrored Left-Hand mesh: {0}", destPath);
                }

                // Copy edited Blocks
                using (destNif = TemplateFactory.CreateSSE(nifModel, true))
                {
                    using (destHeader = destNif.GetHeader())
                    {
                        using (NiNode rootDest = destNif.GetRootNode())
                        {
                            if (rootDest == null)
                                return;
                            // TODO update required for support of Dynamic Display (bUseTemplates false)
                            foreach (var id in rootChildIds)
                            {
                                if (meshHandler._settings.diagnostics.DetailedLog)
                                    meshHandler._settings.diagnostics.logger.WriteLine("\t\tCopy-as-child-of @ Left-Hand mesh Root Child {0}", id);
                                CopyBlockAsChildOf(blockCache.EditableBlockById<NiAVObject>(id), rootDest);
                            }
                            //	if bUseTemplates then begin	//TEMPLATE
                            //		//Copy main TriShapes
                            //		for i := 0 to Pred(ListRootChildren.Count) do begin
                            //			DetailedLog(#9#9'Processing Block: '+inttostr(ListRootChildren[i]));
                            //			SrcBlock := aNifSourceFile.Blocks[ListRootChildren[i]];
                            //			CopyBlockAsChildOf(SrcBlock, Nif.Blocks[0]);
                            //		end;
                            //	end;

                            //Save and finish
                            destNif.SafeSave(destPath, ScriptLess.saveOptions);

                            meshHandler._settings.diagnostics.logger.WriteLine("\tSuccessfully generated Left-Hand mesh: {0}", destPath);
                            ++meshHandler.countGenerated;
                        }
                    }
                }

                // MESH #3 Scabbard by itself for an empty left-hand sheath to use while weapons are drawn
                if (scabbard != null)
                {
                    destPath = meshHandler._settings.meshes.OutputFolder + Path.ChangeExtension(nifPath, null);
                    destPath += "Sheath.nif";

                    meshHandler._settings.diagnostics.logger.WriteLine("\tAttempting to generate Left-Scabbard mesh: {0}", destPath);

                    // Create File - ALWAYS TEMPLATE FOR SHEATH.NIF
                    using (destNif = TemplateFactory.CreateSSE(nifModel, true))
                    {
                        using (destHeader = destNif.GetHeader())
                        {
                            using (NiNode rootDest = destNif.GetRootNode())
                            {
                                if (rootDest == null)
                                    return;
                                if (meshHandler._settings.diagnostics.DetailedLog)
                                    meshHandler._settings.diagnostics.logger.WriteLine("\t\tProcessing Scabbard: {0}", scabbardId);
                                CopyBlockAsChildOf(scabbard, rootDest);

                                //Save and finish
                                destNif.SafeSave(destPath, ScriptLess.saveOptions);
                            }
                        }
                    }

                    meshHandler._settings.diagnostics.logger.WriteLine("\tSuccessfully generated Scabbard: {0}", destPath);
                    ++meshHandler.countGenerated;
                }
            }
            else if (nifWeapon == WeaponType.Shield)
            {
                // MESH #4 Shield but translate z by -5 to adjust for backpacks/cloaks
                destPath = meshHandler._settings.meshes.OutputFolder + Path.ChangeExtension(nifPath, null);
                destPath += "OnBackClk.nif";
                meshHandler._settings.diagnostics.logger.WriteLine("\tAttempting to generate Shield-Adjusted-for-Cloak mesh: {0}", destPath);

                // Edit Blocks
                // TODO conditional if Dynamic Display added
                //	if bUseTemplates then begin
                // Copy Shield models
                using (destNif = TemplateFactory.CreateSSE(nifModel, false))
                {
                    using (destHeader = destNif.GetHeader())
                    {
                        using (NiNode rootDest = destNif.GetRootNode())
                        {
                            if (rootDest == null)
                                return;
                            foreach (var id in rootChildIds)
                            {
                                // Translate Z of each Child block of Root by -5, using a copy for bespoke editing
                                if (meshHandler._settings.diagnostics.DetailedLog)
                                    meshHandler._settings.diagnostics.logger.WriteLine("\t\tCopy-as-child-of @ Shield-Adjusted-for-Cloak Root {0}", id);
                                NiAVObject block = blockCache.EditableBlockById<NiAVObject>(id);

                                using var transform = block.transform;
                                using var translation = transform.translation;
                                translation.z = translation.z - 5.0f;
                                transform.translation = translation;
                                block.transform = transform;

                                CopyBlockAsChildOf(block, rootDest);
                                // Slothability said -4.5 was the most common one. original DSR meshes had -5 i believe?
                            }

                            //Save and finish
                            destNif.SafeSave(destPath, ScriptLess.saveOptions);

                            meshHandler._settings.diagnostics.logger.WriteLine("\tSuccessfully generated Shield-Adjusted-for-Cloak mesh {0}", destPath);
                            ++meshHandler.countGenerated;
                        }
                    }
                }
            }
        }
    }
}
