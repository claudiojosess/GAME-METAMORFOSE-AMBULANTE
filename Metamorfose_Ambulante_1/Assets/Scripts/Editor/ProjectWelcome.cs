#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AeroMorphET.Editor
{
    [InitializeOnLoad]
    public static class ProjectWelcome
    {
        static ProjectWelcome() { EditorApplication.delayCall += FirstOpen; }
        private static void FirstOpen()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += FirstOpen; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string key = "Metamorfose.Rebuilt.Opened." + Application.dataPath;
            if (EditorPrefs.GetBool(key, false)) return;
            var current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity") == null) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorPrefs.SetBool(key, true);
            EditorApplication.delayCall += Frame;
        }
        [MenuItem("Metamorfose Ambulante/Abrir cena completa")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Frame();
        }
        [MenuItem("Metamorfose Ambulante/Enquadrar estrada e ET")]
        public static void Frame()
        {
            var alien = Object.FindFirstObjectByType<AlienMorphController>();
            if (alien == null) return;
            Selection.activeGameObject = alien.gameObject;
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = false;
            view.LookAt(alien.transform.position + Vector3.forward * 15, Quaternion.Euler(24,0,0), 40);
            view.Repaint();
        }
    }
}
#endif
