namespace DialogueEditor
{
    using UnityEngine;
    using XNodeEditor;

    [CustomNodeEditor(typeof(OptionNode))]
    public class OptionNodeEditor : DialogueNodeEditorBase
    {
        protected override string ListName => "optionList";
        protected override float RowHeight => 42;
        protected override void InitItem(int index) { ((OptionNode)target).optionList[index] = string.Empty; }
        protected override void DrawItem(Rect rect, int index)
        {
            DrawText(new Rect(rect.x, rect.y + 2, rect.width, 36),
                serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index),
                serializedObject.FindProperty("localizedOptions").GetArrayElementAtIndex(index));
            DrawError(rect, index);
        }
    }
}
