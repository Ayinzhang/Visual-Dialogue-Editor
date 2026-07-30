namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    [Serializable]
    [NodeWidth(350)]
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

        [Serializable]
        public class FuncInfo
        {
            public GameObject obj; [HideInInspector] public PortType portType;
            [HideInInspector] public string objGlobalId, objPath, compName, funcName, declType, paraType, paraNum;

            public MethodInfo RefreshInfo()
            {
                obj = SceneObjectReference.Find(obj, objPath);
                MethodInfo method = GetMethodInfo();
                if (obj != null && method == null) compName = funcName = declType = paraType = paraNum = null;
                return method;
            }

            MethodInfo GetMethodInfo()
            {
                if (obj == null || string.IsNullOrEmpty(funcName) || string.IsNullOrEmpty(declType)) return null;

                Type componentType = Type.GetType(declType);
                if (componentType == null) return null;

                Component component = obj.GetComponent(componentType);
                if (component == null) return null;

                Type paramType = string.IsNullOrEmpty(paraType) ? null : Type.GetType(paraType);
                if (!string.IsNullOrEmpty(paraType) && paramType == null) return null;
                return componentType.GetMethod(funcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, paramType == null ? Type.EmptyTypes : new Type[] { paramType }, null);
            }

            public void InvokeMethod()
            {
                MethodInfo methodInfo = RefreshInfo();
                if (methodInfo == null) { Debug.LogError("Can't find designated method"); return; }

                object parameter = null;
                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length == 1)
                {
                    Type type = parameters[0].ParameterType;
                    if (type.IsEnum) parameter = Enum.ToObject(type, int.Parse(paraNum));
                    else if (type == typeof(bool)) parameter = paraNum == "1";
                    else parameter = type == typeof(string) ? paraNum : Convert.ChangeType(paraNum, type);
                }

                try
                {
                    methodInfo.Invoke(obj.GetComponent(methodInfo.DeclaringType), parameters.Length == 0 ? null : new object[] { parameter });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Invoke method {methodInfo.Name} fail: {e.Message}");
                }
            }
        }
    }
}