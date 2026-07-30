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
            [SerializeField, HideInInspector] LocalizedText localizedContext = new LocalizedText();

            public string GetContext(int languageIndex)
            { return localizedContext == null ? context ?? string.Empty : localizedContext.Get(languageIndex, context); }

            public void EnsureLocalizationData(int languageCount)
            {
                if (localizedContext == null) localizedContext = new LocalizedText();
                localizedContext.EnsureLanguageCount(languageCount);
            }

            public void RemoveLanguage(int languageIndex)
            { localizedContext?.RemoveLanguage(languageIndex); }
        }

        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public int activeIndex = -1;
        [HideInInspector] public string abstruct;
        [HideInInspector] public List<DialogueInfo> dialogueList = new List<DialogueInfo>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public void EnsureLocalizationData(int languageCount)
        {
            if (dialogueList == null) dialogueList = new List<DialogueInfo>();
            for (int i = 0; i < dialogueList.Count; i++)
            {
                if (dialogueList[i] == null) dialogueList[i] = new DialogueInfo();
                dialogueList[i].EnsureLocalizationData(languageCount);
            }
        }

        public void RemoveLanguage(int languageIndex)
        {
            if (dialogueList == null) return;
            for (int i = 0; i < dialogueList.Count; i++) dialogueList[i]?.RemoveLanguage(languageIndex);
        }
    }
}