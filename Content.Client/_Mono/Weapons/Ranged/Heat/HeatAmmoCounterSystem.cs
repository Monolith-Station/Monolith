using System.Numerics;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Mono.Weapons.Ranged.Heat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using static Content.Client.Weapons.Ranged.Systems.GunSystem;

namespace Content.Client._Mono.Weapons.Ranged.Heat;

public sealed partial class HeatAmmoCounterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HeatAmmoProviderComponent, AmmoCounterControlEvent>(OnControl);
    }

    private void OnControl(Entity<HeatAmmoProviderComponent> ent, ref AmmoCounterControlEvent args)
    {
        args.Control = new HeatStatusControl(ent, EntityManager, _timing);
    }

    private sealed class HeatStatusControl : Control
    {
        private readonly EntityUid _gun;
        private readonly IEntityManager _entities;
        private readonly IGameTiming _timing;

        public HeatStatusControl(EntityUid gun, IEntityManager entities, IGameTiming timing)
        {
            _gun = gun;
            _entities = entities;
            _timing = timing;
            MinSize = new Vector2(60, 15);
            HorizontalExpand = true;
            VerticalAlignment = VAlignment.Center;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (!_entities.TryGetComponent<HeatAmmoProviderComponent>(_gun, out var heat))
                return;

            var heatTop = 3f * UIScale;
            var heatSize = new Vector2(PixelWidth, Math.Max(0f, PixelHeight - heatTop));
            handle.DrawRect(UIBox2.FromDimensions(0, heatTop, heatSize.X, heatSize.Y), heat.BackgroundColor);

            var cooldown = heat.VentCooldown.TotalSeconds > 0
                ? (float) Math.Clamp((heat.NextVent - _timing.CurTime).TotalSeconds / heat.VentCooldown.TotalSeconds, 0, 1)
                : 0f;
            if (cooldown > 0f)
            {
                handle.DrawRect(UIBox2.FromDimensions(0, 0, PixelWidth, 2f * UIScale), heat.BackgroundColor);
                handle.DrawRect(UIBox2.FromDimensions(0, 0, PixelWidth * cooldown, 2f * UIScale), heat.LowHeatColor);
            }

            var fraction = heat.Overheated ? 1f : heat.MaxHeat > 0 ? Math.Clamp(heat.Heat / heat.MaxHeat, 0f, 1f) : 0f;
            var color = Color.InterpolateBetween(heat.LowHeatColor, heat.HighHeatColor, fraction);
            if (heat.Overheated)
            {
                var pulse = 0.5f + 0.5f * MathF.Cos((float) (_timing.RealTime.TotalSeconds % 1) * MathF.Tau * 2f);
                color = Color.InterpolateBetween(heat.BackgroundColor, heat.HighHeatColor, pulse);
            }

            if (fraction > 0f)
                handle.DrawRect(UIBox2.FromDimensions(0, heatTop, heatSize.X * fraction, heatSize.Y), color);
        }
    }
}
