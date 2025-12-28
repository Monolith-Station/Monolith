// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.
using Content.Shared._NF.Shuttles.Events;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI
{
    public sealed partial class NavScreen
    {
        private readonly ButtonGroup _buttonGroup = new();
        public event Action<NetEntity?, InertiaDampeningMode>? OnInertiaDampeningModeChanged;
        public event Action<NetEntity?, float>? OnMaxShuttleSpeedChanged;
        public event Action<string, string>? OnNetworkPortButtonPressed;

        private void NfInitialize()
        {
            // Frontier - IFF search
            IffSearchCriteria.OnTextChanged += args => OnIffSearchChanged(args.Text);

            // Frontier - Maximum IFF Distance
            MaximumIFFDistanceValue.GetChild(0).GetChild(1).Margin = new Thickness(10, 0, 0, 0);
            MaximumIFFDistanceValue.OnValueChanged += args => OnRangeFilterChanged(args);

            // Frontier - Maximum Shuttle Speed
            MaximumShuttleSpeedValue.GetChild(0).GetChild(1).Margin = new Thickness(8, 0, 0, 0);
            MaximumShuttleSpeedValue.OnValueChanged += args => OnMaxSpeedChanged(args);

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

        private void NfUpdateState()
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
        }

        // Frontier - Maximum IFF Distance
        private void OnRangeFilterChanged(int value)
        {
            NavRadar.MaximumIFFDistance = (float) value;
        }

        // Frontier - Maximum Shuttle Speed
        private void OnMaxSpeedChanged(int value)
        {
            _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
            OnMaxShuttleSpeedChanged?.Invoke(shuttle, value);
        }

        private void NfAddShuttleDesignation(EntityUid? shuttle)
        {
            // Frontier - PR #1284 Add Shuttle Designation
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
            // End Frontier - PR #1284
        }

    }
}
