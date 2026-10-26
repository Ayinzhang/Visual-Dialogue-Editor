namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class DialogueSettings : ScriptableObject
    {
        public List<string> languages = new() { "Default" };
        public static event Action LanguageChanged;

        // Lazy loading, providing interface for upper-level calls
        static DialogueSettings instance;
        public static DialogueSettings Instance => instance ? instance : instance = Resources.Load<DialogueSettings>("DialogueSettings");

        // Persistence, to save the user's language preference during gameplay
        const string languageKey = "DialogueEditor.Language";
        public static int LanguageIndex
        {
            get
            {
                int index = PlayerPrefs.GetInt(languageKey, 0);
                return 0 <= index && index < Instance.languages.Count ? index : 0;
            }
            set
            {
                PlayerPrefs.SetInt(languageKey, value);
                LanguageChanged?.Invoke();
            }
        }
    }

    [Serializable] public class LocalizedText
    {
        public List<string> translations = new() { "" };

        public string GetLanguage(int languageIndex) => translations[languageIndex];

        public void AddLanguage() => translations.Add("");

        public void RemoveLanguage(int languageIndex) => translations.RemoveAt(languageIndex);

        public void MoveLanguage(int from, int to) // For reorderable list
        {
            string text = translations[from];
            translations.RemoveAt(from);
            translations.Insert(to, text);
        }
    }
}
