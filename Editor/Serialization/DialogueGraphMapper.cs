namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using XNode;

    public static class DialogueGraphMapper
    {
        public static DialogueDocument Export(DialogueGraph graph)
        {
            graph.EnsureLocalizationData();
            List<DialogueIssue> issues = DialogueValidation.Validate(graph, false).Where(issue => !issue.binding).ToList();
            if (issues.Count > 0) throw new FormatException(string.Join("\n", issues.Select(issue => issue.message)));
            var document = new DialogueDocument
            {
                schemaVersion = 1, entryNodeId = graph.GetNodeId(graph.EntryNode), defaultLanguage = graph.Languages[0],
                languages = graph.Languages.ToList(), speakers = new List<SpeakerDocument>(), nodes = new List<NodeDocument>()
            };
            for (int i = 0; i < graph.speakers.Count; i++)
            {
                int speaker = i;
                document.speakers.Add(new SpeakerDocument { id = graph.GetSpeakerId(i), names = Text(graph, language => graph.GetRawSpeaker(speaker, language)) });
            }
            foreach (Node node in graph.nodes)
            {
                NodeDocument content;
                if (node is DialogueNode dialogue)
                    content = new DialogueNodeDocument
                    {
                        lines = dialogue.dialogueList.Select((line, i) => new LineDocument
                        {
                            id = line.id, portType = Ports((int)line.type),
                            speakerId = line.person < 0 ? null : graph.GetSpeakerId(line.person),
                            text = Text(graph, language => line.GetRawContext(language)),
                            sprite = SpriteReference(line), next = Next(graph, node, i)
                        }).ToList()
                    };
                else if (node is OptionNode options)
                    content = new OptionNodeDocument
                    {
                        options = options.optionList.Select((text, i) => new OptionDocument
                        {
                            id = options.optionIds[i], text = Text(graph, language => options.GetRawOption(i, language)), next = Next(graph, node, i)
                        }).ToList()
                    };
                else if (node is EventNode events)
                    content = new EventNodeDocument
                    {
                        events = events.eventList.Select((function, i) => new EventDocument
                        {
                            id = function.id, portType = Ports((int)function.portType),
                            target = Target(function.obj, function.objGlobalId, function.objPath),
                            componentType = DialogueValidation.FindType(function.declType)?.FullName ?? function.declType,
                            method = function.funcName,
                            parameter = string.IsNullOrEmpty(function.paraType) ? null : Parameter(function.paraType, function.paraNum),
                            next = Next(graph, node, i)
                        }).ToList()
                    };
                else if (node is CheckNode check)
                {
                    ParameterDocument value = Parameter(check.varType, check.checkList[0].checkNum);
                    content = new CheckNodeDocument
                    {
                        target = Target(check.obj, check.objGlobalId, check.objPath),
                        componentType = ComponentName(check), field = check.varName,
                        valueType = value.type, enumType = value.enumType,
                        conditions = check.checkList.Select((condition, i) => new ConditionDocument
                        {
                            id = condition.id, comparison = Operators[(int)condition.checkType],
                            value = Parameter(check.varType, condition.checkNum).value, next = Next(graph, node, i)
                        }).ToList()
                    };
                }
                else throw new FormatException("Unsupported node: " + node.GetType().FullName);
                content.id = graph.GetNodeId(node);
                content.type = node is DialogueNode ? "dialogue" : node is OptionNode ? "option" : node is EventNode ? "event" : "check";
                content.name = node.name;
                var serialized = new SerializedObject(node);
                content.editor = new EditorDocument
                {
                    position = new[] { node.position.x, node.position.y },
                    collapsed = serialized.FindProperty("isMin").boolValue,
                    summary = serialized.FindProperty("abstruct").stringValue
                };
                document.nodes.Add(content);
            }
            DialogueJsonCodec.Validate(document);
            return document;
        }

        public static DialogueGraph Create(DialogueDocument document)
        {
            DialogueJsonCodec.Validate(document);
            DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                graph.SetLanguageName(0, document.defaultLanguage);
                foreach (string language in document.languages)
                    if (language != document.defaultLanguage) graph.AddLanguage(language);
                foreach (SpeakerDocument speaker in document.speakers)
                {
                    graph.AddSpeaker(); graph.EnsureLocalizationData();
                    int index = graph.speakers.Count - 1;
                    graph.SetSpeakerId(index, speaker.id);
                    for (int language = 0; language < graph.Languages.Count; language++)
                    { graph.SetEditorLanguage(language); graph.SetSpeakerName(index, Translation(speaker.names, graph.Languages[language])); }
                }
                graph.SetEditorLanguage(0);
                var nodes = new Dictionary<string, Node>();
                foreach (NodeDocument content in document.nodes)
                {
                    Type type = content is DialogueNodeDocument ? typeof(DialogueNode) : content is OptionNodeDocument ? typeof(OptionNode) :
                        content is EventNodeDocument ? typeof(EventNode) : typeof(CheckNode);
                    Node node = graph.AddNode(type);
                    node.name = content.name ?? type.Name.Replace("Node", string.Empty);
                    node.position = content.editor?.position == null ? new Vector2(nodes.Count * 400, 0) :
                        new Vector2(content.editor.position[0], content.editor.position[1]);
                    graph.SetNodeId(node, content.id); nodes.Add(content.id, node);
                    if (node is DialogueNode dialogue)
                    {
                        foreach (LineDocument line in ((DialogueNodeDocument)content).lines)
                        {
                            var item = new DialogueNode.DialogueInfo
                            {
                                id = line.id, type = (DialogueNode.PortType)PortValue(line.portType),
                                person = line.speakerId == null ? -1 : document.speakers.FindIndex(speaker => speaker.id == line.speakerId),
                                spriteGuid = line.sprite?.guid, spriteFileId = line.sprite?.fileId ?? 0
                            };
                            item.sprite = ResolveSprite(item.spriteGuid, item.spriteFileId);
                            item.EnsureLocalizationData(graph.Languages.Count);
                            for (int language = 0; language < graph.Languages.Count; language++)
                                item.SetContext(language, Translation(line.text, graph.Languages[language]));
                            dialogue.dialogueList.Add(item);
                        }
                    }
                    if (node is OptionNode options)
                    {
                        foreach (OptionDocument option in ((OptionNodeDocument)content).options) options.optionList.Add(string.Empty);
                        options.EnsureLocalizationData(graph.Languages.Count);
                        int index = 0;
                        foreach (OptionDocument option in ((OptionNodeDocument)content).options)
                        {
                            options.optionIds[index] = option.id;
                            for (int language = 0; language < graph.Languages.Count; language++)
                                options.SetOption(index, language, Translation(option.text, graph.Languages[language]));
                            index++;
                        }
                    }
                    if (node is EventNode events)
                        foreach (EventDocument function in ((EventNodeDocument)content).events)
                        {
                            Type component = DialogueValidation.FindType(function.componentType);
                            Type parameter = function.parameter == null ? null : DialogueJsonCodec.ValueType(function.parameter.type, function.parameter.enumType);
                            events.eventList.Add(new EventNode.FuncInfo
                            {
                                id = function.id, portType = (EventNode.PortType)PortValue(function.portType),
                                obj = SceneObjectReferenceEditor.Find(null, function.target.globalObjectId, function.target.path),
                                objGlobalId = function.target.globalObjectId, objPath = function.target.path,
                                compName = function.componentType, declType = component?.AssemblyQualifiedName ?? function.componentType, funcName = function.method,
                                paraType = function.parameter == null ? null : parameter?.AssemblyQualifiedName ?? function.parameter.enumType,
                                paraNum = function.parameter == null ? null : DialogueJsonCodec.ValueText(function.parameter.value, function.parameter.type)
                            });
                        }
                    if (node is CheckNode check)
                    {
                        var data = (CheckNodeDocument)content;
                        Type valueType = DialogueJsonCodec.ValueType(data.valueType, data.enumType);
                        check.obj = SceneObjectReferenceEditor.Find(null, data.target.globalObjectId, data.target.path);
                        check.objGlobalId = data.target.globalObjectId; check.objPath = data.target.path;
                        check.compName = data.componentType; check.varName = data.field;
                        check.varType = valueType?.AssemblyQualifiedName ?? data.enumType;
                        foreach (ConditionDocument condition in data.conditions)
                            check.checkList.Add(new CheckNode.CheckInfo
                            {
                                id = condition.id, checkType = (CheckNode.CheckType)Array.IndexOf(Operators, condition.comparison),
                                checkNum = DialogueJsonCodec.ValueText(condition.value, data.valueType)
                            });
                    }
                    for (int i = 0; i < DialogueValidation.ItemCount(node); i++)
                    {
                        if (node is DialogueNode || node is EventNode)
                            node.AddDynamicInput(typeof(Node.Nothing), Node.ConnectionType.Multiple, Node.TypeConstraint.None, "inList " + i);
                        if (node.GetOutputPort(DialogueValidation.OutputName(node, i)) == null)
                            node.AddDynamicOutput(typeof(Node.Nothing), Node.ConnectionType.Override, Node.TypeConstraint.None, DialogueValidation.OutputName(node, i));
                    }
                    var serialized = new SerializedObject(node);
                    serialized.FindProperty("isMin").boolValue = content.editor?.collapsed ?? false;
                    serialized.FindProperty("abstruct").stringValue = content.editor?.summary ?? string.Empty;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (NodeDocument content in document.nodes)
                {
                    int index = 0;
                    foreach (ItemDocument item in DialogueJsonCodec.Items(content))
                    {
                        if (item.next != null)
                        {
                            Node target = nodes[item.next.nodeId];
                            bool itemInput = target is DialogueNode || target is EventNode;
                            int targetIndex = item.next.itemId == null ? (itemInput ? 0 : -1) :
                                Enumerable.Range(0, DialogueValidation.ItemCount(target)).First(i => DialogueValidation.ItemId(target, i) == item.next.itemId);
                            if (item.next.itemId == null && itemInput)
                            {
                                if (target is DialogueNode dialogue) dialogue.dialogueList[0].type |= DialogueNode.PortType.Input;
                                if (target is EventNode events) events.eventList[0].portType |= EventNode.PortType.Input;
                            }
                            nodes[content.id].GetOutputPort(DialogueValidation.OutputName(nodes[content.id], index))
                                .Connect(target.GetInputPort(targetIndex < 0 ? "before" : "inList " + targetIndex));
                        }
                        index++;
                    }
                }
                graph.SetEntry(nodes[document.entryNodeId]); graph.EnsureLocalizationData();
                List<DialogueIssue> structural = DialogueValidation.Validate(graph, false).Where(issue => !issue.binding).ToList();
                if (structural.Count > 0) throw new FormatException(string.Join("\n", structural.Select(issue => issue.message)));
                return graph;
            }
            catch
            {
                DestroyTemporary(graph);
                throw;
            }
        }

        public static void DestroyTemporary(DialogueGraph graph)
        {
            foreach (Node node in graph.nodes.ToArray()) if (node != null && !AssetDatabase.Contains(node)) UnityEngine.Object.DestroyImmediate(node);
            UnityEngine.Object.DestroyImmediate(graph);
        }

        static readonly string[] Operators = { "equal", "notEqual", "greater", "less" };
        static readonly string[] PortNames = { "normal", "input", "output", "both" };
        static string Ports(int value) => PortNames[value];
        static int PortValue(string value) => Array.IndexOf(PortNames, value);
        static string Translation(Dictionary<string, string> text, string language)
        { return text.TryGetValue(language, out string value) ? value : null; }

        static Dictionary<string, string> Text(DialogueGraph graph, Func<int, string> get)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < graph.Languages.Count; i++) result.Add(graph.Languages[i], i == 0 ? get(i) ?? string.Empty : get(i));
            return result;
        }

        static NextDocument Next(DialogueGraph graph, Node node, int index)
        {
            NodePort input = node.GetOutputPort(DialogueValidation.OutputName(node, index))?.Connection;
            if (input == null) return null;
            DialogueValidation.TryDestination(input, out int destination, out _);
            return new NextDocument { nodeId = graph.GetNodeId(input.node), itemId = input.fieldName == "before" ? null : DialogueValidation.ItemId(input.node, destination) };
        }

        static SpriteDocument SpriteReference(DialogueNode.DialogueInfo line)
        {
            if (line.sprite != null)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(line.sprite, out string guid, out long localId))
                    throw new FormatException("Sprite is not a project asset.");
                return new SpriteDocument { guid = guid, fileId = localId };
            }
            return new SpriteDocument { guid = line.spriteGuid, fileId = string.IsNullOrEmpty(line.spriteGuid) ? (long?)null : line.spriteFileId };
        }

        static Sprite ResolveSprite(string guid, long fileId)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>()
                .FirstOrDefault(sprite => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long localId) && localId == fileId);
        }

        static TargetDocument Target(GameObject obj, string id, string path)
        {
            return new TargetDocument
            {
                globalObjectId = obj == null ? id : SceneObjectReferenceEditor.GetId(obj),
                path = obj == null ? path : SceneObjectReferenceEditor.GetPath(obj)
            };
        }

        static string ComponentName(CheckNode node)
        {
            Type type = DialogueValidation.FindType(node.compName);
            if (type != null) return type.FullName;
            if (node.obj != null)
            {
                Component component = node.obj.GetComponent(node.compName);
                if (component != null) return component.GetType().FullName;
            }
            return node.compName;
        }

        static string TypeName(Type type)
        {
            if (type == null) throw new FormatException("Value type could not be resolved.");
            if (type.IsEnum) return "enum";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            throw new FormatException("Unsupported value type: " + type.FullName);
        }

        static ParameterDocument Parameter(string typeName, string text)
        {
            Type type = DialogueValidation.FindType(typeName);
            if (type == null && !string.IsNullOrWhiteSpace(typeName) &&
                long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long enumeration))
                return new ParameterDocument { type = "enum", enumType = typeName, value = enumeration };
            string kind = TypeName(type);
            if (!DialogueValidation.TryValue(text, type, out object value, out string error)) throw new FormatException(error);
            return new ParameterDocument
            {
                type = kind, enumType = type.IsEnum ? type.AssemblyQualifiedName : null,
                value = type.IsEnum ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : value
            };
        }
    }
}
