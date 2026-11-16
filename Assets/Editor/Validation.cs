using UnityEditor;
using UnityEngine;

namespace Ashfall.Editor
{
    public static class Validation
    {
        [MenuItem("Ashfall/Run Core Checks")]
        public static void Run()
        {
            AshfallCoreChecks.Run();
            Debug.Log("ASHFALL_CHECKS_PASSED: 100 terrain seeds, footprints, path finding, combat, economy and restoration.");
        }

        public static void Build()
        {
            PlayerSettings.companyName = "AshfallContributors";
            PlayerSettings.productName = "Ashfall";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            System.IO.Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Ashfall.unity" },
                locationPathName = "Builds/Windows/Ashfall.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
            Debug.Log("ASHFALL_WINDOWS_BUILD_PASSED");
        }
    }
}
