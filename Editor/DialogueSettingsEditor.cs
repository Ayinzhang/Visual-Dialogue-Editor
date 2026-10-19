namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public static class DialogueSettingsEditor
    {
        static ReorderableList list;

        static DialogueSettings Settings => DialogueSettings.Instance;

        [SettingsProvider]
        static SettingsProvider Provider() => new SettingsProvider("Project/Dialogue", SettingsScope.Project) { guiHandler = _ => OnGUI() };

        public static void DrawLanguage()
        {
            int language = EditorGUILayout.Popup("Language", DialogueSettings.Language, Settings.languages.ToArray());
            if (language != DialogueSettings.Language) SetLanguage(language);
        }

        public static void SetLanguage(int language)
        {
            DialogueSettings.Language = language;
            EditorUtility.SetDirty(Settings);
            InternalEditorUtility.RepaintAllViews();
        }

        static void OnGUI()
        {
            DrawLanguage();
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
                list.onAddCallback = l =>
                {
                    Undo.RecordObject(Settings, "Add Language");
                    Settings.languages.Add("Language " + Settings.languages.Count);
                    EditorUtility.SetDirty(Settings);
                };
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

        static void Rename(int language, string name)
        {
            Undo.RecordObject(Settings, "Rename Language");
            Settings.languages[language] = name;
            EditorUtility.SetDirty(Settings);
        }

        static void Change(string label, Action<LocalizedText> text, Action<List<string>> languages)
        {
            string current = Settings.languages[Settings.languageIndex];
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
            Settings.languageIndex = Mathf.Max(0, Settings.languages.IndexOf(current));
            AssetDatabase.SaveAssets();
            DialogueSettings.Language = Settings.languageIndex;
        }
    }
}
