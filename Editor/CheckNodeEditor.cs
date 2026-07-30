namespace DialogueEditor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using XNode;
    using XNodeEditor;
    using static XNode.Node;

    [CustomNodeEditor(typeof(CheckNode))]
    public class CheckNodeEditor : NodeEditor
    {
        bool isCheckInfo; CheckNode node; NodePort before, after;

        class VariableData
        {
            public Component Component { get; }
            public FieldInfo Field { get; }

            public VariableData(Component component, FieldInfo field) { Component = component; Field = field; }
        }

        public override void OnCreate()
        {
            base.OnCreate();
            node = serializedObject.targetObject as CheckNode;
        }

        public override void OnBodyGUI()
        {
            if (!isCheckInfo)
            {
                isCheckInfo = true;
                node.obj = SceneObjectReferenceEditor.Find(node.obj, node.objGlobalId, node.objPath);
                node.RefreshInfo();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Before"), GUILayout.MinWidth(30));
            if (node.isMin) EditorGUILayout.LabelField(new GUIContent("After"), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
            EditorGUILayout.EndHorizontal();
            if (before == null || after == null) GetPorts();
            Rect rect = GUILayoutUtility.GetLastRect();
            float paddingLeft = NodeEditorWindow.current.graphEditor.GetPortStyle(before).padding.left;
            NodeEditorGUILayout.PortField(rect.position - new Vector2(16 + paddingLeft, 0), before);
            if (node.isMin)
            {
                rect.width += NodeEditorWindow.current.graphEditor.GetPortStyle(after).padding.right;
                NodeEditorGUILayout.PortField(rect.position + new Vector2(rect.width, 0), after);
                node.abstruct = EditorGUILayout.TextArea(node.abstruct, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Show more", EditorStyles.miniButton))
                {
                    node.isMin = false;
                    if (before == null || after == null) GetPorts();
                    after.ClearConnections();
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                node.obj = (GameObject)EditorGUILayout.ObjectField(node.obj, typeof(GameObject), true);

                if (EditorGUILayout.DropdownButton(new GUIContent($"{node.compName}.{node.varName}"), FocusType.Keyboard))
                {
                    if (node.obj == null) return;
                    GenericMenu menu = new GenericMenu();
                    foreach (Component component in node.obj.GetComponents<Component>())
                    {
                        FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                        foreach (FieldInfo field in fields)
                        {
                            TypeCode type = Type.GetTypeCode(field.FieldType);
                            if (field.FieldType.IsEnum || type == TypeCode.Int32 || type == TypeCode.Boolean || type == TypeCode.Single ||
                                type == TypeCode.Double || type == TypeCode.String)
                                menu.AddItem(new GUIContent($"{component.GetType().Name}/{field.Name}"), false, OnVariableSelected, new VariableData(component, field));
                        }
                    }
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();
                NodeEditorGUILayout.DynamicPortList("checkList", typeof(string), serializedObject, NodePort.IO.Output, ConnectionType.Override, TypeConstraint.Inherited, InitList);
                if (GUILayout.Button("Show less", EditorStyles.miniButton))
                {
                    node.isMin = true;
                    if (before == null || after == null) GetPorts();
                    after.ClearConnections();
                    foreach (NodePort port in target.Ports)
                        if (!port.IsConnected) continue;
                        else if (port.fieldName.Contains("checkList "))
                            for (int i = 0; i < port.ConnectionCount; i++) after.Connect(port.GetConnection(i));
                }
            }
        }

        //Init ReorderableList
        void InitList(ReorderableList list)
        {
            SerializedProperty arrayData = serializedObject.FindProperty("checkList");

            list.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Check List");
            };

            list.elementHeightCallback = (index) =>
            {
                return EditorGUIUtility.singleLineHeight;
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index), checkType = itemData.FindPropertyRelative("checkType"),
                checkNum = itemData.FindPropertyRelative("checkNum");

                float fieldHeight = EditorGUIUtility.singleLineHeight;
                float padding = 5f;

                Rect typeRect = new Rect(rect.x, rect.y, rect.width / 2 - padding, fieldHeight);
                checkType.enumValueIndex = (int)(CheckNode.CheckType)EditorGUI.EnumPopup(typeRect, (CheckNode.CheckType)checkType.enumValueIndex);

                Rect checkNumRect = new Rect(rect.x + rect.width / 2 + padding, rect.y, rect.width / 2 - padding, fieldHeight);
                if (!string.IsNullOrEmpty(node.varType))
                {
                    Type varType = Type.GetType(node.varType);
                    if (varType == null) return;
                    if (varType.IsEnum)
                    {
                        Enum enumValue = (Enum)Enum.ToObject(varType, int.Parse(checkNum.stringValue));
                        enumValue = EditorGUI.EnumPopup(checkNumRect, enumValue);
                        checkNum.stringValue = Convert.ToInt32(enumValue).ToString();
                    }
                    else switch (Type.GetTypeCode(varType))
                        {
                            case TypeCode.Int32:
                                int intValue = int.Parse(checkNum.stringValue);
                                intValue = EditorGUI.IntField(checkNumRect, intValue);
                                checkNum.stringValue = intValue.ToString();
                                break;

                            case TypeCode.Single:
                            case TypeCode.Double:
                                double doubleValue = double.Parse(checkNum.stringValue);
                                doubleValue = EditorGUI.DoubleField(checkNumRect, doubleValue);
                                checkNum.stringValue = doubleValue.ToString();
                                break;

                            case TypeCode.Boolean:
                                bool boolValue = checkNum.stringValue == "1";
                                boolValue = EditorGUI.Toggle(checkNumRect, boolValue);
                                checkNum.stringValue = boolValue ? "1" : "0";
                                break;

                            case TypeCode.String:
                                checkNum.stringValue = EditorGUI.TextField(checkNumRect, checkNum.stringValue);
                                break;
                        }
                }
                NodePort port = node.GetPort("checkList " + index);
                Vector2 portPosition = rect.position + new Vector2(rect.width + 6, EditorGUIUtility.singleLineHeight * 0.1f);
                NodeEditorGUILayout.PortField(portPosition, port);
            };

            list.onAddCallback = (ReorderableList list) =>
            {
                node.AddDynamicOutput(typeof(CheckNode), ConnectionType.Override, TypeConstraint.None, "checkList " + node.checkList.Count);

                serializedObject.Update();
                EditorUtility.SetDirty(node);
                arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                SerializedProperty varType = serializedObject.FindProperty("varType"),
                checkNum = arrayData.GetArrayElementAtIndex(arrayData.arraySize - 1).FindPropertyRelative("checkNum");
                checkNum.stringValue = Type.GetType(varType.stringValue) == typeof(string) ? "" : "0";
                serializedObject.ApplyModifiedProperties(); node.RefreshInfo();
            };
        }

        void GetPorts()
        {
            foreach (NodePort port in target.Ports)
            {
                if (port.fieldName == "before") before = port;
                else if (port.fieldName == "after") after = port;
            }
        }

        void OnVariableSelected(object userData)
        {
            VariableData data = (VariableData)userData;
            Component component = data.Component; FieldInfo field = data.Field;
            SerializedProperty objGlobalId = serializedObject.FindProperty("objGlobalId"), objPath = serializedObject.FindProperty("objPath"),
                compName = serializedObject.FindProperty("compName"), varName = serializedObject.FindProperty("varName"),
                varType = serializedObject.FindProperty("varType"), arrayData = serializedObject.FindProperty("checkList");

            objGlobalId.stringValue = SceneObjectReferenceEditor.GetId(node.obj);
            objPath.stringValue = SceneObjectReferenceEditor.GetPath(node.obj);

            compName.stringValue = component.GetType().Name;
            varName.stringValue = field.Name;
            if (varType.stringValue != field.FieldType.AssemblyQualifiedName)
            {
                varType.stringValue = field.FieldType.AssemblyQualifiedName;
                for (int i = 0; i < node.checkList.Count; i++)
                {
                    SerializedProperty checkNum = arrayData.GetArrayElementAtIndex(i).FindPropertyRelative("checkNum");
                    checkNum.stringValue = field.FieldType == typeof(string) ? "" : "0";
                }
            }

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update(); node.RefreshInfo();
        }
    }

}