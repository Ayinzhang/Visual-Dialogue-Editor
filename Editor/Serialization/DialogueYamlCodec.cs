namespace DialogueEditor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.RepresentationModel;
    using YamlDotNet.Serialization;

    public static class DialogueYamlCodec
    {
        static readonly Regex Number = new Regex(@"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);

        public static DialogueDocument Read(string yaml) { return Read(yaml, false); }

        internal static DialogueDocument Read(string text, bool json)
        {
            using (var input = new StringReader(text))
            {
                var parser = new Parser(input);
                parser.Consume<StreamStart>();
                DocumentStart start = parser.Consume<DocumentStart>();
                if (json && !start.IsImplicit) throw new FormatException("JSON cannot contain a YAML document marker.");
                object content = ReadValue(parser, 0, json);
                DocumentEnd end = parser.Consume<DocumentEnd>();
                if (json && !end.IsImplicit) throw new FormatException("JSON cannot contain a YAML document marker.");
                if (!parser.Accept<StreamEnd>(out _)) throw new FormatException("Only one document is supported.");
                parser.Consume<StreamEnd>();
                var document = (DialogueDocument)ReadObject(content, typeof(DialogueDocument), "$");
                DialogueJsonCodec.Validate(document);
                return document;
            }
        }

        public static string Write(DialogueDocument document)
        {
            DialogueJsonCodec.Validate(document);
            var stream = new YamlStream(new YamlDocument(WriteValue(ToValue(document))));
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            {
                stream.Save(text, false);
                return text.ToString();
            }
        }

        internal static string WriteJson(DialogueDocument document)
        {
            DialogueJsonCodec.Validate(document);
            var text = new StringBuilder();
            WriteJsonValue(ToValue(document), text, 0);
            return text.AppendLine().ToString();
        }

        static object ReadValue(IParser parser, int depth, bool json)
        {
            if (depth > 64) throw new FormatException("Document nesting exceeds 64 levels.");
            if (parser.Accept<MappingStart>(out _))
            {
                MappingStart start = parser.Consume<MappingStart>();
                CheckNode(start);
                if (json && start.Style != MappingStyle.Flow) throw new FormatException("JSON objects require braces.");
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                while (!parser.Accept<MappingEnd>(out _))
                {
                    Scalar key = parser.Consume<Scalar>();
                    CheckNode(key);
                    if (json && key.Style != ScalarStyle.DoubleQuoted) throw new FormatException("JSON property names require double quotes.");
                    if (result.ContainsKey(key.Value)) throw new FormatException("Duplicate property: " + key.Value);
                    result.Add(key.Value, ReadValue(parser, depth + 1, json));
                }
                parser.Consume<MappingEnd>();
                return result;
            }
            if (parser.Accept<SequenceStart>(out _))
            {
                SequenceStart start = parser.Consume<SequenceStart>();
                CheckNode(start);
                if (json && start.Style != SequenceStyle.Flow) throw new FormatException("JSON arrays require brackets.");
                var result = new List<object>();
                while (!parser.Accept<SequenceEnd>(out _)) result.Add(ReadValue(parser, depth + 1, json));
                parser.Consume<SequenceEnd>();
                return result;
            }
            if (!parser.Accept<Scalar>(out _)) throw new FormatException("YAML aliases are not supported; use explicit values.");
            Scalar scalar = parser.Consume<Scalar>();
            CheckNode(scalar);
            if (scalar.Style == ScalarStyle.DoubleQuoted || !json && scalar.Style != ScalarStyle.Plain) return scalar.Value;
            if (json && scalar.Style != ScalarStyle.Plain) throw new FormatException("JSON strings require double quotes.");
            string value = scalar.Value;
            if (value == "null" || !json && (value.Length == 0 || value == "~" || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))) return null;
            if (value == "true" || !json && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (value == "false" || !json && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            if (scalar.Style == ScalarStyle.Plain && Number.IsMatch(value))
            {
                if (value.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
                {
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return integer;
                    if (decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal large)) return large;
                }
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                    !double.IsNaN(number) && !double.IsInfinity(number)) return number;
                throw new FormatException("Number is outside the supported range: " + value);
            }
            if (json) throw new FormatException("Invalid JSON scalar: " + value);
            return value;
        }

        static void CheckNode(NodeEvent node)
        {
            if (!node.Anchor.IsEmpty || !node.Tag.IsEmpty) throw new FormatException("YAML anchors and explicit tags are not supported.");
        }

        static object ReadObject(object value, Type type, string path)
        {
            Type nullable = Nullable.GetUnderlyingType(type);
            if (value == null)
            {
                if (type.IsValueType && nullable == null) throw new FormatException(path + ": null is not allowed.");
                return null;
            }
            if (nullable != null) return ReadObject(value, nullable, path);
            if (type == typeof(object)) return value;
            if (type == typeof(string) || type == typeof(bool))
            {
                if (type == typeof(string) && value is string || type == typeof(bool) && value is bool) return value;
                throw new FormatException(path + ": expected " + type.Name + ".");
            }
            if (type == typeof(int) || type == typeof(long) || type == typeof(float))
            {
                if (!DialogueJsonCodec.IsNumber(value) || type != typeof(float) && !DialogueJsonCodec.IsInteger(value))
                    throw new FormatException(path + ": expected " + type.Name + ".");
                try
                {
                    if (type == typeof(int)) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    if (type == typeof(long)) return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    float number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                    if (float.IsNaN(number) || float.IsInfinity(number)) throw new OverflowException();
                    return number;
                }
                catch (OverflowException) { throw new FormatException(path + ": number is out of range."); }
            }
            if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (!(value is List<object> items)) throw new FormatException(path + ": expected an array.");
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                IList list = type.IsArray ? (IList)Array.CreateInstance(itemType, items.Count) : (IList)Activator.CreateInstance(type);
                for (int i = 0; i < items.Count; i++)
                {
                    object item = ReadObject(items[i], itemType, path + "[" + i + "]");
                    if (type.IsArray) list[i] = item; else list.Add(item);
                }
                return list;
            }
            if (!(value is Dictionary<string, object> map)) throw new FormatException(path + ": expected " + type.Name + ".");
            if (type == typeof(Dictionary<string, string>))
            {
                var translations = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in map) translations.Add(entry.Key, (string)ReadObject(entry.Value, typeof(string), path + "." + entry.Key));
                return translations;
            }
            if (type == typeof(NodeDocument))
            {
                if (!map.TryGetValue("type", out object kind)) throw new FormatException(path + ": type is required.");
                switch (kind)
                {
                    case "dialogue": type = typeof(DialogueNodeDocument); break;
                    case "option": type = typeof(OptionNodeDocument); break;
                    case "event": type = typeof(EventNodeDocument); break;
                    case "check": type = typeof(CheckNodeDocument); break;
                    default: throw new FormatException(path + ": unknown node type.");
                }
            }
            FieldInfo[] fields = Fields(type).ToArray();
            foreach (string key in map.Keys)
                if (!fields.Any(field => Name(field) == key)) throw new FormatException(path + ": unknown field " + key + ".");
            object instance = Activator.CreateInstance(type);
            foreach (FieldInfo field in fields)
            {
                string name = Name(field);
                var required = field.GetCustomAttribute<DocumentRequiredAttribute>();
                bool present = map.TryGetValue(name, out object content);
                if (required != null && (!present || content == null && !required.AllowNull))
                    throw new FormatException(path + "." + name + ": required field is missing or null.");
                if (present) field.SetValue(instance, ReadObject(content, field.FieldType, path + "." + name));
            }
            return instance;
        }

        static IEnumerable<FieldInfo> Fields(Type type)
        {
            if (type.BaseType != null && type.BaseType != typeof(object))
                foreach (FieldInfo field in Fields(type.BaseType)) yield return field;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) yield return field;
        }

        static string Name(FieldInfo field) { return field.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? field.Name; }

        static object ToValue(object value)
        {
            if (value == null || value is string || value is bool || DialogueJsonCodec.IsNumber(value)) return value;
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary) map.Add((string)entry.Key, ToValue(entry.Value));
                return map;
            }
            if (value is IEnumerable items)
            {
                var sequence = new List<object>();
                foreach (object item in items) sequence.Add(ToValue(item));
                return sequence;
            }
            foreach (FieldInfo field in Fields(value.GetType())) map.Add(Name(field), ToValue(field.GetValue(value)));
            return map;
        }

        static YamlNode WriteValue(object value)
        {
            if (value is Dictionary<string, object> properties)
            {
                var mapping = new YamlMappingNode();
                foreach (var property in properties) mapping.Add(new YamlScalarNode(property.Key), WriteValue(property.Value));
                return mapping;
            }
            if (value is List<object> items)
            {
                var sequence = new YamlSequenceNode();
                foreach (object item in items) sequence.Add(WriteValue(item));
                return sequence;
            }
            if (value is string text)
                return new YamlScalarNode(text) { Style = text.Contains("\n") && text.IndexOfAny(new[] { '\r', '\u0085', '\u2028', '\u2029' }) < 0 ? ScalarStyle.Literal : ScalarStyle.DoubleQuoted };
            return new YamlScalarNode(ScalarText(value)) { Style = ScalarStyle.Plain };
        }

        static string ScalarText(object value)
        {
            if (value == null) return "null";
            if (value is bool flag) return flag ? "true" : "false";
            string text = DialogueJsonCodec.ValueText(value, null);
            return (value is float || value is double) && text.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 ? text + ".0" : text;
        }

        static void WriteJsonValue(object value, StringBuilder text, int depth)
        {
            if (value is Dictionary<string, object> map)
            {
                text.Append('{');
                int index = 0;
                foreach (var entry in map)
                {
                    if (index++ > 0) text.Append(',');
                    text.AppendLine().Append(' ', (depth + 1) * 2);
                    Quote(entry.Key, text);
                    text.Append(": ");
                    WriteJsonValue(entry.Value, text, depth + 1);
                }
                if (map.Count > 0) text.AppendLine().Append(' ', depth * 2);
                text.Append('}');
            }
            else if (value is List<object> items)
            {
                text.Append('[');
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) text.Append(',');
                    text.AppendLine().Append(' ', (depth + 1) * 2);
                    WriteJsonValue(items[i], text, depth + 1);
                }
                if (items.Count > 0) text.AppendLine().Append(' ', depth * 2);
                text.Append(']');
            }
            else if (value is string str) Quote(str, text);
            else text.Append(ScalarText(value));
        }

        static void Quote(string value, StringBuilder text)
        {
            text.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsSurrogate(character))
                {
                    if (!char.IsHighSurrogate(character) || i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                        throw new FormatException("Text contains an unpaired Unicode surrogate.");
                    text.Append(character).Append(value[++i]);
                    continue;
                }
                if (character == '"' || character == '\\') text.Append('\\').Append(character);
                else if (character < 0x20 || character == '\u0085' || character == '\u2028' || character == '\u2029')
                    text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(character);
            }
            text.Append('"');
        }
    }
}
