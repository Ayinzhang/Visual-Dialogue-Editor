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
        [HideInInspector] public NodeBase entryNode;
        [HideInInspector] public List<LocalizedText> speakers = new();
#if UNITY_EDITOR
        [HideInInspector] public UnityEditor.SceneAsset editorScene; // Open the edit scene when this graph is opened
#endif

        [NonSerialized] public DialogueSystemBase system; // Resolves Event/Check targets for this instance
        [NonSerialized] public DataType dataType;
        [NonSerialized] public DialogueInfo dialogueInfo;
        [NonSerialized] public List<string> optionInfo;
        [NonSerialized] public NodeBase node;
        [NonSerialized] public int index;

        public static readonly List<DialogueGraph> RunningGraphs = new(); // Graphs that have started, so editors can show running info

        bool init;

        public void Next(int num = -1) // -1: continue, 0+: option index
        {
            if (0 <= num) index = num;
            if (!init) { init = true; node = entryNode; index = -1; RunningGraphs.Add(this); }
            MoveOn(); GetInfo();
        }

        public void Resume() // Continue after the switched graph ends
        {
            if (node is EventNode)
                if (node.HasOutput(index)) Follow(node.OutputName(index));
                else if (++index >= node.Items.Count) node = null;
            if (node is EventNode || node is CheckNode) MoveOn();
            GetInfo();
        }

        public void GetInfo()
        {
            int languageIndex = DialogueSettings.LanguageIndex;
            if (node is DialogueNode dialogueNode)
            {
                DialogueNode.DialogueInfo lnfo = dialogueNode.dialogueList[index];
                dialogueInfo.sprite = lnfo.sprite;
                dialogueInfo.name = lnfo.person < 0 ? "" : speakers[lnfo.person].GetLanguage(languageIndex);
                dialogueInfo.context = lnfo.context.GetLanguage(languageIndex);
                dataType = DataType.Dialogue;
            }
            else if (node is OptionNode optionNode)
            {
                optionInfo = optionNode.optionList.ConvertAll(option => option.GetLanguage(languageIndex));
                dataType = DataType.Option;
            }
            else dataType = DataType.End;
        }

        void MoveOn()
        {
            if (index < 0) { index = 0; if (node is not EventNode && node is not CheckNode) return; }

            do
            {
                if (node is EventNode eventNode) eventNode.eventList[index].Invoke(system);
                if (!node || system.graph != this) return;

                if (node is CheckNode checkNode) Follow(checkNode.Next(system));
                else if (node.HasOutput(index)) Follow(node.OutputName(index));
                else if (++index >= node.Items.Count) node = null;
            } while (node is EventNode || node is CheckNode);
        }

        void Follow(string output)
        {
            NodePort input = output == null ? null : node.GetOutputPort(output).Connection;
            node = input == null ? null : (NodeBase)input.node;
            index = input == null ? 0 : Mathf.Max(0, NodeBase.ItemIndex(input));
        }

        public override Node AddNode(Type type)
        {
            Node added = base.AddNode(type);
            if (entryNode == null) entryNode = (NodeBase)added;
            return added;
        }

        public override void RemoveNode(Node removed)
        {
            if (entryNode == removed) entryNode = null;
            base.RemoveNode(removed);
        }

        protected override void OnDestroy()
        {
            RunningGraphs.Remove(this);
            base.OnDestroy();
        }
    }
}
