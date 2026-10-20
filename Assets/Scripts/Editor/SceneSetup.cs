using EsportsManager.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EsportsManager.EditorTools
{
    /// <summary>
    /// Batch-mode scene setup: <c>Unity -batchmode -quit -projectPath . -executeMethod EsportsManager.EditorTools.SceneSetup.Run</c>.
    /// Adds (once) a GameBootstrap object with the four config TextAssets to Assets/Scenes/SampleScene.unity.
    /// </summary>
    public static class SceneSetup
    {
        /// <summary>Entry point for -executeMethod.</summary>
        public static void Run()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var existing = Object.FindAnyObjectByType<GameBootstrap>();
            var go = existing != null ? existing.gameObject : new GameObject("GameBootstrap");
            var boot = existing != null ? existing : go.AddComponent<GameBootstrap>();
            boot.SetConfigs(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/player_config.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/team_config.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/segment_config.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/match_config.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/event_config.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/event_text_zh.json"));
            EditorUtility.SetDirty(boot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SceneSetup: GameBootstrap configured in " + scenePath);
        }
    }
}
