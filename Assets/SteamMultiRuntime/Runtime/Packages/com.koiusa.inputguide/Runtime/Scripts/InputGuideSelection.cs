using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Koiusa.InputGuide
{
    /// <summary>Immutable selection of action maps and binding group shown by an input guide.</summary>
    public sealed class InputGuideSelection
    {
        private readonly ReadOnlyCollection<string> actionMapNames;

        public InputGuideSelection(
            InputGuideMapFilter mapFilter,
            IEnumerable<string> actionMapNames = null,
            string bindingGroup = "")
        {
            MapFilter = mapFilter;
            BindingGroup = bindingGroup?.Trim() ?? string.Empty;

            var names = new List<string>();
            if (actionMapNames != null)
            {
                foreach (var mapName in actionMapNames)
                {
                    if (!string.IsNullOrWhiteSpace(mapName) && !names.Contains(mapName))
                    {
                        names.Add(mapName);
                    }
                }
            }

            this.actionMapNames = names.AsReadOnly();
        }

        public InputGuideMapFilter MapFilter { get; }
        public IReadOnlyList<string> ActionMapNames => actionMapNames;
        public string BindingGroup { get; }

        public static InputGuideSelection All(string bindingGroup = "") =>
            new InputGuideSelection(InputGuideMapFilter.All, bindingGroup: bindingGroup);

        public static InputGuideSelection Specified(
            IEnumerable<string> actionMapNames,
            string bindingGroup = "") =>
            new InputGuideSelection(InputGuideMapFilter.Specified, actionMapNames, bindingGroup);
    }
}
