﻿using System;
using System.IO;
using Xunit;
using AllGUD;
using nifly;

namespace PatcherTest
{
    public class ProcessNifs
    {
        [Fact]
        public void TransformTestNifs()
        {
            Settings settings = new Settings();
            settings.meshes.InputFolder = "../../../Data/";
            settings.meshes.OutputFolder = "../../../Data/Output/";
            MeshHandler meshHandler = new MeshHandler(settings);
            foreach (string nifFile in Directory.EnumerateFiles(settings.meshes.InputFolder, "*.nif"))
            {
                using NifFile originalNif = new NifFile();
                originalNif.Load(nifFile);
                meshHandler.GenerateMeshes(originalNif, Path.GetFileName(nifFile), MeshHandler.ModelType.Unknown);
            }
        }
    }
}
