namespace DialogueEditor
{
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using XNode;
    using XNodeEditor;
    using static XNode.Node;
    using static XNode.NodePort;

    [CustomNodeEditor(typeof(OptionNode))]
    public class OpytionNodeEditor : NodeEditor
    {
        OptionNode node;
        NodePort before, after;

        public override void OnCreate()
        {
            base.OnCreate();
            node = serializedObject.targetObject as OptionNode;
        }

        public override void OnBodyGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Before"), new GUILayoutOption[] { GUILayout.MinWidth(30) });
            if (node.isMin) EditorGUILayout.LabelField(new GUIContent("After"), NodeEditorResources.OutputPort, new GUILayoutOption[] { GUILayout.MinWidth(30) });
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
                NodeEditorGUILayout.DynamicPortList("optionList", typeof(string), serializedObject,
                    IO.Output, ConnectionType.Override, TypeConstraint.Inherited, InitList);
                if (GUILayout.Button("Show less", EditorStyles.miniButton))
                {
                    node.isMin = true;
                    if (before == null || after == null) GetPorts();
                    after.ClearConnections();
                    foreach (NodePort port in target.Ports)
                        if (!port.IsConnected) continue;
                        else if (port.fieldName.Contains("optionList "))
                            for (int i = 0; i < port.ConnectionCount; i++) after.Connect(port.GetConnection(i));
                }
            }
        }

        //Init ReorderableList
        void InitList(ReorderableList list)
        {
            SerializedProperty arrayData = serializedObject.FindProperty("optionList");

            list.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "OptionList");
            };

            list.elementHeightCallback = (index) =>
            {
                return EditorGUIUtility.singleLineHeight * 1.6f;
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                float padding = 5f, fieldHeight = EditorGUIUtility.singleLineHeight;

                Rect textRect = new Rect(rect.x, rect.y, rect.width - padding, fieldHeight * 1.5f);
                itemData.stringValue = EditorGUI.TextArea(textRect, itemData.stringValue, EditorStyles.wordWrappedLabel);

                NodePort port = node.GetOutputPort("optionList " + index);
                if (port != null)
                {
                    Vector2 portPosition = rect.position + new Vector2(rect.width + 6, EditorGUIUtility.singleLineHeight * 0.3f);
                    NodeEditorGUILayout.PortField(portPosition, port);
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
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
}