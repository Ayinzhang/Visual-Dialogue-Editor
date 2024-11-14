using System;
using System.Collections.Generic;
using System.Linq;
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
    List<Component> components = new List<Component>();

    class MethodSelectionData
    {
        public int Index { get; }
        public Component Component { get; }
        public MethodInfo Method { get; }
        
        public MethodSelectionData(int index, Component component, MethodInfo method)
        {
            Index = index;
            Component = component;
            Method = method;
        }
    }

    public override void OnCreate()
    {
        base.OnCreate();
        node = serializedObject.targetObject as EventNode;
    }

    public override void OnBodyGUI()
    {
        if(!isCheckInfo) { isCheckInfo = true; for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo(); }
        if (node.isMin)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Before"), new GUILayoutOption[] { GUILayout.MinWidth(30) });
            EditorGUILayout.LabelField(new GUIContent("After"), NodeEditorResources.OutputPort, new GUILayoutOption[] { GUILayout.MinWidth(30) });
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
            SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index), objInstanceID = itemData.FindPropertyRelative("objInstanceID"),
            portType = itemData.FindPropertyRelative("portType"), objPath = itemData.FindPropertyRelative("objPath"), compName = itemData.FindPropertyRelative("compName"),
            funcName = itemData.FindPropertyRelative("funcName"), declType = itemData.FindPropertyRelative("declType"),
            paraType = itemData.FindPropertyRelative("paraType"), paraNum = itemData.FindPropertyRelative("paraNum");

            MethodInfo methodInfo = node.eventList[index].GetMethodInfo();
            
            float padding = 5f, fieldHeight = EditorGUIUtility.singleLineHeight;
            Rect objRect = new Rect(rect.x, rect.y, rect.width / 3 - padding / 2, fieldHeight);
            node.eventList[index].obj = (GameObject)EditorGUI.ObjectField(objRect, node.eventList[index].obj, typeof(GameObject));
            objRect = new Rect(rect.x + rect.width / 3 + padding, rect.y, 2 * rect.width / 3 - padding / 2, fieldHeight);
            if (EditorGUI.DropdownButton(objRect, new GUIContent($"{node.eventList[index].compName}.{node.eventList[index].funcName}"), FocusType.Keyboard))
            {
                if (node.eventList[index].obj == null) return;
                else
                {
                    objInstanceID.intValue = node.eventList[index].obj.GetInstanceID();

                    Transform trans = node.eventList[index].obj.transform;
                    objPath.stringValue = trans.name;
                    while (trans.parent != null)
                    {
                        trans = trans.parent;
                        objPath.stringValue = trans.name + "/" + objPath.stringValue;
                    }
                    objPath.stringValue = node.eventList[index].obj.scene.name + "/" + objPath.stringValue;
                }

                GenericMenu menu = new GenericMenu();
                components.Clear(); Component[] comps = node.eventList[index].obj.GetComponents<Component>();
                foreach (Component comp in comps) components.Add(comp);

                foreach (Component component in components)
                {
                    MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                    foreach (MethodInfo method in methods)
                        if (!method.IsSpecialName && method.GetParameters().Length <= 1)
                            menu.AddItem(new GUIContent($"{component.GetType().Name}/{method.Name}"), false, OnMethodSelected, new MethodSelectionData(index, component, method));
                }

                menu.ShowAsContext();
            }

            if (!string.IsNullOrEmpty(paraType.stringValue))
            {
                EditorGUILayout.BeginHorizontal();
                objRect = new Rect(rect.x, rect.y + fieldHeight, rect.width / 3 - padding / 2, fieldHeight);
                portType.enumValueIndex = (int)(PortType)EditorGUI.EnumPopup(objRect, (PortType)portType.enumValueIndex);
                Type paramType = Type.GetType(paraType.stringValue);
                objRect = new Rect(rect.x + rect.width / 3 + padding, rect.y + fieldHeight, 2 * rect.width / 3 - padding / 2, fieldHeight);

                if (paramType.IsEnum)
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
                EditorGUILayout.EndHorizontal();

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
            bool hasRect = false, hasNewRect = false;
            Rect rect = Rect.zero, newRect = Rect.zero;

            // Move up
            if (list.index > reorderableListIndex)
            {
                for (int i = reorderableListIndex; i < list.index; ++i)
                {
                    NodePort port = node.GetPort("eventList " + i), nextPort = node.GetPort("eventList " + (i + 1)); port.SwapConnections(nextPort);
                    port = node.GetPort("inList " + i); nextPort = node.GetPort("inList " + (i + 1)); port.SwapConnections(nextPort);

                    // Swap cached positions to mitigate twitching
                    hasRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(port, out rect);
                    hasNewRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(nextPort, out newRect);
                    NodeEditorWindow.current.portConnectionPoints[port] = hasNewRect ? newRect : rect;
                    NodeEditorWindow.current.portConnectionPoints[nextPort] = hasRect ? rect : newRect;
                }
            }

            // Move down
            else
            {
                for (int i = reorderableListIndex; i > list.index; --i)
                {
                    NodePort port = node.GetPort("eventList " + i), nextPort = node.GetPort("eventList " + (i - 1)); port.SwapConnections(nextPort);
                    port = node.GetPort("inList " + i); nextPort = node.GetPort("inList " + (i - 1)); port.SwapConnections(nextPort);

                    hasRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(port, out rect);
                    hasNewRect = NodeEditorWindow.current.portConnectionPoints.TryGetValue(nextPort, out newRect);
                    NodeEditorWindow.current.portConnectionPoints[port] = hasNewRect ? newRect : rect;
                    NodeEditorWindow.current.portConnectionPoints[nextPort] = hasRect ? rect : newRect;
                }
            }
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            arrayData.MoveArrayElement(reorderableListIndex, list.index);

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update(); for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo();
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
            serializedObject.ApplyModifiedProperties(); for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo();;
        };

        list.onRemoveCallback = (ReorderableList list) =>
        {
            var indexedPorts = node.DynamicPorts.Select(x =>
            {
                string[] split = x.fieldName.Split(' ');
                if (split != null && split.Length == 2 && (split[0] == "inList" || split[0] == "eventList"))
                {
                    int i = -1;
                    if (int.TryParse(split[1], out i))
                    {
                        return new { index = i, port = x };
                    }
                }
                return new { index = -1, port = (NodePort)null };
            }).Where(x => x.port != null);
            var dynamicPorts = indexedPorts.OrderBy(x => x.index).Select(x => x.port).ToList();

            int index = list.index;

            dynamicPorts[2 * index].ClearConnections();
            dynamicPorts[2 * index + 1].ClearConnections();
            for (int k = 2 * index + 2; k < dynamicPorts.Count(); k++)
            {
                for (int j = 0; j < dynamicPorts[k].ConnectionCount; j++)
                {
                    NodePort other = dynamicPorts[k].GetConnection(j);
                    dynamicPorts[k].Disconnect(other);
                    dynamicPorts[k - 2].Connect(other);
                }
            }
            node.RemoveDynamicPort(dynamicPorts[dynamicPorts.Count() - 1].fieldName);
            node.RemoveDynamicPort(dynamicPorts[dynamicPorts.Count() - 2].fieldName);
            serializedObject.Update(); for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo();;
            EditorUtility.SetDirty(node);

            if (arrayData.propertyType != SerializedPropertyType.String)
            {
                arrayData.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update(); for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo();;
            }
        };
    }

    void GetPorts()
    {
        foreach (NodePort port in target.Ports)
        {
            if (port.fieldName.Equals("before")) before = port;
            else if (port.fieldName.Equals("after")) after = port;
        }
    }

    void OnMethodSelected(object userData)
    {
        MethodSelectionData data = (MethodSelectionData)userData;
        int index = data.Index;
        Component component = data.Component;
        MethodInfo method = data.Method;

        SerializedProperty arrayData = serializedObject.FindProperty("eventList"), 
            itemData = arrayData.GetArrayElementAtIndex(index), compName = itemData.FindPropertyRelative("compName"),
            funcName = itemData.FindPropertyRelative("funcName"), declType = itemData.FindPropertyRelative("declType"),
            paraType = itemData.FindPropertyRelative("paraType"), paraNum = itemData.FindPropertyRelative("paraNum");

        compName.stringValue = component.GetType().Name;
        funcName.stringValue = method.Name;
        declType.stringValue = method.DeclaringType.AssemblyQualifiedName;

        if (Type.GetTypeCode(Type.GetType(declType.stringValue)) == TypeCode.String) paraNum.stringValue = "";
        else paraNum.stringValue = "0";

        //If method has one parameter, save the type of the parameter
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 1) paraType.stringValue = parameters[0].ParameterType.AssemblyQualifiedName;
        else paraType.stringValue = null;

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update(); for (int i = 0; i < node.eventList.Count; i++) node.eventList[i].RefreshInfo();
    }
}