namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class DialogueSettingsWindow : EditorWindow
    {
        [SerializeField] DialogueGraph graph;
        ReorderableList list, speakers;
        Vector2 scroll;

        static DialogueSettings Settings => DialogueSettings.Instance;

        public static void Open(DialogueGraph graph)
        {
            var window = GetWindow<DialogueSettingsWindow>("Dialogue Settings");
            window.graph = graph; window.speakers = null;
        }

        void OnEnable() => Undo.undoRedoPerformed += Refresh;
        void OnDisable() => Undo.undoRedoPerformed -= Refresh;

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Languages (Project)", EditorStyles.boldLabel);
            DrawLanguages();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Speakers (Graph)", EditorStyles.boldLabel);
            DrawSpeakers();
            EditorGUILayout.EndScrollView();
        }

        void DrawLanguages()
        {
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Default Language", Settings.languages[0]);
            if (EditorGUI.EndChangeCheck()) Rename(0, name);

            if (list == null)
            {
                list = new ReorderableList(new List<string>(), typeof(string));
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Other Languages");
                list.drawElementCallback = (rect, index, active, focused) =>
                {
                    EditorGUI.BeginChangeCheck();
                    string text = EditorGUI.TextField(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight), Settings.languages[index + 1]);
                    if (EditorGUI.EndChangeCheck()) Rename(index + 1, text);
                };
                list.onAddCallback = l => Change("Add Language", text => text.AddLanguage(), languages => languages.Add("Language " + languages.Count));
                list.onRemoveCallback = l =>
                {
                    int language = l.index + 1;
                    if (EditorUtility.DisplayDialog("Remove Language", "Remove " + Settings.languages[language] + " and its translations in every dialogue graph?", "Remove", "Cancel"))
                        Change("Remove Language", text => text.RemoveLanguage(language), languages => languages.RemoveAt(language));
                };
                list.onReorderCallbackWithDetails = (l, from, to) => Change("Move Language", text => text.MoveLanguage(from + 1, to + 1), languages =>
                {
                    string moved = languages[from + 1];
                    languages.RemoveAt(from + 1);
                    languages.Insert(to + 1, moved);
                });
            }
            list.list = Settings.languages.Skip(1).ToList();
            list.DoLayoutList();
        }

        void DrawSpeakers()
        {
            var picked = (DialogueGraph)EditorGUILayout.ObjectField("Graph", graph, typeof(DialogueGraph), false);
            if (picked != graph) { graph = picked; speakers = null; }
            DrawLanguage();
            if (graph == null)
            {
                EditorGUILayout.HelpBox("Choose a dialogue graph to edit its speakers.", MessageType.Info);
                return;
            }

            if (speakers == null) speakers = CreateList(new SerializedObject(graph));
            SerializedObject serializedObject = speakers.serializedProperty.serializedObject;
            serializedObject.Update();
            speakers.DoLayoutList();
            if (serializedObject.ApplyModifiedProperties()) DialogueGraphEditor.Invalidate();
        }

        void DrawLanguage()
        {
            int language = EditorGUILayout.Popup("Language", DialogueSettings.LanguageIndex, Settings.languages.ToArray());
            if (language != DialogueSettings.LanguageIndex) SetLanguage(language);
        }

        ReorderableList CreateList(SerializedObject serializedObject)
        {
            var list = new ReorderableList(serializedObject, serializedObject.FindProperty("speakers"));
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Speakers");
            list.drawElementCallback = (rect, index, active, focused) =>
                DialogueNodeEditorBase.DrawText(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight), list.serializedProperty.GetArrayElementAtIndex(index));
            list.onAddCallback = l =>
            {
                SerializedProperty speaker = l.serializedProperty;
                speaker.InsertArrayElementAtIndex(speaker.arraySize);
                SerializedProperty translations = speaker.GetArrayElementAtIndex(speaker.arraySize - 1).FindPropertyRelative("translations");
                translations.arraySize = Settings.languages.Count;
                for (int i = 0; i < translations.arraySize; i++)
                    translations.GetArrayElementAtIndex(i).stringValue = i == 0 ? "Speaker " + speaker.arraySize : "";
            };
            list.onRemoveCallback = l =>
            {
                int removed = l.index;
                RemapPeople(person => person == removed ? -1 : person > removed ? person - 1 : person);
                ReorderableList.defaultBehaviours.DoRemoveButton(l);
            };
            list.onReorderCallbackWithDetails = (l, from, to) =>
                RemapPeople(person => person == from ? to : from < to && person > from && person <= to ? person - 1 : to < from && person >= to && person < from ? person + 1 : person);
            return list;
        }

        void Rename(int language, string name)
        {
            Undo.RecordObject(Settings, "Rename Language");
            Settings.languages[language] = name;
            EditorUtility.SetDirty(Settings);
            Refresh();
        }

        void Change(string label, Action<LocalizedText> text, Action<List<string>> languages)
        {
            string current = Settings.languages[DialogueSettings.LanguageIndex];
            var objects = new List<Object> { Settings };
            foreach (string guid in AssetDatabase.FindAssets("t:DialogueGraph"))
            {
                var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(AssetDatabase.GUIDToAssetPath(guid));
                objects.Add(graph);
                objects.AddRange(graph.nodes);
            }
            Undo.RecordObjects(objects.ToArray(), label);

            foreach (Object obj in objects)
            {
                if (obj is DialogueGraph graph) graph.speakers.ForEach(text);
                if (obj is DialogueNode dialogue) dialogue.dialogueList.ForEach(line => text(line.context));
                if (obj is OptionNode option) option.optionList.ForEach(text);
                EditorUtility.SetDirty(obj);
            }
            languages(Settings.languages);
            DialogueSettings.LanguageIndex = Mathf.Max(0, Settings.languages.IndexOf(current));
            AssetDatabase.SaveAssets();
            Refresh();
        }

        void RemapPeople(Func<int, int> remap)
        {
            foreach (XNode.Node node in graph.nodes)
                if (node is DialogueNode dialogue)
                {
                    Undo.RecordObject(dialogue, "Edit Speakers");
                    foreach (DialogueNode.DialogueInfo info in dialogue.dialogueList) info.person = remap(info.person);
                    EditorUtility.SetDirty(dialogue);
                }
        }

        public static void SetLanguage(int language)
        {
            DialogueSettings.LanguageIndex = language;
            InternalEditorUtility.RepaintAllViews();
        }

        void Refresh()
        {
            speakers = null;
            DialogueGraphEditor.Invalidate();
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
