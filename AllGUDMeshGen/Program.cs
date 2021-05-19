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

        static Lazy<Settings> _settings = null!;
        static public Settings settings => _settings.Value;

        private static IPatcherState<ISkyrimMod, ISkyrimModGetter>? patcherState;
        public static IPatcherState<ISkyrimMod, ISkyrimModGetter> PatcherState
        {
            get => patcherState!;
        }

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance.SetTypicalOpen(GameRelease.SkyrimSE, "AllGUDMeshGen.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
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

            // Validate Settings, abort if invalid
            var settingsErrors = _settings.Value.GetConfigErrors();
            if (settingsErrors.Count > 0)
            {
                settings.diagnostics.logger.WriteLine("Settings Errors: {0}", settingsErrors.Count);
                foreach (var error in settingsErrors)
                {
                    settings.diagnostics.logger.WriteLine(error);
                }
                throw new ArgumentException("Bad Settings: AllGUD Patcher cannot run. Check diagnostic output and fix problems.");
            }
            // determine the file path for meshes
            string meshGenLocation = String.IsNullOrEmpty(settings.meshes.InputFolder) ?
                ScriptLess.PatcherState!.DataFolderPath : settings.meshes.InputFolder;
            settings.diagnostics.logger.WriteLine("Process meshes relative to {0}", meshGenLocation);
            MeshHandler meshHandler = new MeshHandler(settings);

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Patch human skeletons to have the required nodes
            new SkeletonHandler(settings).PatchIfHuman();
            long skellyTime = stopWatch.ElapsedMilliseconds;

            // Analyze records in scope for models and textures
            meshHandler.Analyze();
            long analysisTime = stopWatch.ElapsedMilliseconds - skellyTime;

            // Transform meshes, including any records with alternate textures
            meshHandler.TransformMeshes();
            long meshTime = stopWatch.ElapsedMilliseconds - analysisTime;

            settings.diagnostics.logger.WriteLine("Records analysis: {0} ms", analysisTime);
            settings.diagnostics.logger.WriteLine("Mesh transformation: {0} ms", meshTime);
            settings.diagnostics.logger.WriteLine("Skeleton patching: {0} ms", skellyTime);
        }
    }
}
