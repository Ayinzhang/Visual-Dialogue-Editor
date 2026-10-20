namespace DialogueEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [Serializable] public class DialogueBindingProfile
    {
        [Serializable] class Binding
        {
            public string key;
            public Object target;
        }

        [SerializeField] List<Binding> bindings = new List<Binding>();

        public Object Find(string key) => Entry(key)?.target;

        public Component Resolve(string key)
        {
            Object target = Find(key);
            if (target == null) throw new MissingReferenceException("Missing dialogue binding: " + key);
            if (target is Component component) return component;
            throw new InvalidOperationException("Select a component method or field for dialogue binding: " + key);
        }

        // Target is a scene GameObject (draft until a member is selected) or the chosen Component.
        public string Bind(Object target, string preferredKey = null)
        {
            GameObject obj = target is Component component ? component.gameObject : (GameObject)target;
            // Preserve the graph's logical key when configuring another scene.
            Binding preferred = Entry(preferredKey);
            if (preferred != null && (preferred.target == null || preferred.target == target || preferred.target == obj))
            {
                preferred.target = target;
                return preferred.key;
            }
            string key = preferred == null ? preferredKey : null;
            if (string.IsNullOrEmpty(key))
            {
                foreach (Binding binding in bindings)
                    if (binding.target != null && (binding.target == target ||
                        target is GameObject && binding.target is Component bound && bound.gameObject == obj))
                        return binding.key;
                key = Guid.NewGuid().ToString("N");
            }
            bindings.Add(new Binding { key = key, target = target });
            return key;
        }

        Binding Entry(string key) => string.IsNullOrEmpty(key) ? null : bindings.Find(binding => binding.key == key);
    }
}
