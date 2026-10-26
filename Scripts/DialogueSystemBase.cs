namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    [DisallowMultipleComponent] public class DialogueSystemBase : MonoBehaviour
    {
        public GameObject cha, bg;
        public GameObject[] options;
        public DialogueGraph[] graphs;
        [NonSerialized] public DialogueGraph graph;
        [HideInInspector] public DialogueBinding bindings = new();

        protected Image chaImg;
        protected Text bgText;
        protected Text[] optionTexts;
        readonly Stack<DialogueGraph> graphStack = new(); // Graphs waiting for switched graphs to end

        protected virtual void OnEnable() => DialogueSettings.LanguageChanged += Refresh;
        protected virtual void OnDisable() => DialogueSettings.LanguageChanged -= Refresh;

        protected virtual void Start()
        {
            chaImg = cha.GetComponent<Image>();
            bgText = bg.GetComponentInChildren<Text>(true);
            optionTexts = new Text[options.Length];
            for (int i = 0; i < options.Length; i++) optionTexts[i] = options[i].GetComponentInChildren<Text>(true);
            Switch(); Next();
        }

        protected virtual void OnDestroy()
        {
            if (graph != null) { graph.node = null; graph.dataType = DialogueGraph.DataType.End; Destroy(graph); }
            while (graphStack.Count > 0) Destroy(graphStack.Pop());
        }

        public virtual void Switch(int graphIndex = 0) // Switch graphs without advancing
        {
            if (graph != null) graphStack.Push(graph);
            graph = Instantiate(graphs[graphIndex]);
            graph.system = this;
        }

        public virtual void Next(int num = -1) // Proceed to the next node in the current graph
        {
            if (graph.dataType == DialogueGraph.DataType.Option && num == -1) return;
            bool resume = false;
            while (true)
            {
                DialogueGraph current = graph;
                if (resume) current.Resume();
                else current.Next(num);
                num = -1; resume = false;

                if (graph != current) continue;
                if (graph.dataType != DialogueGraph.DataType.End || graphStack.Count == 0) break;

                Destroy(graph);
                graph = graphStack.Pop();
                resume = true;
            }
            DrawInfo();
        }

        public void SetLanguage(int index) => DialogueSettings.LanguageIndex = index;

        void Refresh()
        {
            if (graph == null) return;
            graph.GetInfo(); DrawInfo();
        }

        protected virtual void DrawInfo()
        {
            DialogueGraph.DataType type = graph.dataType;
            cha.SetActive(type != DialogueGraph.DataType.End);
            bg.SetActive(type != DialogueGraph.DataType.End);
            for (int i = 0; i < options.Length; i++)
                options[i].SetActive(type == DialogueGraph.DataType.Option && i < graph.optionInfo.Count);
            if (type == DialogueGraph.DataType.Dialogue)
            {
                chaImg.sprite = graph.dialogueInfo.sprite;
                bgText.text = graph.dialogueInfo.name + ":\n" + graph.dialogueInfo.context;
            }
            else if (type == DialogueGraph.DataType.Option)
                for (int i = 0; i < optionTexts.Length && i < graph.optionInfo.Count; i++) 
                    optionTexts[i].text = graph.optionInfo[i];
        }
    }
}
