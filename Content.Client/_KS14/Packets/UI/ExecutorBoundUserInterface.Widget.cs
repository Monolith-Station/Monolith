using System.Linq;
using System.Numerics;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._KS14.Packets.UI
{
    public sealed partial class ExecutorBoundUserInterface
    {
        private void UpdateWidgetElement(WidgetElement element)
        {
            if (_menu?.ExecutorTabs?.Children == null)
                return;

            foreach (var tab in _menu.ExecutorTabs.Children)
            {
                var target = FindElementAndParent(tab, element.Id);
                if (target != null)
                {
                    var (control, parent) = target.Value;
                    parent.RemoveChild(control);
                    CreateChild(element, parent);
                    return;
                }
            }
        }

        private (Control Control, PanelContainer Parent)? FindElementAndParent(Control current, string targetId)
        {
            if (current is not PanelContainer container)
                return null;

            foreach (var child in container.Children)
            {
                if (child.Name == targetId)
                    return (child, container);

                var found = FindElementAndParent(child, targetId);
                if (found != null)
                    return found;
            }

            return null;
        }


        private void InitWidget(Widget widget)
        {
            if (_menu?.ExecutorTabs.Children.FirstOrDefault(c => c.Name == widget.Id) is { } control)
                _menu?.ExecutorTabs.RemoveChild(control);

            var container = new PanelContainer { Name = widget.Id };
            container.StyleIdentifier = "executorBorderless";
            SetupChildren(widget, container);
            _menu?.ExecutorTabs.AddChild(container);
        }

        private void SetupControlLayout<T>(WidgetElement element, ref T control, PanelContainer widgetContainer) where T : Control
        {
            control.StyleIdentifier = "executor";
            control.Margin = new Thickness(
                element.Margin[0],
                element.Margin[1],
                element.Margin[2],
                element.Margin[3]);
            control.SetSize = new Vector2(
                element.Size[0],
                element.Size[1]);
            control.Name = element.Id;
            control.HorizontalAlignment = Enum.TryParse<Control.HAlignment>(element.HorizontalAligment, out var hAl)
                ? hAl
                : Control.HAlignment.Stretch;
            control.VerticalAlignment = Enum.TryParse<Control.VAlignment>(element.VerticalAligment, out var vAl)
                ? vAl
                : Control.VAlignment.Stretch;

            widgetContainer.Children.Add(control);
        }

        private void SetupChildren(WidgetContainer widgetContainer, PanelContainer container)
        {
            InitChild(widgetContainer.Elements, container);
        }

        private void SetupChildren(Widget widgetContainer, PanelContainer container)
        {
            InitChild(widgetContainer.Elements, container);
        }

        private void InitChild(WidgetElement[] widgetElements, PanelContainer container)
        {
            foreach (var widgetElement in widgetElements)
            {
                CreateChild(widgetElement, container);
            }
        }

        private void CreateChild(WidgetElement widgetElement, PanelContainer container)
        {
            switch (widgetElement)
            {
                case WidgetButton widgetButton:
                    CreateButton(widgetButton, container);
                    break;
                case WidgetContainer widgetContainer:
                    CreatePanel(widgetContainer, container);
                    break;
                case WidgetLabel widgetLabel:
                    CreateLabel(widgetLabel, container);
                    break;
                case WidgetTextEdit textEdit:
                    CreateTextEdit(textEdit, container);
                    break;
            }
        }

        private void CreateButton(WidgetButton widgetButton, PanelContainer widgetContainer)
        {
            var button = new Button { Name = widgetButton.Id, Text = widgetButton.Text };
            button.OnPressed += OnWidgetButtonClick;

            SetupControlLayout(widgetButton, ref button, widgetContainer);
        }

        private void CreatePanel(WidgetContainer widgetPanel, PanelContainer widgetContainer)
        {
            var panel = new PanelContainer();
            SetupChildren(widgetPanel, panel);

            SetupControlLayout(widgetPanel, ref panel, widgetContainer);
        }

        private void CreateLabel(WidgetLabel widgetLabel, PanelContainer widgetContainer)
        {
            var label = new Label { Text = widgetLabel.Text };

            SetupControlLayout(widgetLabel, ref label, widgetContainer);
        }

        private void CreateTextEdit(WidgetTextEdit widgetTextEdit, PanelContainer widgetContainer)
        {
            var textEdit = new TextEdit { Editable = !widgetTextEdit.ReadOnly };

            SetupControlLayout(widgetTextEdit, ref textEdit, widgetContainer);
        }

        private Dictionary<string, string> GetArguments(Control control)
        {
            var arguments = new Dictionary<string, string>();

            if (control.Parent is not { } parent)
                return arguments;

            foreach (var child in parent.Children)
            {
                if (child is not TextEdit edit)
                    continue;

                if (edit.Name == null || Rope.Collapse(edit.TextRope) is not { } text)
                    continue;

                arguments.Add(edit.Name, text);
            }

            return arguments;
        }

        private void OnWidgetButtonClick(BaseButton.ButtonEventArgs args)
        {
            SendMessage(new WidgetButtonClickedMessage(args.Button.Name ?? "None", GetArguments(args.Button)));
        }
    }
}

