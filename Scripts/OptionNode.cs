namespace DialogueEditor
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class OptionNode : BaseNode
    {
        [HideInInspector] public List<LocalizedText> optionList = new List<LocalizedText>();

        public override string ListName => nameof(optionList);
        public override IList Items => optionList;
    }
}
