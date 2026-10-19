namespace DialogueEditor
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using XNodeEditor;
    using XNode;

    [CustomNodeGraphEditor(typeof(DialogueGraph))]
    public class DialogueGraphEditor : NodeGraphEditor
    {
        DialogueGraph graph => target as DialogueGraph;
        static readonly Dictionary<DialogueGraph, List<DialogueIssue>> issues = new Dictionary<DialogueGraph, List<DialogueIssue>>();
        static double nextRefresh;

        static DialogueGraphEditor()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => Invalidate();
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += scene => Invalidate();
        }

        public static void Invalidate() { issues.Clear(); nextRefresh = 0; NodeEditorWindow.RepaintAll(); }
        public static List<DialogueIssue> Issues(DialogueGraph graph)
        {
            if (EditorApplication.timeSinceStartup >= nextRefresh)
            { issues.Clear(); nextRefresh = EditorApplication.timeSinceStartup + 0.35; }
            if (!issues.TryGetValue(graph, out List<DialogueIssue> result))
            {
                result = DialogueValidation.Validate(graph);
                issues[graph] = result;
            }
            return result;
        }

        public static IEnumerable<DialogueSystem> Players(DialogueGraph graph)
        {
            if (!EditorApplication.isPlaying) return Enumerable.Empty<DialogueSystem>();
            return UnityEngine.Object.FindObjectsOfType<DialogueSystem>()
                .Where(system => system.isActiveAndEnabled && system.graph != null &&
                    system.graph.entryNode != null && system.graph.entryNode.graph == graph);
        }

        public override void OnOpen()
        {
            if (!EditorApplication.isPlaying) graph?.EnsureLocalizationData();
            Invalidate();
            Undo.undoRedoPerformed -= Invalidate; Undo.undoRedoPerformed += Invalidate;
            DialogueGraphToolbar.Attach(window);
        }
        public override void OnGUI()
        {
            if (window.hoveredPort != null && (Folded(window.hoveredPort.node) ||
                window.hoveredPort.GetConnections().Any(port => Folded(port.node)))) window.hoveredPort = null;
            DialogueGraphToolbar.Attach(window);
            if (EditorApplication.isPlaying) window.Repaint();
        }

        public static bool Folded(Node node)
        {
            if (node is DialogueNode dialogue) return dialogue.isMin;
            if (node is OptionNode options) return options.isMin;
            if (node is EventNode events) return events.isMin;
            return node is CheckNode check && check.isMin;
        }

        public override bool CanConnect(NodePort output, NodePort input)
        {
            if (EditorApplication.isPlaying || Folded(output.node) || Folded(input.node)) return false;
            int count = DialogueValidation.ItemCount(output.node);
            bool valid = false;
            for (int i = 0; i < count; i++)
                if (output.fieldName == DialogueValidation.OutputName(output.node, i)) valid = DialogueValidation.HasOutput(output.node, i);
            return valid && DialogueValidation.TryDestination(input, out _, out _) && base.CanConnect(output, input);
        }

        public override string GetPortTooltip(NodePort port) => port.fieldName;
    }
}
