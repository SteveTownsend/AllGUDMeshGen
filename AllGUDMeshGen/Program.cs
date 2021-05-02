using System;
using System.Diagnostics;
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

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            patcherState = state;

            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");
            configuration = new Config(configFilePath);

            // determine the file path for meshes
            string meshGenLocation = String.IsNullOrEmpty(ScriptLess.Configuration!.meshGenInputFolder) ?
                ScriptLess.PatcherState!.DataFolderPath : ScriptLess.Configuration.meshGenInputFolder;
            Console.WriteLine("Process meshes relative to {0}", meshGenLocation);
            MeshHandler meshHandler = new MeshHandler(meshGenLocation);

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Patch human skeletons to have the required nodes
            SkeletonHandler.PatchIfHuman();
            long skellyTime = stopWatch.ElapsedMilliseconds;

            // Analyze records in scope for models and textures
            meshHandler.Analyze();
            long analysisTime = stopWatch.ElapsedMilliseconds - skellyTime;

            // Transform meshes, including any records with alternate textures
            meshHandler.TransformMeshes();
            long meshTime = stopWatch.ElapsedMilliseconds - analysisTime;

            Console.WriteLine("Records analysyis: {0} ms", analysisTime);
            Console.WriteLine("Mesh transformation: {0} ms", meshTime);
            Console.WriteLine("Skeleton patching: {0} ms", skellyTime);
        }
    }
}
