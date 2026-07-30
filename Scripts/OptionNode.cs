using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace DialogueEditor
{
    [Serializable]
    [CreateNodeMenu("Dialogue Editor/Option")]
    public class OptionNode : Node
    {
        [Input] public Nothing before;
        [HideInInspector] public bool isMin, isActive;
        [HideInInspector] public string abstruct;
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
        [Output(dynamicPortList = true, connectionType = ConnectionType.Multiple)]
        public List<string> optionList = new List<string>();
        [SerializeField, HideInInspector] List<LocalizedText> localizedOptions = new List<LocalizedText>();

        public void EnsureLocalizationData(int languageCount)
        {
            if (optionList == null) optionList = new List<string>();
            if (localizedOptions == null) localizedOptions = new List<LocalizedText>();
            while (localizedOptions.Count < optionList.Count) localizedOptions.Add(new LocalizedText());
            while (localizedOptions.Count > optionList.Count) localizedOptions.RemoveAt(localizedOptions.Count - 1);
            for (int i = 0; i < localizedOptions.Count; i++)
            {
                if (localizedOptions[i] == null) localizedOptions[i] = new LocalizedText();
                localizedOptions[i].EnsureLanguageCount(languageCount);
            }
        }

        public List<string> GetOptions(int languageIndex)
        {
            List<string> result = new List<string>(optionList.Count);
            for (int i = 0; i < optionList.Count; i++)
                result.Add(languageIndex <= 0 ? optionList[i] ?? string.Empty : localizedOptions[i].Get(languageIndex, optionList[i]));
            return result;
        }

        public void RemoveLanguage(int languageIndex)
        {
            if (localizedOptions == null) return;
            for (int i = 0; i < localizedOptions.Count; i++) localizedOptions[i]?.RemoveLanguage(languageIndex);
        }
    }
}
