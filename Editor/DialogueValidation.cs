namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using XNode;

    public class DialogueIssue
    {
        public Node node;
        public int item = -1;
        public string message;
        public bool unverified;
    }

    public static class DialogueValidation
    {
        public static bool CanLink(NodePort output, NodePort input)
        {
            BaseNode from = (BaseNode)output.node, to = (BaseNode)input.node;
            int o = BaseNode.Item(output), i = BaseNode.Item(input);
            return o >= 0 && o < from.Count && from.HasOutput(o) && (i < 0 ? !to.ItemInputs : i < to.Count && to.HasInput(i));
        }

        public static List<DialogueIssue> Validate(DialogueGraph graph)
        {
            var issues = new List<DialogueIssue>();
            DialogueSystemBase system = DialogueBindingEditor.SceneSystem(graph, out string systemIssue, out bool pending);
            void Add(Node node, int item, string message, bool unverified = false) => issues.Add(new DialogueIssue { node = node, item = item, message = message, unverified = unverified });
            void Bind(Node node, int item, string key, Action resolve)
            {
                if (string.IsNullOrEmpty(key)) Add(node, item, "Target is not assigned.");
                else if (system == null) Add(node, item, systemIssue, pending);
                else try { resolve(); } catch (Exception e) { Add(node, item, (e.InnerException ?? e).Message); }
            }

            if (graph.entryNode == null) Add(null, -1, "Entry node is missing. Right-click a node to set it.");
            foreach (BaseNode node in graph.nodes)
            {
                if (node.Count == 0) Add(node, -1, "Node has no content.");
                if (node is CheckNode check) Bind(node, -1, check.bindingKey, () => check.Next(system));
                if (node is EventNode events)
                    for (int i = 0; i < events.Count; i++)
                    {
                        EventNode.FuncInfo function = events.eventList[i];
                        Bind(node, i, function.bindingKey, () => function.Resolve(system, out _, out _, out _));
                    }
                foreach (NodePort output in node.DynamicOutputs)
                    foreach (NodePort input in output.GetConnections())
                        if (!CanLink(output, input)) Add(node, BaseNode.Item(output), "Connection is not used: " + output.fieldName);
            }
            return issues;
        }
    }
}
