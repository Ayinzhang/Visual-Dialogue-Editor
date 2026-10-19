namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;
    using XNode;

    [CustomNodeGraphEditor(typeof(DialogueGraph))]
    public class DialogueGraphEditor : NodeGraphEditor
    {
        DialogueGraph graph => target as DialogueGraph;
        static readonly Dictionary<DialogueGraph, List<DialogueIssue>> issues = new Dictionary<DialogueGraph, List<DialogueIssue>>();
        static double nextRefresh;
        public override bool IsReadOnly => EditorApplication.isPlaying;

        static DialogueGraphEditor()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => Invalidate();
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += scene => Invalidate();
        }

        public static void Invalidate() { issues.Clear(); nextRefresh = 0; NodeEditorWindow.RepaintAll(); }
        public static List<DialogueIssue> Issues(DialogueGraph graph)
        {
            if (EditorApplication.timeSinceStartup >= nextRefresh)
            { issues.Clear(); nextRefresh = EditorApplication.timeSinceStartup + 0.35; }
            if (!issues.TryGetValue(graph, out List<DialogueIssue> result))
            {
                result = DialogueValidation.Validate(graph);
                issues[graph] = result;
            }
            return result;
        }

        public static IEnumerable<DialogueSystem> Players(DialogueGraph graph)
        {
            if (!EditorApplication.isPlaying) return Enumerable.Empty<DialogueSystem>();
            return UnityEngine.Object.FindObjectsOfType<DialogueSystem>()
                .Where(system => system.isActiveAndEnabled && system.graph != null &&
                    system.graph.EntryNode != null && system.graph.EntryNode.graph == graph);
        }

        public override void OnOpen()
        {
            if (!EditorApplication.isPlaying) graph?.EnsureLocalizationData();
            Invalidate();
            Undo.undoRedoPerformed -= Invalidate; Undo.undoRedoPerformed += Invalidate;
        }
        public override void OnGUI()
        {
            if (window.hoveredPort != null && (Folded(window.hoveredPort.node) ||
                window.hoveredPort.GetConnections().Any(port => Folded(port.node)))) window.hoveredPort = null;
            window.onLateGUI += DrawToolbar;
            if (EditorApplication.isPlaying) window.Repaint();
        }

        public static bool Folded(Node node)
        {
            if (node is DialogueNode dialogue) return dialogue.isMin;
            if (node is OptionNode options) return options.isMin;
            if (node is EventNode events) return events.isMin;
            return node is CheckNode check && check.isMin;
        }

        public override bool CanConnect(NodePort output, NodePort input)
        {
            if (EditorApplication.isPlaying || Folded(output.node) || Folded(input.node)) return false;
            int count = DialogueValidation.ItemCount(output.node);
            bool valid = false;
            for (int i = 0; i < count; i++)
                if (output.fieldName == DialogueValidation.OutputName(output.node, i)) valid = DialogueValidation.HasOutput(output.node, i);
            return valid && DialogueValidation.TryDestination(input, out _, out _) && base.CanConnect(output, input);
        }

        public override string GetPortTooltip(NodePort port) => port.fieldName;

        void DrawToolbar()
        {
            if (graph == null) return;
            Matrix4x4 matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;

            GUILayout.BeginArea(new Rect(8, 5, 52, EditorGUIUtility.singleLineHeight + 4), EditorStyles.toolbar);
            if (GUILayout.Button("Edit", EditorStyles.toolbarDropDown))
            {
                GenericMenu menu = new GenericMenu();
                if (EditorApplication.isPlaying)
                {
                    menu.AddDisabledItem(new GUIContent("Edit Languages"));
                    menu.AddDisabledItem(new GUIContent("Edit Speakers"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Edit Languages"), false, () => DialogueEditWindow.Open(graph, true));
                    menu.AddItem(new GUIContent("Edit Speakers"), false, () => DialogueEditWindow.Open(graph, false));
                }
                menu.AddSeparator("");
                if (EditorApplication.isPlaying || !DialogueSerialization.Available)
                {
                    menu.AddDisabledItem(new GUIContent("Import"));
                    menu.AddDisabledItem(new GUIContent("Export"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Import"), false, () => DialogueSerialization.Import(graph));
                    menu.AddItem(new GUIContent("Export"), false, () => DialogueSerialization.Export(graph));
                }
                if (!DialogueSerialization.Available)
                {
                    menu.AddSeparator("");
                    GUIContent install = new GUIContent(DialogueDependencyInstaller.IsInstalling ? "Installing YamlDotNet..." : "Install YamlDotNet");
                    if (EditorApplication.isPlayingOrWillChangePlaymode || DialogueDependencyInstaller.IsInstalling) menu.AddDisabledItem(install);
                    else menu.AddItem(install, false, DialogueDependencyInstaller.Install);
                }
                menu.ShowAsContext();
            }
            GUILayout.EndArea();
            List<DialogueIssue> diagnostics = Issues(graph);
            if (diagnostics.Count > 0)
                GUI.Label(new Rect(70, 5, Mathf.Max(0, window.position.width - 280), 22),
                    new GUIContent(diagnostics.Count(issue => !issue.deferred) + " error(s), " +
                        diagnostics.Count(issue => issue.deferred) + " deferred", string.Join("\n", diagnostics.Select(issue => issue.message))), EditorStyles.toolbarButton);

            GUILayout.BeginArea(new Rect(window.position.width - 198, 5, 190, EditorGUIUtility.singleLineHeight + 4), EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Language:", GUILayout.Width(65));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if (GUILayout.Button(graph.CurrentLanguage, EditorStyles.toolbarDropDown, GUILayout.Width(110)))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < graph.Languages.Count; i++)
                {
                    int index = i;
                    menu.AddItem(new GUIContent(graph.Languages[i]), i == graph.LanguageIndex, () => SetLanguage(index));
                }
                menu.ShowAsContext();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = matrix;
        }

        void SetLanguage(int index)
        {
            if (EditorApplication.isPlaying) return;
            Undo.RecordObject(graph, "Switch dialogue language");
            graph.SetEditorLanguage(index); EditorUtility.SetDirty(graph); window.Repaint();
        }
    }

    public class DialogueEditWindow : EditorWindow
    {
        DialogueGraph graph; bool editLanguages; Vector2 scroll;

        public static void Open(DialogueGraph graph, bool editLanguages)
        {
            if (!EditorApplication.isPlaying) graph.EnsureLocalizationData();
            DialogueEditWindow window = CreateInstance<DialogueEditWindow>();
            window.graph = graph; window.editLanguages = editLanguages;
            window.titleContent = new GUIContent(editLanguages ? "Dialogue Languages" : "Dialogue Speakers");
            window.minSize = new Vector2(320, 180); window.ShowUtility();
        }

        void OnGUI()
        {
            if (graph == null) { Close(); return; }
            if (!editLanguages) { DialogueEditorGUI.DrawLanguage(graph); EditorGUILayout.Space(); }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (editLanguages) DialogueEditorGUI.DrawLanguages(graph);
            else DialogueEditorGUI.DrawSpeakers(graph);
            EditorGUILayout.EndScrollView();
        }
    }

    [CustomEditor(typeof(DialogueGraph))]
    public class DialogueGraphInspector : UnityEditor.Editor
    {
        void OnEnable() { if (!EditorApplication.isPlaying) (target as DialogueGraph)?.EnsureLocalizationData(); }

        public override void OnInspectorGUI()
        {
            DialogueGraph graph = target as DialogueGraph;
            if (graph == null) return;
            if (GUILayout.Button("Edit Graph", GUILayout.Height(40))) NodeEditorWindow.Open(graph);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !DialogueSerialization.Available))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Import")) DialogueSerialization.Import(graph);
                if (GUILayout.Button("Export")) DialogueSerialization.Export(graph);
                EditorGUILayout.EndHorizontal();
            }
            DialogueDependencyInstaller.DrawInspector();
            foreach (DialogueIssue issue in DialogueGraphEditor.Issues(graph))
                EditorGUILayout.HelpBox((issue.node != null ? issue.node.name + ": " : string.Empty) + issue.message,
                    issue.deferred ? MessageType.Info : MessageType.Error);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Languages", EditorStyles.boldLabel);
            DialogueEditorGUI.DrawLanguages(graph);
            EditorGUILayout.Space();
            DialogueEditorGUI.DrawLanguage(graph);
            EditorGUILayout.LabelField("Speakers", EditorStyles.boldLabel);
            DialogueEditorGUI.DrawSpeakers(graph);
        }
    }

    static class DialogueEditorGUI
    {
        public static void DrawLanguage(DialogueGraph graph)
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                string[] languages = new string[graph.Languages.Count];
                for (int i = 0; i < languages.Length; i++) languages[i] = graph.Languages[i];
                EditorGUI.BeginChangeCheck();
                int index = EditorGUILayout.Popup("Language", graph.LanguageIndex, languages);
                if (EditorGUI.EndChangeCheck()) Change(graph, "Switch dialogue language", () => graph.SetEditorLanguage(index));
            }
        }

        public static void DrawLanguages(DialogueGraph graph)
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                for (int i = 0; i < graph.Languages.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    string language = EditorGUILayout.TextField("Language " + (i + 1), graph.Languages[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(graph, "Rename dialogue language");
                        if (graph.SetLanguageName(i, language)) Dirty(graph);
                    }

                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && i > 0;
                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        ChangeAll(graph, "Remove dialogue language", () => graph.RemoveLanguage(i));
                        GUIUtility.ExitGUI();
                    }
                    GUI.enabled = enabled;
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Language"))
                    ChangeAll(graph, "Add dialogue language", () => graph.AddLanguage(NewLanguageName(graph)));
                EditorGUILayout.HelpBox("The first language is the fallback language and cannot be removed.", MessageType.Info);
            }
        }

        public static void DrawSpeakers(DialogueGraph graph)
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                string[] speakers = graph.GetSpeakerNames();
                for (int i = 0; i < speakers.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    string speaker = EditorGUILayout.TextField("Speaker " + (i + 1), speakers[i]);
                    if (EditorGUI.EndChangeCheck()) Change(graph, "Edit localized speaker", () => graph.SetSpeakerName(i, speaker));

                    if (graph.LanguageIndex > 0)
                    {
                        string raw = graph.GetRawSpeaker(i, graph.LanguageIndex);
                        EditorGUI.BeginChangeCheck();
                        bool translated = EditorGUILayout.Toggle(new GUIContent(string.Empty,
                            "Use this translation, including empty text. Uncheck to use the fallback language."), raw != null, GUILayout.Width(18));
                        if (EditorGUI.EndChangeCheck()) Change(graph, "Toggle speaker translation", () => graph.SetSpeakerName(i, translated ? raw ?? string.Empty : null));
                    }
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && graph.LanguageIndex == 0;
                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        ChangeAll(graph, "Remove dialogue speaker", () => graph.RemoveSpeaker(i));
                        GUIUtility.ExitGUI();
                    }
                    GUI.enabled = enabled;
                    EditorGUILayout.EndHorizontal();
                }

                bool canEdit = GUI.enabled;
                GUI.enabled = canEdit && graph.LanguageIndex == 0;
                if (GUILayout.Button("Add Speaker")) Change(graph, "Add dialogue speaker", graph.AddSpeaker);
                GUI.enabled = canEdit;
                if (graph.LanguageIndex != 0) EditorGUILayout.HelpBox("Add or remove speakers in the fallback language.", MessageType.Info);
            }
        }

        static string NewLanguageName(DialogueGraph graph)
        {
            int num = graph.Languages.Count + 1;
            while (true)
            {
                string language = "Language " + num++;
                bool exists = false;
                for (int i = 0; i < graph.Languages.Count; i++)
                    if (string.Equals(graph.Languages[i], language, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (!exists) return language;
            }
        }

        static void Change(DialogueGraph graph, string name, Action action)
        {
            Undo.RecordObject(graph, name); action(); Dirty(graph);
        }

        static void ChangeAll(DialogueGraph graph, string name, Action action)
        {
            UnityEngine.Object[] objects = new UnityEngine.Object[graph.nodes.Count + 1]; objects[0] = graph;
            for (int i = 0; i < graph.nodes.Count; i++) objects[i + 1] = graph.nodes[i];
            Undo.RecordObjects(objects, name); action();
            for (int i = 1; i < objects.Length; i++) EditorUtility.SetDirty(objects[i]);
            Dirty(graph);
        }

        static void Dirty(DialogueGraph graph)
        {
            EditorUtility.SetDirty(graph);
            DialogueGraphEditor.Invalidate();
            if (NodeEditorWindow.current != null) NodeEditorWindow.current.Repaint();
        }
    }

    public static class SceneObjectReferenceEditor
    {
        public static string GetId(GameObject obj) { return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString(); }

        public static string GetPath(GameObject obj)
        {
            string path = obj.name;
            for (Transform trans = obj.transform.parent; trans != null; trans = trans.parent) path = trans.name + "/" + path;
            return obj.scene.name + "/" + path;
        }

        public static GameObject Find(GameObject obj, string id, string path)
        {
            SceneObjectReference.TryFind(obj, path, id, out GameObject found, out _);
            return found;
        }
    }
}
