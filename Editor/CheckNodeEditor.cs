namespace DialogueEditor
{
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;

    [CustomNodeEditor(typeof(CheckNode))]
    public class CheckNodeEditor : DialogueNodeEditorBase
    {
        protected override string ListName => "checkList";
        protected override float RowHeight => 28;
        protected override void InitItem(int index)
        {
            var node = (CheckNode)target;
            node.checkList[index] = new CheckNode.CheckInfo { checkNum = DialogueValidation.FindType(node.varType) == typeof(string) ? string.Empty : "0" };
        }

        protected override void DrawFields()
        {
            var node = (CheckNode)target;
            GameObject selected = DrawTarget(EditorGUILayout.GetControlRect(), label: "Target");
            if (EditorGUILayout.DropdownButton(new GUIContent(node.compName + "." + node.varName), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                if (selected != null) foreach (Component component in selected.GetComponents<Component>())
                {
                    if (component == null) continue;
                    foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (!Supported(field.FieldType)) continue;
                        FieldInfo chosen = field;
                        menu.AddItem(new GUIContent(component.GetType().Name + "/" + field.Name), false, () => Select(chosen));
                    }
                }
                menu.ShowAsContext();
            }
            foreach (DialogueIssue issue in DialogueGraphEditor.Issues(Graph).FindAll(issue => issue.node == target && issue.binding))
                EditorGUILayout.HelpBox(issue.message, issue.deferred ? MessageType.Info : MessageType.Error);
        }

        protected override void DrawItem(Rect rect, int index)
        {
            SerializedProperty item = serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + 2, rect.width * 0.45f, 18), item.FindPropertyRelative("checkType"), GUIContent.none);
            DrawValue(new Rect(rect.x + rect.width * 0.48f, rect.y + 2, rect.width * 0.52f, 18), item.FindPropertyRelative("checkNum"),
                DialogueValidation.FindType(((CheckNode)target).varType));
            DrawError(rect, index);
        }

        void Select(FieldInfo field)
        {
            if (EditorApplication.isPlaying) return;
            serializedObject.Update();
            string oldType = serializedObject.FindProperty("varType").stringValue;
            serializedObject.FindProperty("compName").stringValue = field.DeclaringType.FullName;
            serializedObject.FindProperty("varName").stringValue = field.Name;
            serializedObject.FindProperty("varType").stringValue = field.FieldType.AssemblyQualifiedName;
            if (oldType != field.FieldType.AssemblyQualifiedName)
            {
                SerializedProperty list = serializedObject.FindProperty(ListName);
                for (int i = 0; i < list.arraySize; i++)
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("checkNum").stringValue = field.FieldType == typeof(string) ? string.Empty : "0";
            }
            serializedObject.ApplyModifiedProperties(); DialogueGraphEditor.Invalidate(); window.Repaint();
        }
    }
}
