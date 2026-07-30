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
    public class OptionNodeEditor : NodeEditor
    {
        OptionNode node; DialogueGraph dialogGraph;
        NodePort before, after;

        public override void OnCreate()
        {
            base.OnCreate();
            node = serializedObject.targetObject as OptionNode;
        }

        public override void OnBodyGUI()
        {
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
            SerializedProperty arrayData = serializedObject.FindProperty("optionList"), localizedData = serializedObject.FindProperty("localizedOptions");
            int reorderableListIndex = -1;
            var defaultSelect = list.onSelectCallback;
            var defaultReorder = list.onReorderCallback;
            var defaultAdd = list.onAddCallback;
            var defaultRemove = list.onRemoveCallback;
            if (dialogGraph == null) dialogGraph = window.graph as DialogueGraph;

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
                if (EditorApplication.isPlaying && node.isActive)
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0.2f, 0.8f, 0.2f, 0.3f));
                float padding = 5f, fieldHeight = EditorGUIUtility.singleLineHeight;

                Rect textRect = new Rect(rect.x, rect.y, rect.width - padding, fieldHeight * 1.5f);
                int languageIndex = dialogGraph.LanguageIndex;
                SerializedProperty option = arrayData.GetArrayElementAtIndex(index);
                if (languageIndex > 0) option = localizedData.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("translations").GetArrayElementAtIndex(languageIndex - 1);
                option.stringValue = EditorGUI.TextArea(textRect, option.stringValue, EditorStyles.wordWrappedLabel);

                NodePort port = node.GetOutputPort("optionList " + index);
                if (port != null)
                {
                    Vector2 portPosition = rect.position + new Vector2(rect.width + 6, EditorGUIUtility.singleLineHeight * 0.3f);
                    NodeEditorGUILayout.PortField(portPosition, port);
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            };

            list.onSelectCallback = (ReorderableList reorderableList) =>
            {
                reorderableListIndex = reorderableList.index;
                defaultSelect?.Invoke(reorderableList);
            };

            list.onReorderCallback = (ReorderableList reorderableList) =>
            {
                Undo.RecordObject(node, "Reorder localized options");
                defaultReorder?.Invoke(reorderableList);
                serializedObject.Update();
                localizedData.MoveArrayElement(reorderableListIndex, reorderableList.index);
                serializedObject.ApplyModifiedProperties();
            };

            list.onAddCallback = (ReorderableList reorderableList) =>
            {
                defaultAdd?.Invoke(reorderableList);
                node.EnsureLocalizationData(dialogGraph.Languages.Count);
                EditorUtility.SetDirty(node);
            };

            list.onRemoveCallback = (ReorderableList reorderableList) =>
            {
                int removedIndex = reorderableList.index;
                Undo.RecordObject(node, "Remove localized option");
                defaultRemove?.Invoke(reorderableList);
                serializedObject.Update();
                localizedData.DeleteArrayElementAtIndex(removedIndex);
                serializedObject.ApplyModifiedProperties();
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