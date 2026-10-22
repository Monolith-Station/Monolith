using Content.Shared._Mono.Company;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Shuttles.Systems;

public abstract partial class SharedShuttleSystem
{
    /*
     * Handles the label visibility on radar controls. This can be hiding the label or applying other effects.
     */

    protected virtual void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component) {}

    public Color GetIFFColor(EntityUid gridUid, bool self = false, IFFComponent? component = null)
    {
        if (self)
        {
            return IFFComponent.SelfColor;
        }

        if (!Resolve(gridUid, ref component, false))
        {
            return IFFComponent.IFFColor;
        }

        return component.Color;
    }

    public string? GetIFFLabel(EntityUid gridUid, bool self = false, IFFComponent? component = null)
    {
        var entName = MetaData(gridUid).EntityName;

        if (self)
        {
            return entName;
        }

        if (Resolve(gridUid, ref component, false) && (component.Flags & (IFFFlags.HideLabel | IFFFlags.Hide)) != 0x0)
        {
            return null;
        }

        // Get the company information if available
        Color? companyColor = Color.White;
        string? companyName = Loc.GetString("shuttle-console-company-unknown");

        if (TryComp<_Mono.Company.CompanyComponent>(gridUid, out var companyComp) && !string.IsNullOrEmpty(companyComp.CompanyName))
        {
            if (IoCManager.Resolve<IPrototypeManager>().TryIndex<CompanyPrototype>(companyComp.CompanyName, out var prototype))
            {
                // Don't include "None" companies in the IFF label
                if (prototype.ID != "None")
                {
                    companyName = prototype.Name;
                    companyColor = prototype.Color;
                }
            }
            else
            {
                // For unknown companies, still check if it's not "None"
                if (companyComp.CompanyName != "None")
                {
                    companyName = companyComp.CompanyName;
                    companyColor = Color.Yellow;
                }
            }
        }

        var labelText = string.IsNullOrEmpty(entName) ? Loc.GetString("shuttle-console-unknown") : entName;

        // Add company info if available
        if (companyName != null && companyColor != null)
        {
            // Return a formatted label that the client can parse properly
            return $"{labelText}\n{companyName}";
        }

        return labelText;
    }

    /// <summary>
    /// Sets the color for this grid to appear as on radar.
    /// </summary>
    [PublicAPI]
    public void SetIFFColor(EntityUid gridUid, Color color, IFFComponent? component = null)
    {
        component ??= EnsureComp<IFFComponent>(gridUid);

        if (component.ReadOnly) // Frontier: POI IFF protection
            return; // Frontier: POI IFF protection

        if (component.Color.Equals(color))
            return;

        component.Color = color;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }

    [PublicAPI]
    public void AddIFFFlag(EntityUid gridUid, IFFFlags flags, IFFComponent? component = null)
    {
        component ??= EnsureComp<IFFComponent>(gridUid);

        if (component.ReadOnly) // Frontier: POI IFF protection
            return; // Frontier: POI IFF protection

        if ((component.Flags & flags) == flags)
            return;

        component.Flags |= flags;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }

    [PublicAPI]
    public void RemoveIFFFlag(EntityUid gridUid, IFFFlags flags, IFFComponent? component = null)
    {
        if (!Resolve(gridUid, ref component, false))
            return;

        if (component.ReadOnly) // Frontier: POI IFF protection
            return; // Frontier: POI IFF protection

        if ((component.Flags & flags) == 0x0)
            return;

        component.Flags &= ~flags;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }

    // Frontier: POI IFF protection
    [PublicAPI]
    public void SetIFFReadOnly(EntityUid gridUid, bool readOnly, IFFComponent? component = null)
    {
        if (!Resolve(gridUid, ref component, false))
            return;

        if (component.ReadOnly == readOnly)
            return;

        component.ReadOnly = readOnly;
    }
    // End Frontier
}
