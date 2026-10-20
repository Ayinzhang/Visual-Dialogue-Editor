namespace DialogueEditor
{
    using System;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static class DialogueBindingEditor
    {
        public static DialogueSystemBase SceneSystem(out string issue) => SceneSystem(null, out issue, out _);

        public static DialogueSystemBase SceneSystem(DialogueGraph graph, out string issue, out bool unverified)
        {
            Scene scene = SceneManager.GetActiveScene();
            issue = null;
            unverified = false;
            try
            {
                DialogueSystemBase system = InScene(scene);
                if (system == null)
                {
                    issue = "Bindings unverified. Open the edit scene or add one Dialogue System to the active scene.";
                    unverified = true;
                    return null;
                }
                if (graph != null && graph.editorScene != null && AssetDatabase.GetAssetPath(graph.editorScene) != scene.path &&
                    (system.graphs == null || !Array.Exists(system.graphs, asset => asset == graph)))
                {
                    issue = "Bindings unverified. Open the edit scene or assign this Graph to the active scene's System.";
                    unverified = true;
                    return null;
                }
                return system;
            }
            catch (InvalidOperationException e)
            {
                issue = e.Message;
                return null;
            }
        }

        static DialogueSystemBase InScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            DialogueSystemBase found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (DialogueSystemBase system in root.GetComponentsInChildren<DialogueSystemBase>(true))
                {
                    if (found != null) throw new InvalidOperationException("Only one Dialogue System is allowed in scene: " + scene.name);
                    found = system;
                }
            return found;
        }

        public static GameObject Target(DialogueSystemBase system, string key)
        {
            UnityEngine.Object target = system == null ? null : system.Bindings.Find(key);
            return target == null ? null : target is Component component ? component.gameObject : target as GameObject;
        }

        public static string ComponentName(Component component, Component[] components)
        {
            Component[] same = Array.FindAll(components, other => other != null && other.GetType() == component.GetType());
            return component.GetType().Name + (same.Length > 1 ? " (" + (Array.IndexOf(same, component) + 1) + ")" : "");
        }

        public static void RememberScene(DialogueGraph graph, Scene scene)
        {
            if (graph.editorScene != null || string.IsNullOrEmpty(scene.path)) return;
            Undo.RecordObject(graph, "Set Edit Scene");
            graph.editorScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            EditorUtility.SetDirty(graph);
        }

        public static void OpenScene(DialogueGraph graph)
        {
            if (graph == null || graph.editorScene == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = AssetDatabase.GetAssetPath(graph.editorScene);
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
