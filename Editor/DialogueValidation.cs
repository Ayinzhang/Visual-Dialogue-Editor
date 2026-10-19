namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;
    using XNode;

    public class DialogueIssue
    {
        public Node node;
        public int item = -1;
        public string message;
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
            void Add(Node node, int item, string message) => issues.Add(new DialogueIssue { node = node, item = item, message = message });
            void Bind(Node node, int item, string path, Action resolve)
            {
                if (string.IsNullOrEmpty(path)) Add(node, item, "Target is not assigned.");
                else if (SceneManager.GetSceneByName(path.Split('/')[0]).isLoaded)
                    try { resolve(); } catch (Exception e) { Add(node, item, (e.InnerException ?? e).Message); }
            }

            if (graph.entryNode == null) Add(null, -1, "Entry node is missing. Right-click a node to set it.");
            foreach (BaseNode node in graph.nodes)
            {
                if (node.Count == 0) Add(node, -1, "Node has no content.");
                if (node is CheckNode check) Bind(node, -1, check.objPath, () => check.Next());
                if (node is EventNode events)
                    for (int i = 0; i < events.Count; i++)
                    {
                        EventNode.FuncInfo function = events.eventList[i];
                        Bind(node, i, function.objPath, () => function.Resolve(out _, out _, out _));
                    }
                foreach (NodePort output in node.DynamicOutputs)
                    foreach (NodePort input in output.GetConnections())
                        if (!CanLink(output, input)) Add(node, BaseNode.Item(output), "Connection is not used: " + output.fieldName);
            }
            return issues;
        }
    }
}
