using UnityEngine;
using UnityEngine.UI;

namespace DialogueEditor
{
    public class DialogueSystem : MonoBehaviour
    {
        public GameObject cha, bg;
        public GameObject[] options;
        public DialogueGraph[] graphs;
        [System.NonSerialized] public DialogueGraph graph;
        [HideInInspector] public int cnt; // cnt is for the Demo check node

        Image chaImg;
        Text bgText;
        Text[] optionTexts;

        void OnEnable() => DialogueSettings.LanguageChanged += Refresh;
        void OnDisable() => DialogueSettings.LanguageChanged -= Refresh;

        void Start()
        {
            chaImg = cha.GetComponent<Image>();
            bgText = bg.GetComponentInChildren<Text>(true);
            optionTexts = new Text[options.Length];
            for (int i = 0; i < options.Length; i++) optionTexts[i] = options[i].GetComponentInChildren<Text>(true);
            Init();
        }

        public void Init(int graphIndex = 0)
        {
            if (graph != null) { graph.Stop(); Destroy(graph); }
            graph = Instantiate(graphs[graphIndex]); Next();
        }

        public void Next(int num = -1)
        {
            if (graph.dataType == DialogueGraph.DataType.Option && num == -1) return;
            graph.Next(num); DrawInfo();
        }

        void Refresh()
        {
            if (graph == null) return;
            graph.GetInfo(); DrawInfo();
        }

        void DrawInfo()
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

        void OnDestroy() => Destroy(graph);

        public void DemoPrint(int num) => Debug.Log(cnt += num, this);
    }
}
