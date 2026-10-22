// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.
using System.Numerics;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Client.UserInterface.Controls;
using System.Text.RegularExpressions;
using System;

namespace Content.Client.Shuttles.UI
{
    public sealed partial class NavScreen
    {
        private readonly ButtonGroup _buttonGroup = new();
        public event Action<NetEntity?, InertiaDampeningMode>? OnInertiaDampeningModeChanged;
        public event Action<float?>? OnMaxShuttleSpeedChanged;
        public event Action<string, string>? OnNetworkPortButtonPressed;
        public event Action<NetEntity?, Vector2>? OnSetTargetCoordinates;
        public event Action<NetEntity?, bool>? OnSetHideTarget;

        private bool _targetCoordsModified = false;

        private void NfInitialize()
        {
            IffSearchCriteria.OnTextChanged += args => OnIffSearchChanged(args.Text);

            MaximumIFFDistanceValue.GetChild(0).GetChild(1).Margin = new Thickness(8, 0, 0, 0);
            MaximumIFFDistanceValue.OnValueChanged += args => OnRangeFilterChanged(args);

            // Frontier - Maximum Shuttle Speed
            MaximumShuttleSpeedValue.OnTextChanged += args => OnMaxSpeedChanged(args);

            DampenerOff.OnPressed += _ => SetDampenerMode(InertiaDampeningMode.Off);
            DampenerOn.OnPressed += _ => SetDampenerMode(InertiaDampeningMode.Dampen);
            AnchorOn.OnPressed += _ => SetDampenerMode(InertiaDampeningMode.Anchor);

            DampenerOff.Group = _buttonGroup;
            DampenerOn.Group = _buttonGroup;
            AnchorOn.Group = _buttonGroup;

            // Network Port Buttons
            DeviceButton1.OnPressed += _ => OnPortButtonPressed("device-button-1", "button-1");
            DeviceButton2.OnPressed += _ => OnPortButtonPressed("device-button-2", "button-2");
            DeviceButton3.OnPressed += _ => OnPortButtonPressed("device-button-3", "button-3");
            DeviceButton4.OnPressed += _ => OnPortButtonPressed("device-button-4", "button-4");
            DeviceButton5.OnPressed += _ => OnPortButtonPressed("device-button-5", "button-5");
            DeviceButton6.OnPressed += _ => OnPortButtonPressed("device-button-6", "button-6");
            DeviceButton7.OnPressed += _ => OnPortButtonPressed("device-button-7", "button-7");
            DeviceButton8.OnPressed += _ => OnPortButtonPressed("device-button-8", "button-8");

            // Send off a request to get the current dampening mode.
            _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
            OnInertiaDampeningModeChanged?.Invoke(shuttle, InertiaDampeningMode.Query);

            TargetX.OnTextChanged += _ => _targetCoordsModified = true;
            TargetY.OnTextChanged += _ => _targetCoordsModified = true;
            TargetSet.OnPressed += _ => SetTargetCoords();
            TargetShow.OnPressed += _ => SetHideTarget(!TargetShow.Pressed);
        }

        private void OnPortButtonPressed(string sourcePort, string targetPort)
        {
            OnNetworkPortButtonPressed?.Invoke(sourcePort, targetPort);
        }

        private void SetDampenerMode(InertiaDampeningMode mode)
        {
            NavRadar.DampeningMode = mode;
            _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
            OnInertiaDampeningModeChanged?.Invoke(shuttle, mode);
        }

        private void NfUpdateState(NavInterfaceState state)
        {
            if (NavRadar.DampeningMode == InertiaDampeningMode.Station)
            {
                DampenerModeButtons.Visible = false;
            }
            else
            {
                DampenerModeButtons.Visible = true;
                DampenerOff.Pressed = NavRadar.DampeningMode == InertiaDampeningMode.Off;
                DampenerOn.Pressed = NavRadar.DampeningMode == InertiaDampeningMode.Dampen;
                AnchorOn.Pressed = NavRadar.DampeningMode == InertiaDampeningMode.Anchor;

                // Disable the Park button (AnchorOn) while in FTL, but keep other dampener buttons enabled
                if (NavRadar.InFtl)
                {
                    AnchorOn.Disabled = true;
                    // If the AnchorOn button is pressed while it gets disabled, we need to switch to another mode
                    if (AnchorOn.Pressed)
                    {
                        DampenerOn.Pressed = true;
                        SetDampenerMode(InertiaDampeningMode.Dampen);
                    }
                }
                else
                {
                    AnchorOn.Disabled = false;
                }
            }

            TargetShow.Pressed = !state.HideTarget;
            if (!_targetCoordsModified)
            {
                if (state.Target != null)
                {
                    var target = state.Target.Value;
                    TargetX.Text = target.X.ToString("F1");
                    TargetY.Text = target.Y.ToString("F1");
                }
                else
                {
                    TargetX.Text = 0.0f.ToString("F1");
                    TargetY.Text = 0.0f.ToString("F1");
                }
            }
        }

        private void OnRangeFilterChanged(int value)
        {
            NavRadar.MaximumIFFDistance = value;
        }

        // Frontier - Maximum Shuttle Speed
        private void OnMaxSpeedChanged(LineEdit.LineEditEventArgs value)
        {
            MaximumShuttleSpeedValue.Text = Regex.Replace(MaximumShuttleSpeedValue.Text, "[^0-9]", "");
            float.TryParse(MaximumShuttleSpeedValue.Text, out var speed);
            OnMaxShuttleSpeedChanged?.Invoke(MaximumShuttleSpeedValue.Text == "" ? null : speed);
        }

        private void NfAddShuttleDesignation(EntityUid? shuttle)
        {
            if (_entManager.TryGetComponent<MetaDataComponent>(shuttle, out var metadata))
            {
                var shipName = metadata.EntityName;

                // Try to find a designation in the format XXX-### (like CIV-748)
                // by checking each word in the ship name
                var shipNameParts = shipName.Split(' ');

                foreach (var part in shipNameParts)
                {
                    // Check if this part matches the designation format (e.g., CIV-748)
                    // The format is 2+ characters, followed by a dash, followed by more characters
                    if (part.Length > 3 && part.Contains('-'))
                    {
                        var dashIndex = part.IndexOf('-');
                        if (dashIndex >= 2 && dashIndex < part.Length - 1)
                        {
                            // This part looks like a designation
                            NavDisplayLabel.Text = shipName.Replace(part, "").Trim();
                            ShuttleDesignation.Text = part;
                            return;
                        }
                    }
                }

                // If we get here, no designation was found, so just show the full name
                NavDisplayLabel.Text = shipName;
                // Leave ShuttleDesignation.Text as "Unknown" (the default)
            }
        }

        private void SetTargetCoords()
        {
            Vector2 outputVector;
            if (!float.TryParse(TargetX.Text, out outputVector.X))
                outputVector.X = 0.0f;

            if (!float.TryParse(TargetY.Text, out outputVector.Y))
                outputVector.Y = 0.0f;

            NavRadar.Target = outputVector;
            NavRadar.TargetEntity = NetEntity.Invalid;
            _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
            OnSetTargetCoordinates?.Invoke(shuttle, outputVector);
            _targetCoordsModified = false;
        }

        private void SetHideTarget(bool hide)
        {
            _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
            OnSetHideTarget?.Invoke(shuttle, hide);
        }
    }
}
