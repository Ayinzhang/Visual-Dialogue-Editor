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
            [HideInInspector] public string id;
            public Sprite sprite;
            [HideInInspector] public string spriteGuid;
            [HideInInspector] public long spriteFileId;
            public int person;
            public PortType type;
            public string context;
            [SerializeField, HideInInspector] LocalizedText localizedContext = new LocalizedText();

            public string GetContext(int languageIndex)
            { return localizedContext == null ? context ?? string.Empty : localizedContext.Get(languageIndex, context); }

            public void EnsureLocalizationData(int languageCount)
            {
                if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
                if (localizedContext == null) localizedContext = new LocalizedText();
                localizedContext.EnsureLanguageCount(languageCount);
            }

            public void RemoveLanguage(int languageIndex)
            { localizedContext?.RemoveLanguage(languageIndex); }

            public string GetRawContext(int languageIndex)
            { return languageIndex == 0 ? context : localizedContext?.GetRaw(languageIndex); }

            public void SetContext(int languageIndex, string value)
            {
                if (languageIndex == 0) context = value;
                else localizedContext.Set(languageIndex, value);
            }
        }

        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
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