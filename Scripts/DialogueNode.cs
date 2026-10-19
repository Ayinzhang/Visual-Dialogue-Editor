namespace DialogueEditor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [NodeWidth(350)] public class DialogueNode : BaseNode
    {
        [Serializable] public class DialogueInfo
        {
            public Sprite sprite;
            public int person;
            public PortType type;
            public LocalizedText context = new LocalizedText();
        }

        [HideInInspector] public List<DialogueInfo> dialogueList = new List<DialogueInfo>();

        public override string ListName => nameof(dialogueList);
        public override IList Items => dialogueList;
        public override bool ItemInputs => true;
        public override bool HasInput(int index) => (dialogueList[index].type & PortType.Input) != 0;
        public override bool HasOutput(int index) => (dialogueList[index].type & PortType.Output) != 0;
    }
}
