namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEditorInternal;
    using UnityEngine;
    using XNode;
    using XNodeEditor;
    using Object = UnityEngine.Object;

    public abstract class DialogueNodeEditorBase : NodeEditor
    {
        static GUIStyle textStyle;

        protected BaseNode Node => (BaseNode)target;
        protected DialogueGraph Graph => (DialogueGraph)target.graph;
        protected abstract float RowHeight { get; }
        protected abstract void DrawItem(Rect rect, SerializedProperty item, int index);
        protected abstract object NewItem();
        protected virtual void DrawFields() { }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            base.AddContextMenuItems(menu);
            menu.AddItem(new GUIContent("Set as Entry"), Graph.entryNode == Node, () =>
            {
                Undo.RecordObject(Graph, "Set Entry Node");
                Graph.entryNode = Node;
                DialogueGraphEditor.Invalidate();
            });
        }

        public override Color GetTint()
        {
            if (DialogueGraphEditor.Issues(Graph).Exists(issue => issue.node == Node && !issue.unverified)) return new Color(0.5f, 0.2f, 0.2f);
            if (DialogueGraphEditor.Playing(Node) != null) return new Color(0.5f, 0.45f, 0.15f);
            if (Graph.entryNode == Node) return new Color(0.2f, 0.4f, 0.25f);
            return base.GetTint();
        }

        public override void OnBodyGUI()
        {
            serializedObject.Update();
            NodePort before = Node.GetInputPort("before"), after = Node.GetOutputPort("after");
            if (Node.isMin)
            {
                NodeEditorGUILayout.PortPair(before, after);
                SerializedProperty summary = serializedObject.FindProperty("summary");
                summary.stringValue = EditorGUILayout.TextArea(summary.stringValue, TextStyle);
                foreach (NodePort port in Node.DynamicPorts)
                    if (portPositions.TryGetValue(port.IsInput ? before : after, out Vector2 position)) portPositions[port] = position;
            }
            else
            {
                if (!Node.ItemInputs) NodeEditorGUILayout.PortField(new GUIContent("Before"), before);
                DrawFields();
                NodeEditorGUILayout.DynamicPortList(Node.ListName, typeof(XNode.Node.Nothing), serializedObject, NodePort.IO.Output, XNode.Node.ConnectionType.Override, onCreation: InitList);
            }
            if (GUILayout.Button(Node.isMin ? "Expand" : "Fold")) serializedObject.FindProperty("isMin").boolValue = !Node.isMin;
            if (serializedObject.ApplyModifiedProperties()) DialogueGraphEditor.Invalidate();
        }

        void InitList(ReorderableList list)
        {
            list.elementHeightCallback = null;
            list.elementHeight = RowHeight;
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                DrawItem(rect, serializedObject.FindProperty(Node.ListName).GetArrayElementAtIndex(index), index);
                DialogueGraph running = DialogueGraphEditor.Playing(Node);
                if (running != null && running.index == index) Border(rect, Color.yellow, null);
                DialogueIssue issue = DialogueGraphEditor.Issues(Graph).Find(i => i.node == Node && i.item == index && !i.unverified);
                if (issue != null) Border(rect, Color.red, issue.message);
                if (Node.HasOutput(index)) NodeEditorGUILayout.PortField(new Vector2(rect.xMax + 6, rect.y + 8), Node.GetOutputPort(Node.OutputName(index)));
                if (Node.HasInput(index)) NodeEditorGUILayout.PortField(new Vector2(rect.x - 36, rect.y + 8), InList(index));
            };

            var add = list.onAddCallback;
            list.onAddCallback = l => Change("Add Item", () =>
            {
                add(l);
                int index = Node.Count - 1;
                Node.Items[index] = NewItem();
                if (Node.ItemInputs) Node.AddDynamicInput(typeof(XNode.Node.Nothing), XNode.Node.ConnectionType.Multiple, XNode.Node.TypeConstraint.None, "inList " + index);
            });

            var remove = list.onRemoveCallback;
            list.onRemoveCallback = l => Change("Remove Item", () =>
            {
                if (Node.ItemInputs)
                {
                    InList(l.index).ClearConnections();
                    for (int i = l.index + 1; i < Node.Count; i++)
                        foreach (NodePort output in InList(i).GetConnections())
                        {
                            InList(i).Disconnect(output);
                            InList(i - 1).Connect(output);
                        }
                    Node.RemoveDynamicPort("inList " + (Node.Count - 1));
                }
                remove(l);
            });

            var reorder = list.onReorderCallback;
            list.onReorderCallback = null;
            list.onReorderCallbackWithDetails = (l, from, to) => Change("Move Item", () =>
            {
                if (Node.ItemInputs)
                    for (int i = from; i != to; i += from < to ? 1 : -1)
                        InList(i).SwapConnections(InList(from < to ? i + 1 : i - 1));
                reorder(l);
            });
        }

        NodePort InList(int index) => Node.GetInputPort("inList " + index);

        protected void Change(string label, Action action, Object extra = null)
        {
            serializedObject.ApplyModifiedProperties();
            var objects = new List<Object> { target };
            if (extra != null) objects.Add(extra);
            foreach (NodePort port in Node.Ports)
                foreach (NodePort other in port.GetConnections()) objects.Add(other.node);
            Undo.RecordObjects(objects.ToArray(), label);
            action();
            EditorUtility.SetDirty(target);
            if (extra is Component component)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
            serializedObject.Update();
            DialogueGraphEditor.Invalidate();
        }

        static void Border(Rect rect, Color color, string tooltip)
        {
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, 2, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax, rect.y, 2, rect.height), color);
            if (tooltip != null) GUI.Label(rect, new GUIContent("", tooltip));
        }

        protected static GUIStyle TextStyle => textStyle ?? (textStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true });

        public static void DrawText(Rect rect, SerializedProperty localized)
        {
            SerializedProperty translations = localized.FindPropertyRelative("translations");
            int language = DialogueSettings.Language;
            string text = language < translations.arraySize ? translations.GetArrayElementAtIndex(language).stringValue : "";
            EditorGUI.BeginChangeCheck();
            text = EditorGUI.TextArea(rect, text, TextStyle);
            if (!EditorGUI.EndChangeCheck()) return;
            while (translations.arraySize <= language)
            {
                translations.InsertArrayElementAtIndex(translations.arraySize);
                translations.GetArrayElementAtIndex(translations.arraySize - 1).stringValue = "";
            }
            translations.GetArrayElementAtIndex(language).stringValue = text;
        }

        protected void DrawTarget(Rect rect, SerializedProperty key, string componentType)
        {
            DialogueSystemBase system = DialogueBindingEditor.SceneSystem(out string issue);
            using (new EditorGUI.DisabledScope(system == null || Application.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                var picked = (GameObject)EditorGUI.ObjectField(rect, new GUIContent("Target", issue ?? "Binding is saved in the active scene's Dialogue System."),
                    DialogueBindingEditor.Target(system, key.stringValue), typeof(GameObject), true);
                if (!EditorGUI.EndChangeCheck()) return;
                Object component = picked == null ? null : Array.Find(picked.GetComponents<Component>(), c => c != null && c.GetType().FullName == componentType);
                ChangeBinding("Assign Dialogue Target", component ?? picked, key.stringValue, value =>
                {
                    key.stringValue = value;
                    serializedObject.ApplyModifiedProperties();
                });
            }
        }

        protected void ChangeBinding(string label, Object target, string key, Action<string> action)
        {
            DialogueSystemBase system = DialogueBindingEditor.SceneSystem(out string issue);
            if (system == null) throw new InvalidOperationException(issue);
            GameObject obj = target is Component component ? component.gameObject : target as GameObject;
            string error = Application.isPlaying ? "Exit Play mode before changing scene bindings." :
                obj != null && obj.scene != system.gameObject.scene ? "Choose a target in the active scene, not an asset or another scene." : null;
            if (error != null) { EditorUtility.DisplayDialog("Dialogue Binding", error, "OK"); return; }
            Change(label, () =>
            {
                action(target == null ? "" : system.Bindings.Bind(target, key));
                if (target != null) DialogueBindingEditor.RememberScene(Graph, system.gameObject.scene);
            }, system);
        }

        protected static bool Supported(Type type) =>
            type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(string) || type.IsEnum;

        protected static void DrawValue(Rect rect, SerializedProperty text, Type type)
        {
            object value = DialogueReflection.Parse(text.stringValue, type);
            EditorGUI.BeginChangeCheck();
            if (type == typeof(int)) value = EditorGUI.IntField(rect, (int)value);
            else if (type == typeof(float)) value = EditorGUI.FloatField(rect, (float)value);
            else if (type == typeof(bool)) value = EditorGUI.Toggle(rect, (bool)value);
            else if (type == typeof(string)) value = EditorGUI.TextField(rect, (string)value);
            else value = EditorGUI.EnumPopup(rect, (Enum)value);
            if (EditorGUI.EndChangeCheck()) text.stringValue = DialogueReflection.Format(value);
        }
    }
}
