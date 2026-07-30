using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetIndicatorThemeProvider : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset targetIndicatorVisualTree;
        [SerializeField] private StyleSheet targetIndicatorStyleSheet;

        public VisualTreeAsset TargetIndicatorVisualTree => targetIndicatorVisualTree;
        public StyleSheet TargetIndicatorStyleSheet => targetIndicatorStyleSheet;

        public void Configure(VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            targetIndicatorVisualTree = visualTree;
            targetIndicatorStyleSheet = styleSheet;
        }
    }
}
