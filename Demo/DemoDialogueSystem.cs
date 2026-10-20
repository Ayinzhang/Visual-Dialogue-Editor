using UnityEngine;

namespace DialogueEditor
{
    public class DemoDialogueSystem : DialogueSystemBase
    {
        [HideInInspector] public int cnt; // cnt is for the Demo check node

        public void DemoPrint(int num) => Debug.Log(cnt += num, this);
    }
}
