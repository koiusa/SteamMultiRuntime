using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal sealed class KeyConfigInputMonitor : System.IDisposable
    {
        private readonly List<Row> rows = new List<Row>();
        private Label dot;
        private LocalizedTextBinding statusBinding;
        private int lastActiveCount = int.MinValue;

        public void Configure(Label monitorDot, Label monitorStatus)
        {
            dot = monitorDot;
            statusBinding?.Dispose();
            statusBinding = monitorStatus == null ? null : new LocalizedTextBinding(monitorStatus);
            lastActiveCount = int.MinValue;
        }

        public void Clear() => rows.Clear();

        public void Add(InputBindingService.BindingEntry entry, VisualElement element, Label stateLabel, InputControl control)
        {
            rows.Add(new Row(entry, element, stateLabel, control));
        }

        public void Update(InputActionAsset inputActionAsset)
        {
            var activeCount = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                if (!InputControlActivity.IsUsable(item.Control))
                    item.Control = InputControlActivity.Resolve(item.Entry.BindingPath);
                var activeControl = InputControlActivity.FindActive(item.Entry.BindingPath, item.Control);
                if (activeControl != null) item.Control = activeControl;
                var magnitude = InputControlActivity.EvaluateMagnitude(item.Control);
                var isActive = activeControl != null;
                if (!isActive && magnitude < 0f && inputActionAsset != null)
                {
                    var action = inputActionAsset.FindAction(item.Entry.ActionId.ToString());
                    isActive = action != null && action.IsPressed();
                }

                item.Element.EnableInClassList("input-active", isActive);
                item.StateLabel.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                if (isActive) activeCount++;
            }

            if (activeCount != lastActiveCount)
            {
                lastActiveCount = activeCount;
                if (activeCount == 0) statusBinding?.Set("keyconfig.waiting_input");
                else if (activeCount == 1) statusBinding?.Set("keyconfig.input_detected");
                else statusBinding?.Set("keyconfig.inputs_detected", activeCount);
            }
            dot?.EnableInClassList("active", activeCount > 0);
        }

        public void Dispose()
        {
            statusBinding?.Dispose();
            statusBinding = null;
            rows.Clear();
        }

        private sealed class Row
        {
            public Row(InputBindingService.BindingEntry entry, VisualElement element, Label stateLabel, InputControl control)
            {
                Entry = entry;
                Element = element;
                StateLabel = stateLabel;
                Control = control;
            }

            public InputBindingService.BindingEntry Entry { get; }
            public VisualElement Element { get; }
            public Label StateLabel { get; }
            public InputControl Control { get; set; }
        }
    }
}
