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
        static int currentLanguage = -1;
        public static event Action LanguageChanged;

        public static DialogueSettings Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<DialogueSettings>("DialogueSettings");
                if (instance == null) throw new InvalidOperationException("Missing Resources/DialogueSettings.asset.");
                return instance;
            }
        }

        public static int Language
        {
            get { return Application.isPlaying && currentLanguage >= 0 ? currentLanguage : Instance.languageIndex; }
            set
            {
                if (value < 0 || value >= Instance.languages.Count) throw new ArgumentOutOfRangeException(nameof(value));
                if (Language == value) return;
                if (Application.isPlaying) currentLanguage = value;
                else Instance.languageIndex = value;
                LanguageChanged?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLanguage() { currentLanguage = -1; }
    }
}
