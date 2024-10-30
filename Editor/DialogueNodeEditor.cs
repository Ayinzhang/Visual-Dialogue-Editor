using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using XNode;
using XNodeEditor;
using static DialogueNode;
using static XNode.Node;
using static XNode.NodePort;

[CustomNodeEditor(typeof(DialogueNode))]
public class DialogueNodeEditor : NodeEditor
{
    DialogueNode node; DialogueGraph dialogGraph;
    NodePort before, after;

    public override void OnCreate()
    {
        base.OnCreate();
        node = serializedObject.targetObject as DialogueNode;
    }

    public override void OnBodyGUI()
    {
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
            NodeEditorGUILayout.DynamicPortList("dialogueList", typeof(DialogueInfo), serializedObject,
                IO.Output, ConnectionType.Override, TypeConstraint.Inherited, InitList);
            if (GUILayout.Button("Show less", EditorStyles.miniButton))
            {
                node.isMin = true;
                if (before == null || after == null) GetPorts();
                before.ClearConnections(); after.ClearConnections();
                foreach (NodePort port in target.Ports)
                    if (!port.IsConnected) continue;
                    else if (port.fieldName.Contains("inList "))
                        for (int i = 0; i < port.ConnectionCount; i++) before.Connect(port.GetConnection(i));
                    else if (port.fieldName.Contains("dialogueList ")) 
                        for (int i = 0; i < port.ConnectionCount; i++) after.Connect(port.GetConnection(i));
            }
        }
    }

    //Init ReorderableList
    void InitList(ReorderableList list)
    {
        int reorderableListIndex = -1;
        SerializedProperty arrayData = serializedObject.FindProperty("dialogueList");
        if (dialogGraph == null) dialogGraph = window.graph as DialogueGraph;

        list.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "DialogueList");
        };

        list.elementHeightCallback = (index) =>
        {
            return EditorGUIUtility.singleLineHeight * 3;
        };

        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index), sprite = itemData.FindPropertyRelative("sprite"),
            person = itemData.FindPropertyRelative("person"), type = itemData.FindPropertyRelative("type"),
            context = itemData.FindPropertyRelative("context");

            float padding = 5f, labelWidth = 50f, enumWidth = 60f, imageX = rect.x + padding;
            float imageWidth = 50f, y = rect.y + padding, fieldHeight = EditorGUIUtility.singleLineHeight;

            Rect imageRect = new Rect(imageX, y, imageWidth, imageWidth);
            sprite.objectReferenceValue = EditorGUI.ObjectField(imageRect, sprite.objectReferenceValue, typeof(Sprite), false);

            float contentStartX = imageX + imageWidth + padding;

            Rect personLabelRect = new Rect(contentStartX, y, labelWidth, fieldHeight);
            EditorGUI.LabelField(personLabelRect, "Person");

            Rect personFieldRect = new Rect(contentStartX + labelWidth, y, enumWidth, fieldHeight);
            person.intValue = EditorGUI.Popup(personFieldRect, person.intValue, dialogGraph.names.ToArray());

            Rect typeLabelRect = new Rect(contentStartX + labelWidth + enumWidth + padding, y, labelWidth, fieldHeight);
            EditorGUI.LabelField(typeLabelRect, "Type");

            Rect typeFieldRect = new Rect(contentStartX + labelWidth + enumWidth + padding + labelWidth, y, enumWidth, fieldHeight);
            type.enumValueIndex = (int)(PortType)EditorGUI.EnumPopup(typeFieldRect, (PortType)type.enumValueIndex);

            y += fieldHeight + padding;
            Rect contextLabelRect = new Rect(contentStartX, y, labelWidth, fieldHeight);
            EditorGUI.LabelField(contextLabelRect, "Context");

            Rect contextFieldRect = new Rect(contentStartX + labelWidth, y, rect.width - labelWidth - 2 * padding - imageWidth, 1.5f * fieldHeight);
            context.stringValue = EditorGUI.TextArea(contextFieldRect, context.stringValue, EditorStyles.wordWrappedLabel);

            NodePort port = node.GetPort("inList " + index);
            if (port != null && (type.enumValueIndex & (int)DialogueNode.PortType.Input) != 0)
            {
                Vector2 portPosition = rect.position + new Vector2(-35, EditorGUIUtility.singleLineHeight * 1.2f);
                NodeEditorGUILayout.PortField(portPosition, port);
            }
            port = node.GetPort("dialogueList " + index);

            if (port != null && (type.enumValueIndex & (int)DialogueNode.PortType.Output) != 0)
            {
                Vector2 portPosition = rect.position + new Vector2(rect.width + 6, EditorGUIUtility.singleLineHeight * 1.2f);
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
            bool hasRect = false, hasNewRect = false;
            Rect rect = Rect.zero, newRect = Rect.zero;

            // Move up
            if (list.index > reorderableListIndex)
            {
                for (int i = reorderableListIndex; i < list.index; ++i)
                {
                    NodePort port = node.GetPort("dialogueList " + i), nextPort = node.GetPort("dialogueList " + (i + 1)); port.SwapConnections(nextPort);
                    port = node.GetPort("inList " + i); nextPort = node.GetPort("inList " + (i + 1)); port.SwapConnections(nextPort);

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
                    NodePort port = node.GetPort("dialogueList " + i), nextPort = node.GetPort("dialogueList " + (i - 1)); port.SwapConnections(nextPort);
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
            serializedObject.Update();
            NodeEditorWindow.current.Repaint();
            EditorApplication.delayCall += NodeEditorWindow.current.Repaint;
        };

        list.onAddCallback = (ReorderableList list) =>
        {
            node.AddDynamicInput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "inList " + node.dialogueList.Count);
            node.AddDynamicOutput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "dialogueList " + node.dialogueList.Count);

            serializedObject.Update();
            EditorUtility.SetDirty(node);
            arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
            serializedObject.ApplyModifiedProperties();
        };

        list.onRemoveCallback = (ReorderableList list) =>
        {
            var indexedPorts = node.DynamicPorts.Select(x =>
            {
                string[] split = x.fieldName.Split(' ');

                if (split != null && split.Length == 2 && (split[0] == "inList" || split[0] == "dialogueList"))
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
            if (port.fieldName.Equals("before")) before = port;
            else if (port.fieldName.Equals("after")) after = port;
        }
    }
}
