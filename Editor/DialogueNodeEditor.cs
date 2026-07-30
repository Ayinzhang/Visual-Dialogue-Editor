namespace DialogueEditor
{
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
                if (EditorApplication.isPlaying && index == node.activeIndex)
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0.2f, 0.8f, 0.2f, 0.3f));
                SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index), sprite = itemData.FindPropertyRelative("sprite"),
                person = itemData.FindPropertyRelative("person"), type = itemData.FindPropertyRelative("type");

                float padding = 5f, labelWidth = 50f, enumWidth = 60f, imageX = rect.x + padding;
                float imageWidth = 50f, y = rect.y + padding, fieldHeight = EditorGUIUtility.singleLineHeight;

                Rect imageRect = new Rect(imageX, y, imageWidth, imageWidth);
                sprite.objectReferenceValue = EditorGUI.ObjectField(imageRect, sprite.objectReferenceValue, typeof(Sprite), false);

                float contentStartX = imageX + imageWidth + padding;

                Rect personLabelRect = new Rect(contentStartX, y, labelWidth, fieldHeight);
                EditorGUI.LabelField(personLabelRect, "Person");

                Rect personFieldRect = new Rect(contentStartX + labelWidth, y, enumWidth, fieldHeight);
                string[] speakerNames = dialogGraph.GetSpeakerNames();
                if (speakerNames.Length == 0) { EditorGUI.LabelField(personFieldRect, "No speakers"); person.intValue = 0; }
                else person.intValue = EditorGUI.Popup(personFieldRect, Mathf.Clamp(person.intValue, 0, speakerNames.Length - 1), speakerNames);

                Rect typeLabelRect = new Rect(contentStartX + labelWidth + enumWidth + padding, y, labelWidth, fieldHeight);
                EditorGUI.LabelField(typeLabelRect, "Type");

                Rect typeFieldRect = new Rect(contentStartX + labelWidth + enumWidth + padding + labelWidth, y, enumWidth, fieldHeight);
                type.enumValueIndex = (int)(PortType)EditorGUI.EnumPopup(typeFieldRect, (PortType)type.enumValueIndex);

                y += fieldHeight + padding;
                Rect contextLabelRect = new Rect(contentStartX, y, labelWidth, fieldHeight);
                EditorGUI.LabelField(contextLabelRect, "Context");

                Rect contextFieldRect = new Rect(contentStartX + labelWidth, y, rect.width - labelWidth - 2 * padding - imageWidth, 1.5f * fieldHeight);
                int languageIndex = dialogGraph.LanguageIndex;
                SerializedProperty context = itemData.FindPropertyRelative("context");
                if (languageIndex > 0) context = itemData.FindPropertyRelative("localizedContext").FindPropertyRelative("translations").GetArrayElementAtIndex(languageIndex - 1);
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
                int step = list.index > reorderableListIndex ? 1 : -1;
                for (int i = reorderableListIndex; i != list.index; i += step)
                {
                    NodePort port = node.GetPort("dialogueList " + i), nextPort = node.GetPort("dialogueList " + (i + step)); port.SwapConnections(nextPort);
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
                Undo.RecordObject(node, "Add dialogue");
                node.AddDynamicInput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "inList " + node.dialogueList.Count);
                node.AddDynamicOutput(typeof(Nothing), ConnectionType.Multiple, TypeConstraint.None, "dialogueList " + node.dialogueList.Count);
                node.dialogueList.Add(new DialogueInfo());
                node.dialogueList[node.dialogueList.Count - 1].EnsureLocalizationData(dialogGraph.Languages.Count);
                EditorUtility.SetDirty(node);
                serializedObject.Update();
            };

            list.onRemoveCallback = (ReorderableList list) =>
            {
                int index = list.index;
                NodePort[] ports = new NodePort[node.dialogueList.Count * 2];
                for (int i = 0; i < node.dialogueList.Count; i++)
                {
                    ports[2 * i] = node.GetPort("inList " + i);
                    ports[2 * i + 1] = node.GetPort("dialogueList " + i);
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
    }
}