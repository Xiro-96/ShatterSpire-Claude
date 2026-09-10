#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shatterspire.Editor
{
    public static class PrototypeSceneTools
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("SHATTERSPIRE/Rebuild Prototype Scene", false, 0)]
        public static void RebuildPrototypeScene()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("SHATTERSPIRE: Prototype scene rebuilt. Press Play to open the new main menu.");
        }
    }
}
#endif
