namespace DialogueEditor
{
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static class SceneObjectReference
    {
        public static bool SceneUnloaded(string path, string id)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(id) && UnityEditor.GlobalObjectId.TryParse(id, out UnityEditor.GlobalObjectId globalId))
            {
                string scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(globalId.assetGUID.ToString());
                if (scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    return !SceneManager.GetSceneByPath(scenePath).isLoaded;
                if (globalId.identifierType == 2 && string.IsNullOrEmpty(scenePath)) return false;
            }
#endif
            return !string.IsNullOrEmpty(path) && !SceneManager.GetSceneByName(path.Split('/')[0]).isLoaded;
        }

        public static bool TryFind(GameObject obj, string path, string id, out GameObject found, out string error)
        {
            found = obj; error = null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(id))
            {
                if (!UnityEditor.GlobalObjectId.TryParse(id, out UnityEditor.GlobalObjectId globalId))
                { error = "Invalid GlobalObjectId: " + id; found = null; return false; }
                var identified = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                if (identified != null) found = identified;
            }
#endif
            if (string.IsNullOrEmpty(path))
            {
                if (found != null) return true;
                error = "Target object is missing."; return false;
            }
            string[] names = path.Split('/');
            if (names.Length < 2) { error = "Invalid scene object path: " + path; found = null; return false; }
            Scene scene = SceneManager.GetSceneByName(names[0]);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (found != null) return true;
                error = "Target scene is not loaded: " + names[0]; return false;
            }

            Transform trans = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == names[1])
                {
                    if (trans != null) { error = "Ambiguous target path: " + path; found = null; return false; }
                    trans = root.transform;
                }
            for (int i = 2; trans != null && i < names.Length; i++)
            {
                Transform child = null;
                foreach (Transform candidate in trans)
                    if (candidate.name == names[i])
                    {
                        if (child != null) { error = "Ambiguous target path: " + path; found = null; return false; }
                        child = candidate;
                    }
                trans = child;
            }
            if (trans != null && found != null && trans.gameObject != found)
            { error = "Object ID/reference and hierarchy path point to different objects: " + path; found = null; return false; }
            if (found == null && trans != null) found = trans.gameObject;
            if (found != null) return true;
            error = "Missing target: " + path; return false;
        }
    }
}
