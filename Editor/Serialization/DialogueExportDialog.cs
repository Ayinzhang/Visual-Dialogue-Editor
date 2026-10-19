namespace DialogueEditor
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using UnityEditor;

    internal static class DialogueExportDialog
    {
        public static string Show(string name)
        {
#if UNITY_EDITOR_WIN
            const int capacity = 32768;
            const int explorer = 0x00080000, pathMustExist = 0x00000800, noChangeDirectory = 0x00000008;
            IntPtr buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
            try
            {
                char[] initial = (name + ".json\0").ToCharArray();
                if (initial.Length > capacity) throw new ArgumentException("Export file name is too long.");
                Marshal.Copy(initial, 0, buffer, initial.Length);
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    var dialog = new SaveFileDialog
                    {
                        size = Marshal.SizeOf(typeof(SaveFileDialog)),
                        owner = process.MainWindowHandle,
                        filter = "JSON (*.json)\0*.json\0YAML (*.yaml)\0*.yaml\0\0",
                        filterIndex = 1,
                        file = buffer,
                        maxFile = capacity,
                        title = "Export dialogue",
                        flags = explorer | pathMustExist | noChangeDirectory
                    };
                    if (!GetSaveFileName(ref dialog))
                    {
                        uint error = CommDlgExtendedError();
                        if (error != 0) throw new InvalidOperationException("Windows export dialog failed: 0x" + error.ToString("X"));
                        return null;
                    }
                    string path = WithExtension(Marshal.PtrToStringUni(buffer), dialog.filterIndex == 2);
                    if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace file?",
                        "The file already exists:\n" + path, "Replace", "Cancel")) return null;
                    return path;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
#else
            int format = EditorUtility.DisplayDialogComplex("Export dialogue", "Choose the file format.", "JSON", "Cancel", "YAML");
            if (format == 1) return null;
            return EditorUtility.SaveFilePanel("Export dialogue", string.Empty, name, format == 2 ? "yaml" : "json");
#endif
        }

        internal static string WithExtension(string path, bool yaml)
        {
            string extension = Path.GetExtension(path);
            string selected = yaml ? ".yaml" : ".json";
            return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase) ?
                Path.ChangeExtension(path, selected) : path + selected;
        }

#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SaveFileDialog
        {
            public int size;
            public IntPtr owner, instance;
            public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDirectory, title;
            public int flags;
            public short fileOffset, fileExtension;
            public string defaultExtension;
            public IntPtr customData, hook;
            public string templateName;
            public IntPtr reserved;
            public int reservedFlags, flagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetSaveFileName(ref SaveFileDialog dialog);

        [DllImport("comdlg32.dll")]
        static extern uint CommDlgExtendedError();
#endif
    }
}
