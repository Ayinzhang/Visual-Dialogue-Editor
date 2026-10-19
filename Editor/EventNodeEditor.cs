namespace DialogueEditor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;

    [CustomNodeEditor(typeof(EventNode))]
    public class EventNodeEditor : DialogueNodeEditorBase
    {
        protected override string ListName => "eventList";
        protected override float RowHeight => 68;
        protected override void InitItem(int index) { ((EventNode)target).eventList[index] = new EventNode.FuncInfo(); }
        protected override void DrawItem(Rect rect, int index)
        {
            SerializedProperty item = serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index);
            GameObject obj = DrawTarget(new Rect(rect.x, rect.y + 2, rect.width * 0.4f, 18), item);
            string label = item.FindPropertyRelative("compName").stringValue + "." + item.FindPropertyRelative("funcName").stringValue;
            if (EditorGUI.DropdownButton(new Rect(rect.x + rect.width * 0.42f, rect.y + 2, rect.width * 0.58f, 18), new GUIContent(label), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                if (obj != null) foreach (Component component in obj.GetComponents<Component>())
                {
                    if (component == null) continue;
                    foreach (MethodInfo method in component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        if (method.IsSpecialName || method.IsGenericMethod || parameters.Length > 1 ||
                            parameters.Length == 1 && !Supported(parameters[0].ParameterType)) continue;
                        MethodInfo selected = method;
                        menu.AddItem(new GUIContent(component.GetType().Name + "/" + method.Name + (parameters.Length == 0 ? "()" : "(" + parameters[0].ParameterType.Name + ")")), false,
                            () => Select(index, selected));
                    }
                }
                menu.ShowAsContext();
            }
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + 24, rect.width * 0.4f, 18), item.FindPropertyRelative("portType"), GUIContent.none);
            string typeName = item.FindPropertyRelative("paraType").stringValue;
            if (!string.IsNullOrEmpty(typeName))
                DrawValue(new Rect(rect.x + rect.width * 0.42f, rect.y + 24, rect.width * 0.58f, 18), item.FindPropertyRelative("paraNum"), DialogueValidation.FindType(typeName));
            DrawError(rect, index);
        }

        internal static bool Supported(Type type)
        { return type.IsEnum || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(bool) || type == typeof(string); }

        GameObject DrawTarget(Rect rect, SerializedProperty owner)
        {
            SerializedProperty property = owner.FindPropertyRelative("obj");
            string id = owner.FindPropertyRelative("objGlobalId").stringValue, path = owner.FindPropertyRelative("objPath").stringValue;
            GameObject current = SceneObjectReferenceEditor.Find(property.objectReferenceValue as GameObject, id, path);
            if (!EditorApplication.isPlaying && current != null && property.objectReferenceValue == null) property.objectReferenceValue = current;
            EditorGUI.BeginChangeCheck();
            GameObject selected = (GameObject)EditorGUI.ObjectField(rect, new GUIContent(string.Empty, path), current, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = selected;
                owner.FindPropertyRelative("objGlobalId").stringValue = selected == null ? null : SceneObjectReferenceEditor.GetId(selected);
                owner.FindPropertyRelative("objPath").stringValue = selected == null ? null : SceneObjectReferenceEditor.GetPath(selected);
            }
            return selected;
        }

        void Select(int index, MethodInfo method)
        {
            if (EditorApplication.isPlaying) return;
            serializedObject.Update();
            SerializedProperty item = serializedObject.FindProperty(ListName).GetArrayElementAtIndex(index);
            item.FindPropertyRelative("compName").stringValue = method.DeclaringType.FullName;
            item.FindPropertyRelative("declType").stringValue = method.DeclaringType.AssemblyQualifiedName;
            item.FindPropertyRelative("funcName").stringValue = method.Name;
            ParameterInfo[] parameters = method.GetParameters();
            item.FindPropertyRelative("paraType").stringValue = parameters.Length == 0 ? null : parameters[0].ParameterType.AssemblyQualifiedName;
            item.FindPropertyRelative("paraNum").stringValue = parameters.Length == 1 && parameters[0].ParameterType == typeof(string) ? string.Empty : "0";
            serializedObject.ApplyModifiedProperties(); DialogueGraphEditor.Invalidate(); window.Repaint();
        }
    }
}
