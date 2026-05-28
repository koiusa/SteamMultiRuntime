using System;
using System.Collections.Generic;

namespace Koiusa.Keyconfig.Editor
{
    internal static class InputBindingIconEditorUi
    {
        public static string[] BuildMapTabs<T>(IReadOnlyList<T> rows, Func<T, string> mapNameSelector)
        {
            var tabs = new List<string> { "All" };
            var mapSet = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var mapName = mapNameSelector(rows[i]);
                if (string.IsNullOrWhiteSpace(mapName) || !mapSet.Add(mapName))
                {
                    continue;
                }

                tabs.Add(mapName);
            }

            return tabs.ToArray();
        }
    }
}
