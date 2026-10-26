namespace DialogueEditor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using XNode;

    public enum PortType { None, Input, Output, All }

    public abstract class NodeBase : Node
    {
        [Input] public Nothing before;
        [HideInInspector] public bool isFolded;
        [HideInInspector] public string summary;
        [Output(connectionType = ConnectionType.Multiple)] public Nothing after;

        public abstract string ListName { get; }
        public abstract IList Items { get; }
        public virtual bool ItemInputs => false;
        public virtual bool HasInput(int index) => false;
        public virtual bool HasOutput(int index) => true;
        public string OutputName(int index) => ListName + " " + index;
        public static int ItemIndex(NodePort port) => port.IsDynamic ? int.Parse(port.fieldName.Substring(port.fieldName.LastIndexOf(' ') + 1)) : -1;
    }

    [NodeWidth(350)] public class DialogueNode : NodeBase
    {
        [Serializable] public class DialogueInfo
        {
            public Sprite sprite;
            public int person;
            public PortType type;
            public LocalizedText context = new();
        }

        [HideInInspector] public List<DialogueInfo> dialogueList = new();

        public override string ListName => nameof(dialogueList);
        public override IList Items => dialogueList;
        public override bool ItemInputs => true;
        public override bool HasInput(int index) => (dialogueList[index].type & PortType.Input) != 0;
        public override bool HasOutput(int index) => (dialogueList[index].type & PortType.Output) != 0;
    }

    public class OptionNode : NodeBase
    {
        [HideInInspector] public List<LocalizedText> optionList = new();

        public override string ListName => nameof(optionList);
        public override IList Items => optionList;
    }

    [NodeWidth(350)] public class EventNode : NodeBase
    {
        [Serializable] public class FuncInfo
        {
            public PortType portType;
            public string bindingKey, compName, funcName, paraType, paraNum;

            public void Invoke(DialogueSystemBase system)
            {
                Resolve(system, out Component component, out MethodInfo method, out object[] arguments);
                method.Invoke(component, arguments);
            }

            public void Resolve(DialogueSystemBase system, out Component component, out MethodInfo method, out object[] arguments)
            {
                component = system.bindings.Resolve(bindingKey, compName);
                Type parameter = string.IsNullOrEmpty(paraType) ? null : Type.GetType(paraType, true);
                method = component.GetType().GetMethod(funcName, parameter == null ? Type.EmptyTypes : new[] { parameter });
                arguments = parameter == null ? null : new[] { DialogueReflection.Parse(paraNum, parameter) };
            }
        }

        [HideInInspector] public List<FuncInfo> eventList = new();

        public override string ListName => nameof(eventList);
        public override IList Items => eventList;
        public override bool ItemInputs => true;
        public override bool HasInput(int index) => (eventList[index].portType & PortType.Input) != 0;
        public override bool HasOutput(int index) => (eventList[index].portType & PortType.Output) != 0;
    }

    [NodeWidth(350)] public class CheckNode : NodeBase
    {
        public enum CheckType { Equal, NotEqual, Greater, Less }

        [Serializable] public class CheckInfo
        {
            public CheckType checkType;
            public string checkNum;
        }

        [HideInInspector] public string bindingKey, compName, varName, varType;
        [HideInInspector] public List<CheckInfo> checkList = new();

        public string Next(DialogueSystemBase system)
        {
            Component component = system.bindings.Resolve(bindingKey, compName);
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

        public override string ListName => nameof(checkList);
        public override IList Items => checkList;
    }
}