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
        Text[] optionTests;
        [SerializeField] int languageIndex;
        bool option;

        void Start()
        {
            chaImg = cha.GetComponent<Image>();
            bgText = bg.GetComponentInChildren<Text>(true);
            optionTests = new Text[options.Length];
            for (int i = 0; i < options.Length; i++) optionTests[i] = options[i].GetComponentInChildren<Text>(true);
            Init();
        }

        public void Init(int graphIndex = 0)
        {
            if (graph != null) { graph.Stop(); Destroy(graph); }
            graph = Instantiate(graphs[graphIndex]);
            graph.languageIndex = languageIndex;
            cha.SetActive(true); bg.SetActive(true);
            Next();
        }

        public void Next(int num = -1)
        {
            if (option && num == -1) return;
            else if (option)
            {
                option = false;
                for (int i = 0; i < options.Length; i++) options[i].SetActive(false);
            }
            DialogueGraph current = graph;
            DialogueGraph.DataType state = current.Next(num);
            if (current != graph) return;
            switch (state)
            {
                case DialogueGraph.DataType.Dialogue:
                    chaImg.sprite = graph.dialogueInfo.sprite;
                    bgText.text = graph.dialogueInfo.name + ":\n" + graph.dialogueInfo.context;
                    break;
                case DialogueGraph.DataType.Option:
                    option = true;
                    for (int i = 0; i < options.Length; i++) options[i].SetActive(i < graph.optionInfo.Count);
                    for (int i = 0; i < graph.optionInfo.Count; i++) optionTests[i].text = graph.optionInfo[i];
                    break;
                case DialogueGraph.DataType.End:
                    cha.SetActive(false); bg.SetActive(false);
                    for (int i = 0; i < options.Length; i++) options[i].SetActive(false);
                    break;
            }
        }

        void OnDestroy()
        {
            Destroy(graph);
        }

        public void DemoPrint(int num)
        {
            Debug.Log(cnt += num, this);
        }
    }
}
