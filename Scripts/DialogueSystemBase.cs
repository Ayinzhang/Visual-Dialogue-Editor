namespace DialogueEditor
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    [DisallowMultipleComponent]
    public abstract class DialogueSystemBase : MonoBehaviour
    {
        [SerializeField, HideInInspector] DialogueBindingProfile bindings = new DialogueBindingProfile();
        public DialogueBindingProfile Bindings => bindings;

        public GameObject cha, bg;
        public GameObject[] options;
        public DialogueGraph[] graphs;
        [NonSerialized] public DialogueGraph graph;

        protected Image chaImg;
        protected Text bgText;
        protected Text[] optionTexts;

        protected virtual void OnEnable() => DialogueSettings.LanguageChanged += Refresh;
        protected virtual void OnDisable() => DialogueSettings.LanguageChanged -= Refresh;
        protected virtual void OnDestroy() => Release();

        protected virtual void Start()
        {
            chaImg = cha.GetComponent<Image>();
            bgText = bg.GetComponentInChildren<Text>(true);
            optionTexts = new Text[options.Length];
            for (int i = 0; i < options.Length; i++) optionTexts[i] = options[i].GetComponentInChildren<Text>(true);
            Init();
        }

        public virtual void Init(int graphIndex = 0)
        {
            Release();
            graph = Instantiate(graphs[graphIndex]);
            graph.system = this; Next();
        }

        void Release()
        {
            if (graph != null) { graph.Stop(); Destroy(graph); }
        }

        public virtual void Next(int num = -1)
        {
            if (graph.dataType == DialogueGraph.DataType.Option && num == -1) return;
            graph.Next(num); DrawInfo();
        }

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
                for (int i = 0; i < graph.optionInfo.Count; i++) optionTexts[i].text = graph.optionInfo[i];
        }
    }
}
