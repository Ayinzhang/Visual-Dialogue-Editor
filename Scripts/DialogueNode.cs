namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using XNode;

    [Serializable, NodeWidth(350)]
    public class DialogueNode : Node
    {
        public enum PortType { None, Input, Output, All }
        [Serializable] public class DialogueInfo
        {
            public Sprite sprite;
            public int person;
            public PortType type;
            public LocalizedText context = new LocalizedText();
        }

        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        [HideInInspector] public List<DialogueInfo> dialogueList = new List<DialogueInfo>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public void EnsureLocalizationData(int languageCount)
        {
            for (int i = 0; i < dialogueList.Count; i++)
                dialogueList[i].context.EnsureLanguageCount(languageCount);
        }

        public void RemoveLanguage(int languageIndex)
        {
            foreach (DialogueInfo line in dialogueList) line.context.RemoveLanguage(languageIndex);
        }

        public void MoveLanguage(int from, int to)
        {
            foreach (DialogueInfo line in dialogueList) line.context.MoveLanguage(from, to);
        }
    }
}