using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;

[Serializable]
public class OptionNode : Node
{
    public class Anything { }
    [Input] public Nothing before;
    [HideInInspector] public bool isMin, isActive;
    [HideInInspector] public string abstruct;
    [Output(connectionType = ConnectionType.Multiple)] public Nothing after;
    [Output(dynamicPortList = true, connectionType = ConnectionType.Multiple)] 
    public List<string> optionList = new List<string>();
}
