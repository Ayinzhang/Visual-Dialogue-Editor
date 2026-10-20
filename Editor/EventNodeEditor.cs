namespace DialogueEditor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [CustomNodeEditor(typeof(EventNode))]
    public class EventNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 46;

        protected override object NewItem() => new EventNode.FuncInfo { portType = PortType.All };

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            EventNode.FuncInfo info = ((EventNode)Node).eventList[index];
            Rect line = new Rect(rect.x, rect.y + 4, rect.width - 84, EditorGUIUtility.singleLineHeight);
            DrawTarget(line, item.FindPropertyRelative("bindingKey"), info.compName);
            EditorGUI.PropertyField(new Rect(line.xMax + 4, line.y, 80, line.height), item.FindPropertyRelative("portType"), GUIContent.none);

            line.y += line.height + 2;
            Type parameter = string.IsNullOrEmpty(info.paraType) ? null : Type.GetType(info.paraType);
            if (parameter != null) line.width = rect.width / 2;
            string label = string.IsNullOrEmpty(info.funcName) ? "Select method" : info.compName.Substring(info.compName.LastIndexOf('.') + 1) + "." + info.funcName;
            if (EditorGUI.DropdownButton(line, new GUIContent(label), FocusType.Keyboard)) MethodMenu(info).DropDown(line);
            if (parameter != null) DrawValue(new Rect(line.xMax + 4, line.y, rect.width - line.width - 4, line.height), item.FindPropertyRelative("paraNum"), parameter);
        }

        GenericMenu MethodMenu(EventNode.FuncInfo info)
        {
            var menu = new GenericMenu();
            GameObject obj = DialogueBindingEditor.Target(DialogueBindingEditor.SceneSystem(out _), info.bindingKey);
            if (obj == null) { menu.AddDisabledItem(new GUIContent("Assign a target in an open scene")); return menu; }
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                string label = DialogueBindingEditor.ComponentName(component, components) + "/";
                foreach (MethodInfo method in component.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.IsSpecialName || method.ContainsGenericParameters || method.DeclaringType.Namespace?.StartsWith("UnityEngine") != false) continue;
                    if (parameters.Length > 1 || parameters.Length == 1 && !Supported(parameters[0].ParameterType)) continue;
                    Type parameter = parameters.Length == 1 ? parameters[0].ParameterType : null;
                    string compName = component.GetType().FullName;
                    menu.AddItem(new GUIContent(label + method.Name + "(" + parameter?.Name + ")"), false,
                        () => ChangeBinding("Select Method", component, info.bindingKey, key =>
                    {
                        info.bindingKey = key;
                        info.compName = compName;
                        info.funcName = method.Name;
                        info.paraType = parameter == null ? "" : parameter.AssemblyQualifiedName;
                        info.paraNum = parameter == typeof(string) ? "" : "0";
                    }));
                }
            }
            return menu;
        }
    }
}
