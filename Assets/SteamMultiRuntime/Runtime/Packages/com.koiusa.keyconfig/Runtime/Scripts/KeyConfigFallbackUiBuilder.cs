using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal static class KeyConfigFallbackUiBuilder
    {
        internal sealed class Result
        {
            public Label StatusLabel;
            public Label InputMonitorDot;
            public Label InputMonitorStatus;
            public DropdownField BindingGroupDropdown;
            public ScrollView MapTabBar;
            public ScrollView BindingListView;
            public Button LoadButton;
            public Button SaveButton;
            public Button ResetAllButton;
            public Button CloseButton;
        }

        public static Result Build(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            container.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            container.style.paddingLeft = 24;
            container.style.paddingRight = 24;
            container.style.paddingTop = 24;
            container.style.paddingBottom = 24;
            root.Add(container);

            var title = new Label("keyconfig.title");
            title.style.fontSize = 30;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            container.Add(title);

            var result = new Result { StatusLabel = new Label("Ready") };
            result.StatusLabel.style.marginBottom = 12;
            container.Add(result.StatusLabel);

            var monitor = new VisualElement();
            monitor.AddToClassList("keyconfig-input-monitor");
            result.InputMonitorDot = new Label("●");
            result.InputMonitorDot.AddToClassList("keyconfig-input-monitor-dot");
            result.InputMonitorStatus = new Label("WAITING FOR INPUT");
            result.InputMonitorStatus.AddToClassList("keyconfig-input-monitor-status");
            monitor.Add(result.InputMonitorDot);
            monitor.Add(result.InputMonitorStatus);
            container.Add(monitor);

            result.BindingGroupDropdown = new DropdownField("BindingGroup");
            result.BindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
            container.Add(result.BindingGroupDropdown);
            result.MapTabBar = CreateMapTabBar();
            result.MapTabBar.AddToClassList("keyconfig-map-tabs");
            container.Add(result.MapTabBar);
            result.BindingListView = new ScrollView(ScrollViewMode.Vertical);
            result.BindingListView.style.flexGrow = 1;
            result.BindingListView.style.marginBottom = 12;
            container.Add(result.BindingListView);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            container.Add(buttonRow);
            result.LoadButton = CreateButton("keyconfig.load");
            result.SaveButton = CreateButton("keyconfig.save");
            result.ResetAllButton = CreateButton("keyconfig.reset_all");
            result.CloseButton = CreateButton("keyconfig.close");
            buttonRow.Add(result.LoadButton);
            buttonRow.Add(result.SaveButton);
            buttonRow.Add(result.ResetAllButton);
            buttonRow.Add(result.CloseButton);
            return result;
        }

        public static ScrollView CreateMapTabBar()
        {
            var scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.contentContainer.style.flexDirection = FlexDirection.Row;
            scrollView.contentContainer.style.flexWrap = Wrap.NoWrap;
            return scrollView;
        }

        private static Button CreateButton(string text)
        {
            var button = new Button { text = text };
            button.style.width = 110;
            button.style.marginLeft = 8;
            return button;
        }
    }
}
