using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using XNode;
using System.Linq;

[Serializable][NodeWidth(350)]
public class CheckNode : Node
{
    public enum CheckType { Equal, NotEqual, Greater, Less}
    [Serializable] public class CheckInfo
    {
        public CheckType checkType;
        public string checkNum;
    }
    [Input] public Nothing before;
    [HideInInspector] public bool isMin;
    [HideInInspector] public string abstruct;
    public GameObject obj;
    [HideInInspector] public int objInstanceID;
    [HideInInspector] public string objPath, compName, varName, varType;
    [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
    [Output(dynamicPortList = true, connectionType = ConnectionType.Multiple)] 
    public List<CheckInfo> checkList = new List<CheckInfo>();

    public void RefreshInfo()
    {
        UnityEngine.Object o = EditorUtility.InstanceIDToObject(objInstanceID);
        if (o is GameObject go) obj = go;
        else if (obj == null && objPath != null)
        {
            string[] names = objPath.Split('/');
            Scene scene = SceneManager.GetSceneByName(names[0]);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                string scenePath = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(path => path.EndsWith(names[0] + ".unity"));
                if (!string.IsNullOrEmpty(scenePath)) scene = EditorSceneManager.OpenScene(scenePath);
            }

            Transform trans = null;
            for (int i = 1; i < names.Length; i++)
            {
                trans = i == 1 ? GameObject.Find(names[i])?.transform : trans.Find(names[i]);
                if (trans == null) break;
            }
            if (trans != null) obj = trans.gameObject;
        }

        if (obj != null)
        {
            Component component = obj.GetComponent(compName);
            if (component != null)
            {
                FieldInfo field = component.GetType().GetField(varName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field == null || field.FieldType.AssemblyQualifiedName != varType)
                    compName = varName = varType = null;
            }
            else compName = varName = varType = null;
        }
        else { obj = null; compName = varName = varType = null; }
    }

    public string GetNextStr()
    {
        RefreshInfo();

        //Check Object and Component Name
        if (obj == null || string.IsNullOrEmpty(compName) || string.IsNullOrEmpty(varName))
        {
            Debug.LogWarning("GameObject or component or variable is not set");
            return null;
        }

        //Find the component
        Component component = obj.GetComponent(compName);
        if (component == null)
        {
            Debug.LogWarning($"Component not found: {compName}");
            return null;
        }

        //Get the field
        FieldInfo field = component.GetType().GetField(varName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogWarning($"Field not found: {varName} on component: {compName}");
            return null;
        }

        //Get the value of the field
        string varNum = field.GetValue(component).ToString();
        Type paramType = Type.GetType(varType);

        switch (Type.GetTypeCode(paramType))
        {
            case TypeCode.Int32:
            case TypeCode.Boolean:
                int intValue = int.Parse(varNum);
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
                double doubleValue = double.Parse(varNum);
                for (int i = 0; i < checkList.Count; i++)
                    if ((checkList[i].checkType == CheckType.Equal && doubleValue == double.Parse(checkList[i].checkNum)) ||
                       (checkList[i].checkType == CheckType.NotEqual && doubleValue != double.Parse(checkList[i].checkNum)) ||
                       (checkList[i].checkType == CheckType.Greater && doubleValue > double.Parse(checkList[i].checkNum)) ||
                       (checkList[i].checkType == CheckType.Less && doubleValue < double.Parse(checkList[i].checkNum)))
                        return "checkList " + i;
                break;

            case TypeCode.String:
                for (int i = 0; i < checkList.Count; i++)
                {
                    int cmp = string.Compare(varNum, checkList[i].checkNum);
                    if((cmp == 1 && (checkList[i].checkType == CheckType.NotEqual || checkList[i].checkType == CheckType.Less)) ||
                       (cmp == 0 && checkList[i].checkType == CheckType.Equal) ||
                       (cmp == -1 && (checkList[i].checkType == CheckType.NotEqual || checkList[i].checkType == CheckType.Greater)))
                        return "checkList " + i;
                }
                break;
        }
        return null;
    }
}