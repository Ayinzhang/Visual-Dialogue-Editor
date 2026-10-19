namespace DialogueEditor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using XNodeEditor;

    [CustomEditor(typeof(DialogueGraph))]
    public class DialogueGraphInspector : UnityEditor.Editor
    {
        DialogueSpeakerList speakers;

        void OnEnable()
        {
            if (!EditorApplication.isPlaying) (target as DialogueGraph)?.EnsureLocalizationData();
            if (target is DialogueGraph graph) speakers = new DialogueSpeakerList(graph);
        }

        public override void OnInspectorGUI()
        {
            DialogueGraph graph = target as DialogueGraph;
            if (graph == null) return;
            if (GUILayout.Button("Edit Graph", GUILayout.Height(40))) NodeEditorWindow.Open(graph);
            foreach (DialogueIssue issue in DialogueGraphEditor.Issues(graph))
                EditorGUILayout.HelpBox((issue.node != null ? issue.node.name + ": " : string.Empty) + issue.message,
                    issue.deferred ? MessageType.Info : MessageType.Error);

            EditorGUILayout.Space();
            if (GUILayout.Button("Edit Project Languages")) SettingsService.OpenProjectSettings(DialogueSettingsEditor.SettingsPath);
            EditorGUILayout.Space();
            DialogueSettingsEditor.DrawLanguage();
            EditorGUILayout.LabelField("Speakers", EditorStyles.boldLabel);
            speakers.Draw();
        }
    }

    public class DialogueSpeakersWindow : EditorWindow
    {
        [SerializeField] DialogueGraph graph;
        Vector2 scroll;
        DialogueSpeakerList speakers;

        public static void Open(DialogueGraph graph)
        {
            if (!EditorApplication.isPlaying) graph.EnsureLocalizationData();
            DialogueSpeakersWindow window = CreateInstance<DialogueSpeakersWindow>();
            window.graph = graph;
            window.titleContent = new GUIContent("Dialogue Speakers");
            window.minSize = new Vector2(320, 180); window.ShowUtility();
        }

        void OnGUI()
        {
            if (graph == null) { Close(); return; }
            if (speakers == null) speakers = new DialogueSpeakerList(graph);
            DialogueSettingsEditor.DrawLanguage(); EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            speakers.Draw();
            EditorGUILayout.EndScrollView();
        }
    }

    class DialogueSpeakerList
    {
        readonly DialogueGraph graph;
        readonly ReorderableList list;

        public DialogueSpeakerList(DialogueGraph graph)
        {
            this.graph = graph;
            list = new ReorderableList(graph.speakers.ToList(), typeof(LocalizedText), true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Speakers");
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                EditorGUI.BeginChangeCheck();
                string name = EditorGUI.TextField(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight),
                    "Speaker " + (index + 1), graph.GetRawSpeaker(index, DialogueSettings.Language));
                if (EditorGUI.EndChangeCheck())
                    Change("Edit localized speaker", () => graph.SetSpeakerName(index, DialogueSettings.Language, name));
            };
            list.onCanAddCallback = value => DialogueSettings.Language == 0;
            list.onCanRemoveCallback = value => DialogueSettings.Language == 0 && value.index >= 0 && value.index < graph.speakers.Count;
            list.onAddCallback = value => Change("Add dialogue speaker", () =>
            {
                graph.AddSpeaker("Speaker " + (graph.speakers.Count + 1));
                value.index = graph.speakers.Count - 1;
            });
            list.onRemoveCallback = value => Change("Remove dialogue speaker", () =>
            {
                graph.RemoveSpeaker(value.index);
                value.index = Mathf.Min(value.index, graph.speakers.Count - 1);
            }, true);
            list.onReorderCallbackWithDetails = (value, from, to) =>
                Change("Reorder dialogue speakers", () => graph.MoveSpeaker(from, to), true);
        }

        public void Draw()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                list.list = graph.speakers.ToList();
                list.draggable = DialogueSettings.Language == 0;
                list.DoLayoutList();
                if (DialogueSettings.Language != 0) EditorGUILayout.HelpBox("Add, remove, or reorder speakers in the default language.", MessageType.Info);
            }
        }

        void Change(string name, Action action, bool nodes = false)
        {
            UnityEngine.Object[] objects = nodes ? graph.nodes.OfType<DialogueNode>().Cast<UnityEngine.Object>().Append(graph).ToArray() :
                new UnityEngine.Object[] { graph };
            Undo.RecordObjects(objects, name);
            action();
            foreach (UnityEngine.Object obj in objects) EditorUtility.SetDirty(obj);
            DialogueGraphEditor.Invalidate();
        }
    }
}
