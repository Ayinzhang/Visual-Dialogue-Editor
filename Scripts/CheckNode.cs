namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    [Serializable]
    [NodeWidth(350)]
    public class CheckNode : Node
    {
        public enum CheckType { Equal, NotEqual, Greater, Less }
        [Serializable]
        public class CheckInfo
        {
            public CheckType checkType;
            public string checkNum;
        }
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        public GameObject obj;
        [HideInInspector] public string objGlobalId, objPath, compName, varName, varType;
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
        [Output(dynamicPortList = true, connectionType = ConnectionType.Multiple)]
        public List<CheckInfo> checkList = new List<CheckInfo>();

        public void RefreshInfo()
        {
            obj = SceneObjectReference.Find(obj, objPath);
            if (obj == null || string.IsNullOrEmpty(compName)) return;
            Component component = obj.GetComponent(compName);
            FieldInfo field = component?.GetType().GetField(varName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null || field.FieldType.AssemblyQualifiedName != varType) compName = varName = varType = null;
        }

        public string GetNextStr()
        {
            RefreshInfo();

            if (obj == null || string.IsNullOrEmpty(compName) || string.IsNullOrEmpty(varName))
            {
                Debug.LogWarning("GameObject or component or variable is not set");
                return null;
            }

            Component component = obj.GetComponent(compName);
            if (component == null)
            {
                Debug.LogWarning($"Component not found: {compName}");
                return null;
            }

            FieldInfo field = component.GetType().GetField(varName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogWarning($"Field not found: {varName} on component: {compName}");
                return null;
            }

            object value = field.GetValue(component);
            switch (Type.GetTypeCode(field.FieldType))
            {
                case TypeCode.Int32:
                case TypeCode.Boolean:
                    int intValue = field.FieldType == typeof(bool) && (bool)value ? 1 : Convert.ToInt32(value);
                    for (int i = 0; i < checkList.Count; i++)
                    {
                        if ((checkList[i].checkType == CheckType.Equal && intValue == int.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.NotEqual && intValue != int.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.Greater && intValue > int.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.Less && intValue < int.Parse(checkList[i].checkNum)))
                            return "checkList " + i;
                    }
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                    double doubleValue = Convert.ToDouble(value);
                    for (int i = 0; i < checkList.Count; i++)
                        if ((checkList[i].checkType == CheckType.Equal && doubleValue == double.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.NotEqual && doubleValue != double.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.Greater && doubleValue > double.Parse(checkList[i].checkNum)) ||
                           (checkList[i].checkType == CheckType.Less && doubleValue < double.Parse(checkList[i].checkNum)))
                            return "checkList " + i;
                    break;

                case TypeCode.String:
                    string text = value as string;
                    for (int i = 0; i < checkList.Count; i++)
                    {
                        int cmp = string.Compare(text, checkList[i].checkNum);
                        if ((checkList[i].checkType == CheckType.Equal && cmp == 0) ||
                           (checkList[i].checkType == CheckType.NotEqual && cmp != 0) ||
                           (checkList[i].checkType == CheckType.Greater && cmp > 0) ||
                           (checkList[i].checkType == CheckType.Less && cmp < 0))
                            return "checkList " + i;
                    }
                    break;
            }
            return null;
        }
    }

}