namespace DialogueEditor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using XNode;
    using XNodeEditor;
    using YamlDotNet.Core;

    [InitializeOnLoad]
    public static class DialogueImportExport
    {
        static DialogueImportExport() { DialogueSerialization.Register(Import, Export); }

        public static void Export(DialogueGraph graph)
        {
            string format = "JSON / YAML";
            try
            {
                string path = DialogueExportDialog.Show(graph.name);
                if (string.IsNullOrEmpty(path)) return;
                bool yaml = string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase);
                format = yaml ? "YAML" : "JSON";
                DialogueDocument document = DialogueGraphMapper.Export(graph);
                string content = yaml ? DialogueYamlCodec.Write(document) : DialogueJsonCodec.Write(document);
                File.WriteAllText(path, content, new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is FormatException || exception is YamlException || exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is InvalidOperationException)
            { Debug.LogError("Dialogue " + format + " export failed: " + exception.Message, graph); }
        }

        public static void Import(DialogueGraph graph)
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Import dialogue JSON / YAML", string.Empty,
                new[] { "Dialogue JSON / YAML", "json,yaml" });
            if (string.IsNullOrEmpty(path)) return;
            string format = "JSON";
            try
            {
                string extension = Path.GetExtension(path);
                bool yaml = string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase);
                if (!yaml && !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose a .json or .yaml file.");
                format = yaml ? "YAML" : "JSON";
                string content = File.ReadAllText(path);
                DialogueDocument document = yaml ? DialogueYamlCodec.Read(content) : DialogueJsonCodec.Read(content);
                if (!EditorUtility.DisplayDialog("Import dialogue " + format, "Replace this graph's contents with the " + format + " document? This can be undone.", "Import", "Cancel")) return;
                ImportInto(graph, document);
            }
            catch (Exception exception) when (exception is FormatException || exception is YamlException || exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is InvalidOperationException)
            { Debug.LogError("Dialogue " + format + " import failed: " + exception.Message, graph); }
        }

        public static void ImportInto(DialogueGraph target, DialogueDocument document)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Dialogue import is unavailable during playback.");
            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Save the graph as an asset before importing.");
            DialogueGraph staged = DialogueGraphMapper.Create(document);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import dialogue");
            try
            {
                Node[] previous = target.nodes.ToArray();
                string name = target.name;
                Undo.RegisterCompleteObjectUndo(target, "Import dialogue");
                foreach (Node node in previous) Undo.RegisterCompleteObjectUndo(node, "Replace dialogue node");
                foreach (Node node in previous) node.ClearConnections();
                EditorUtility.CopySerialized(staged, target);
                target.name = name;
                foreach (Node node in target.nodes)
                {
                    node.graph = target;
                    AssetDatabase.AddObjectToAsset(node, target);
                    Undo.RegisterCreatedObjectUndo(node, "Import dialogue node");
                    EditorUtility.SetDirty(node);
                }
                staged.nodes.Clear();
                foreach (Node node in previous) if (node != null) Undo.DestroyObjectImmediate(node);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            finally { DialogueGraphMapper.DestroyTemporary(staged); }
            DialogueGraphEditor.Invalidate();
            NodeEditorWindow.RepaintAll();
            foreach (DialogueIssue issue in DialogueValidation.Validate(target).Where(issue => issue.binding))
            {
                string message = $"[Dialogue import] Node '{issue.node?.name}', item {issue.item + 1}: {issue.message}";
                if (issue.deferred) Debug.Log(message, issue.node);
                else Debug.LogError(message, issue.node);
            }
        }
    }
}
