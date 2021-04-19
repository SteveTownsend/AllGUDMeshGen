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
            NiShader shader = header.NiShaderBlock(shaderRef.index);
            shader.SetSkinned(false);
        }

        private void RemoveSkin(ISet<int> alreadyDone, int blockId)
        {
            // Basically just remove anything related to skin
            BSTriShape? bsTriShape = header.BSTriShapeBlockPrecise(blockId);
            if (bsTriShape != null)
            {
                using (bsTriShape)
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
                }
            }
            else
            {
                NiTriShape? niTriShape = header.NiTriShapeBlockPrecise(blockId);
                if (niTriShape != null)
                {
                    using (niTriShape)
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

                        }
                    }
                }
                else
                {
                    NiTriStrips? niTriStrips = header.NiTriStripsBlockPrecise(blockId);
                    if (niTriStrips != null)
                    {
                        using (niTriStrips)
                        {
                            // Remove skin flag from shader		
                            if (niTriStrips.HasShaderProperty())
                            {
                                // remove unnecessary skinning on bows.
                                UnskinShader(niTriStrips.ShaderPropertyRef());
                            }
                        }
                    }
                    else
                    {
                        // Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
                        NiAVObject block = header.NiAVObjectBlock(blockId);
                        if (block == null)
                            return;
                        using (block)
                        {
                            var childNodes = block.CopyChildRefs();
                            foreach (var childNode in childNodes)
                            {
                                using (childNode)
                                {
                                    if (alreadyDone.Contains(childNode.index))
                                        continue;
                                    alreadyDone.Add(childNode.index);
                                    RemoveSkin(alreadyDone, childNode.index);
                                }
                            }
                        }
                    }
                }
            }

            ////Basically just remove anything related to skin
            //Nif := aBlock.NifFile;
            //if (aBlock.BlockType = 'BSTriShape') or (aBlock.BlockType = 'NiTriShape') or (aBlock.BlockType = 'NiTriStrips') then begin
            //	//Remove skin flag from shader		
            //	SubBlock := TwbNifBlock(aBlock.Elements['Shader Property'].LinksTo);
            //	if Assigned(SubBlock) then begin
            //		SubBlock.NativeValues['Shader Flags 1\Skinned'] := 0; // remove unecessary skinning on bows.
            //	end;

            //	//Remove skin from BSTriShape
            //	if aBlock.BlockType = 'BSTriShape' then begin
            //		SubBlock := TwbNifBlock(aBlock.Elements['Skin'].LinksTo);
            //		if Assigned(SubBlock) then begin
            //			aBlock.NativeValues['VertexDesc\VF\VF_SKINNED'] := 0; //Clear skinned flag
            //			aBlock.NativeValues['Data Size'] := 1; //Allow 'Vertex Data' and 'Triangles' Elements to be edited.
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
            //		end;
            //	end
            //	//Remove skin from NiTriShape
            //	else begin
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
            //		end;
            //	end;
            //end else begin //Non-trishape, FIND THE CHILDREN AND REMOVE THEIR SKIN!
            //	Element := aBlock.Elements['Children'];
            //	if Assigned(Element) then begin
            //		ListChildBlocks := TList.Create;
            //		for i := 0 to Pred(Element.Count) do begin
            //			SubBlock := TwbNifBlock(Element[i].LinksTo);
            //			if Assigned(SubBlock) then begin
            //				if(ListChildBlocks.IndexOf(SubBlock.Index) >= 0) then continue;
            //				Tlist(ListChildBlocks).Add(SubBlock.Index);
            //				DetailedLog(#9#9'Processing Block:'+inttostr(SubBlock.Index));
            //				RemoveSkin(SubBlock);
            //			end;
            //		end;
            //		ListChildBlocks.Free;
            //	end;
            //end;
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
                using (childNode)
                {
                    if (alreadyDone.Contains(childNode.index))
                        continue;
                    alreadyDone.Add(childNode.index);
                    RenameScabbard(alreadyDone, header.NiNodeBlock(childNode.index));
                }
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
                using (childNode)
                {
                    var block = header.NiAVObjectBlock(childNode.index);
                    if (block == null)
                        continue;
                    RemoveSkin(new HashSet<int>(), childNode.index);
                    childBlocks.Add(block);
                    if (block.name.copy().ToLower().Contains("scb", StringComparison.OrdinalIgnoreCase))
                    {
                        scabbard = block;
                    }
                    // Check for controller
                    if (!meshHasController)
                    {

                    }
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
