namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public static class DialogueReflection
    {
        public static bool Supported(Type type) => type.IsEnum ||
            type == typeof(int) || type == typeof(float) || type == typeof(bool) || type == typeof(string);

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

    [Serializable] public class DialogueBinding
    {
        [Serializable] class Binding { public string key; public Object target; }
        [SerializeField] List<Binding> bindings = new();

        public Object Find(string key) => bindings.Find(binding => binding.key == key)?.target;

        public Component Resolve(string key, string type)
        {
            Object target = Find(key);
            if (target == null) throw new MissingReferenceException("Missing dialogue binding: " + key);
            if (target is not Component component) throw new InvalidOperationException("Select a component method or field for dialogue binding: " + key);
            if (component.GetType().FullName != type)
                throw new InvalidOperationException("Binding " + key + " requires " + type + ", but points to " + component.GetType().FullName);
            return component;
        }

        // One graph could be used in multiple scenes
        public string Bind(Object target, string preferredKey = null)
        {
            GameObject obj = target is Component component ? component.gameObject : (GameObject)target;
            Binding preferred = string.IsNullOrEmpty(preferredKey) ? null : bindings.Find(binding => binding.key == preferredKey);
            if (preferred != null && (preferred.target == null || preferred.target == target || preferred.target == obj))
            {
                preferred.target = target;
                return preferred.key;
            }
            string key = preferred == null ? preferredKey : null;
            if (string.IsNullOrEmpty(key))
            {
                foreach (Binding binding in bindings)
                    if (binding.target == target)
                        return binding.key;
                key = Guid.NewGuid().ToString("N");
            }
            bindings.Add(new Binding { key = key, target = target });
            return key;
        }
    }
}
