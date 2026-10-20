namespace DialogueEditor
{
    using System;
    using System.Globalization;
    using UnityEngine;

    public static class DialogueReflection
    {
        public static Component FindComponent(DialogueSystemBase system, string key, string type)
        {
            if (system == null) throw new MissingReferenceException("Dialogue System is missing. Set graph.system before calling Next().");
            Component component = system.Bindings.Resolve(key);
            if (component.GetType().FullName != type)
                throw new InvalidOperationException("Binding " + key + " requires " + type + ", but points to " + component.GetType().FullName);
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
