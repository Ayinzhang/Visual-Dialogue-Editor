namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
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

        protected NodeBase Node => (NodeBase)target;
        protected DialogueGraph Graph => (DialogueGraph)target.graph;
        protected static GUIStyle TextStyle => textStyle ?? (textStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true });
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
            if (Node.isFolded)
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
            if (GUILayout.Button(Node.isFolded ? "Expand" : "Fold")) serializedObject.FindProperty("isFolded").boolValue = !Node.isFolded;
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

            list.onAddCallback = l => Change("Add ItemIndex", () =>
            {
                AddItem(0 <= l.index && l.index < Node.Items.Count ? serializedObject.FindProperty(Node.ListName).GetArrayElementAtIndex(l.index).boxedValue : null);
                l.index = Node.Items.Count - 1;
            });

            var remove = list.onRemoveCallback;
            list.onRemoveCallback = l => Change("Remove ItemIndex", () =>
            {
                if (Node.ItemInputs)
                {
                    InList(l.index).ClearConnections();
                    for (int i = l.index + 1; i < Node.Items.Count; i++)
                        foreach (NodePort output in InList(i).GetConnections())
                        {
                            InList(i).Disconnect(output);
                            InList(i - 1).Connect(output);
                        }
                    Node.RemoveDynamicPort("inList " + (Node.Items.Count - 1));
                }
                remove(l);
            });

            var reorder = list.onReorderCallback;
            list.onReorderCallback = null;
            list.onReorderCallbackWithDetails = (l, from, to) => Change("Move ItemIndex", () =>
            {
                if (Node.ItemInputs)
                    for (int i = from; i != to; i += from < to ? 1 : -1)
                        InList(i).SwapConnections(InList(from < to ? i + 1 : i - 1));
                reorder(l);
            });
        }

        public void AddItem(object item = null)
        {
            if (item == null)
            {
                item = NewItem();
                LocalizedText text = item is DialogueNode.DialogueInfo dialogue ? dialogue.context : item as LocalizedText;
                if (text != null)
                    while (text.translations.Count < DialogueSettings.Instance.languages.Count) text.AddLanguage();
            }
            Node.Items.Add(item);
            int index = Node.Items.Count - 1;
            Node.AddDynamicOutput(typeof(XNode.Node.Nothing), XNode.Node.ConnectionType.Override, XNode.Node.TypeConstraint.None, Node.OutputName(index));
            if (Node.ItemInputs) Node.AddDynamicInput(typeof(XNode.Node.Nothing), XNode.Node.ConnectionType.Multiple, XNode.Node.TypeConstraint.None, "inList " + index);
        }

        NodePort InList(int index) => Node.GetInputPort("inList " + index);

        public static void DrawText(Rect rect, SerializedProperty localized)
        {
            SerializedProperty translations = localized.FindPropertyRelative("translations");
            int language = DialogueSettings.LanguageIndex;
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
                var picked = (GameObject)EditorGUI.ObjectField(rect, new GUIContent("TargetGameObject", issue ?? "Binding is saved in the active scene's Dialogue System."),
                    DialogueBindingEditor.TargetGameObject(system, key.stringValue), typeof(GameObject), true);
                if (!EditorGUI.EndChangeCheck()) return;
                Object component = picked == null ? null : Array.Find(picked.GetComponents<Component>(), c => c != null && c.GetType().FullName == componentType);
                ChangeBinding("Assign Dialogue TargetGameObject", component ?? picked, key.stringValue, value =>
                {
                    key.stringValue = value;
                    serializedObject.ApplyModifiedProperties();
                });
            }
        }

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
                action(target == null ? "" : system.bindings.Bind(target, key));
                if (target != null) DialogueBindingEditor.RememberScene(Graph, system.gameObject.scene);
            }, system);
        }

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

        protected void Show(GenericMenu menu) => window.onLateGUI += () => menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));

        static void Border(Rect rect, Color color, string tooltip)
        {
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, 2, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax, rect.y, 2, rect.height), color);
            if (tooltip != null) GUI.Label(rect, new GUIContent("", tooltip));
        }
    }

    [CustomNodeEditor(typeof(DialogueNode))]
    public class DialogueNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 64;

        protected override object NewItem() => new DialogueNode.DialogueInfo { person = Graph.speakers.Count == 0 ? -1 : 0 };

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            SerializedProperty person = item.FindPropertyRelative("person");
            DrawSprite(new Rect(rect.x, rect.y + 4, 56, 56), item, index);

            Rect line = new Rect(rect.x + 62, rect.y + 4, (rect.width - 66) / 2, EditorGUIUtility.singleLineHeight);
            int language = DialogueSettings.LanguageIndex;
            string[] names = new[] { "(No speaker)" }.Concat(Graph.speakers.Select(s => s.GetLanguage(language))).ToArray();
            person.intValue = EditorGUI.Popup(line, person.intValue + 1, names) - 1;
            line.x += line.width + 4;
            EditorGUI.PropertyField(line, item.FindPropertyRelative("type"), GUIContent.none);

            DrawText(new Rect(rect.x + 62, line.yMax + 2, rect.width - 62, rect.yMax - line.yMax - 6), item.FindPropertyRelative("context"));
        }

        void DrawSprite(Rect rect, SerializedProperty item, int index)
        {
            Sprite sprite = item.FindPropertyRelative("sprite").objectReferenceValue as Sprite;
            int control = GUIUtility.GetControlID(target.GetInstanceID() ^ index, FocusType.Passive, rect);
            Event current = Event.current;
            if (GUI.enabled && current.type == EventType.ExecuteCommand &&
                (current.commandName == "ObjectSelectorUpdated" || current.commandName == "ObjectSelectorClosed") &&
                EditorGUIUtility.GetObjectPickerControlID() == control)
            {
                sprite = EditorGUIUtility.GetObjectPickerObject() as Sprite;
                SetSprite(item, sprite);
                current.Use();
            }
            if (GUI.enabled && rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
                {
                    Sprite dropped = null;
                    foreach (UnityEngine.Object reference in DragAndDrop.objectReferences)
                    {
                        dropped = reference as Sprite;
                        if (dropped == null && reference is Texture2D)
                            dropped = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(reference));
                        if (dropped != null) break;
                    }
                    DragAndDrop.visualMode = dropped == null ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
                    if (dropped != null && current.type == EventType.DragPerform)
                    { DragAndDrop.AcceptDrag(); SetSprite(item, dropped); }
                    current.Use();
                }
                else if (current.type == EventType.ContextClick)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear Sprite"), false, () =>
                    {
                        Change("Clear Sprite", () =>
                            SetSprite(serializedObject.FindProperty(Node.ListName).GetArrayElementAtIndex(index), null));
                    });
                    Show(menu);
                    current.Use();
                }
            }
            GUI.BeginGroup(rect);
            GUI.Box(new Rect(0, 0, rect.width, rect.height), GUIContent.none, EditorStyles.objectFieldThumb);
            if (sprite != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
                if (preview != null)
                    GUI.DrawTexture(new Rect(1, 1, rect.width - 2, rect.height - 2), preview, ScaleMode.ScaleToFit, true);
                if (AssetPreview.IsLoadingAssetPreview(sprite.GetInstanceID())) window.Repaint();
            }
            if (GUI.Button(new Rect(0, 0, rect.width, rect.height),
                new GUIContent(string.Empty, sprite != null ? sprite.name : "Select Sprite"), GUIStyle.none))
                EditorGUIUtility.ShowObjectPicker<Sprite>(sprite, false, string.Empty, control);
            GUI.EndGroup();
        }

        static void SetSprite(SerializedProperty item, Sprite sprite)
        {
            item.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            GUI.changed = true;
        }
    }

    [CustomNodeEditor(typeof(OptionNode))]
    public class OptionNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 40;

        protected override object NewItem() => new LocalizedText();

        protected override void DrawItem(Rect rect, SerializedProperty item, int index) =>
            DrawText(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), item);
    }

    [CustomNodeEditor(typeof(EventNode))]
    public class EventNodeEditor : DialogueNodeEditorBase
    {
        protected override float RowHeight => 46;

        protected override object NewItem() => new EventNode.FuncInfo { portType = PortType.All };

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            EventNode.FuncInfo info = ((EventNode)Node).eventList[index];
            Rect line = new Rect(rect.x, rect.y + 4, rect.width - 84, EditorGUIUtility.singleLineHeight);
            DrawTarget(line, item.FindPropertyRelative("bindingKey"), info.compName);
            EditorGUI.PropertyField(new Rect(line.xMax + 4, line.y, 80, line.height), item.FindPropertyRelative("portType"), GUIContent.none);

            line.y += line.height + 2;
            Type parameter = string.IsNullOrEmpty(info.paraType) ? null : Type.GetType(info.paraType);
            if (parameter != null) line.width = rect.width / 2;
            string label = string.IsNullOrEmpty(info.funcName) ? "Select method" : info.compName.Substring(info.compName.LastIndexOf('.') + 1) + "." + info.funcName;
            if (EditorGUI.DropdownButton(line, new GUIContent(label), FocusType.Keyboard)) Show(MethodMenu(info));
            if (parameter != null) DrawValue(new Rect(line.xMax + 4, line.y, rect.width - line.width - 4, line.height), item.FindPropertyRelative("paraNum"), parameter);
        }

        GenericMenu MethodMenu(EventNode.FuncInfo info)
        {
            var menu = new GenericMenu();
            GameObject obj = DialogueBindingEditor.TargetGameObject(DialogueBindingEditor.SceneSystem(out _), info.bindingKey);
            if (obj == null) { menu.AddDisabledItem(new GUIContent("Assign a target in an open scene")); return menu; }
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                string label = DialogueBindingEditor.ComponentName(component, components) + "/";
                foreach (MethodInfo method in component.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.IsSpecialName || method.ContainsGenericParameters || method.DeclaringType.Namespace?.StartsWith("UnityEngine") == true) continue;
                    if (parameters.Length > 1 || parameters.Length == 1 && !DialogueReflection.Supported(parameters[0].ParameterType)) continue;
                    Type parameter = parameters.Length == 1 ? parameters[0].ParameterType : null;
                    string compName = component.GetType().FullName;
                    menu.AddItem(new GUIContent(label + method.Name + "(" + parameter?.Name + ")"), false,
                        () => ChangeBinding("Select Method", component, info.bindingKey, key =>
                        {
                            info.bindingKey = key;
                            info.compName = compName;
                            info.funcName = method.Name;
                            info.paraType = parameter == null ? "" : parameter.AssemblyQualifiedName;
                            info.paraNum = parameter == typeof(string) ? "" : "0";
                        }));
                }
            }
            return menu;
        }
    }

    [CustomNodeEditor(typeof(CheckNode))]
    public class CheckNodeEditor : DialogueNodeEditorBase
    {
        CheckNode Check => (CheckNode)Node;
        Type VarType => string.IsNullOrEmpty(Check.varType) ? null : Type.GetType(Check.varType);

        protected override float RowHeight => 24;

        protected override object NewItem() => new CheckNode.CheckInfo { checkNum = VarType == typeof(string) ? "" : "0" };

        protected override void DrawFields()
        {
            DrawTarget(EditorGUILayout.GetControlRect(), serializedObject.FindProperty("bindingKey"), Check.compName);
            Rect line = EditorGUILayout.GetControlRect();
            string label = string.IsNullOrEmpty(Check.varName) ? "Select field" : Check.compName.Substring(Check.compName.LastIndexOf('.') + 1) + "." + Check.varName;
            if (EditorGUI.DropdownButton(line, new GUIContent(label), FocusType.Keyboard)) Show(FieldMenu());
        }

        protected override void DrawItem(Rect rect, SerializedProperty item, int index)
        {
            Rect line = new Rect(rect.x, rect.y + 3, 90, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, item.FindPropertyRelative("checkType"), GUIContent.none);
            if (VarType != null) DrawValue(new Rect(line.xMax + 4, line.y, rect.width - 94, line.height), item.FindPropertyRelative("checkNum"), VarType);
        }

        GenericMenu FieldMenu()
        {
            var menu = new GenericMenu();
            GameObject obj = DialogueBindingEditor.TargetGameObject(DialogueBindingEditor.SceneSystem(out _), Check.bindingKey);
            if (obj == null) { menu.AddDisabledItem(new GUIContent("Assign a target in an open scene")); return menu; }
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                string label = DialogueBindingEditor.ComponentName(component, components) + "/";
                foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!DialogueReflection.Supported(field.FieldType)) continue;
                    string compName = component.GetType().FullName;
                    menu.AddItem(new GUIContent(label + field.Name), false,
                        () => ChangeBinding("Select Field", component, Check.bindingKey, key =>
                        {
                            if (VarType != field.FieldType)
                                foreach (CheckNode.CheckInfo info in Check.checkList) info.checkNum = field.FieldType == typeof(string) ? "" : "0";
                            Check.bindingKey = key;
                            Check.compName = compName;
                            Check.varName = field.Name;
                            Check.varType = field.FieldType.AssemblyQualifiedName;
                        }));
                }
            }
            return menu;
        }
    }
}
