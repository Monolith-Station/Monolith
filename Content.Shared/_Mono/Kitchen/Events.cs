// SPDX-FileCopyrightText: 2025 imatsoup
// SPDX-FileCopyrightText: 2025 tonotom1
//
// SPDX-License-Identifier: MPL-2.0


using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen;
[Serializable, NetSerializable]
public sealed partial class ContainerDoAfterEvent : SimpleDoAfterEvent { }
