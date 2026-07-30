namespace DialogueEditor
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;

    [CustomNodeGraphEditor(typeof(DialogueGraph))]
    public class DialogueGraphEditor : NodeGraphEditor
    {
        DialogueGraph graph => target as DialogueGraph;

        public override void OnOpen() { graph?.EnsureLocalizationData(); }
        public override void OnGUI() { window.onLateGUI += DrawToolbar; }

        void DrawToolbar()
        {
            if (graph == null) return;
            Matrix4x4 matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;

            GUILayout.BeginArea(new Rect(8, 5, 52, EditorGUIUtility.singleLineHeight + 4), EditorStyles.toolbar);
            if (GUILayout.Button("Edit", EditorStyles.toolbarDropDown))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Edit Languages"), false, () => DialogueEditWindow.Open(graph, true));
                menu.AddItem(new GUIContent("Edit Speakers"), false, () => DialogueEditWindow.Open(graph, false));
                menu.ShowAsContext();
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(window.position.width - 198, 5, 190, EditorGUIUtility.singleLineHeight + 4), EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Language:", GUILayout.Width(65));
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
            Undo.RecordObject(graph, "Switch dialogue language");
            graph.SetEditorLanguage(index); EditorUtility.SetDirty(graph); window.Repaint();
        }
    }

    public class DialogueEditWindow : EditorWindow
    {
        DialogueGraph graph; bool editLanguages; Vector2 scroll;

        public static void Open(DialogueGraph graph, bool editLanguages)
        {
            graph.EnsureLocalizationData();
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
        void OnEnable() { (target as DialogueGraph)?.EnsureLocalizationData(); }

        public override void OnInspectorGUI()
        {
            DialogueGraph graph = target as DialogueGraph;
            if (graph == null) return;
            if (GUILayout.Button("Edit Graph", GUILayout.Height(40))) NodeEditorWindow.Open(graph);

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
            string[] languages = new string[graph.Languages.Count];
            for (int i = 0; i < languages.Length; i++) languages[i] = graph.Languages[i];
            EditorGUI.BeginChangeCheck();
            int index = EditorGUILayout.Popup("Language", graph.LanguageIndex, languages);
            if (EditorGUI.EndChangeCheck()) Change(graph, "Switch dialogue language", () => graph.SetEditorLanguage(index));
        }

        public static void DrawLanguages(DialogueGraph graph)
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

                GUI.enabled = i > 0;
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    ChangeAll(graph, "Remove dialogue language", () => graph.RemoveLanguage(i));
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Language"))
                ChangeAll(graph, "Add dialogue language", () => graph.AddLanguage(NewLanguageName(graph)));
            EditorGUILayout.HelpBox("The first language is the fallback language and cannot be removed.", MessageType.Info);
        }

        public static void DrawSpeakers(DialogueGraph graph)
        {
            string[] speakers = graph.GetSpeakerNames();
            for (int i = 0; i < speakers.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string speaker = EditorGUILayout.TextField("Speaker " + (i + 1), speakers[i]);
                if (EditorGUI.EndChangeCheck()) Change(graph, "Edit localized speaker", () => graph.SetSpeakerName(i, speaker));

                GUI.enabled = graph.LanguageIndex == 0;
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    ChangeAll(graph, "Remove dialogue speaker", () => graph.RemoveSpeaker(i));
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            GUI.enabled = graph.LanguageIndex == 0;
            if (GUILayout.Button("Add Speaker")) Change(graph, "Add dialogue speaker", graph.AddSpeaker);
            GUI.enabled = true;
            if (graph.LanguageIndex != 0) EditorGUILayout.HelpBox("Add or remove speakers in the fallback language.", MessageType.Info);
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
            if (NodeEditorWindow.current != null) NodeEditorWindow.current.Repaint();
        }
    }

    static class SceneObjectReferenceEditor
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
            if (obj != null) return obj;
            if (!string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out GlobalObjectId globalId))
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
            return SceneObjectReference.Find(obj, path);
        }
    }
}
