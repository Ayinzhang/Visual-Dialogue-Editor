namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using XNode;

    [Serializable]
    [NodeWidth(350)]
    public class DialogueNode : Node
    {
        public enum PortType { Normal, Input, Output, All }
        [Serializable]
        public class DialogueInfo
        {
            public Sprite sprite;
            public int person;
            public PortType type;
            public string context;
        }
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        [HideInInspector] public List<DialogueInfo> dialogueList = new List<DialogueInfo>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
    }
}