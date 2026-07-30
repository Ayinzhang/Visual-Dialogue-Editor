using UnityEngine;
using UnityEngine.UI;

namespace DialogueEditor
{
    public class DialogueSystem : MonoBehaviour
    {
        public GameObject cha, bg;
        public GameObject[] options;
        public DialogueGraph graph;
        [SerializeField, HideInInspector] int languageIndex;
        [HideInInspector] public int cnt;

        Image chaImg;
        Text bgText;
        Text[] optionTests;
        DialogueGraph dialogueGraph;

        bool option;

        void Start()
        {
            chaImg = cha.GetComponent<Image>();
            bgText = bg.GetComponentInChildren<Text>();
            optionTests = new Text[options.Length];
            for (int i = 0; i < options.Length; i++) optionTests[i] = options[i].GetComponentInChildren<Text>();
            cha.SetActive(true); bg.SetActive(true);
            if (graph == null)
            {
                Debug.LogError("Dialogue graph is not assigned.", this);
                enabled = false;
                return;
            }

            dialogueGraph = Instantiate(graph);
            dialogueGraph.SetLanguage(languageIndex);
            Next();
        }

        public void Next(int num = -1)
        {
            if (option && num == -1) return;
            else if (option)
            {
                option = false; cnt += 1 - num;
                for (int i = 0; i < options.Length; i++)
                    options[i].SetActive(false);
            }

            switch (dialogueGraph.Next(num))
            {
                case DialogueGraph.DataType.Dialogue:
                    chaImg.sprite = dialogueGraph.dialogueInfo.sprite;
                    bgText.text = dialogueGraph.dialogueInfo.name + ":\n" + dialogueGraph.dialogueInfo.context;
                    break;
                case DialogueGraph.DataType.Option:
                    option = true;
                    for (int i = 0; i < options.Length; i++) options[i].SetActive(true);
                    for (int i = 0; i < dialogueGraph.optionInfo.Count; i++) optionTests[i].text = dialogueGraph.optionInfo[i];
                    break;
                case DialogueGraph.DataType.End:
                    cha.SetActive(false); bg.SetActive(false);
                    for (int i = 0; i < options.Length; i++) options[i].SetActive(false);
                    break;
            }
        }

        public void Print(int num)
        {
            Debug.Log(num, this);
        }
    }
}
