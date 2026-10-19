namespace DialogueEditor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    [NodeWidth(350)] public class EventNode : BaseNode
    {
        [Serializable] public class FuncInfo
        {
            public PortType portType;
            public string objPath, compName, funcName, paraType, paraNum;

            public void Invoke()
            {
                Resolve(out Component component, out MethodInfo method, out object[] arguments);
                method.Invoke(component, arguments);
            }

            public void Resolve(out Component component, out MethodInfo method, out object[] arguments)
            {
                component = DialogueReflection.FindComponent(objPath, compName);
                Type parameter = string.IsNullOrEmpty(paraType) ? null : Type.GetType(paraType, true);
                method = component.GetType().GetMethod(funcName, parameter == null ? Type.EmptyTypes : new[] { parameter });
                if (method == null) throw new MissingMethodException(compName, funcName);
                arguments = parameter == null ? null : new[] { DialogueReflection.Parse(paraNum, parameter) };
            }
        }

        [HideInInspector] public List<FuncInfo> eventList = new List<FuncInfo>();

        public override string ListName => nameof(eventList);
        public override IList Items => eventList;
        public override bool ItemInputs => true;
        public override bool HasInput(int index) => (eventList[index].portType & PortType.Input) != 0;
        public override bool HasOutput(int index) => (eventList[index].portType & PortType.Output) != 0;
    }
}
