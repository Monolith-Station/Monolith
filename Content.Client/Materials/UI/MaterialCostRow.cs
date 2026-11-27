using System.Numerics;
using Content.Shared.Materials;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Materials.UI;

public sealed class MaterialCostRow : BoxContainer
{
    public MaterialCostRow(MaterialPrototype material, long amount, SpriteSystem sprites, string? labelStyle = null, bool clipName = false)
    {
        SeparationOverride = 8;
        AddChild(new TextureRect
        {
            Texture = material.StackEntity is { } stack
                ? sprites.GetPrototypeIcon(stack).Default : sprites.Frame0(material.Icon),
            SetSize = new Vector2(24),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        });
        var name = new Label
        {
            Text = Loc.GetString("mass-fabricator-display-name", ("name", Loc.GetString(material.Name))),
            HorizontalExpand = true,
            ClipText = clipName,
        };
        var cost = new Label { Text = $"{amount:N0}" };
        if (labelStyle != null)
        {
            name.AddStyleClass(labelStyle);
            cost.AddStyleClass(labelStyle);
        }
        AddChild(name);
        AddChild(cost);
    }
}
