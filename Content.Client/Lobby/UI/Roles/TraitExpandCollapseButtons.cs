using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.Lobby.UI.Roles;

public sealed class TraitExpandCollapseButtons : PanelContainer
{
    public event Action<bool>? OnExpandCollapseAll;

    public TraitExpandCollapseButtons()
    {
        // Create a container for the buttons
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 0 // No separation between buttons
        };

        // Create the expand all button
        var expandAllButton = new Button
        {
            Text = Loc.GetString("capibara-ui-expand-all"), // Capibara ESP
            StyleClasses = { "OpenRight" },
            HorizontalExpand = true,
            VerticalExpand = true,
            TextAlign = Label.AlignMode.Center,
            MinSize = new Vector2(0, 30)
        };

        // Create the collapse all button
        var collapseAllButton = new Button
        {
            Text = Loc.GetString("capibara-ui-collapse-all"), // Capibara ESP
            StyleClasses = { "OpenLeft" },
            HorizontalExpand = true,
            VerticalExpand = true,
            TextAlign = Label.AlignMode.Center,
            MinSize = new Vector2(0, 30)
        };

        // Add event handlers
        expandAllButton.OnPressed += _ => OnExpandCollapseAll?.Invoke(true);
        collapseAllButton.OnPressed += _ => OnExpandCollapseAll?.Invoke(false);

        // Add the buttons to the container
        container.AddChild(expandAllButton);
        container.AddChild(collapseAllButton);

        // Add the container to this control
        AddChild(container);

        // Add some margin at the bottom
        Margin = new Thickness(0, 0, 0, 5);
    }
}
