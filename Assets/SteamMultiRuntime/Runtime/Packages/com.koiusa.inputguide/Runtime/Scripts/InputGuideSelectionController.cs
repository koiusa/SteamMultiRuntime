using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.InputGuide
{
    /// <summary>Pushes an Inspector-configured map and binding-group selection to an overlay.</summary>
    [DisallowMultipleComponent]
    public sealed class InputGuideSelectionController : MonoBehaviour
    {
        [SerializeField] private InputGuideOverlay overlay;
        [SerializeField] private InputGuideMapFilter mapFilter = InputGuideMapFilter.All;
        [SerializeField] private List<string> actionMapNames = new List<string>();
        [Tooltip("Empty displays bindings from every control scheme.")]
        [SerializeField] private string bindingGroup = string.Empty;

        public InputGuideSelection Current =>
            new InputGuideSelection(mapFilter, actionMapNames, bindingGroup);

        public string[] GetAvailableActionMapNames()
        {
            return overlay != null ? overlay.GetAvailableActionMapNames() : System.Array.Empty<string>();
        }

        public string[] GetAvailableBindingGroups()
        {
            return overlay != null ? overlay.GetAvailableBindingGroups() : System.Array.Empty<string>();
        }

        private void OnEnable() => PushSelection();

        public void ApplySelection(InputGuideSelection selection)
        {
            if (selection == null)
            {
                return;
            }

            mapFilter = selection.MapFilter;
            actionMapNames.Clear();
            actionMapNames.AddRange(selection.ActionMapNames);
            bindingGroup = selection.BindingGroup;
            PushSelection();
        }

        public void PushSelection() => overlay?.ApplySelection(Current);
    }
}
