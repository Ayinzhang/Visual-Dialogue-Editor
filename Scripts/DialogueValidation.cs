namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    public sealed class DialogueIssue
    {
        public Node node;
        public int item = -1;
        public string message;
        public bool binding;
        public bool deferred;
    }

    public static class DialogueValidation
    {
        public static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Type type = Type.GetType(name, false);
            if (type != null) return type;
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidate = assembly.GetType(name, false);
                if (candidate == null) continue;
                if (type != null) return null;
                type = candidate;
            }
            return type;
        }

        public static bool TryValue(string text, Type type, out object value, out string error)
        {
            value = null; error = null;
            if (type == null) { error = "Value type is missing."; return false; }
            if (type == typeof(string)) { value = text ?? string.Empty; return true; }
            if (type == typeof(bool))
            {
                if (text == "1" || text == "true") { value = true; return true; }
                if (text == "0" || text == "false") { value = false; return true; }
            }
            else if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
            { value = integer; return true; }
            else if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float single) &&
                !float.IsNaN(single) && !float.IsInfinity(single)) { value = single; return true; }
            else if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                !double.IsNaN(number) && !double.IsInfinity(number)) { value = number; return true; }
            else if (type.IsEnum && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long enumeration))
            { value = Enum.ToObject(type, enumeration); return true; }
            error = "Invalid " + type.Name + " value: " + text;
            return false;
        }

        public static bool TryEvent(EventNode.FuncInfo function, out Component component, out MethodInfo method, out object[] arguments, out string error)
        {
            component = null; method = null; arguments = null;
            if (!SceneObjectReference.TryFind(function.obj, function.objPath, function.objGlobalId, out GameObject obj, out error)) return false;
            Type type = FindType(function.declType);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            { error = "Missing component type: " + function.declType; return false; }
            Component[] components = obj.GetComponents(type);
            if (components.Length != 1) { error = "Expected one component: " + type.FullName; return false; }
            Type parameter = string.IsNullOrEmpty(function.paraType) ? null : FindType(function.paraType);
            if (!string.IsNullOrEmpty(function.paraType) && parameter == null)
            { error = "Missing parameter type: " + function.paraType; return false; }
            if (string.IsNullOrEmpty(function.funcName)) { error = "Method is not assigned."; return false; }
            method = type.GetMethod(function.funcName, BindingFlags.Public | BindingFlags.Instance,
                null, parameter == null ? Type.EmptyTypes : new[] { parameter }, null);
            if (method == null || method.IsGenericMethod)
            { error = "Missing or unsupported method: " + type.FullName + "." + function.funcName; return false; }
            if (parameter != null)
            {
                if (!TryValue(function.paraNum, parameter, out object value, out error)) return false;
                arguments = new[] { value };
            }
            component = components[0];
            return true;
        }

        public static bool TryCheck(CheckNode check, out Component component, out FieldInfo field, out object[] comparisons, out string error)
        {
            component = null; field = null; comparisons = null;
            if (!SceneObjectReference.TryFind(check.obj, check.objPath, check.objGlobalId, out GameObject obj, out error)) return false;
            Type type = FindType(check.compName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            { error = "Missing component type: " + check.compName; return false; }
            Component[] components = obj.GetComponents(type);
            if (components.Length != 1) { error = "Expected one component: " + type.FullName; return false; }
            component = components[0];
            field = string.IsNullOrEmpty(check.varName) ? null :
                component.GetType().GetField(check.varName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != FindType(check.varType))
            { error = "Missing or changed field: " + check.compName + "." + check.varName; return false; }
            comparisons = new object[check.checkList.Count];
            for (int i = 0; i < comparisons.Length; i++)
            {
                if (check.checkList[i] == null) { error = "Missing condition " + (i + 1); return false; }
                if (!TryValue(check.checkList[i].checkNum, field.FieldType, out comparisons[i], out error)) return false;
            }
            return true;
        }

        public static int ItemCount(Node node)
        {
            if (node is DialogueNode dialogue) return dialogue.dialogueList.Count;
            if (node is OptionNode options) return options.optionList.Count;
            if (node is EventNode events) return events.eventList.Count;
            if (node is CheckNode check) return check.checkList.Count;
            return 0;
        }

        public static string OutputName(Node node, int index)
        { return (node is DialogueNode ? "dialogueList " : node is OptionNode ? "optionList " : node is EventNode ? "eventList " : "checkList ") + index; }

        public static bool HasOutput(Node node, int index)
        {
            if (node is DialogueNode dialogue) return (dialogue.dialogueList[index].type & DialogueNode.PortType.Output) != 0;
            if (node is EventNode events) return (events.eventList[index].portType & EventNode.PortType.Output) != 0;
            return node is OptionNode || node is CheckNode;
        }

        public static bool TryDestination(NodePort port, out int index, out string error)
        {
            index = 0; error = null;
            if (port == null || !port.IsInput) { error = "Destination input port is missing."; return false; }
            if (port.fieldName == "before" && (port.node is OptionNode || port.node is CheckNode)) return true;
            if (!port.fieldName.StartsWith("inList ", StringComparison.Ordinal) ||
                !int.TryParse(port.fieldName.Substring("inList ".Length), out index) || index < 0 || index >= ItemCount(port.node))
            { error = "Destination item is invalid: " + port.fieldName; return false; }
            bool accepts = port.node is DialogueNode dialogue && (dialogue.dialogueList[index].type & DialogueNode.PortType.Input) != 0 ||
                port.node is EventNode events && (events.eventList[index].portType & EventNode.PortType.Input) != 0;
            if (!accepts) { error = "Destination item does not expose an input."; return false; }
            return true;
        }

        public static List<DialogueIssue> Validate(DialogueGraph graph, bool includeBindings = true)
        {
            var result = new List<DialogueIssue>();
            if (graph.entryNode == null || !graph.nodes.Contains(graph.entryNode))
                result.Add(new DialogueIssue { message = "Entry node is missing. Right-click a node to set the entry." });
            foreach (Node node in graph.nodes)
            {
                if (node == null) { result.Add(new DialogueIssue { message = "Graph contains a missing node." }); continue; }
                int count = ItemCount(node);
                if (count == 0) Add(result, node, -1, "Node has no content.");
                for (int i = 0; i < count; i++)
                {
                    NodePort output = node.GetOutputPort(OutputName(node, i));
                    if (HasOutput(node, i) && output == null) Add(result, node, i, "Output port is missing.");
                    if (node is DialogueNode dialogue)
                    {
                        if (dialogue.dialogueList[i].person < -1 || dialogue.dialogueList[i].person >= graph.speakers.Count)
                            Add(result, node, i, "Speaker index is invalid.");
                    }
                    if (includeBindings && node is EventNode events)
                    {
                        EventNode.FuncInfo function = events.eventList[i];
                        if (!Application.isPlaying && SceneObjectReference.SceneUnloaded(function.objPath, function.objGlobalId))
                            Add(result, node, i, "Target scene is not loaded; binding check deferred: " + function.objPath, true, true);
                        else if (!TryEvent(function, out _, out _, out _, out string error))
                            Add(result, node, i, error, true);
                    }
                }
                if (includeBindings && node is CheckNode check)
                {
                    if (!Application.isPlaying && SceneObjectReference.SceneUnloaded(check.objPath, check.objGlobalId))
                        Add(result, node, -1, "Target scene is not loaded; binding check deferred: " + check.objPath, true, true);
                    else if (!TryCheck(check, out _, out _, out _, out string checkError))
                        Add(result, node, -1, checkError, true);
                }
                foreach (NodePort output in node.Outputs)
                {
                    if (output.ConnectionCount == 0) continue;
                    int item = -1;
                    bool valid = false;
                    for (int i = 0; i < count; i++)
                        if (output.fieldName == OutputName(node, i)) { item = i; valid = HasOutput(node, i); break; }
                    if (!valid) Add(result, node, item, "Connection uses an inactive or display-only output: " + output.fieldName);
                    if (output.ConnectionCount > 1) Add(result, node, item, "An output has more than one destination.");
                    foreach (NodePort input in output.GetConnections())
                        if (!graph.nodes.Contains(input.node)) Add(result, node, item, "Connection points outside the graph.");
                        else if (!TryDestination(input, out _, out string error)) Add(result, node, item, error);
                }
            }
            return result;
        }

        static void Add(List<DialogueIssue> issues, Node node, int item, string message, bool binding = false, bool deferred = false)
        { issues.Add(new DialogueIssue { node = node, item = item, message = message, binding = binding, deferred = deferred }); }
    }
}
