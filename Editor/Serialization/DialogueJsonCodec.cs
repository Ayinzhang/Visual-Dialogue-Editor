namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    public static class DialogueJsonCodec
    {
        public static DialogueDocument Read(string json)
        { return DialogueYamlCodec.Read(NormalizeSurrogates(json), true); }

        static string NormalizeSurrogates(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var text = new StringBuilder(json.Length);
            bool quoted = false;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (current == '"') quoted = !quoted;
                if (quoted && current == '\\' && i + 1 < json.Length)
                {
                    // YAML's scanner rejects JSON's paired UTF-16 escapes.
                    if (i + 11 < json.Length && json[i + 1] == 'u' && json[i + 6] == '\\' && json[i + 7] == 'u' &&
                        ushort.TryParse(json.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort high) &&
                        ushort.TryParse(json.Substring(i + 8, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort low) &&
                        char.IsHighSurrogate((char)high) && char.IsLowSurrogate((char)low))
                    {
                        text.Append((char)high).Append((char)low);
                        i += 11;
                        continue;
                    }
                    text.Append(current).Append(json[++i]);
                }
                else text.Append(current);
            }
            return text.ToString();
        }

        public static string Write(DialogueDocument document)
        {
            return DialogueYamlCodec.WriteJson(document);
        }

        public static IEnumerable<ItemDocument> Items(NodeDocument node)
        {
            if (node is DialogueNodeDocument dialogue) return dialogue.lines;
            if (node is OptionNodeDocument options) return options.options;
            if (node is EventNodeDocument events) return events.events;
            if (node is CheckNodeDocument check) return check.conditions;
            throw new FormatException("Unsupported node type.");
        }

        public static void Validate(DialogueDocument document)
        {
            Require(document != null && document.schemaVersion == 1, "schemaVersion must be 1.");
            Require(document.languages != null && document.languages.Count > 0, "languages must not be empty.");
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string language in document.languages)
                Require(!string.IsNullOrWhiteSpace(language) && languages.Add(language), "Empty or duplicate language.");
            Require(document.defaultLanguage != null && document.languages.Contains(document.defaultLanguage), "defaultLanguage is not in languages.");
            Require(document.speakers != null, "speakers must be an array, not null.");
            var speakers = new HashSet<string>();
            foreach (SpeakerDocument speaker in document.speakers)
            {
                Require(speaker != null && !string.IsNullOrWhiteSpace(speaker.id) && speakers.Add(speaker.id), "Empty or duplicate speaker ID.");
                ValidateText(speaker.names, document, "speaker " + speaker.id);
            }
            Require(document.nodes != null && document.nodes.Count > 0, "nodes must not be empty.");
            var nodes = new Dictionary<string, NodeDocument>();
            foreach (NodeDocument node in document.nodes)
            {
                Require(node != null && !string.IsNullOrWhiteSpace(node.id) && !nodes.ContainsKey(node.id), "Empty or duplicate node ID.");
                nodes.Add(node.id, node);
                if (node.editor?.position != null)
                    Require(node.editor.position.Length == 2 && node.editor.position.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), node.id + ": position must contain two finite numbers.");
                var ids = new HashSet<string>();
                IEnumerable<ItemDocument> content = Items(node);
                Require(content != null && content.Any(), node.id + ": content must not be empty.");
                foreach (ItemDocument item in content)
                {
                    Require(item != null && !string.IsNullOrWhiteSpace(item.id) && ids.Add(item.id), node.id + ": empty or duplicate item ID.");
                    string location = node.id + "/" + item.id;
                    if (item is LineDocument line)
                    {
                        ValidatePorts(line.portType, line.next, location);
                        Require(line.speakerId == null || speakers.Contains(line.speakerId), location + ": unknown speakerId.");
                        ValidateText(line.text, document, location);
                        if (line.sprite != null)
                        {
                            bool hasGuid = !string.IsNullOrEmpty(line.sprite.guid);
                            Require(hasGuid == line.sprite.fileId.HasValue, location + ": sprite guid and fileId must be provided together.");
                            Require(!hasGuid || Guid.TryParseExact(line.sprite.guid, "N", out _), location + ": invalid sprite GUID.");
                        }
                    }
                    if (item is OptionDocument option) ValidateText(option.text, document, location);
                    if (item is EventDocument function)
                    {
                        ValidatePorts(function.portType, function.next, location);
                        ValidateTarget(function.target, location);
                        Require(!string.IsNullOrWhiteSpace(function.componentType) && !string.IsNullOrWhiteSpace(function.method), location + ": componentType and method are required.");
                        if (function.parameter != null) ValidateValue(function.parameter.type, function.parameter.enumType, function.parameter.value, location);
                    }
                }
                if (node is CheckNodeDocument check)
                {
                    ValidateTarget(check.target, node.id);
                    Require(!string.IsNullOrWhiteSpace(check.componentType) && !string.IsNullOrWhiteSpace(check.field), node.id + ": componentType and field are required.");
                    foreach (ConditionDocument condition in check.conditions)
                    {
                        Require(new[] { "equal", "notEqual", "greater", "less" }.Contains(condition.comparison), node.id + ": invalid operator.");
                        ValidateValue(check.valueType, check.enumType, condition.value, node.id + "/" + condition.id);
                    }
                }
            }
            Require(document.entryNodeId != null && nodes.ContainsKey(document.entryNodeId), "entryNodeId does not identify a node.");
            foreach (NodeDocument node in document.nodes)
                foreach (ItemDocument item in Items(node))
                {
                    if (item.next == null) continue;
                    Require(item.next.nodeId != null && nodes.ContainsKey(item.next.nodeId), node.id + "/" + item.id + ": unknown next.nodeId.");
                    NodeDocument destination = nodes[item.next.nodeId];
                    if (item.next.itemId == null) continue;
                    ItemDocument target = Items(destination).FirstOrDefault(candidate => candidate.id == item.next.itemId);
                    Require(target != null, node.id + "/" + item.id + ": unknown next.itemId.");
                    string ports = target is LineDocument line ? line.portType : target is EventDocument function ? function.portType : null;
                    Require(ports == "input" || ports == "both", node.id + "/" + item.id + ": destination item has no input port.");
                }
        }

        static void ValidatePorts(string ports, NextDocument next, string location)
        {
            Require(new[] { "normal", "input", "output", "both" }.Contains(ports), location + ": invalid portType.");
            Require(next == null || ports == "output" || ports == "both", location + ": next requires an output port.");
        }

        static void ValidateTarget(TargetDocument target, string location)
        {
            Require(target != null && (!string.IsNullOrWhiteSpace(target.globalObjectId) || !string.IsNullOrWhiteSpace(target.path)), location + ": target needs globalObjectId or path.");
            Require(string.IsNullOrEmpty(target.globalObjectId) || UnityEditor.GlobalObjectId.TryParse(target.globalObjectId, out _), location + ": invalid globalObjectId.");
            Require(string.IsNullOrEmpty(target.path) || target.path.Split('/').Length >= 2, location + ": invalid scene hierarchy path.");
        }

        static void ValidateText(Dictionary<string, string> text, DialogueDocument document, string location)
        {
            Require(text != null && text.TryGetValue(document.defaultLanguage, out string fallback) && fallback != null, location + ": default language text is required.");
            foreach (string language in text.Keys) Require(document.languages.Contains(language), location + ": text uses an undeclared language.");
        }

        public static Type ValueType(string type, string enumType)
        {
            switch (type)
            {
                case "int": return typeof(int);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
                case "enum": return DialogueValidation.FindType(enumType);
                default: throw new FormatException("Unsupported value type: " + type);
            }
        }

        static void ValidateValue(string type, string enumType, object value, string location)
        {
            bool scalar = value == null || value is string || value is bool || IsNumber(value);
            Require(scalar, location + ": value must be a scalar.");
            if (type == "enum")
            {
                Require(!string.IsNullOrWhiteSpace(enumType) && IsInteger(value), location + ": enum needs enumType and an integer value.");
                Type enumeration = ValueType(type, enumType);
                Require(enumeration == null || enumeration.IsEnum, location + ": enumType is not an enum.");
                return;
            }
            Require(enumType == null, location + ": enumType must be null for non-enum values.");
            Type expected = ValueType(type, null);
            Require(type == "string" ? value == null || value is string :
                type == "bool" ? value is bool :
                type == "int" ? IsInteger(value) : IsNumber(value), location + ": value does not match " + type + ".");
            Require(DialogueValidation.TryValue(ValueText(value, type), expected, out _, out string error), location + ": " + error);
        }

        internal static bool IsInteger(object value)
        {
            return value is int || value is long || value is uint || value is ulong ||
                value is short || value is ushort || value is byte || value is sbyte ||
                value is decimal integer && decimal.Truncate(integer) == integer;
        }

        internal static bool IsNumber(object value)
        { return IsInteger(value) || value is float || value is double || value is decimal; }

        public static string ValueText(object value, string type)
        {
            if (value == null) return null;
            if (type == "bool") return (bool)value ? "1" : "0";
            if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double number) return number.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new FormatException(message); }
    }
}
