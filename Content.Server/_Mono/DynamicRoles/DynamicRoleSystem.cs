using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Server._Mono.DynamicRoles;

/// <summary>
/// Dynamically enables or disables role timers and whitelists based on player count.
/// </summary>
public sealed class DynamicRoleSettingsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _dynamicRolesEnabled;
    private int _playerThreshold;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.DynamicRolesEnabled, OnDynamicRolesEnabledChanged, true);
        _cfg.OnValueChanged(CCVars.DynamicRolesPlayerThreshold, OnPlayerThresholdChanged, true);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);
        UpdateRoleSettings();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.DynamicRolesEnabled, OnDynamicRolesEnabledChanged);
        _cfg.UnsubValueChanged(CCVars.DynamicRolesPlayerThreshold, OnPlayerThresholdChanged);
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnDynamicRolesEnabledChanged(bool value)
    {
        _dynamicRolesEnabled = value;
        UpdateRoleSettings();
    }

    private void OnPlayerThresholdChanged(int value)
    {
        _playerThreshold = value;
        UpdateRoleSettings();
    }

    private void OnPlayerStatusChanged(object? sender, Robust.Shared.Player.SessionStatusEventArgs e)
    {
        UpdateRoleSettings();
    }

     private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        UpdateRoleSettings();
    }

    /// <summary>
    /// Checks the current player count and updates the role timer and whitelist CVars accordingly.
    /// </summary>
    private void UpdateRoleSettings()
    {
        if (!_dynamicRolesEnabled)
            return;

        var playerCount = _playerManager.PlayerCount;

        var shouldBeEnabled = playerCount > _playerThreshold;

        if (_cfg.GetCVar(CCVars.GameRoleTimers) != shouldBeEnabled)
        {
            _cfg.SetCVar(CCVars.GameRoleTimers, shouldBeEnabled);
        }

        if (_cfg.GetCVar(CCVars.GameRoleWhitelist) != shouldBeEnabled)
        {
            _cfg.SetCVar(CCVars.GameRoleWhitelist, shouldBeEnabled);
        }
    }
}
