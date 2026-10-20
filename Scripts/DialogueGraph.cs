namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using XNode;

    [CreateAssetMenu(fileName = "New Dialogue Graph", menuName = "Dialogue Graph")]
    public class DialogueGraph : NodeGraph
    {
        public enum DataType { End, Dialogue, Option }
        public struct DialogueInfo { public Sprite sprite; public string name, context; }
        [HideInInspector] public List<LocalizedText> speakers = new List<LocalizedText>();
        [HideInInspector] public BaseNode entryNode;
#if UNITY_EDITOR
        [HideInInspector] public UnityEditor.SceneAsset editorScene;
#endif

        [NonSerialized] public DataType dataType;
        [NonSerialized] public DialogueInfo dialogueInfo;
        [NonSerialized] public List<string> optionInfo;
        [NonSerialized] public BaseNode node;
        [NonSerialized] public int index;
        [NonSerialized] public DialogueSystemBase system; // Resolves Event/Check targets for this instance

        // Graphs that have started, so editors can show where each one is.
        public static readonly List<DialogueGraph> Running = new List<DialogueGraph>();

        bool init;

        public DataType Next(int num = -1)
        {
            if (0 <= num) index = num;
            if (!init) { node = entryNode; index = 0; init = true; Running.Add(this); }
            else if (dataType != DataType.End) MoveOn();
            if (node is EventNode || node is CheckNode) MoveOn();
            GetInfo(); return dataType;
        }

        public void Stop() => node = null;

        protected override void OnDestroy()
        {
            Running.Remove(this);
            base.OnDestroy();
        } // Called by events that switch to another graph

        public void GetInfo()
        {
            int language = DialogueSettings.Language;
            if (node is DialogueNode dialogueNode)
            {
                DialogueNode.DialogueInfo line = dialogueNode.dialogueList[index];
                dialogueInfo.sprite = line.sprite;
                dialogueInfo.name = line.person < 0 ? "" : speakers[line.person].Get(language);
                dialogueInfo.context = line.context.Get(language);
                dataType = DataType.Dialogue;
            }
            else if (node is OptionNode optionNode)
            {
                optionInfo = optionNode.optionList.ConvertAll(option => option.Get(language));
                dataType = DataType.Option;
            }
            else dataType = DataType.End;
        }

        void MoveOn()
        {
            do
            {
                BaseNode current = node;
                int item = index;
                string output = null;
                try
                {
                    if (current is EventNode eventNode) eventNode.eventList[index].Invoke(system);
                    else if (current is CheckNode checkNode) output = checkNode.Next(system);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Dialogue graph '" + name + "', node '" + current.name +
                        "' (#" + nodes.IndexOf(current) + "), item " + item + ": " + (e.InnerException ?? e).Message, e);
                }
                if (node == null) return;
                if (current is CheckNode) Follow(output);
                else if (current.HasOutput(index)) Follow(current.OutputName(index));
                else if (++index >= current.Count) node = null;
            } while (node is EventNode || node is CheckNode);
        }

        void Follow(string output)
        {
            NodePort input = output == null ? null : node.GetOutputPort(output).Connection;
            node = input == null ? null : (BaseNode)input.node;
            index = input == null ? 0 : Mathf.Max(0, BaseNode.Item(input));
        }

        public override Node AddNode(Type type)
        {
            Node added = base.AddNode(type);
            if (entryNode == null) entryNode = (BaseNode)added;
            return added;
        }

        public override void RemoveNode(Node removed)
        {
            if (entryNode == removed) entryNode = null;
            base.RemoveNode(removed);
        }
    }
}
