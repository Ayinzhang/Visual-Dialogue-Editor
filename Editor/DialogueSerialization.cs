namespace DialogueEditor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public static class DialogueSerialization
    {
        static Action<DialogueGraph> importer, exporter;
        public static bool Available => importer != null && exporter != null;

        public static void Register(Action<DialogueGraph> import, Action<DialogueGraph> export)
        {
            importer = import ?? throw new ArgumentNullException(nameof(import));
            exporter = export ?? throw new ArgumentNullException(nameof(export));
            EditorApplication.delayCall += DialogueGraphEditor.Invalidate;
        }

        public static void Import(DialogueGraph graph)
        {
            if (Available) importer(graph);
            else Debug.LogError("Install YamlDotNet to enable dialogue JSON / YAML import.", graph);
        }

        public static void Export(DialogueGraph graph)
        {
            if (Available) exporter(graph);
            else Debug.LogError("Install YamlDotNet to enable dialogue JSON / YAML export.", graph);
        }
    }
}
