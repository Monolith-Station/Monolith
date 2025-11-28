// SPDX-FileCopyrightText: 2025 Aiden
//
// SPDX-License-Identifier: MPL-2.0

namespace Content.Shared._Goobstation.LastWords;

/// <summary>
/// Tracks the last words a user has said.
/// </summary>
[RegisterComponent]
public sealed partial class LastWordsComponent : Component
{
    [DataField]
    public string? LastWords;
}
