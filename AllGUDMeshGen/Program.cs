using System;
using System.IO;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using nifly;

namespace AllGUD
{
    public class ScriptLess
    {
        private static readonly ModKey AllGUDModKey = ModKey.FromNameAndExtension("All Geared Up Derivative.esp");

        private static Config? configuration;
        internal static Config Configuration
        {
            get => configuration!;
        }
        private static IPatcherState<ISkyrimMod, ISkyrimModGetter>? patcherState;
        public static IPatcherState<ISkyrimMod, ISkyrimModGetter> PatcherState
        {
            get => patcherState!;
        }

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance.SetTypicalOpen(GameRelease.SkyrimSE, "AllGUDMeshGen.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        // save options set to simplify binary comparison of output vs Edit Script output
        public static readonly nifly.NifSaveOptions saveOptions = new NifSaveOptions()
        {
            optimize = true,
            sortBlocks = true
        };

        public static void CheckDestinationExists(string destDir)
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

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            patcherState = state;

            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");
            configuration = new Config(configFilePath);

            SkeletonHandler.PatchAllHuman();
            MeshHandler.Generate();
         }
    }
}
