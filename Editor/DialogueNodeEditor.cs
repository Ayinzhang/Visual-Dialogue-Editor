namespace DialogueEditor
{
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    [CustomNodeEditor(typeof(DialogueNode))]
    public class DialogueNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 64;

        protected override object NewItem() => new DialogueNode.DialogueInfo { person = Graph.speakers.Count == 0 ? -1 : 0 };

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            SerializedProperty sprite = item.FindPropertyRelative("sprite"), person = item.FindPropertyRelative("person");
            sprite.objectReferenceValue = EditorGUI.ObjectField(new Rect(rect.x, rect.y + 4, 56, 56), sprite.objectReferenceValue, typeof(Sprite), false);

            Rect line = new Rect(rect.x + 62, rect.y + 4, (rect.width - 66) / 2, EditorGUIUtility.singleLineHeight);
            int language = DialogueSettings.Language;
            string[] names = new[] { "(No speaker)" }.Concat(Graph.speakers.Select(s => s.Get(language))).ToArray();
            person.intValue = EditorGUI.Popup(line, person.intValue + 1, names) - 1;
            line.x += line.width + 4;
            EditorGUI.PropertyField(line, item.FindPropertyRelative("type"), GUIContent.none);

            DrawText(new Rect(rect.x + 62, line.yMax + 2, rect.width - 62, rect.yMax - line.yMax - 6), item.FindPropertyRelative("context"));
        }
    }
}
