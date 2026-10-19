namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class DialogueSettings : ScriptableObject
    {
        public List<string> languages = new List<string> { "Default" };
        public int languageIndex;

        static DialogueSettings instance;
        static int playLanguage = -1;
        public static event Action LanguageChanged;

        public static DialogueSettings Instance => instance != null ? instance : instance = Resources.Load<DialogueSettings>("DialogueSettings");

        // In play mode the language is switched without touching the asset.
        public static int Language
        {
            get => Application.isPlaying && playLanguage >= 0 ? playLanguage : Instance.languageIndex;
            set
            {
                if (Application.isPlaying) playLanguage = value;
                else Instance.languageIndex = value;
                LanguageChanged?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLanguage() => playLanguage = -1;
    }

    [Serializable] public class LocalizedText
    {
        public List<string> translations = new List<string> { "" };

        public string Get(int language) => string.IsNullOrEmpty(GetRaw(language)) ? translations[0] : translations[language];

        public string GetRaw(int language) => language < translations.Count ? translations[language] : "";

        public void Set(int language, string value)
        {
            while (translations.Count <= language) translations.Add("");
            translations[language] = value;
        }

        public void RemoveLanguage(int language)
        {
            if (language < translations.Count) translations.RemoveAt(language);
        }

        public void MoveLanguage(int from, int to)
        {
            Set(Math.Max(from, to), GetRaw(Math.Max(from, to)));
            string text = translations[from];
            translations.RemoveAt(from);
            translations.Insert(to, text);
        }
    }
}
