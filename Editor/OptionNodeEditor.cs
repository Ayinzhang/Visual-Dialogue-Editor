namespace DialogueEditor
{
    using UnityEditor;
    using UnityEngine;

    [CustomNodeEditor(typeof(OptionNode))]
    public class OptionNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 40;

        protected override object NewItem() => new LocalizedText();

        protected override void DrawItem(Rect rect, SerializedProperty item, int index) =>
            DrawText(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), item);
    }
}
