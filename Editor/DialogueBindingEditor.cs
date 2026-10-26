namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using XNode;

    public class DialogueIssue
    {
        public Node node;
        public int item = -1;
        public string message;
        public bool unverified;
    }

    public static class DialogueValidation
    {
        public static bool CanLink(NodePort output, NodePort input)
        {
            NodeBase from = (NodeBase)output.node, to = (NodeBase)input.node;
            int o = NodeBase.ItemIndex(output), i = NodeBase.ItemIndex(input);
            return o >= 0 && o < from.Items.Count && from.HasOutput(o) && (i < 0 ? !to.ItemInputs : i < to.Items.Count && to.HasInput(i));
        }

        public static List<DialogueIssue> Validate(DialogueGraph graph)
        {
            var issues = new List<DialogueIssue>();
            DialogueSystemBase system = DialogueBindingEditor.SceneSystem(graph, out string systemIssue, out bool unverified);
            void Add(Node node, int item, string message, bool unverified = false) => issues.Add(new DialogueIssue { node = node, item = item, message = message, unverified = unverified });
            void Bind(Node node, int item, string key, Action resolve)
            {
                if (string.IsNullOrEmpty(key)) Add(node, item, "TargetGameObject is not assigned.");
                else if (system == null) Add(node, item, systemIssue, unverified);
                else try { resolve(); } catch (Exception e) { Add(node, item, (e.InnerException ?? e).Message); }
            }

            if (graph.entryNode == null) Add(null, -1, "Entry node is missing. Right-click a node to set it.");
            foreach (NodeBase node in graph.nodes)
            {
                if (node.Items.Count == 0) Add(node, -1, "Node has no content.");
                if (node is CheckNode check) Bind(node, -1, check.bindingKey, () => check.Next(system));
                if (node is EventNode events)
                    for (int i = 0; i < events.Items.Count; i++)
                    {
                        EventNode.FuncInfo function = events.eventList[i];
                        Bind(node, i, function.bindingKey, () => function.Resolve(system, out _, out _, out _));
                    }
                foreach (NodePort output in node.DynamicOutputs)
                    foreach (NodePort input in output.GetConnections())
                        if (!CanLink(output, input)) Add(node, NodeBase.ItemIndex(output), "Connection is not used: " + output.fieldName);
            }
            return issues;
        }
    }

    public static class DialogueBindingEditor
    {
        public static DialogueSystemBase SceneSystem(out string issue) => SceneSystem(null, out issue, out _);

        public static DialogueSystemBase SceneSystem(DialogueGraph graph, out string issue, out bool unverified)
        {
            Scene scene = SceneManager.GetActiveScene(); issue = null; unverified = false;
            try
            {
                DialogueSystemBase system = InScene(scene);
                if (system == null)
                {
                    issue = "bindings unverified. Open the edit scene or add one Dialogue System to the active scene.";
                    unverified = true; return null;
                }
                if (graph != null && graph.editorScene != null && AssetDatabase.GetAssetPath(graph.editorScene) != scene.path &&
                    (system.graphs == null || !Array.Exists(system.graphs, asset => asset == graph)))
                {
                    issue = "bindings unverified. Open the edit scene or assign this Graph to the active scene's System.";
                    unverified = true; return null;
                }
                return system;
            }
            catch (InvalidOperationException e) { issue = e.Message; return null; }
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

        public static GameObject TargetGameObject(DialogueSystemBase system, string key)
        {
            UnityEngine.Object target = system == null ? null : system.bindings.Find(key);
            return target == null ? null : target is Component component ? component.gameObject : target as GameObject;
        }

        public static string ComponentName(Component component, Component[] components)
        {
            Component[] same = Array.FindAll(components, other => other != null && other.GetType() == component.GetType());
            return component.GetType().Name + (same.Length > 1 ? " (" + (Array.IndexOf(same, component) + 1) + ")" : "");
        }

        public static void RememberScene(DialogueGraph graph, Scene scene)
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            if (graph.editorScene == asset) return;
            Undo.RecordObject(graph, "Set Edit Scene");
            graph.editorScene = asset;
            EditorUtility.SetDirty(graph);
        }

        public static void OpenScene(DialogueGraph graph)
        {
            if (graph.editorScene == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = AssetDatabase.GetAssetPath(graph.editorScene);
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
