using System;
using System.Collections.Generic;
using nifly;
using ModelType = AllGUD.MeshHandler.ModelType;

namespace AllGUD
{
    class NifTransformer
    {
        private static readonly string ScbTag = "scb";
        private static readonly string NonStickScbTag = "NonStickScb";

        NifFile nif;
        NiHeader header;
        internal NifTransformer(NifFile source)
        {
            nif = source;
            header = nif.GetHeader();
        }

        private void UnskinShader(BlockRefNiShader shaderRef)
        {
            NiShader shader = header.GetBlockById<NiShader>(shaderRef.index);
            shader.SetSkinned(false);
        }

        private void RemoveSkin(ISet<int> alreadyDone, NiAVObject blockObj)
        {
            // Basically just remove anything related to skin
            BSTriShape? bsTriShape = blockObj as BSTriShape;
            if (bsTriShape != null)
            {
                // Remove skin flag from shader		
                if (bsTriShape.HasShaderProperty())
                {
                    // remove unnecessary skinning on bows.
                    UnskinShader(bsTriShape.ShaderPropertyRef());
                }
                // Remove skin from BSTriShape
                if (bsTriShape.HasSkinInstance())
                {
                    bsTriShape.SetSkinned(false);
                }
                //			TransferVertexData(Nif.Blocks[SubBlock.NativeValues['Skin Partition']], aBlock);

                //			//Check for all scale transforms.
                //			//On the first bone, hope all bones have the same scale here! cause seriously, what the heck could you do if they weren't?
                //			Element := SubBlock.Elements['Bones'];
                //			If Assigned(Element) then begin
                //				SubSubBlock :=  TwbNifBlock(Element[0].LinksTo);
                //				If Assigned(SubSubBlock) then begin
                //					aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * SubSubBlock.NativeValues['Transform\Scale'];
                //				end;
                //			end;

                //			//In the Skin Data
                //			SubSubBlock := TwbNifBlock(SubBlock.Elements['Data'].LinksTo);
                //			If Assigned(SubSubBlock) then begin
                //				Element := SubSubBlock.Elements['Skin Transform'];
                //				If Assigned(Element) then begin
                //					aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * Element.NativeValues['Scale'];
                //				end;

                //				//In the bone List of the Skin Data
                //				Element := SubSubBlock.Elements['Bone List'];
                //				If Assigned(Element) then begin
                //					Element := Element[0].Elements['Skin Transform'];
                //					If Assigned(Element) then begin
                //						aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * Element.NativeValues['Scale'];
                //					end;
                //				end;
                //			end;

                //			Nif.Delete(SubBlock.NativeValues['Skin Partition']);
                //			Nif.Delete(SubBlock.NativeValues['Data']);
                //			Nif.Delete(SubBlock.Index);
            }
            else
            {
                NiTriShape? niTriShape = blockObj as NiTriShape;
                if (niTriShape != null)
                {
                    // Remove skin flag from shader		
                    if (niTriShape.HasShaderProperty())
                    {
                        // remove unnecessary skinning on bows.
                        UnskinShader(niTriShape.ShaderPropertyRef());
                    }
                    // Remove skin from NiTriShape
                    if (niTriShape.IsSkinned())
                    {
                        //		SubBlock := TwbNifBlock(aBlock.Elements['Skin Instance'].LinksTo);
                        //		if Assigned(SubBlock) then begin

                        //			//Check for all scale transforms.
                        //			//On the first bone, hope all bones have the same scale here! cause seriously, what the heck could you do if they weren't?
                        //			Element := SubBlock.Elements['Bones'];
                        //			If Assigned(Element) then begin
                        //				SubSubBlock :=  TwbNifBlock(Element[0].LinksTo);
                        //				If Assigned(SubSubBlock) then begin
                        //					aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * SubSubBlock.NativeValues['Transform\Scale'];
                        //				end;
                        //			end;

                        //			//In the Skin Data
                        //			SubSubBlock := TwbNifBlock(SubBlock.Elements['Data'].LinksTo);
                        //			If Assigned(SubSubBlock) then begin
                        //				Element := SubSubBlock.Elements['Skin Transform'];
                        //				If Assigned(Element) then begin
                        //					aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * Element.NativeValues['Scale'];
                        //				end;

                        //				//In the bone List of the Skin Data
                        //				Element := SubSubBlock.Elements['Bone List'];
                        //				If Assigned(Element) then begin
                        //					Element := Element[0].Elements['Skin Transform'];
                        //					If Assigned(Element) then begin
                        //						aBlock.NativeValues['Transform\Scale'] := aBlock.NativeValues['Transform\Scale'] * Element.NativeValues['Scale'];
                        //					end;
                        //				end;
                        //			end;

                        //			Nif.Delete(SubBlock.NativeValues['Skin Partition']);
                        //			Nif.Delete(SubBlock.NativeValues['Data']);
                        //			Nif.Delete(SubBlock.Index);
                    }
                }
                else
                {
                    NiTriStrips? niTriStrips = blockObj as NiTriStrips;
                    if (niTriStrips != null)
                    {
                        // Remove skin flag from shader		
                        if (niTriStrips.HasShaderProperty())
                        {
                            // remove unnecessary skinning on bows.
                            UnskinShader(niTriStrips.ShaderPropertyRef());
                        }
                    }
                    else
                    {
                        // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                        var childNodes = blockObj.CopyChildRefs();
                        foreach (var childNode in childNodes)
                        {
                            if (alreadyDone.Contains(childNode.index))
                                continue;
                            alreadyDone.Add(childNode.index);
                            var block = header.GetBlockById<NiAVObject>(childNode.index);
                            if (block == null)
                                continue;
                            RemoveSkin(alreadyDone, block);
                        }
                    }
                }
            }
        }

        private void RenameScabbard(ISet<int> alreadyDone, NiAVObject scabbard)
        {
            if (scabbard == null)
                return;
            string newName = scabbard.name.copy().Replace(ScbTag, NonStickScbTag, StringComparison.OrdinalIgnoreCase);
            int newId = header.AddOrFindStringId(NonStickScbTag);
            NiStringRef newRef = new NiStringRef(newName);
            newRef.SetIndex(newId);
            scabbard.name = newRef;

            var childNodes = scabbard.CopyChildRefs();
            foreach (var childNode in childNodes)
            {
                if (alreadyDone.Contains(childNode.index))
                    continue;
                alreadyDone.Add(childNode.index);
                RenameScabbard(alreadyDone, header.GetBlockById<NiNode>(childNode.index));
            }
        }

        // We treat the loaded source NIF data as a writable scratchpad, to ease mirroring of script logic
        internal void Generate()
        {
            // Populate the list of child blocks, have to use these to Apply Transforms from non-trishapes to their kids
            NiAVObject? scabbard = null;
            bool meshHasController = false;

            IList<NiAVObject> childBlocks = new List<NiAVObject>();
            NiNode rootNode = nif.GetRootNode();
            if (rootNode == null)
                return;

            var childNodes = rootNode.GetChildren().GetRefs();
            foreach (var childNode in childNodes)
            {
                var block = header.GetBlockById<NiAVObject>(childNode.index);
                if (block == null)
                    continue;
                RemoveSkin(new HashSet<int>(), block);
                childBlocks.Add(block);
                if (block.name.copy().ToLower().Contains(ScbTag, StringComparison.OrdinalIgnoreCase))
                {
                    scabbard = block;
                }
                // Check for controller
                if (!meshHasController)
                {

                }
            }

            //Rename Scabbard if present
            if (scabbard != null)
            {
                RenameScabbard(new HashSet<int>(), scabbard);
            }

            // static display only at present - start from template
            using (NifFile transformed = TemplateFactory.CreateSSE(ModelType.Unknown, false))
            {

            }
        }
    }
}
