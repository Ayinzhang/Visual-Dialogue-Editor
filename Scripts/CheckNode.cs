namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    [Serializable, NodeWidth(350)]
    public class CheckNode : Node
    {
        public enum CheckType { Equal, NotEqual, Greater, Less }
        [Serializable] public class CheckInfo
        {
            public CheckType checkType;
            public string checkNum;
        }
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        public GameObject obj;
        [HideInInspector] public string objGlobalId, objPath, compName, varName, varType;
        [HideInInspector] public List<CheckInfo> checkList = new List<CheckInfo>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public string GetNextStr()
        {
            if (!DialogueValidation.TryCheck(this, out Component component, out FieldInfo field, out object[] comparisons, out string error))
                throw new InvalidOperationException(error);
            object value = field.GetValue(component);
            for (int i = 0; i < checkList.Count; i++)
            {
                int compare = value is string text ? string.Compare(text, (string)comparisons[i], StringComparison.Ordinal) :
                    value == null ? (comparisons[i] == null ? 0 : -1) : ((IComparable)value).CompareTo(comparisons[i]);
                CheckType condition = checkList[i].checkType;
                if (condition == CheckType.Equal && compare == 0 || condition == CheckType.NotEqual && compare != 0 ||
                    condition == CheckType.Greater && compare > 0 || condition == CheckType.Less && compare < 0)
                    return "checkList " + i;
            }
            return null;
        }
    }
}
