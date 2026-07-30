namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.Serialization;
    using XNode;

    [Serializable, CreateAssetMenu(fileName = "New Dialogue Graph", menuName = "Dialogue Graph")]
    public class DialogueGraph : NodeGraph
    {
        public enum DataType { End, Dialogue, Option }
        public struct DialogueInfo { public Sprite sprite; public string name, context; }
        public List<string> speakers = new List<string>();
        [SerializeField, HideInInspector] List<string> languages = new List<string>();
        [SerializeField, HideInInspector] List<LocalizedText> localizedSpeakers = new List<LocalizedText>();
        [SerializeField, HideInInspector] int editorLanguageIndex;
        [NonSerialized] int runtimeLanguageIndex;

        //Information available for reading
        [HideInInspector] public DataType dataType;
        [HideInInspector] public DialogueInfo dialogueInfo;
        [HideInInspector] public List<string> optionInfo;

        Node node; int index; bool init;

        public IReadOnlyList<string> Languages { get { InitLanguages(); return languages; } }
        public int LanguageIndex { get { InitLanguages(); return editorLanguageIndex; } }
        public string CurrentLanguage { get { InitLanguages(); return languages[editorLanguageIndex]; } }

        public void EnsureLocalizationData()
        {
            InitSpeakers();
            if (nodes == null) return;
            foreach (Node graphNode in nodes)
            {
                if (graphNode is DialogueNode dialogueNode) dialogueNode.EnsureLocalizationData(languages.Count);
                else if (graphNode is OptionNode optionNode) optionNode.EnsureLocalizationData(languages.Count);
            }
        }

        void InitLanguages()
        {
            if (languages == null) languages = new List<string>();
            if (languages.Count == 0) languages.Add("Default");
            editorLanguageIndex = Mathf.Clamp(editorLanguageIndex, 0, languages.Count - 1);
            runtimeLanguageIndex = Mathf.Clamp(runtimeLanguageIndex, 0, languages.Count - 1);
        }

        void InitSpeakers()
        {
            InitLanguages();
            if (speakers == null) speakers = new List<string>();
            if (localizedSpeakers == null) localizedSpeakers = new List<LocalizedText>();
            while (localizedSpeakers.Count < speakers.Count) localizedSpeakers.Add(new LocalizedText());
            while (localizedSpeakers.Count > speakers.Count) localizedSpeakers.RemoveAt(localizedSpeakers.Count - 1);
            for (int i = 0; i < localizedSpeakers.Count; i++)
            {
                if (localizedSpeakers[i] == null) localizedSpeakers[i] = new LocalizedText();
                localizedSpeakers[i].EnsureLanguageCount(languages.Count);
            }
        }

        public bool SetLanguage(int newLanguageIndex)
        {
            InitLanguages();
            if (newLanguageIndex < 0 || newLanguageIndex >= languages.Count) return false;
            runtimeLanguageIndex = newLanguageIndex;
            return true;
        }

        public bool SetLanguage(string language)
        {
            InitLanguages();
            return SetLanguage(languages.FindIndex(item => string.Equals(item, language, StringComparison.OrdinalIgnoreCase)));
        }

        public int AddLanguage(string language)
        {
            InitLanguages();
            language = language?.Trim();
            if (string.IsNullOrEmpty(language)) return -1;

            int index = languages.FindIndex(item => string.Equals(item, language, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) return editorLanguageIndex = index;

            languages.Add(language);
            editorLanguageIndex = languages.Count - 1;
            EnsureLocalizationData();
            return editorLanguageIndex;
        }

        public bool SetLanguageName(int index, string language)
        {
            InitLanguages();
            if (index < 0 || index >= languages.Count) return false;

            language = language?.Trim();
            if (string.IsNullOrEmpty(language)) return false;
            int duplicate = languages.FindIndex(item => string.Equals(item, language, StringComparison.OrdinalIgnoreCase));
            if (duplicate >= 0 && duplicate != index) return false;

            languages[index] = language; return true;
        }

        public bool RemoveLanguage(int index)
        {
            EnsureLocalizationData();
            if (index <= 0 || index >= languages.Count) return false;

            languages.RemoveAt(index);
            for (int i = 0; i < localizedSpeakers.Count; i++) localizedSpeakers[i].RemoveLanguage(index);

            if (nodes != null) foreach (Node graphNode in nodes)
                if (graphNode is DialogueNode dialogueNode) dialogueNode.RemoveLanguage(index);
                else if (graphNode is OptionNode optionNode) optionNode.RemoveLanguage(index);

            if (editorLanguageIndex >= index) editorLanguageIndex--;
            if (runtimeLanguageIndex >= index) runtimeLanguageIndex--;
            return true;
        }

        public bool SetEditorLanguage(int newLanguageIndex)
        {
            InitLanguages();
            if (newLanguageIndex < 0 || newLanguageIndex >= languages.Count) return false;
            editorLanguageIndex = newLanguageIndex; return true;
        }

        public string[] GetSpeakerNames()
        {
            InitSpeakers();
            string[] names = new string[speakers.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = editorLanguageIndex == 0 ? speakers[i] ?? string.Empty : localizedSpeakers[i].Get(editorLanguageIndex, speakers[i]);
            return names;
        }

        public void SetSpeakerName(int speakerIndex, string value)
        {
            InitSpeakers();
            if (speakerIndex < 0 || speakerIndex >= speakers.Count) return;
            if (editorLanguageIndex == 0) speakers[speakerIndex] = value;
            else localizedSpeakers[speakerIndex].Set(editorLanguageIndex, value);
        }

        public void AddSpeaker()
        {
            InitSpeakers();
            speakers.Add("Speaker " + (speakers.Count + 1));
            LocalizedText localized = new LocalizedText(); localized.EnsureLanguageCount(languages.Count);
            localizedSpeakers.Add(localized);
        }

        public void RemoveSpeaker(int speakerIndex)
        {
            InitSpeakers();
            if (speakerIndex < 0 || speakerIndex >= speakers.Count) return;
            speakers.RemoveAt(speakerIndex);
            localizedSpeakers.RemoveAt(speakerIndex);
            if (nodes == null) return;
            foreach (Node graphNode in nodes)
            {
                if (!(graphNode is DialogueNode dialogueNode)) continue;
                for (int i = 0; i < dialogueNode.dialogueList.Count; i++)
                {
                    if (dialogueNode.dialogueList[i].person > speakerIndex) dialogueNode.dialogueList[i].person--;
                    else if (dialogueNode.dialogueList[i].person == speakerIndex)
                        dialogueNode.dialogueList[i].person = speakers.Count == 0 ? 0 : Mathf.Clamp(speakerIndex, 0, speakers.Count - 1);
                }
            }
        }

        public DataType Next(int num = -1)
        {
            if (0 <= num) index = num;
            if (!init) Init(); //Find the start node
            else if (dataType != DataType.End) MoveOn();
            GetInfo(); return dataType;
        }

        void Init()
        {
            EnsureLocalizationData(); init = true;
            for (int i = 0; i < nodes.Count; i++)
            {
                bool isStartNode = true;
                if (nodes[i] is DialogueNode dNode) dNode.activeIndex = -1;
                else if (nodes[i] is OptionNode oNode) oNode.isActive = false;
                foreach (NodePort port in nodes[i].Inputs) isStartNode &= !port.IsConnected;
                if (isStartNode) node = nodes[i];
            }
            if (node is DialogueNode dialogueNode) dialogueNode.activeIndex = 0;
            else if (node is OptionNode optionNode) optionNode.isActive = true;

        }

        void GetInfo()
        {
            if (node is DialogueNode dialogueNode && index < dialogueNode.dialogueList.Count)
            {
                int person = dialogueNode.dialogueList[index].person;
                dialogueInfo.sprite = dialogueNode.dialogueList[index].sprite;
                dialogueInfo.name = person < 0 || person >= speakers.Count ? string.Empty : runtimeLanguageIndex == 0
                    ? speakers[person] ?? string.Empty : localizedSpeakers[person].Get(runtimeLanguageIndex, speakers[person]);
                dialogueInfo.context = dialogueNode.dialogueList[index].GetContext(runtimeLanguageIndex);
                dataType = DataType.Dialogue;
            }
            else if (node is OptionNode optionNode && index == 0)
            {
                optionInfo = optionNode.GetOptions(runtimeLanguageIndex);
                dataType = DataType.Option;
            }
            else dataType = DataType.End;
        }

        void MoveOn()
        {
            do
            {
                switch (node)
                {
                    case DialogueNode dialogueNode:
                        dialogueNode.activeIndex = -1;
                        if (dialogueNode.dialogueList[index].type >= DialogueNode.PortType.Output) GetNodeAndIndex("dialogueList " + index);
                        else if (++index >= dialogueNode.dialogueList.Count) node = null;
                        else dialogueNode.activeIndex = index;
                        break;
                    case OptionNode optionNode:
                        optionNode.isActive = false;
                        if (index >= optionNode.optionList.Count) node = null;
                        else GetNodeAndIndex("optionList " + index);
                        break;
                    case EventNode eventNode:
                        index = eventNode.Invoke(index); GetNodeAndIndex("eventList " + index);
                        break;
                    case CheckNode checkNode:
                        GetNodeAndIndex(checkNode.GetNextStr());
                        break;
                }
            } while (node is EventNode || node is CheckNode);
        }

        void GetNodeAndIndex(string s)
        {
            NodePort port = string.IsNullOrEmpty(s) ? null : node.GetOutputPort(s);
            if (port == null || port.Connection == null) node = null;
            else
            {
                port = port.Connection; node = port.node;
                string[] names = port.fieldName.Split(' ');
                index = 0; int.TryParse(names[names.Length - 1], out index);
                if (node is DialogueNode dialogueNode) dialogueNode.activeIndex = 0;
                else if (node is OptionNode optionNode) optionNode.isActive = true; 
            }
        }
    }

    [Serializable]
    public class LocalizedText
    {
        [SerializeField] List<string> translations = new List<string>();

        public string Get(int languageIndex, string fallback)
        {
            if (languageIndex <= 0) return fallback ?? string.Empty;
            int translationIndex = languageIndex - 1;
            if (translations != null && translationIndex < translations.Count && !string.IsNullOrEmpty(translations[translationIndex]))
                return translations[translationIndex];
            return fallback ?? string.Empty;
        }

        public void Set(int languageIndex, string value)
        {
            if (languageIndex <= 0) return;
            EnsureLanguageCount(languageIndex + 1); translations[languageIndex - 1] = value;
        }

        public void EnsureLanguageCount(int languageCount)
        {
            if (translations == null) translations = new List<string>();
            int translationCount = Mathf.Max(0, languageCount - 1);
            while (translations.Count < translationCount) translations.Add(string.Empty);
        }

        public void RemoveLanguage(int languageIndex)
        {
            if (languageIndex <= 0 || translations == null) return;
            int translationIndex = languageIndex - 1;
            if (translationIndex < translations.Count) translations.RemoveAt(translationIndex);
        }
    }

    public static class SceneObjectReference
    {
        public static GameObject Find(GameObject obj, string path)
        {
            if (obj != null) return obj;
            if (string.IsNullOrEmpty(path)) return null;
            string[] names = path.Split('/');
            if (names.Length < 2) return null;
            Scene scene = SceneManager.GetSceneByName(names[0]);
            if (!scene.IsValid() || !scene.isLoaded) return null;

            Transform trans = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == names[1]) { trans = root.transform; break; }
            for (int i = 2; trans != null && i < names.Length; i++) trans = trans.Find(names[i]);
            return trans == null ? null : trans.gameObject;
        }
    }
}