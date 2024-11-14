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
public class EventNode : Node 
{
    public enum PortType { Normal, Input, Output, All }
    [Input] public Nothing before;
    [HideInInspector] public bool isMin;
    [HideInInspector] public string abstruct;
    [HideInInspector] public List<FuncInfo> eventList = new List<FuncInfo>();
    [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

    public int Invoke(int num)
    {
        for (int i = num; i < eventList.Count; i++)
        {
            eventList[i].InvokeMethod();
            if (eventList[i].portType >= PortType.Output) return i;
        }
        return -1;
    }

    [Serializable] public class FuncInfo
    {
        public GameObject obj; [HideInInspector] public int objInstanceID; [HideInInspector] public PortType portType;
        [HideInInspector] public string objPath, compName, funcName, declType, paraType, paraNum;

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
                for (int j = 1; j < names.Length; j++)
                {
                    trans = j == 1 ? GameObject.Find(names[j])?.transform : trans.Find(names[j]);
                    if (trans == null) break;
                }
                if (trans != null) obj = trans.gameObject;
            }
           
            if (obj != null)
            {
                Component targetComponent = obj.GetComponent(compName);
                if (targetComponent == null)
                {
                    compName = funcName = declType = paraType = paraNum = null;
                    return;
                }
           
                MethodInfo targetMethod = targetComponent.GetType().GetMethod(funcName);
                if (targetMethod == null || targetMethod.GetParameters().Length > 1)
                {
                    compName = funcName = declType = paraType = paraNum = null;
                    return;
                }
           
                ParameterInfo[] parameters = targetMethod.GetParameters();
                if (parameters.Length == 1)
                {
                    Type paramType = parameters[0].ParameterType;
                    if (paramType.AssemblyQualifiedName != paraType)
                        paraType = paraNum = null;
                }
                else
                    paraType = paraNum = null;
            }
            else
                compName = funcName = declType = paraType = paraNum = null;
        }

        public MethodInfo GetMethodInfo()
        {
            if (obj == null || string.IsNullOrEmpty(funcName) || string.IsNullOrEmpty(declType)) return null;

            Type componentType = Type.GetType(declType);
            if (componentType == null) return null;

            Component component = obj.GetComponent(componentType);
            if (component == null) return null;

            Type paramType = null;
            if (!string.IsNullOrEmpty(paraType))
            {
                paramType = Type.GetType(paraType);
                if (paramType == null) return null;
            }

            return paramType != null
                ? componentType.GetMethod(funcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { paramType }, null)
                : componentType.GetMethod(funcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public void InvokeMethod()
        {
            //Get the method info
            RefreshInfo();
            if (obj == null || string.IsNullOrEmpty(funcName) || string.IsNullOrEmpty(declType)) return;

            Type componentType = Type.GetType(declType);
            if (componentType == null) return;

            Component component = obj.GetComponent(componentType);
            if (component == null) return;

            Type paramType = null;
            if (!string.IsNullOrEmpty(paraType))
            {
                paramType = Type.GetType(paraType);
                if (paramType == null) return;
            }

            MethodInfo methodInfo = paramType != null
                ? componentType.GetMethod(funcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { paramType }, null)
                : componentType.GetMethod(funcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (methodInfo == null) { Debug.LogError("Can't find designated method"); return; }

            //Prepare the parameter
            object parameter = null;
            if (!string.IsNullOrEmpty(paraType))
            {
                if (paramType == typeof(int) || paramType == typeof(bool))
                {
                    int intValue = int.Parse(paraNum);
                    parameter = intValue;
                }
                else if (paramType == typeof(float) || paramType == typeof(double))
                {
                    double doubleValue = double.Parse(paraNum);
                    parameter = doubleValue;
                }
                else if (paramType == typeof(string))
                {
                    parameter = paraNum;
                }
            }

            //Invoke the method
            try
            {
                if (methodInfo.GetParameters().Length == 1) methodInfo.Invoke(component, new object[] { parameter });
                else methodInfo.Invoke(component, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"Invoke method {methodInfo.Name} fail: {e.Message}");
            }
        }
    }
}