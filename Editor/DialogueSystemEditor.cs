namespace DialogueEditor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(DialogueSystem))]
    public class DialogueSystemEditor : UnityEditor.Editor
    {
        SerializedProperty graphs;
        SerializedProperty languageIndex;

        void OnEnable()
        {
            graphs = serializedObject.FindProperty("graphs");
            languageIndex = serializedObject.FindProperty("languageIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cha"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bg"));

            DialogueGraph graph = graphs.arraySize == 0 ? null : graphs.GetArrayElementAtIndex(0).objectReferenceValue as DialogueGraph;
            if (graph == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup("Language", 0, new[] { "Assign Graphs[0]" });
            }
            else
            {
                IReadOnlyList<string> languages = graph.Languages;
                string[] options = new string[languages.Count];
                for (int i = 0; i < options.Length; i++) options[i] = languages[i];

                int currentIndex = Mathf.Clamp(languageIndex.intValue, 0, options.Length - 1);
                languageIndex.intValue = EditorGUILayout.Popup("Language", currentIndex, options);
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "cha", "bg", "languageIndex");
            serializedObject.ApplyModifiedProperties();
            if (EditorApplication.isPlaying)
            {
                DialogueSystem system = (DialogueSystem)target;
                EditorGUILayout.ObjectField("Current Graph", system.graph, typeof(DialogueGraph), false);
                EditorGUILayout.ObjectField("Current Node", system.graph == null ? null : system.graph.node, typeof(XNode.Node), false);
                Repaint();
            }
        }
    }
}
