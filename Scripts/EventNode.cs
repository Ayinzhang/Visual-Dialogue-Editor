namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    [Serializable, NodeWidth(350)]
    public class EventNode : Node
    {
        public enum PortType { Normal, Input, Output, All }
        [Input] public Nothing before;
        [HideInInspector] public bool isMin;
        [HideInInspector] public string abstruct;
        [HideInInspector] public List<FuncInfo> eventList = new List<FuncInfo>();
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        [Serializable] public class FuncInfo
        {
            [HideInInspector] public string id;
            public GameObject obj;
            [HideInInspector] public PortType portType;
            [HideInInspector] public string objGlobalId, objPath, compName, funcName, declType, paraType, paraNum;

            public void InvokeMethod()
            {
                if (!DialogueValidation.TryEvent(this, out Component component, out MethodInfo method, out object[] arguments, out string error))
                    throw new InvalidOperationException(error);
                method.Invoke(component, arguments);
            }
        }
    }
}
