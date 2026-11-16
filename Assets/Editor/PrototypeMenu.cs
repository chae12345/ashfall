using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ashfall.Editor
{
    public static class PrototypeMenu
    {
        [MenuItem("Ashfall/Open Prototype")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/Ashfall.unity");
        }

        [MenuItem("Ashfall/Build Windows Prototype")]
        public static void BuildWindows()
        {
            string folder = EditorUtility.OpenFolderPanel("Build Ashfall", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Ashfall.unity" },
                locationPathName = System.IO.Path.Combine(folder, "Ashfall.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            UnityEngine.Debug.Log("Ashfall build: " + report.summary.result);
        }
    }
}
