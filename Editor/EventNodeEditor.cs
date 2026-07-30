namespace DialogueEditor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using XNode;
    using XNodeEditor;
    using static EventNode;
    using static XNode.Node;
    using static XNode.NodePort;

    [CustomNodeEditor(typeof(EventNode))]
    public class EventNodeEditor : NodeEditor
    {
        bool isCheckInfo; EventNode node; NodePort before, after;

        class MethodSelectionData
        {
            public int Index { get; }
            public MethodInfo Method { get; }

            public MethodSelectionData(int index, MethodInfo method) { Index = index; Method = method; }
        }

        public override void OnCreate()
        {
            base.OnCreate();
            node = serializedObject.targetObject as EventNode;
        }

        public override void OnBodyGUI()
        {
            if (!isCheckInfo)
            {
                isCheckInfo = true;
                for (int i = 0; i < node.eventList.Count; i++)
                {
                    FuncInfo info = node.eventList[i];
                    info.obj = SceneObjectReferenceEditor.Find(info.obj, info.objGlobalId, info.objPath);
                    info.RefreshInfo();
                }
            }
            if (node.isMin)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Before"), GUILayout.MinWidth(30));
                EditorGUILayout.LabelField(new GUIContent("After"), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
                EditorGUILayout.EndHorizontal();
                if (before == null || after == null) GetPorts();
                Rect rect = GUILayoutUtility.GetLastRect();
                float paddingLeft = NodeEditorWindow.current.graphEditor.GetPortStyle(before).padding.left;
                NodeEditorGUILayout.PortField(rect.position - new Vector2(16 + paddingLeft, 0), before);
                rect.width += NodeEditorWindow.current.graphEditor.GetPortStyle(after).padding.right;
                NodeEditorGUILayout.PortField(rect.position + new Vector2(rect.width, 0), after);
                node.abstruct = EditorGUILayout.TextArea(node.abstruct, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Show more", EditorStyles.miniButton))
                {
                    node.isMin = false;
                    if (before == null || after == null) GetPorts();
                    before.ClearConnections(); after.ClearConnections();
                }
            }
            else
            {
                NodeEditorGUILayout.DynamicPortList("eventList", typeof(FuncInfo), serializedObject,
                    IO.Output, ConnectionType.Override, TypeConstraint.Inherited, InitList);
                if (GUILayout.Button("Show less", EditorStyles.miniButton))
                {
                    node.isMin = true;
                    if (before == null || after == null) GetPorts();
                    before.ClearConnections(); after.ClearConnections();
                    foreach (NodePort port in target.Ports)
                    {
                        if (!port.IsConnected) continue;
                        else if (port.fieldName.Contains("inList "))
                            for (int i = 0; i < port.ConnectionCount; i++) before.Connect(port.GetConnection(i));
                        else if (port.fieldName.Contains("eventList "))
                            for (int i = 0; i < port.ConnectionCount; i++) after.Connect(port.GetConnection(i));
                    }
                }
            }
        }

        void InitList(ReorderableList list)
        {
            int reorderableListIndex = -1;
            SerializedProperty arrayData = serializedObject.FindProperty("eventList");

            list.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "EventList");
            };

            list.elementHeightCallback = (index) =>
            {
                return EditorGUIUtility.singleLineHeight * 2f;
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index), objGlobalId = itemData.FindPropertyRelative("objGlobalId"),
                portType = itemData.FindPropertyRelative("portType"), objPath = itemData.FindPropertyRelative("objPath"), compName = itemData.FindPropertyRelative("compName"),
                funcName = itemData.FindPropertyRelative("funcName"), declType = itemData.FindPropertyRelative("declType"),
                paraType = itemData.FindPropertyRelative("paraType"), paraNum = itemData.FindPropertyRelative("paraNum");

                float padding = 5f, fieldHeight = EditorGUIUtility.singleLineHeight;
                Rect objRect = new Rect(rect.x, rect.y, rect.width / 3 - padding / 2, fieldHeight);
                node.eventList[index].obj = (GameObject)EditorGUI.ObjectField(objRect, node.eventList[index].obj, typeof(GameObject));
                objRect = new Rect(rect.x + rect.width / 3 + padding, rect.y, 2 * rect.width / 3 - padding / 2, fieldHeight);
                if (EditorGUI.DropdownButton(objRect, new GUIContent($"{node.eventList[index].compName}.{node.eventList[index].funcName}"), FocusType.Keyboard))
                {
                    if (node.eventList[index].obj == null) return;
                    objGlobalId.stringValue = SceneObjectReferenceEditor.GetId(node.eventList[index].obj);
                    objPath.stringValue = SceneObjectReferenceEditor.GetPath(node.eventList[index].obj);

                    GenericMenu menu = new GenericMenu();
                    foreach (Component component in node.eventList[index].obj.GetComponents<Component>())
                    {
                        MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                        foreach (MethodInfo method in methods)
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            Type type = parameters.Length == 1 ? parameters[0].ParameterType : null;
                            if (!method.IsSpecialName && parameters.Length <= 1 && (type == null || type.IsEnum || type == typeof(int) ||
                                type == typeof(float) || type == typeof(double) || type == typeof(bool) || type == typeof(string)))
                                menu.AddItem(new GUIContent($"{component.GetType().Name}/{method.Name}"), false, OnMethodSelected, new MethodSelectionData(index, method));
                        }
                    }

                    menu.ShowAsContext();
                }

                objRect = new Rect(rect.x, rect.y + fieldHeight, rect.width / 3 - padding / 2, fieldHeight);
                portType.enumValueIndex = (int)(PortType)EditorGUI.EnumPopup(objRect, (PortType)portType.enumValueIndex);
                if (!string.IsNullOrEmpty(paraType.stringValue))
                {
                    Type paramType = Type.GetType(paraType.stringValue);
                    objRect = new Rect(rect.x + rect.width / 3 + padding, rect.y + fieldHeight, 2 * rect.width / 3 - padding / 2, fieldHeight);

                    if (paramType != null && paramType.IsEnum)
                    {
                        Enum enumValue = (Enum)Enum.ToObject(paramType, int.Parse(paraNum.stringValue));
                        enumValue = EditorGUI.EnumPopup(objRect, enumValue);
                        paraNum.stringValue = Convert.ToInt32(enumValue).ToString();
                    }
                    else switch (Type.GetTypeCode(paramType))
                        {
                            case TypeCode.Int32:
                                int intValue = int.Parse(paraNum.stringValue);
                                intValue = EditorGUI.IntField(objRect, intValue);
                                paraNum.stringValue = intValue.ToString();
                                break;

                            case TypeCode.Single:
                            case TypeCode.Double:
                                double doubleValue = double.Parse(paraNum.stringValue);
                                doubleValue = EditorGUI.DoubleField(objRect, doubleValue);
                                paraNum.stringValue = doubleValue.ToString();
                                break;

                            case TypeCode.String:
                                paraNum.stringValue = EditorGUI.TextField(objRect, node.eventList[index].paraNum);
                                break;

                            case TypeCode.Boolean:
                                bool boolValue = int.Parse(paraNum.stringValue) == 1;
                                boolValue = EditorGUI.Toggle(objRect, boolValue);
                                paraNum.stringValue = boolValue ? "1" : "0";
                                break;
                        }
                }

                NodePort port = node.GetPort("inList " + index);
                if (port != null && (portType.enumValueIndex & (int)PortType.Input) != 0)
                {
                    Vector2 portPosition = rect.position + new Vector2(-35, EditorGUIUtility.singleLineHeight * 0.6f);
                    NodeEditorGUILayout.PortField(portPosition, port);
                }
                port = node.GetPort("eventList " + index);
                if (port != null && (portType.enumValueIndex & (int)PortType.Output) != 0)
                {
                    Vector2 portPosition = rect.position + new Vector2(rect.width + 6, EditorGUIUtility.singleLineHeight * 0.6f);
                    NodeEditorGUILayout.PortField(portPosition, port);
                }
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            };

            list.onSelectCallback = (ReorderableList list) =>
            {
                reorderableListIndex = list.index;
            };

            list.onReorderCallback = (ReorderableList list) =>
            {
                serializedObject.Update();
                int step = list.index > reorderableListIndex ? 1 : -1;
                for (int i = reorderableListIndex; i != list.index; i += step)
                {
                    NodePort port = node.GetPort("eventList " + i), nextPort = node.GetPort("eventList " + (i + step)); port.SwapConnections(nextPort);
                    port = node.GetPort("inList " + i); nextPort = node.GetPort("inList " + (i + step)); port.SwapConnections(nextPort);

                    bool hasRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(port, out Rect rect);
                    bool hasNewRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(nextPort, out Rect newRect);
                    NodeEditorWindow.current.portConnectionPoints[port] = hasNewRect ? newRect : rect;
                    NodeEditorWindow.current.portConnectionPoints[nextPort] = hasRect ? rect : newRect;
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                arrayData.MoveArrayElement(reorderableListIndex, list.index);

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                NodeEditorWindow.current.Repaint();
                EditorApplication.delayCall += NodeEditorWindow.current.Repaint;
            };

            list.onAddCallback = (ReorderableList list) =>
            {
                node.AddDynamicInput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "inList " + node.eventList.Count);
                node.AddDynamicOutput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "eventList " + node.eventList.Count);

                serializedObject.Update();
                EditorUtility.SetDirty(node);
                arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                serializedObject.ApplyModifiedProperties();
            };

            list.onRemoveCallback = (ReorderableList list) =>
            {
                int index = list.index;
                NodePort[] ports = new NodePort[node.eventList.Count * 2];
                for (int i = 0; i < node.eventList.Count; i++)
                {
                    ports[2 * i] = node.GetPort("inList " + i);
                    ports[2 * i + 1] = node.GetPort("eventList " + i);
                }

                ports[2 * index].ClearConnections();
                ports[2 * index + 1].ClearConnections();
                for (int i = 2 * index + 2; i < ports.Length; i++)
                {
                    while (ports[i].ConnectionCount > 0)
                    {
                        NodePort other = ports[i].GetConnection(0);
                        ports[i].Disconnect(other);
                        ports[i - 2].Connect(other);
                    }
                }
                node.RemoveDynamicPort(ports[ports.Length - 1].fieldName);
                node.RemoveDynamicPort(ports[ports.Length - 2].fieldName);
                serializedObject.Update();
                EditorUtility.SetDirty(node);

                if (arrayData.propertyType != SerializedPropertyType.String)
                {
                    arrayData.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                }
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

        void OnMethodSelected(object userData)
        {
            MethodSelectionData data = (MethodSelectionData)userData;
            int index = data.Index;
            MethodInfo method = data.Method;

            SerializedProperty arrayData = serializedObject.FindProperty("eventList"),
                itemData = arrayData.GetArrayElementAtIndex(index), compName = itemData.FindPropertyRelative("compName"),
                funcName = itemData.FindPropertyRelative("funcName"), declType = itemData.FindPropertyRelative("declType"),
                paraType = itemData.FindPropertyRelative("paraType"), paraNum = itemData.FindPropertyRelative("paraNum");

            compName.stringValue = method.DeclaringType.Name;
            funcName.stringValue = method.Name;
            declType.stringValue = method.DeclaringType.AssemblyQualifiedName;

            ParameterInfo[] parameters = method.GetParameters();
            paraType.stringValue = parameters.Length == 1 ? parameters[0].ParameterType.AssemblyQualifiedName : null;
            paraNum.stringValue = parameters.Length == 1 && parameters[0].ParameterType == typeof(string) ? "" : "0";

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}