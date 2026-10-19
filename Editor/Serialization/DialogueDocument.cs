namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using YamlDotNet.Serialization;

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class DocumentRequiredAttribute : Attribute
    {
        public bool AllowNull { get; }
        public DocumentRequiredAttribute(bool allowNull = false) { AllowNull = allowNull; }
    }

    public sealed class DialogueDocument
    {
        [DocumentRequired] public int schemaVersion;
        [DocumentRequired] public string entryNodeId;
        [DocumentRequired] public string defaultLanguage;
        [DocumentRequired] public List<string> languages;
        public List<SpeakerDocument> speakers = new List<SpeakerDocument>();
        [DocumentRequired] public List<NodeDocument> nodes;
    }

    public sealed class SpeakerDocument
    {
        [DocumentRequired] public string id;
        [DocumentRequired] public Dictionary<string, string> names;
    }

    public abstract class NodeDocument
    {
        [DocumentRequired] public string id;
        [DocumentRequired] public string type;
        public string name;
        public EditorDocument editor;
    }

    public sealed class DialogueNodeDocument : NodeDocument
    {
        [DocumentRequired] public List<LineDocument> lines;
    }

    public sealed class OptionNodeDocument : NodeDocument
    {
        [DocumentRequired] public List<OptionDocument> options;
    }

    public sealed class EventNodeDocument : NodeDocument
    {
        [DocumentRequired] public List<EventDocument> events;
    }

    public sealed class CheckNodeDocument : NodeDocument
    {
        [DocumentRequired] public TargetDocument target;
        [DocumentRequired] public string componentType;
        [DocumentRequired] public string field;
        [DocumentRequired] public string valueType;
        public string enumType;
        [DocumentRequired] public List<ConditionDocument> conditions;
    }

    public abstract class ItemDocument
    {
        [DocumentRequired] public string id;
        [DocumentRequired(true)] public NextDocument next;
    }

    public sealed class LineDocument : ItemDocument
    {
        [DocumentRequired] public string portType;
        public string speakerId;
        [DocumentRequired] public Dictionary<string, string> text;
        public SpriteDocument sprite;
    }

    public sealed class OptionDocument : ItemDocument
    {
        [DocumentRequired] public Dictionary<string, string> text;
    }

    public sealed class EventDocument : ItemDocument
    {
        [DocumentRequired] public string portType;
        [DocumentRequired] public TargetDocument target;
        [DocumentRequired] public string componentType;
        [DocumentRequired] public string method;
        public ParameterDocument parameter;
    }

    public sealed class ConditionDocument : ItemDocument
    {
        [YamlMember(Alias = "operator"), DocumentRequired] public string comparison;
        [DocumentRequired(true)] public object value;
    }

    public sealed class NextDocument
    {
        [DocumentRequired] public string nodeId;
        public string itemId;
    }

    public sealed class TargetDocument
    {
        public string globalObjectId;
        public string path;
    }

    public sealed class ParameterDocument
    {
        [DocumentRequired] public string type;
        public string enumType;
        [DocumentRequired(true)] public object value;
    }

    public sealed class SpriteDocument
    {
        public string guid;
        public long? fileId;
    }

    public sealed class EditorDocument
    {
        public float[] position;
        public bool? collapsed;
        public string summary;
    }
}
