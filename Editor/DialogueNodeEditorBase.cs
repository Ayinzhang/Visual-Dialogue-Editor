namespace DialogueEditor
{
    using System;
    using System.Globalization;
    using System.Linq;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using XNode;
    using XNodeEditor;

    public abstract class DialogueNodeEditorBase : NodeEditor
    {
        protected DialogueGraph Graph => (DialogueGraph)target.graph;
        protected abstract string ListName { get; }
        protected abstract float RowHeight { get; }
        protected abstract void DrawItem(Rect rect, int index);
        protected abstract void InitItem(int index);
        Type itemType;

        public override void OnCreate()
        {
            base.OnCreate();
            itemType = target.GetType().GetField(ListName).FieldType.GetGenericArguments()[0];
            if (!EditorApplication.isPlaying) EnsureLocalizationData();
        }

        public override string GetHeaderTooltip()
        { return string.Join("\n", DialogueGraphEditor.Issues(Graph).Where(issue => issue.node == target).Select(issue => issue.message)); }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            if (EditorApplication.isPlaying)
            { menu.AddDisabledItem(new GUIContent("Read-only during playback")); return; }
            base.AddContextMenuItems(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set as Entry"), Graph.entryNode == target, () =>
            {
                Undo.RecordObject(Graph, "Set dialogue entry");
                Graph.SetEntry(target); EditorUtility.SetDirty(Graph); DialogueGraphEditor.Invalidate();
            });
        }

        public override void OnBodyGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                serializedObject.Update();
                bool folded = serializedObject.FindProperty("isMin").boolValue;
                if (folded)
                {
                    NodePort before = target.GetInputPort("before"), after = target.GetOutputPort("after");
                    NodeEditorGUILayout.PortPair(before, after);
                    EditorGUILayout.LabelField("Summary", EditorStyles.centeredGreyMiniLabel);
                    SerializedProperty summary = serializedObject.FindProperty("abstruct");
                    summary.stringValue = EditorGUILayout.TextArea(summary.stringValue,
                        new GUIStyle(EditorStyles.textArea) { alignment = TextAnchor.MiddleCenter }, GUILayout.MinHeight(40));
                    foreach (NodePort port in target.DynamicPorts)
                        if (NodeEditor.portPositions.TryGetValue(port.IsInput ? before : after, out Vector2 position))
                            NodeEditor.portPositions[port] = position;
                }
                else
                {
                    if (target is OptionNode || target is CheckNode)
                        NodeEditorGUILayout.PortField(new GUIContent("Before"), target.GetInputPort("before"));
                    DrawFields();
                    NodeEditorGUILayout.DynamicPortList(ListName, itemType,
                        serializedObject, NodePort.IO.Output, Node.ConnectionType.Override, Node.TypeConstraint.None, InitList);
                }
                if (GUILayout.Button(folded ? "Show more" : "Show less", EditorStyles.miniButton))
                    serializedObject.FindProperty("isMin").boolValue = !folded;
                DrawStateBorder(GUILayoutUtility.GetLastRect().yMax + GetBodyStyle().padding.bottom);
                if (serializedObject.ApplyModifiedProperties())
                { EditorUtility.SetDirty(target); DialogueGraphEditor.Invalidate(); }
            }
        }

        protected virtual void DrawFields() { }

        void EnsureLocalizationData()
        {
            int count = DialogueSettings.Instance.languages.Count;
            if (target is DialogueNode dialogue) dialogue.EnsureLocalizationData(count);
            else if (target is OptionNode options) options.EnsureLocalizationData(count);
        }

        void DrawStateBorder(float height)
        {
            Color color;
            if (DialogueGraphEditor.Issues(Graph).Any(issue => issue.node == target && !issue.deferred)) color = new Color(1f, 0.3f, 0.3f);
            else if (DialogueGraphEditor.Players(Graph).Any(system => system.graph.node == target))
                color = new Color(1f, 0.8f, 0.1f);
            else if (Graph.entryNode == target) color = new Color(0.3f, 0.9f, 0.4f);
            else return;
            DrawOutline(new Rect(0, 0, GetWidth(), height), color);
        }

        protected void DrawPorts(Rect rect, int index)
        {
            NodePort input = target.GetInputPort("inList " + index);
            bool accepts = target is DialogueNode dialogue && (dialogue.dialogueList[index].type & DialogueNode.PortType.Input) != 0 ||
                target is EventNode events && (events.eventList[index].portType & EventNode.PortType.Input) != 0;
            if (input != null && accepts) NodeEditorGUILayout.PortField(new Vector2(rect.x - 35, rect.y + 4), input);
            if (DialogueValidation.HasOutput(target, index))
                NodeEditorGUILayout.PortField(new Vector2(rect.xMax + 6, rect.y + 4), target.GetOutputPort(DialogueValidation.OutputName(target, index)));
        }

        protected void Highlight(Rect rect, int index)
        {
            if (DialogueGraphEditor.Players(Graph).Any(system => system.graph.node == target &&
                (target is OptionNode || system.graph.index == index)))
                DrawOutline(rect, new Color(1f, 0.8f, 0.1f));
        }

        protected void DrawError(Rect rect, int index)
        {
            string error = string.Join("\n", DialogueGraphEditor.Issues(Graph)
                .Where(issue => issue.node == target && !issue.deferred && (issue.item == index || issue.item == -1)).Select(issue => issue.message));
            if (string.IsNullOrEmpty(error)) return;
            DrawOutline(rect, new Color(1f, 0.3f, 0.3f));
            GUI.Label(new Rect(rect.xMax - 20, rect.y, 20, 18), new GUIContent("!", error), EditorStyles.boldLabel);
        }

        internal static void DrawOutline(Rect rect, Color color, float width = 2f)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        void InitList(ReorderableList list)
        {
            bool itemInputs = target is DialogueNode || target is EventNode;
            if (itemInputs)
                for (int i = 0; i < DialogueValidation.ItemCount(target); i++)
                    if (target.GetInputPort("inList " + i) == null)
                        target.AddDynamicInput(typeof(Node.Nothing), Node.ConnectionType.Multiple, Node.TypeConstraint.None, "inList " + i);
            list.elementHeightCallback = null;
            list.elementHeight = RowHeight + 8;
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                Rect content = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, RowHeight);
                DrawItem(content, index);
                Highlight(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), index);
                Rect ports = rect;
                ports.y += 4;
                DrawPorts(ports, index);
            };
            var add = list.onAddCallback;
            list.onAddCallback = value =>
            {
                serializedObject.ApplyModifiedProperties(); Node[] nodes = RecordNodes("Add dialogue item");
                int index = DialogueValidation.ItemCount(target);
                add(value);
                InitItem(index);
                if (itemInputs)
                    target.AddDynamicInput(typeof(Node.Nothing), Node.ConnectionType.Multiple, Node.TypeConstraint.None, "inList " + index);
                EnsureLocalizationData();
                Changed(nodes);
            };
            var remove = list.onRemoveCallback;
            list.onRemoveCallback = value =>
            {
                if (value.index < 0) return;
                serializedObject.ApplyModifiedProperties(); Node[] nodes = RecordNodes("Remove dialogue item");
                int count = DialogueValidation.ItemCount(target), removed = value.index;
                if (itemInputs)
                {
                    target.GetInputPort("inList " + removed).ClearConnections();
                    for (int i = removed + 1; i < count; i++)
                        MoveConnections(target.GetInputPort("inList " + i), target.GetInputPort("inList " + (i - 1)));
                    target.RemoveDynamicPort("inList " + (count - 1));
                }
                remove(value);
                Changed(nodes);
            };
            int oldIndex = 0;
            var select = list.onSelectCallback;
            list.onSelectCallback = value => { oldIndex = value.index; select(value); };
            var reorder = list.onReorderCallback;
            list.onReorderCallback = value =>
            {
                serializedObject.ApplyModifiedProperties(); Node[] nodes = RecordNodes("Reorder dialogue connections");
                int newIndex = value.index;
                int direction = oldIndex < newIndex ? 1 : -1;
                if (itemInputs)
                    for (int i = oldIndex; i != newIndex; i += direction)
                        target.GetInputPort("inList " + i).SwapConnections(target.GetInputPort("inList " + (i + direction)));
                reorder(value);
                Changed(nodes);
            };
        }

        static void MoveConnections(NodePort source, NodePort destination)
        {
            if (source == null || destination == null) return;
            foreach (NodePort other in source.GetConnections().ToArray())
            { source.Disconnect(other); destination.Connect(other); }
        }

        Node[] RecordNodes(string label)
        {
            Node[] nodes = target.Ports.SelectMany(port => port.GetConnections()).Select(port => port.node).Append(target).Distinct().ToArray();
            Undo.RecordObjects(nodes, label);
            return nodes;
        }

        void Changed(Node[] nodes)
        {
            foreach (Node node in nodes) EditorUtility.SetDirty(node);
            serializedObject.Update(); DialogueGraphEditor.Invalidate(); window.Repaint();
        }

        protected static bool Supported(Type type)
        { return type.IsEnum || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(bool) || type == typeof(string); }

        protected GameObject DrawTarget(Rect rect, SerializedProperty owner = null, string label = "")
        {
            SerializedProperty Find(string name) => owner == null ? serializedObject.FindProperty(name) : owner.FindPropertyRelative(name);
            SerializedProperty obj = Find("obj"), id = Find("objGlobalId"), path = Find("objPath");
            SceneObjectReference.TryFind(obj.objectReferenceValue as GameObject, path.stringValue, id.stringValue, out GameObject current, out _);
            if (!EditorApplication.isPlaying && current != null && obj.objectReferenceValue == null) obj.objectReferenceValue = current;
            EditorGUI.BeginChangeCheck();
            GameObject selected = (GameObject)EditorGUI.ObjectField(rect, new GUIContent(label, path.stringValue), current, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                obj.objectReferenceValue = selected;
                id.stringValue = selected == null ? null : GlobalObjectId.GetGlobalObjectIdSlow(selected).ToString();
                path.stringValue = selected == null ? null : GetPath(selected);
            }
            return selected;
        }

        static string GetPath(GameObject obj)
        {
            string path = obj.name;
            for (Transform parent = obj.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return obj.scene.name + "/" + path;
        }

        protected static void DrawValue(Rect rect, SerializedProperty value, Type type)
        {
            if (!DialogueValidation.TryValue(value.stringValue, type, out object parsed, out string error))
            {
                value.stringValue = EditorGUI.TextField(rect, new GUIContent(string.Empty, error), value.stringValue);
                DrawOutline(rect, new Color(1f, 0.3f, 0.3f)); return;
            }
            if (type.IsEnum) value.stringValue = Convert.ToInt64(EditorGUI.EnumPopup(rect, (Enum)parsed)).ToString(CultureInfo.InvariantCulture);
            else if (type == typeof(bool)) value.stringValue = EditorGUI.Toggle(rect, (bool)parsed) ? "1" : "0";
            else if (type == typeof(int)) value.stringValue = EditorGUI.IntField(rect, (int)parsed).ToString(CultureInfo.InvariantCulture);
            else if (type == typeof(float)) value.stringValue = EditorGUI.FloatField(rect, (float)parsed).ToString("R", CultureInfo.InvariantCulture);
            else if (type == typeof(double)) value.stringValue = EditorGUI.DoubleField(rect, (double)parsed).ToString("R", CultureInfo.InvariantCulture);
            else value.stringValue = EditorGUI.TextField(rect, value.stringValue);
        }

        protected void DrawText(Rect rect, SerializedProperty localized)
        {
            int index = DialogueSettings.Language;
            SerializedProperty text = localized.FindPropertyRelative("translations").GetArrayElementAtIndex(index);
            text.stringValue = EditorGUI.TextArea(rect, text.stringValue, EditorStyles.textArea);
        }
    }
}
