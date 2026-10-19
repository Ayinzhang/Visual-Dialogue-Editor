namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable] public class LocalizedText
    {
        [SerializeField] List<string> translations = new List<string> { string.Empty };

        public string Get(int language)
        { return string.IsNullOrEmpty(translations[language]) ? translations[0] ?? string.Empty : translations[language]; }

        public string GetRaw(int language)
        { return translations[language]; }

        public void Set(int language, string value)
        {
            translations[language] = value ?? string.Empty;
        }

        public void EnsureLanguageCount(int count)
        {
            while (translations.Count < count)
                translations.Add(string.Empty);
            while (translations.Count > count)
                translations.RemoveAt(translations.Count - 1);
        }

        public void RemoveLanguage(int language)
        {
            translations.RemoveAt(language);
        }

        public void MoveLanguage(int from, int to)
        {
            string text = translations[from];
            translations.RemoveAt(from);
            translations.Insert(to, text);
        }
    }
}
