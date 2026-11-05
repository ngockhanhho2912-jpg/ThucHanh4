using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlowerEscape.EditorTools
{
    /// <summary>
    /// Watches FlowerEscapeBuild/request.txt (next to Assets/). Writing "build" or "capture" into it makes
    /// the editor refresh assets and rebuild the gameplay scene / previews without touching the menus.
    /// The scene is also built automatically the first time these scripts compile.
    /// </summary>
    [InitializeOnLoad]
    static class FlowerEscapeAutoBuild
    {
        const string PendingKey = "FlowerEscape.PendingCommand";
        static double _nextPoll;

        static string RequestPath => Path.Combine(FlowerEscapeSceneBuilder.PreviewDir, "request.txt");

        static FlowerEscapeAutoBuild()
        {
            if (!File.Exists(FlowerEscapeSceneBuilder.ScenePath) && string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")))
                SessionState.SetString(PendingKey, "build");
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            try
            {
                if (File.Exists(RequestPath))
                {
                    string cmd = File.ReadAllText(RequestPath).Trim();
                    File.Delete(RequestPath);
                    SessionState.SetString(PendingKey, string.IsNullOrEmpty(cmd) ? "build" : cmd);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return; // a domain reload may follow; run on a later poll
                }

                string pending = SessionState.GetString(PendingKey, "");
                if (string.IsNullOrEmpty(pending)) return;
                SessionState.EraseString(PendingKey);
                if (pending.StartsWith("reimport"))
                    AssetDatabase.ImportAsset(FlowerEscapeSpriteImporter.SpriteFolder.TrimEnd('/'), ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                FlowerEscapeSceneBuilder.Build(capture: true);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(FlowerEscapeSceneBuilder.PreviewDir);
                File.WriteAllText(Path.Combine(FlowerEscapeSceneBuilder.PreviewDir, "build_log.txt"), "FAILED\n" + e);
                Debug.LogException(e);
            }
        }
    }
}
