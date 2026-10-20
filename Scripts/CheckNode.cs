namespace DialogueEditor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    [NodeWidth(350)] public class CheckNode : BaseNode
    {
        public enum CheckType { Equal, NotEqual, Greater, Less }
        [Serializable] public class CheckInfo
        {
            public CheckType checkType;
            public string checkNum;
        }

        [HideInInspector] public string bindingKey, compName, varName, varType;
        [HideInInspector] public List<CheckInfo> checkList = new List<CheckInfo>();

        public override string ListName => nameof(checkList);
        public override IList Items => checkList;

        public string Next(DialogueSystemBase system)
        {
            Component component = DialogueReflection.FindComponent(system, bindingKey, compName);
            FieldInfo field = component.GetType().GetField(varName);
            if (field == null) throw new MissingFieldException(compName, varName);
            IComparable value = (IComparable)field.GetValue(component);
            for (int i = 0; i < checkList.Count; i++)
            {
                int compare = value.CompareTo(DialogueReflection.Parse(checkList[i].checkNum, field.FieldType));
                CheckType type = checkList[i].checkType;
                if (type == CheckType.Equal && compare == 0 || type == CheckType.NotEqual && compare != 0 ||
                    type == CheckType.Greater && compare > 0 || type == CheckType.Less && compare < 0)
                    return OutputName(i);
            }
            return null;
        }
    }
}
