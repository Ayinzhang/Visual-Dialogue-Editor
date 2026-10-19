namespace DialogueEditor
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Serialization;
    using XNode;

    public enum PortType { None, Input, Output, All }

    public abstract class BaseNode : Node
    {
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector, FormerlySerializedAs("abstruct")] public string summary;
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public abstract string ListName { get; }
        public abstract IList Items { get; }
        public int Count => Items.Count;
        public virtual bool ItemInputs => false;
        public virtual bool HasInput(int index) => false;
        public virtual bool HasOutput(int index) => true;
        public string OutputName(int index) => ListName + " " + index;
        public static int Item(NodePort port) => port.IsDynamic ? int.Parse(port.fieldName.Substring(port.fieldName.LastIndexOf(' ') + 1)) : -1;
    }
}
