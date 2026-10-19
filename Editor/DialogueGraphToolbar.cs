namespace DialogueEditor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using XNodeEditor;

    internal sealed class DialogueGraphToolbar : IMGUIContainer
    {
        readonly NodeEditorWindow window;
        IMGUIContainer canvas;
        Action drawCanvas;

        DialogueGraphToolbar(NodeEditorWindow window)
        {
            this.window = window;
            onGUIHandler = DrawToolbar;
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.height = EditorGUIUtility.singleLineHeight + 6;
            focusable = false;
            RegisterCallback<AttachToPanelEvent>(_ => BindCanvas());
            RegisterCallback<DetachFromPanelEvent>(_ => UnbindCanvas());
        }

        public static void Attach(NodeEditorWindow window)
        {
            VisualElement root = window.rootVisualElement;
            DialogueGraphToolbar toolbar = root.Q<DialogueGraphToolbar>();
            if (toolbar == null)
            {
                toolbar = new DialogueGraphToolbar(window);
                root.Add(toolbar);
            }
        }

        void BindCanvas()
        {
            // Unity's legacy window canvas is a sibling of rootVisualElement, not its child.
            canvas = panel.visualTree.Q<IMGUIContainer>();
            if (canvas == null || canvas == this || canvas.onGUIHandler == null)
                throw new InvalidOperationException("The dialogue graph window requires an IMGUI canvas.");
            drawCanvas = canvas.onGUIHandler;
            canvas.onGUIHandler = DrawCanvas;
        }

        void UnbindCanvas()
        {
            if (canvas != null && canvas.onGUIHandler == DrawCanvas) canvas.onGUIHandler = drawCanvas;
            canvas = null;
        }

        void DrawCanvas()
        {
            bool dialogue = window.graph is DialogueGraph;
            style.display = dialogue ? DisplayStyle.Flex : DisplayStyle.None;
            if (dialogue)
            {
                Event e = Event.current;
                if (e != null && IsPointerEvent(e) && canvas.WorldToLocal(worldBound).Contains(e.mousePosition) &&
                    NodeEditorWindow.currentActivity == NodeEditorWindow.NodeActivity.Idle &&
                    !window.IsDraggingPort && !NodeEditorWindow.isPanning)
                    e.Use();
                ProtectPlayback(window);
            }
            // Filter input before xNode.Controls(), which runs before the graph editor's OnGUI.
            drawCanvas();
        }

        void DrawToolbar()
        {
            if (!(window.graph is DialogueGraph graph)) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Edit", EditorStyles.toolbarDropDown, GUILayout.Width(52)))
            {
                GenericMenu menu = new GenericMenu();
                if (EditorApplication.isPlaying)
                {
                    menu.AddDisabledItem(new GUIContent("Edit Languages"));
                    menu.AddDisabledItem(new GUIContent("Edit Speakers"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Edit Languages"), false, () => SettingsService.OpenProjectSettings(DialogueSettingsEditor.SettingsPath));
                    menu.AddItem(new GUIContent("Edit Speakers"), false, () => DialogueSpeakersWindow.Open(graph));
                }
                menu.ShowAsContext();
            }
            var diagnostics = DialogueGraphEditor.Issues(graph);
            if (diagnostics.Count > 0)
                GUILayout.Label(new GUIContent(diagnostics.Count(issue => !issue.deferred) + " error(s), " +
                    diagnostics.Count(issue => issue.deferred) + " deferred", string.Join("\n", diagnostics.Select(issue => issue.message))),
                    EditorStyles.toolbarButton, GUILayout.MaxWidth(Mathf.Max(0, window.position.width - 260)));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Language:", GUILayout.Width(65));
            DialogueSettings settings = DialogueSettings.Instance;
            if (GUILayout.Button(settings.languages[DialogueSettings.Language], EditorStyles.toolbarDropDown, GUILayout.Width(110)))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < settings.languages.Count; i++)
                {
                    int index = i;
                    menu.AddItem(new GUIContent(settings.languages[i]), i == DialogueSettings.Language, () => DialogueSettingsEditor.SetLanguage(index));
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            Event e = Event.current;
            if (IsPointerEvent(e)) e.Use();
        }

        static bool IsPointerEvent(Event e) => e.isMouse || e.type == EventType.ScrollWheel ||
            e.type == EventType.ContextClick || e.type == EventType.DragUpdated || e.type == EventType.DragPerform;

        internal static void ProtectPlayback(NodeEditorWindow window)
        {
            if (!EditorApplication.isPlaying || Event.current == null) return;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && window.IsHoveringPort ||
                e.type == EventType.MouseDrag && e.button == 0 &&
                    (NodeEditorWindow.currentActivity == NodeEditorWindow.NodeActivity.HoldNode ||
                     NodeEditorWindow.currentActivity == NodeEditorWindow.NodeActivity.DragNode || window.IsDraggingPort) ||
                e.type == EventType.DragUpdated || e.type == EventType.DragPerform ||
                (e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && e.commandName != "Copy" ||
                e.type == EventType.KeyDown && (e.keyCode == KeyCode.F2 || e.keyCode == KeyCode.Return) ||
                e.type == EventType.MouseUp && (e.button == 1 || e.button == 2) && !NodeEditorWindow.isPanning)
                e.Use();
        }
    }
}
