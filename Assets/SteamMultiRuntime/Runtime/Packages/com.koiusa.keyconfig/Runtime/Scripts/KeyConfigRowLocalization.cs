using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal sealed class KeyConfigRowLocalization : System.IDisposable
    {
        private readonly List<LocalizedTextBinding> bindings = new List<LocalizedTextBinding>();
        private readonly List<(VisualElement element, string key)> tooltips = new List<(VisualElement, string)>();

        public void Bind(TextElement element, string key)
        {
            var binding = new LocalizedTextBinding(element);
            binding.Set(key);
            bindings.Add(binding);
        }

        public void BindTooltip(VisualElement element, string key)
        {
            tooltips.Add((element, key));
            element.tooltip = KeyConfigLocalization.Get(key);
        }

        public void Refresh()
        {
            for (var i = 0; i < tooltips.Count; i++)
            {
                var item = tooltips[i];
                if (item.element != null) item.element.tooltip = KeyConfigLocalization.Get(item.key);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < bindings.Count; i++) bindings[i].Dispose();
            bindings.Clear();
            tooltips.Clear();
        }

        public void Dispose() => Clear();
    }
}
