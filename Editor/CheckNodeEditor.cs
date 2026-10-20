namespace DialogueEditor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [CustomNodeEditor(typeof(CheckNode))]
    public class CheckNodeEditor : DialogueNodeEditorBase
    {
        CheckNode Check => (CheckNode)Node;
        Type VarType => string.IsNullOrEmpty(Check.varType) ? null : Type.GetType(Check.varType);

        protected override float RowHeight => 24;

        protected override object NewItem() => new CheckNode.CheckInfo { checkNum = VarType == typeof(string) ? "" : "0" };

        protected override void DrawFields()
        {
            DrawTarget(EditorGUILayout.GetControlRect(), serializedObject.FindProperty("bindingKey"), Check.compName);
            Rect line = EditorGUILayout.GetControlRect();
            string label = string.IsNullOrEmpty(Check.varName) ? "Select field" : Check.compName.Substring(Check.compName.LastIndexOf('.') + 1) + "." + Check.varName;
            if (EditorGUI.DropdownButton(line, new GUIContent(label), FocusType.Keyboard)) FieldMenu().DropDown(line);
        }

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            Rect line = new Rect(rect.x, rect.y + 3, 90, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, item.FindPropertyRelative("checkType"), GUIContent.none);
            if (VarType != null) DrawValue(new Rect(line.xMax + 4, line.y, rect.width - 94, line.height), item.FindPropertyRelative("checkNum"), VarType);
        }

        GenericMenu FieldMenu()
        {
            var menu = new GenericMenu();
            GameObject obj = DialogueBindingEditor.Target(DialogueBindingEditor.SceneSystem(out _), Check.bindingKey);
            if (obj == null) { menu.AddDisabledItem(new GUIContent("Assign a target in an open scene")); return menu; }
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                string label = DialogueBindingEditor.ComponentName(component, components) + "/";
                foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!Supported(field.FieldType)) continue;
                    string compName = component.GetType().FullName;
                    menu.AddItem(new GUIContent(label + field.Name), false,
                        () => ChangeBinding("Select Field", component, Check.bindingKey, key =>
                    {
                        if (VarType != field.FieldType)
                            foreach (CheckNode.CheckInfo info in Check.checkList) info.checkNum = field.FieldType == typeof(string) ? "" : "0";
                        Check.bindingKey = key;
                        Check.compName = compName;
                        Check.varName = field.Name;
                        Check.varType = field.FieldType.AssemblyQualifiedName;
                    }));
                }
            }
            return menu;
        }
    }
}
