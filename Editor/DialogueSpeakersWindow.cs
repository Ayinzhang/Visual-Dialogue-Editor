namespace DialogueEditor
{
    using System;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;

    public class DialogueSpeakersWindow : EditorWindow
    {
        [SerializeField] DialogueGraph graph;
        ReorderableList speakers;

        public static void Open(DialogueGraph graph)
        {
            var window = GetWindow<DialogueSpeakersWindow>("Speakers");
            window.graph = graph;
            window.speakers = null;
        }

        void OnEnable() => Undo.undoRedoPerformed += Repaint;
        void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        void OnGUI()
        {
            var picked = (DialogueGraph)EditorGUILayout.ObjectField("Graph", graph, typeof(DialogueGraph), false);
            if (picked != graph) { graph = picked; speakers = null; }
            DialogueSettingsEditor.DrawLanguage();
            if (graph == null) return;

            if (speakers == null) speakers = CreateList(new SerializedObject(graph));
            SerializedObject serializedObject = speakers.serializedProperty.serializedObject;
            serializedObject.Update();
            speakers.DoLayoutList();
            if (serializedObject.ApplyModifiedProperties()) DialogueGraphEditor.Invalidate();
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
                translations.arraySize = 1;
                translations.GetArrayElementAtIndex(0).stringValue = "Speaker " + speaker.arraySize;
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

        void RemapPeople(Func<int, int> remap)
        {
            foreach (XNode.Node node in graph.nodes)
                if (node is DialogueNode dialogue)
                {
                    Undo.RecordObject(dialogue, "Edit Speakers");
                    foreach (DialogueNode.DialogueInfo info in dialogue.dialogueList) info.person = remap(info.person);
                }
        }
    }
}
