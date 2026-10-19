using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace DialogueEditor
{
    [Serializable]
    public class OptionNode : Node
    {
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        [Output(dynamicPortList = true, connectionType = ConnectionType.Override)]
        public List<string> optionList = new List<string>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
        [SerializeField, HideInInspector] List<LocalizedText> localizedOptions = new List<LocalizedText>();
        [HideInInspector] public List<string> optionIds = new List<string>();

        public void EnsureLocalizationData(int languageCount)
        {
            if (optionList == null) optionList = new List<string>();
            if (localizedOptions == null) localizedOptions = new List<LocalizedText>();
            while (optionIds.Count < optionList.Count) optionIds.Add(Guid.NewGuid().ToString("N"));
            while (optionIds.Count > optionList.Count) optionIds.RemoveAt(optionIds.Count - 1);
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
                result.Add(languageIndex <= 0 || localizedOptions == null || i >= localizedOptions.Count || localizedOptions[i] == null ?
                    optionList[i] ?? string.Empty : localizedOptions[i].Get(languageIndex, optionList[i]));
            return result;
        }

        public void RemoveLanguage(int languageIndex)
        {
            if (localizedOptions == null) return;
            for (int i = 0; i < localizedOptions.Count; i++) localizedOptions[i]?.RemoveLanguage(languageIndex);
        }

        public string GetRawOption(int index, int languageIndex)
        { return languageIndex == 0 ? optionList[index] : localizedOptions[index].GetRaw(languageIndex); }

        public void SetOption(int index, int languageIndex, string value)
        {
            if (languageIndex == 0) optionList[index] = value;
            else localizedOptions[index].Set(languageIndex, value);
        }
    }
}
