using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.UI.Common
{
    public static class UiMenuNavigator
    {
        private static readonly List<IUiMenu> Stack = new();

        public static IUiMenu Current => Stack.Count > 0 ? Stack[^1] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Stack.Clear();
        }

        public static void ToggleRoot(IUiMenu menu)
        {
            if (menu == null) return;
            if (ReferenceEquals(Current, menu)) Back(menu);
            else OpenRoot(menu);
        }

        public static void OpenRoot(IUiMenu menu)
        {
            if (menu == null) return;
            CloseAll();
            Stack.Add(menu);
            menu.Activate();
            menu.FocusInitial();
        }

        public static void Push(IUiMenu menu)
        {
            if (menu == null || ReferenceEquals(Current, menu)) return;
            Current?.Deactivate();
            Stack.Add(menu);
            menu.Activate();
            menu.FocusInitial();
        }

        public static void Back(IUiMenu menu = null)
        {
            if (Current == null || (menu != null && !ReferenceEquals(Current, menu))) return;
            var current = Current;
            Stack.RemoveAt(Stack.Count - 1);
            current.Deactivate();
            Current?.Activate();
            Current?.FocusInitial();
        }

        public static void Close(IUiMenu menu)
        {
            if (menu == null) return;
            if (ReferenceEquals(Current, menu))
            {
                Back(menu);
                return;
            }

            var index = Stack.IndexOf(menu);
            if (index >= 0) Stack.RemoveAt(index);
            menu.Deactivate();
        }

        public static void CloseAll()
        {
            for (var i = Stack.Count - 1; i >= 0; i--)
            {
                Stack[i]?.Deactivate();
            }
            Stack.Clear();
        }
    }
}
