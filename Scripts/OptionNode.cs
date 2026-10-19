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
        [HideInInspector] public List<LocalizedText> optionList = new List<LocalizedText>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public void EnsureLocalizationData(int languageCount)
        {
            foreach (LocalizedText option in optionList) option.EnsureLanguageCount(languageCount);
        }

        public List<string> GetOptions(int languageIndex)
        {
            List<string> result = new List<string>(optionList.Count);
            for (int i = 0; i < optionList.Count; i++)
                result.Add(optionList[i].Get(languageIndex));
            return result;
        }

        public void RemoveLanguage(int languageIndex)
        {
            foreach (LocalizedText option in optionList) option.RemoveLanguage(languageIndex);
        }

        public void MoveLanguage(int from, int to)
        {
            foreach (LocalizedText option in optionList) option.MoveLanguage(from, to);
        }
    }
}
