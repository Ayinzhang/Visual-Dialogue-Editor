namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using XNode;

    [Serializable, CreateAssetMenu(fileName = "New Dialogue Graph", menuName = "Dialogue Graph")]
    public class DialogueGraph : NodeGraph
    {
        public enum DataType { End, Dialogue, Option }
        public struct DialogueInfo { public Sprite sprite; public string name, context; }
        [HideInInspector] public List<LocalizedText> speakers = new List<LocalizedText>();
        [HideInInspector] public DataType dataType;
        [HideInInspector] public DialogueInfo dialogueInfo;
        [HideInInspector] public List<string> optionInfo;
        [HideInInspector] public Node node, entryNode;
        [HideInInspector] public int index;

        bool init;

        public void Stop()
        {
            node = null; dataType = DataType.End; init = true;
        }
        public DataType Next(int num = -1)
        {
            if (0 <= num) index = num;
            if (!init) { node = entryNode; index = 0; init = true; }
            else if (dataType != DataType.End) MoveOn();
            if (node is EventNode || node is CheckNode) MoveOn();
            GetTypeInfo(); return dataType;
        }

        public void GetTypeInfo()
        {
            int language = DialogueSettings.Language;
            if (node is DialogueNode dialogueNode)
            {
                DialogueNode.DialogueInfo line = dialogueNode.dialogueList[index];
                dialogueInfo.sprite = line.sprite;
                dialogueInfo.name = line.person < 0 ? string.Empty : GetSpeaker(line.person, language);
                dialogueInfo.context = line.context.Get(language);
                dataType = DataType.Dialogue;
            }
            else if (node is OptionNode optionNode)
            {
                optionInfo = optionNode.GetOptions(language);
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
            NodePort output = node.GetOutputPort(name);
            if (output == null) throw new InvalidOperationException("Output port is missing: " + name);
            NodePort input = output.Connection;
            int nextIndex = 0;
            if (input != null && !DialogueValidation.TryDestination(input, out nextIndex, out string error))
                throw new InvalidOperationException(error);
            node = input?.node;
            index = nextIndex;
        }

        public override Node AddNode(Type type)
        {
            bool first = nodes.Count == 0;
            Node added = base.AddNode(type);
            if (first) entryNode = added;
            return added;
        }

        public override void RemoveNode(Node removed)
        {
            if (entryNode == removed) entryNode = null;
            base.RemoveNode(removed);
        }

        public void SetEntry(Node value)
        {
            if (value != null && !nodes.Contains(value)) throw new ArgumentException("Entry must belong to this graph.");
            entryNode = value;
        }

        public string GetSpeaker(int speaker, int language)
        {
            return speakers[speaker].Get(language);
        }

        public string GetRawSpeaker(int speaker, int language)
        { return speakers[speaker].GetRaw(language); }

        public void EnsureLocalizationData()
        {
            int count = DialogueSettings.Instance.languages.Count;
            foreach (LocalizedText speaker in speakers) speaker.EnsureLanguageCount(count);
            foreach (Node graphNode in nodes)
                if (graphNode is DialogueNode dialogueNode) dialogueNode.EnsureLocalizationData(count);
                else if (graphNode is OptionNode optionNode) optionNode.EnsureLocalizationData(count);
        }

        public void RemoveLanguage(int index)
        {
            if (index == 0) throw new InvalidOperationException("The default language cannot be removed.");
            foreach (LocalizedText speaker in speakers) speaker.RemoveLanguage(index);

            foreach (Node graphNode in nodes)
                if (graphNode is DialogueNode dialogueNode) dialogueNode.RemoveLanguage(index);
                else if (graphNode is OptionNode optionNode) optionNode.RemoveLanguage(index);
        }

        public string[] GetSpeakerNames(int language)
        {
            string[] names = new string[speakers.Count];
            for (int i = 0; i < names.Length; i++) names[i] = GetSpeaker(i, language);
            return names;
        }

        public void SetSpeakerName(int speakerIndex, int language, string value)
        {
            speakers[speakerIndex].Set(language, value);
        }

        public void AddSpeaker(string name)
        {
            LocalizedText localized = new LocalizedText(); localized.EnsureLanguageCount(DialogueSettings.Instance.languages.Count);
            localized.Set(0, name);
            speakers.Add(localized);
        }

        public void RemoveSpeaker(int speakerIndex)
        {
            speakers.RemoveAt(speakerIndex);
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

        public void MoveLanguage(int from, int to)
        {
            int count = DialogueSettings.Instance.languages.Count;
            if (from <= 0 || to <= 0 || from >= count || to >= count)
                throw new ArgumentOutOfRangeException(nameof(from), "Only non-default language indices can be reordered.");
            foreach (LocalizedText speaker in speakers) speaker.MoveLanguage(from, to);
            foreach (Node graphNode in nodes)
                if (graphNode is DialogueNode dialogueNode) dialogueNode.MoveLanguage(from, to);
                else if (graphNode is OptionNode optionNode) optionNode.MoveLanguage(from, to);
        }

        public void MoveSpeaker(int from, int to)
        {
            LocalizedText speaker = speakers[from];
            speakers.RemoveAt(from); speakers.Insert(to, speaker);
            foreach (Node graphNode in nodes)
                if (graphNode is DialogueNode dialogueNode)
                    foreach (DialogueNode.DialogueInfo line in dialogueNode.dialogueList)
                        line.person = ReorderedIndex(line.person, from, to);
        }

        static int ReorderedIndex(int index, int from, int to)
        {
            if (index == from) return to;
            if (from < to && index > from && index <= to) return index - 1;
            if (from > to && index >= to && index < from) return index + 1;
            return index;
        }
    }
}