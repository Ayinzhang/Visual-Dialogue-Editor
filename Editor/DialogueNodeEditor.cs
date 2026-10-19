namespace DialogueEditor
{
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;

    [CustomNodeEditor(typeof(DialogueNode))]
    public class DialogueNodeEditor : DialogueNodeEditorBase
    {
        protected override string ListName => "dialogueList";
        protected override float RowHeight => 98;
        protected override void InitItem(int index)
        { ((DialogueNode)target).dialogueList[index] = new DialogueNode.DialogueInfo { person = Graph.speakers.Count == 0 ? -1 : 0 }; }

        protected override void DrawItem(Rect rect, int index)
        {
            SerializedProperty item = serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index);
            DrawSprite(new Rect(rect.x, rect.y + 2, 50, 50), item, index);
            SerializedProperty person = item.FindPropertyRelative("person"), type = item.FindPropertyRelative("type");
            string[] speakers = Graph.GetSpeakerNames();
            var choices = new string[speakers.Length + 1]; choices[0] = "(No speaker)";
            speakers.CopyTo(choices, 1);
            person.intValue = EditorGUI.Popup(new Rect(rect.x + 56, rect.y + 2, rect.width - 56, 18), person.intValue + 1, choices) - 1;
            EditorGUI.PropertyField(new Rect(rect.x + 56, rect.y + 24, rect.width - 56, 18), type, GUIContent.none);
            DrawText(new Rect(rect.x, rect.y + 56, rect.width, 36),
                item.FindPropertyRelative("context"), item.FindPropertyRelative("localizedContext"));
            DrawError(new Rect(rect.x, rect.y, rect.width, 52), index);
        }

        void DrawSprite(Rect rect, SerializedProperty item, int index)
        {
            Sprite sprite = item.FindPropertyRelative("sprite").objectReferenceValue as Sprite;
            int control = GUIUtility.GetControlID(target.GetInstanceID() ^ index, FocusType.Passive, rect);
            Event current = Event.current;
            if (GUI.enabled && current.type == EventType.ExecuteCommand &&
                (current.commandName == "ObjectSelectorUpdated" || current.commandName == "ObjectSelectorClosed") &&
                EditorGUIUtility.GetObjectPickerControlID() == control)
            {
                sprite = EditorGUIUtility.GetObjectPickerObject() as Sprite;
                SetSprite(item, sprite);
                current.Use();
            }
            if (GUI.enabled && rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
                {
                    Sprite dropped = null;
                    foreach (UnityEngine.Object reference in DragAndDrop.objectReferences)
                    {
                        dropped = reference as Sprite;
                        if (dropped == null && reference is Texture2D)
                            dropped = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(reference));
                        if (dropped != null) break;
                    }
                    DragAndDrop.visualMode = dropped == null ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
                    if (dropped != null && current.type == EventType.DragPerform)
                    { DragAndDrop.AcceptDrag(); SetSprite(item, dropped); }
                    current.Use();
                }
                else if (current.type == EventType.ContextClick)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear Sprite"), false, () =>
                    {
                        if (EditorApplication.isPlaying) return;
                        serializedObject.Update();
                        SetSprite(serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index), null);
                        serializedObject.ApplyModifiedProperties();
                        DialogueGraphEditor.Invalidate();
                    });
                    menu.ShowAsContext();
                    current.Use();
                }
            }
            GUI.BeginGroup(rect);
            GUI.Box(new Rect(0, 0, rect.width, rect.height), GUIContent.none, EditorStyles.objectFieldThumb);
            if (sprite != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
                if (preview != null)
                    GUI.DrawTexture(new Rect(1, 1, rect.width - 2, rect.height - 2), preview, ScaleMode.ScaleToFit, true);
                if (AssetPreview.IsLoadingAssetPreview(sprite.GetInstanceID())) window.Repaint();
            }
            if (GUI.Button(new Rect(0, 0, rect.width, rect.height),
                new GUIContent(string.Empty, sprite != null ? sprite.name : "Select Sprite"), GUIStyle.none))
                EditorGUIUtility.ShowObjectPicker<Sprite>(sprite, false, string.Empty, control);
            GUI.EndGroup();
        }

        static void SetSprite(SerializedProperty item, Sprite sprite)
        {
            item.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            item.FindPropertyRelative("spriteGuid").stringValue = null;
            item.FindPropertyRelative("spriteFileId").longValue = 0;
            GUI.changed = true;
        }
    }
}
