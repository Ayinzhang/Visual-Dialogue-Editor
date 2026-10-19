namespace DialogueEditor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.PackageManager;
    using UnityEditor.PackageManager.Requests;
    using UnityEngine;
    using XNodeEditor;

    [InitializeOnLoad]
    public static class DialogueDependencyInstaller
    {
        const string PackageName = "com.yamldotnet.uni";
        const string PackageUrl = "https://github.com/epishev-m/YamlDotNet.UniEx.git";
        const string PendingKey = "DialogueEditor.InstallingYamlDotNet";
        static AddRequest installation;
        static ListRequest recovery;
        public static bool IsInstalling => SessionState.GetBool(PendingKey, false);

        static DialogueDependencyInstaller()
        {
            if (IsInstalling) EditorApplication.delayCall += Resume;
        }

        public static void Install()
        {
            if (DialogueSerialization.Available || IsInstalling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorUtility.DisplayDialog("Install YamlDotNet",
                "Download YamlDotNet from GitHub using Unity Package Manager?\n\n" + PackageUrl +
                "\n\nThis requires Git and network access. Dialogue editing and playback do not require this package.",
                "Install", "Cancel")) return;
            SessionState.SetBool(PendingKey, true);
            try
            {
                installation = Client.Add(PackageUrl);
                EditorApplication.update += Poll;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { Finish(exception.Message); }
        }

        static void Resume()
        {
            if (DialogueSerialization.Available) { Finish(null); return; }
            try
            {
                recovery = Client.List();
                EditorApplication.update += Poll;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { Finish(exception.Message); }
        }

        static void Poll()
        {
            if (installation != null)
            {
                if (!installation.IsCompleted) return;
                Finish(installation.Status == StatusCode.Success ? null : installation.Error.message);
            }
            else if (recovery != null && recovery.IsCompleted)
            {
                if (recovery.Status != StatusCode.Success) Finish(recovery.Error.message);
                else Finish(recovery.Result.Any(package => package.name == PackageName) ? null : "Installation was interrupted. Please try again.");
            }
        }

        static void Finish(string error)
        {
            EditorApplication.update -= Poll;
            installation = null; recovery = null;
            SessionState.SetBool(PendingKey, false);
            if (error == null) Debug.Log("YamlDotNet is installed. JSON / YAML import/export becomes available after script compilation.");
            else Debug.LogError("YamlDotNet installation failed: " + error + "\nCheck Git/network access, then retry or install manually from " + PackageUrl);
            NodeEditorWindow.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public static void DrawInspector()
        {
            if (DialogueSerialization.Available) return;
            EditorGUILayout.HelpBox("JSON / YAML import/export requires YamlDotNet. Dialogue editing and playback remain available.", MessageType.Info);
            using (new EditorGUI.DisabledScope(IsInstalling || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(IsInstalling ? "Installing YamlDotNet..." : "Install YamlDotNet")) Install();
        }
    }
}
