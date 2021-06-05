using System;
using System.Collections.Generic;
using nifly;
using Mutagen.Bethesda.Skyrim;

namespace AllGUD
{
    class AlternateTextureRemover : IDisposable
    {
        MeshHandler meshHandler;
        NifFile nif;
        niflycpp.BlockCache blockCache;
        NiHeader header;
        IReadOnlyList<IAlternateTextureGetter> altTextures;
        string nifPath;
        string destPath;
        private static readonly string TexturePrefix = "textures\\";

        internal AlternateTextureRemover(MeshHandler handler, NifFile source, IReadOnlyList<IAlternateTextureGetter> textures,
            string modelPath, string newPath)
        {
            meshHandler = handler;
            // this operation writes to the NifFile in hand - take a copy
            nif = new NifFile(source);
            blockCache = new niflycpp.BlockCache(nif.GetHeader());
            header = blockCache.Header;
            altTextures = textures;
            nifPath = modelPath;
			destPath = meshHandler._settings.meshes.OutputFolder + MeshHandler.MeshPrefix + newPath;
        }

        public void Dispose()
        {
            blockCache.Dispose();
        }

        internal NifFile Execute()
        {
            using (header)
            {
                IDictionary<int, ITextureSetGetter> inputTextureSets = new Dictionary<int, ITextureSetGetter>();
                foreach (IAlternateTextureGetter altTextureSetLink in altTextures!)
                {
                    if (altTextureSetLink == null || altTextureSetLink.NewTexture == null)
                        continue;
                    ITextureSetGetter? textureSet = altTextureSetLink.NewTexture.TryResolve(ScriptLess.PatcherState.LinkCache);
                    if (textureSet == null)
                        continue;
                    inputTextureSets[altTextureSetLink.Index] = textureSet;

                }
                int shapeIndex = 0;
                int processedSets = 0;
                for (uint blockId = 0; blockId < header.GetNumBlocks() && processedSets < inputTextureSets.Count; ++blockId)
                {
                    NiShape shape = blockCache.EditableBlockById<NiShape>(blockId);
                    if (shape == null || (shape is not BSTriShape && shape is not NiTriShape && shape is not NiTriStrips))
                        continue;
                    ITextureSetGetter? textureSet;
                    if (inputTextureSets.TryGetValue(shapeIndex, out textureSet) && textureSet != null)
                    {
                        // Change the new Model's texture set at the index
                        // Exception, hide the shape if it's a null texture set, probably not totally correct but I haven't found one that I can really test this on.
                        if (textureSet.EditorID == "NullTextureSet")
                        {
                            shape.flags = 1;
                            break;
                        }
                        using var shaderRef = shape.ShaderPropertyRef();
                        if (shape.HasShaderProperty() && !shaderRef.IsEmpty())
                        {
                            using BSShaderProperty? shaderProperty = niflycpp.BlockCache.SafeClone<BSShaderProperty>(
                                blockCache.EditableBlockById<BSShaderProperty>(shaderRef.index));
                            if (shaderProperty != null)
                            {
                                BSEffectShaderProperty? effectShader = shaderProperty as BSEffectShaderProperty;
                                if (effectShader != null && textureSet.Diffuse != null)
                                {
                                    // BSEffectShaderProperty, they only have 2 textures and the only one I saw using this was a NullTextureSet
                                    // soooo have to test later when I encounter a real one.
                                    var newDiffuse = new NiString(TexturePrefix + textureSet.Diffuse);
                                    effectShader.sourceTexture = newDiffuse;
                                    header.ReplaceBlock(shaderRef.index, effectShader);
                                }
                                BSLightingShaderProperty? lightingShader = shaderProperty as BSLightingShaderProperty;
                                if (lightingShader != null)
                                {
                                    using var lightingRef = lightingShader.TextureSetRef();
                                    if (!lightingRef.IsEmpty())
                                    {
                                        using BSShaderTextureSet shaderTextures = niflycpp.BlockCache.SafeClone<BSShaderTextureSet>(
                                            blockCache.EditableBlockById<BSShaderTextureSet>(lightingRef.index));
                                        if (shaderTextures != null)
                                        {
                                            // Not every texture block has all paths, avoid error if it does not
                                            using vectorNiString newTextures = new vectorNiString();
                                            using var newDiffuse = textureSet.Diffuse != null ?
                                                new NiString(TexturePrefix + textureSet.Diffuse) : new NiString();
                                            newTextures.Add(newDiffuse);
                                            using var newNormalOrGloss = textureSet.NormalOrGloss != null ?
                                                new NiString(TexturePrefix + textureSet.NormalOrGloss) : new NiString();
                                            newTextures.Add(newNormalOrGloss);
                                            using var newGlowOrDetailMap = textureSet.GlowOrDetailMap != null ?
                                                new NiString(TexturePrefix + textureSet.GlowOrDetailMap) : new NiString();
                                            newTextures.Add(newGlowOrDetailMap);
                                            using var newHeight = textureSet.Height != null ?
                                                new NiString(TexturePrefix + textureSet.Height) : new NiString();
                                            newTextures.Add(newHeight);
                                            using var newEnvironment = textureSet.Environment != null ?
                                                new NiString(TexturePrefix + textureSet.Environment) : new NiString();
                                            newTextures.Add(newEnvironment);
                                            using var newEnvironmentMaskOrSubsurfaceTint = textureSet.EnvironmentMaskOrSubsurfaceTint != null ?
                                                new NiString(TexturePrefix + textureSet.EnvironmentMaskOrSubsurfaceTint) : new NiString();
                                            newTextures.Add(newEnvironmentMaskOrSubsurfaceTint);
                                            using var newMultilayer = textureSet.Multilayer != null ?
                                                new NiString(TexturePrefix + textureSet.Multilayer) : new NiString();
                                            newTextures.Add(newMultilayer);
                                            using var newBacklightMaskOrSpecular = textureSet.BacklightMaskOrSpecular != null ?
                                                new NiString(TexturePrefix + textureSet.BacklightMaskOrSpecular) : new NiString();
                                            newTextures.Add(newBacklightMaskOrSpecular);

                                            using NiStringVector textureWrapper = new NiStringVector();
                                            textureWrapper.SetItems(newTextures);
                                            shaderTextures.textures = textureWrapper;
                                            header.ReplaceBlock(lightingRef.index, shaderTextures);
                                        }
                                    }
                                }
                            }
                        }
                        ++processedSets;
                    }
                    ++shapeIndex;
                }
                if (processedSets != inputTextureSets.Count)
                {
                    meshHandler._settings.diagnostics.logger.WriteLine("Expected {0} Alternate Texture Sets in {1}, got {2}",
                        inputTextureSets.Count, nifPath, processedSets);
                }

                nif.SafeSave(destPath, ScriptLess.saveOptions);
                return nif;
            }
        }
    }
}
