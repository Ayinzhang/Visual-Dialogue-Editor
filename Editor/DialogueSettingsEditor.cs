namespace DialogueEditor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;

    [CustomEditor(typeof(DialogueSettings)), InitializeOnLoad]
    public class DialogueSettingsEditor : UnityEditor.Editor
    {
        public const string SettingsPath = "Project/Dialogue Editor";
        ReorderableList languages;

        static DialogueSettingsEditor()
        {
            Undo.undoRedoPerformed += RepaintViews;
            EditorApplication.playModeStateChanged += state => RepaintViews();
        }

        void OnEnable()
        {
            DialogueSettings settings = (DialogueSettings)target;
            languages = new ReorderableList(settings.languages.Skip(1).ToList(), typeof(string), true, true, true, true);
            languages.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Other Languages");
            languages.drawElementCallback = (rect, index, active, focused) =>
                DrawName(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight), index + 1);
            languages.onAddCallback = list =>
            {
                int number = settings.languages.Count + 1;
                string name;
                do { name = "Language " + number++; }
                while (settings.languages.Any(language => string.Equals(language, name, StringComparison.OrdinalIgnoreCase)));
                AddLanguage(name);
                list.index = settings.languages.Count - 2;
            };
            languages.onCanRemoveCallback = list => list.index >= 0 && list.index < settings.languages.Count - 1;
            languages.onRemoveCallback = list =>
            {
                int index = list.index + 1;
                if (!EditorUtility.DisplayDialog("Remove Language", "Remove " + settings.languages[index] +
                    " and its translations from every dialogue graph?", "Remove", "Cancel")) return;
                RemoveLanguage(index);
                list.index = Mathf.Min(list.index, settings.languages.Count - 2);
            };
            languages.onReorderCallbackWithDetails = (list, from, to) => MoveLanguage(from + 1, to + 1);
        }

        public override void OnInspectorGUI()
        {
            if (target != DialogueSettings.Instance)
            {
                EditorGUILayout.HelpBox("Use the shared Resources/DialogueSettings.asset, not a copy.", MessageType.Error);
                return;
            }
            DrawLanguage();
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                DrawName(EditorGUILayout.GetControlRect(), 0);
                languages.list = DialogueSettings.Instance.languages.Skip(1).ToList();
                languages.DoLayoutList();
            }
        }

        void DrawName(Rect rect, int index)
        {
            DialogueSettings settings = DialogueSettings.Instance;
            EditorGUI.BeginChangeCheck();
            string name = EditorGUI.DelayedTextField(rect, index == 0 ? "Default Language" : "Language " + (index + 1), settings.languages[index]);
            if (!EditorGUI.EndChangeCheck()) return;
            name = name.Trim();
            if (string.IsNullOrEmpty(name) || settings.languages.Where((language, i) => i != index)
                .Any(language => string.Equals(language, name, StringComparison.OrdinalIgnoreCase)))
            { Debug.LogError("Language names must be nonempty and unique.", settings); return; }
            if (!Writable(new UnityEngine.Object[] { settings })) return;
            Undo.RecordObject(settings, "Rename dialogue language");
            settings.languages[index] = name;
            Save(settings);
        }

        public static void DrawLanguage()
        {
            EditorGUI.BeginChangeCheck();
            int index = EditorGUILayout.Popup("Language", DialogueSettings.Language, DialogueSettings.Instance.languages.ToArray());
            if (EditorGUI.EndChangeCheck()) SetLanguage(index);
        }

        public static void SetLanguage(int index)
        {
            DialogueSettings settings = DialogueSettings.Instance;
            if (!EditorApplication.isPlaying)
            {
                if (!Writable(new UnityEngine.Object[] { settings })) return;
                Undo.RecordObject(settings, "Switch dialogue language");
            }
            DialogueSettings.Language = index;
            if (!EditorApplication.isPlaying) Save(settings);
            else RepaintViews();
        }

        internal static void AddLanguage(string name)
        {
            Change("Add dialogue language", graphs =>
            {
                DialogueSettings settings = DialogueSettings.Instance;
                settings.languages.Add(name);
                settings.languageIndex = settings.languages.Count - 1;
                foreach (DialogueGraph graph in graphs) graph.EnsureLocalizationData();
            });
        }

        internal static void RemoveLanguage(int index)
        {
            if (index <= 0 || index >= DialogueSettings.Instance.languages.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Change("Remove dialogue language", graphs =>
            {
                DialogueSettings settings = DialogueSettings.Instance;
                settings.languages.RemoveAt(index);
                if (settings.languageIndex >= index) settings.languageIndex--;
                foreach (DialogueGraph graph in graphs) graph.RemoveLanguage(index);
            });
        }

        internal static void MoveLanguage(int from, int to)
        {
            DialogueSettings settings = DialogueSettings.Instance;
            if (from <= 0 || to <= 0 || from >= settings.languages.Count || to >= settings.languages.Count)
                throw new ArgumentOutOfRangeException(nameof(from));
            Change("Reorder dialogue languages", graphs =>
            {
                string name = settings.languages[from];
                settings.languages.RemoveAt(from); settings.languages.Insert(to, name);
                if (settings.languageIndex == from) settings.languageIndex = to;
                else if (from < to && settings.languageIndex > from && settings.languageIndex <= to) settings.languageIndex--;
                else if (from > to && settings.languageIndex >= to && settings.languageIndex < from) settings.languageIndex++;
                foreach (DialogueGraph graph in graphs) graph.MoveLanguage(from, to);
            });
        }

        static void Change(string name, Action<DialogueGraph[]> action)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Dialogue languages are read-only during playback.");
            DialogueGraph[] graphs = AssetDatabase.FindAssets("t:DialogueGraph")
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<DialogueGraph>().ToArray();
            UnityEngine.Object[] objects = graphs.Cast<UnityEngine.Object>()
                .Concat(graphs.SelectMany(graph => graph.nodes).Cast<UnityEngine.Object>())
                .Append(DialogueSettings.Instance).Distinct().ToArray();
            if (objects.Any(obj => obj == null))
            { Debug.LogError("Cannot change dialogue languages: a graph contains a missing node."); return; }
            if (!Writable(objects)) return;
            Undo.IncrementCurrentGroup();
            Undo.RecordObjects(objects, name);
            action(graphs);
            foreach (UnityEngine.Object obj in objects) EditorUtility.SetDirty(obj);
            foreach (UnityEngine.Object obj in objects) AssetDatabase.SaveAssetIfDirty(obj);
            RepaintViews();
        }

        static bool Writable(UnityEngine.Object[] objects)
        {
            foreach (string path in objects.Select(AssetDatabase.GetAssetPath).Distinct())
                if (!AssetDatabase.IsOpenForEdit(path) || File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                { Debug.LogError("Cannot change dialogue languages: asset is read-only: " + path); return false; }
            return true;
        }

        static void Save(DialogueSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            RepaintViews();
        }

        static void RepaintViews()
        {
            DialogueGraphEditor.Invalidate();
            InternalEditorUtility.RepaintAllViews();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            UnityEditor.Editor editor = null;
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                activateHandler = (search, root) => editor = CreateEditor(DialogueSettings.Instance),
                deactivateHandler = () => DestroyImmediate(editor),
                guiHandler = search => editor.OnInspectorGUI()
            };
        }
    }
}
