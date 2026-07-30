namespace DialogueEditor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(DialogueSystem))]
    public class DialogueSystemEditor : UnityEditor.Editor
    {
        SerializedProperty graph;
        SerializedProperty languageIndex;

        void OnEnable()
        {
            graph = serializedObject.FindProperty("graph");
            languageIndex = serializedObject.FindProperty("languageIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "languageIndex");

            DialogueGraph dialogueGraph = graph.objectReferenceValue as DialogueGraph;
            if (dialogueGraph == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup("Language", 0, new[] { "Assign a Dialogue Graph" });
            }
            else
            {
                IReadOnlyList<string> languages = dialogueGraph.Languages;
                string[] options = new string[languages.Count];
                for (int i = 0; i < options.Length; i++) options[i] = languages[i];

                int currentIndex = Mathf.Clamp(languageIndex.intValue, 0, options.Length - 1);
                languageIndex.intValue = EditorGUILayout.Popup("Language", currentIndex, options);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
