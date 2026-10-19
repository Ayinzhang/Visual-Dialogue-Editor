namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;
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
        [SerializeField, HideInInspector] Node entryNode;
        [SerializeField, HideInInspector] List<NodeIdentity> nodeIdentities = new List<NodeIdentity>();
        [SerializeField, HideInInspector] List<string> speakerIds = new List<string>();
        [Serializable] class NodeIdentity { public Node node; public string id; }

        [HideInInspector] public DataType dataType;
        [HideInInspector] public DialogueInfo dialogueInfo;
        [HideInInspector] public List<string> optionInfo;
        [HideInInspector] public Node node;
        [HideInInspector] public int index, languageIndex;
        bool init;

        public Node EntryNode => entryNode;

        public DataType Next(int num = -1)
        {
            if (0 <= num) index = num;
            if (!init) Init();
            else if (dataType != DataType.End) MoveOn();
            if (node is EventNode || node is CheckNode) MoveOn();
            GetInfo(); return dataType;
        }

        public void Stop()
        {
            node = null; dataType = DataType.End; init = true;
        }

        void Init()
        {
            node = entryNode; index = 0; init = true;
        }

        void GetInfo()
        {
            if (node is DialogueNode dialogueNode)
            {
                DialogueNode.DialogueInfo line = dialogueNode.dialogueList[index];
                dialogueInfo.sprite = line.sprite;
                dialogueInfo.name = line.person < 0 ? string.Empty : GetSpeaker(line.person, languageIndex);
                dialogueInfo.context = line.GetContext(languageIndex);
                dataType = DataType.Dialogue;
            }
            else if (node is OptionNode optionNode)
            {
                optionInfo = optionNode.GetOptions(languageIndex);
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
                        if (DialogueValidation.HasOutput(node, index)) GetNodeAndIndex("dialogueList " + index);
                        else if (++index >= dialogueNode.dialogueList.Count) node = null;
                        break;
                    case OptionNode:
                        GetNodeAndIndex("optionList " + index);
                        break;
                    case EventNode eventNode:
                        eventNode.eventList[index].InvokeMethod();
                        if (node == null) return; // An event may have switched graphs and stopped this instance.
                        if (DialogueValidation.HasOutput(node, index)) GetNodeAndIndex("eventList " + index);
                        else if (++index >= eventNode.eventList.Count) node = null;
                        break;
                    case CheckNode checkNode:
                        GetNodeAndIndex(checkNode.GetNextStr());
                        break;
                }
            } while (node is EventNode || node is CheckNode);
        }

        void GetNodeAndIndex(string name)
        {
            if (name == null) { node = null; return; }
            NodePort input = node.GetOutputPort(name).Connection;
            node = input?.node;
            index = input == null || input.fieldName == "before" ? 0 : int.Parse(input.fieldName.Substring(7));
        }

        public override Node AddNode(Type type)
        {
            bool first = nodes.Count == 0;
            Node added = base.AddNode(type);
            nodeIdentities.Add(new NodeIdentity { node = added, id = Guid.NewGuid().ToString("N") });
            if (first) entryNode = added;
            return added;
        }

        public override Node CopyNode(Node original)
        {
            Node copy = base.CopyNode(original);
            nodeIdentities.Add(new NodeIdentity { node = copy, id = Guid.NewGuid().ToString("N") });
            return copy;
        }

        public override void RemoveNode(Node removed)
        {
            if (entryNode == removed) entryNode = null;
            nodeIdentities.RemoveAll(item => item.node == removed);
            base.RemoveNode(removed);
        }

        public void SetEntry(Node value)
        {
            if (value != null && !nodes.Contains(value)) throw new ArgumentException("Entry must belong to this graph.");
            entryNode = value;
        }

        public void EnsureIdentifiers()
        {
            nodeIdentities.RemoveAll(item => item.node == null || !nodes.Contains(item.node));
            foreach (Node item in nodes)
                if (item != null && !nodeIdentities.Exists(identity => identity.node == item))
                    nodeIdentities.Add(new NodeIdentity { node = item, id = Guid.NewGuid().ToString("N") });
            while (speakerIds.Count < speakers.Count) speakerIds.Add(Guid.NewGuid().ToString("N"));
            while (speakerIds.Count > speakers.Count) speakerIds.RemoveAt(speakerIds.Count - 1);
            foreach (Node item in nodes)
            {
                if (item is EventNode events)
                    foreach (EventNode.FuncInfo info in events.eventList)
                        if (info != null && string.IsNullOrEmpty(info.id)) info.id = Guid.NewGuid().ToString("N");
                if (item is CheckNode check)
                    foreach (CheckNode.CheckInfo info in check.checkList)
                        if (info != null && string.IsNullOrEmpty(info.id)) info.id = Guid.NewGuid().ToString("N");
            }
        }

        public string GetNodeId(Node value) => nodeIdentities.Find(item => item.node == value)?.id;
        public void SetNodeId(Node value, string id)
        {
            NodeIdentity identity = nodeIdentities.Find(item => item.node == value);
            if (identity == null) throw new ArgumentException("Node is not registered.");
            identity.id = id;
        }
        public string GetSpeakerId(int value) => speakerIds[value];
        public void SetSpeakerId(int value, string id) { speakerIds[value] = id; }
        public string GetRawSpeaker(int speaker, int language)
        { return language == 0 ? speakers[speaker] : localizedSpeakers[speaker].GetRaw(language); }
        public string GetSpeaker(int speaker, int language)
        {
            string fallback = speakers[speaker] ?? string.Empty;
            return language <= 0 || localizedSpeakers == null || speaker >= localizedSpeakers.Count || localizedSpeakers[speaker] == null ?
                fallback : localizedSpeakers[speaker].Get(language, fallback);
        }
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
            EnsureIdentifiers();
        }

        void InitLanguages()
        {
            if (languages == null) languages = new List<string>();
            if (languages.Count == 0) languages.Add("Default");
            editorLanguageIndex = Mathf.Clamp(editorLanguageIndex, 0, languages.Count - 1);
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
            EnsureIdentifiers();
        }

        public void RemoveSpeaker(int speakerIndex)
        {
            InitSpeakers();
            EnsureIdentifiers();
            if (speakerIndex < 0 || speakerIndex >= speakers.Count) return;
            speakers.RemoveAt(speakerIndex);
            speakerIds.RemoveAt(speakerIndex);
            localizedSpeakers.RemoveAt(speakerIndex);
            if (nodes == null) return;
            foreach (Node graphNode in nodes)
            {
                if (!(graphNode is DialogueNode dialogueNode)) continue;
                for (int i = 0; i < dialogueNode.dialogueList.Count; i++)
                {
                    if (dialogueNode.dialogueList[i].person > speakerIndex) dialogueNode.dialogueList[i].person--;
                    else if (dialogueNode.dialogueList[i].person == speakerIndex)
                        dialogueNode.dialogueList[i].person = -1;
                }
            }
        }

    }

    [Serializable]
    public class LocalizedText
    {
        [SerializeField] List<string> translations = new List<string>();
        [SerializeField] List<bool> translated = new List<bool>();

        public string Get(int languageIndex, string fallback)
        {
            return GetRaw(languageIndex) ?? fallback ?? string.Empty;
        }

        public void Set(int languageIndex, string value)
        {
            if (languageIndex <= 0) return;
            EnsureLanguageCount(languageIndex + 1);
            translations[languageIndex - 1] = value ?? string.Empty;
            translated[languageIndex - 1] = value != null;
        }

        public string GetRaw(int languageIndex)
        {
            int index = languageIndex - 1;
            if (index < 0 || translations == null || index >= translations.Count) return null;
            bool exists = translated != null && index < translated.Count ? translated[index] : !string.IsNullOrEmpty(translations[index]);
            return exists ? translations[index] ?? string.Empty : null;
        }

        public void EnsureLanguageCount(int languageCount)
        {
            if (translations == null) translations = new List<string>();
            if (translated == null) translated = new List<bool>();
            while (translated.Count < translations.Count)
                translated.Add(!string.IsNullOrEmpty(translations[translated.Count]));
            int translationCount = Mathf.Max(0, languageCount - 1);
            while (translations.Count < translationCount)
            { translations.Add(string.Empty); translated.Add(false); }
        }

        public void RemoveLanguage(int languageIndex)
        {
            if (languageIndex <= 0 || translations == null) return;
            int translationIndex = languageIndex - 1;
            if (translationIndex < translations.Count)
            {
                EnsureLanguageCount(0);
                translations.RemoveAt(translationIndex); translated.RemoveAt(translationIndex);
            }
        }
    }

    public static class SceneObjectReference
    {
        public static bool SceneUnloaded(string path, string id)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(id) && UnityEditor.GlobalObjectId.TryParse(id, out UnityEditor.GlobalObjectId globalId))
            {
                string scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(globalId.assetGUID.ToString());
                if (scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    return !SceneManager.GetSceneByPath(scenePath).isLoaded;
                if (globalId.identifierType == 2 && string.IsNullOrEmpty(scenePath)) return false;
            }
#endif
            return !string.IsNullOrEmpty(path) && !SceneManager.GetSceneByName(path.Split('/')[0]).isLoaded;
        }

        public static bool TryFind(GameObject obj, string path, string id, out GameObject found, out string error)
        {
            found = obj; error = null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(id))
            {
                if (!UnityEditor.GlobalObjectId.TryParse(id, out UnityEditor.GlobalObjectId globalId))
                { error = "Invalid GlobalObjectId: " + id; found = null; return false; }
                var identified = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                if (identified != null) found = identified;
            }
#endif
            if (string.IsNullOrEmpty(path))
            {
                if (found != null) return true;
                error = "Target object is missing."; return false;
            }
            string[] names = path.Split('/');
            if (names.Length < 2) { error = "Invalid scene object path: " + path; found = null; return false; }
            Scene scene = SceneManager.GetSceneByName(names[0]);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (found != null) return true;
                error = "Target scene is not loaded: " + names[0]; return false;
            }

            Transform trans = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == names[1])
                {
                    if (trans != null) { error = "Ambiguous target path: " + path; found = null; return false; }
                    trans = root.transform;
                }
            for (int i = 2; trans != null && i < names.Length; i++)
            {
                Transform child = null;
                foreach (Transform candidate in trans)
                    if (candidate.name == names[i])
                    {
                        if (child != null) { error = "Ambiguous target path: " + path; found = null; return false; }
                        child = candidate;
                    }
                trans = child;
            }
            if (trans != null && found != null && trans.gameObject != found)
            { error = "Object ID/reference and hierarchy path point to different objects: " + path; found = null; return false; }
            if (found == null && trans != null) found = trans.gameObject;
            if (found != null) return true;
            error = "Missing target: " + path; return false;
        }
    }
}