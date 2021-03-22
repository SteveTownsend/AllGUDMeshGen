using System;
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

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance.SetTypicalOpen(GameRelease.SkyrimSE, "AllGUDMeshGen.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // determine the file path for meshes
            string meshRoot = state.Settings.DataFolderPath;
            Console.WriteLine("Process meshes relative to {0}", meshRoot);
        }
    }
}
