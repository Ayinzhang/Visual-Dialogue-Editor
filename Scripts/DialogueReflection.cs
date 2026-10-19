namespace DialogueEditor
{
    using System;
    using System.Globalization;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static class DialogueReflection
    {
        public static GameObject Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] names = path.Split(new[] { '/' }, 3);
            Scene scene = SceneManager.GetSceneByName(names[0]);
            if (!scene.isLoaded) return null;
            GameObject root = Array.Find(scene.GetRootGameObjects(), obj => obj.name == names[1]);
            if (root == null) return null;
            Transform found = names.Length == 2 ? root.transform : root.transform.Find(names[2]);
            return found == null ? null : found.gameObject;
        }

        public static string GetPath(GameObject obj)
        {
            string path = obj.name;
            for (Transform parent = obj.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return obj.scene.name + "/" + path;
        }

        public static Component FindComponent(string path, string type)
        {
            GameObject obj = Find(path);
            Component component = obj == null ? null : Array.Find(obj.GetComponents<Component>(), c => c.GetType().FullName == type);
            if (component == null) throw new MissingReferenceException("Missing " + type + " at " + path);
            return component;
        }

        public static object Parse(string text, Type type)
        {
            if (type.IsEnum) return Enum.ToObject(type, long.Parse(text, CultureInfo.InvariantCulture));
            if (type == typeof(bool)) return text == "1";
            return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        }

        public static string Format(object value)
        {
            if (value is bool flag) return flag ? "1" : "0";
            if (value is Enum) return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
