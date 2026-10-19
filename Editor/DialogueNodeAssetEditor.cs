namespace DialogueEditor
{
    using UnityEditor;
    using UnityEngine;
    using XNode;
    using XNodeEditor;

    public abstract class DialogueNodeAssetEditorBase : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Edit graph", GUILayout.Height(40)))
            {
                Node node = (Node)target;
                NodeEditorWindow window = NodeEditorWindow.Open(node.graph);
                window.Home();
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);
                GUILayout.Label("Raw data", "BoldLabel");
                DrawDefaultInspector();
            }
        }
    }

    [CustomEditor(typeof(DialogueNode), true), CanEditMultipleObjects]
    public sealed class DialogueNodeAssetEditor : DialogueNodeAssetEditorBase { }

    [CustomEditor(typeof(OptionNode), true), CanEditMultipleObjects]
    public sealed class OptionNodeAssetEditor : DialogueNodeAssetEditorBase { }

    [CustomEditor(typeof(EventNode), true), CanEditMultipleObjects]
    public sealed class EventNodeAssetEditor : DialogueNodeAssetEditorBase { }

    [CustomEditor(typeof(CheckNode), true), CanEditMultipleObjects]
    public sealed class CheckNodeAssetEditor : DialogueNodeAssetEditorBase { }
}
