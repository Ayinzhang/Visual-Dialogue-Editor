namespace DialogueEditor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using XNode;
    using XNodeEditor;

    [InitializeOnLoad, CustomNodeGraphEditor(typeof(DialogueGraph))]
    public class DialogueGraphEditor : NodeGraphEditor
    {
        static readonly Dictionary<DialogueGraph, List<DialogueIssue>> issues = new Dictionary<DialogueGraph, List<DialogueIssue>>();
        static double issuesTime;

        Label issuesLabel;
        ToolbarButton languageButton;

        static DialogueGraphEditor()
        {
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Invalidate();
        }

        [UnityEditor.Callbacks.OnOpenAsset(-1)]
        static bool OpenGraph(int instanceID, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceID) is DialogueGraph graph)) return false;
            NodeEditorWindow window = NodeEditorWindow.Open(graph);
            EditorApplication.delayCall += () => { if (window != null && window.graph == graph) DialogueBindingEditor.OpenScene(graph); };
            return true;
        }

        public static void Invalidate()
        {
            issues.Clear();
            NodeEditorWindow.RepaintAll();
        }

        public static List<DialogueIssue> Issues(DialogueGraph graph)
        {
            if (EditorApplication.timeSinceStartup - issuesTime > 0.5) { issues.Clear(); issuesTime = EditorApplication.timeSinceStartup; }
            if (!issues.TryGetValue(graph, out List<DialogueIssue> list)) issues[graph] = list = DialogueValidation.Validate(graph);
            return list;
        }

        public static DialogueGraph Playing(Node node) => DialogueGraph.Running.Find(graph => graph.node == node);

        public override bool CanConnect(NodePort output, NodePort input) =>
            !((BaseNode)output.node).isMin && !((BaseNode)input.node).isMin && DialogueValidation.CanLink(output, input) && base.CanConnect(output, input);

        public override void OnOpen()
        {
            window.rootVisualElement.Q("dialogue-toolbar")?.RemoveFromHierarchy();
            var toolbar = new Toolbar { name = "dialogue-toolbar" };
            toolbar.style.position = Position.Absolute;
            toolbar.style.left = toolbar.style.right = toolbar.style.top = 0;
            toolbar.Add(new ToolbarButton(() => SettingsService.OpenProjectSettings("Project/Dialogue")) { text = "Languages" });
            toolbar.Add(new ToolbarButton(() => DialogueSpeakersWindow.Open((DialogueGraph)target)) { text = "Speakers" });
            toolbar.Add(issuesLabel = new Label());
            issuesLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(languageButton = new ToolbarButton());
            languageButton.clicked += () =>
            {
                var menu = new GenericMenu();
                List<string> languages = DialogueSettings.Instance.languages;
                for (int i = 0; i < languages.Count; i++)
                {
                    int language = i;
                    menu.AddItem(new GUIContent(languages[i]), DialogueSettings.Language == i, () => DialogueSettingsEditor.SetLanguage(language));
                }
                menu.DropDown(languageButton.worldBound);
            };
            window.rootVisualElement.Add(toolbar);
        }

        public override void OnGUI()
        {
            if (issuesLabel == null) OnOpen();
            List<DialogueIssue> list = Issues((DialogueGraph)target);
            int pending = list.FindAll(issue => issue.unverified).Count, errors = list.Count - pending;
            issuesLabel.text = list.Count == 0 ? "No issues" : errors == 0 ? pending + " unverified" : errors + " issue(s)" + (pending > 0 ? ", " + pending + " unverified" : "");
            issuesLabel.tooltip = string.Join("\n", list.ConvertAll(issue => (issue.node == null ? "" : issue.node.name + ": ") + issue.message));
            languageButton.text = "Language: " + DialogueSettings.Instance.languages[DialogueSettings.Language] + " ▾";

            if (window.hoveredPort != null && ((BaseNode)window.hoveredPort.node).isMin && window.hoveredPort.IsDynamic) window.hoveredPort = null;
            if (Application.isPlaying) window.Repaint();
        }
    }
}
