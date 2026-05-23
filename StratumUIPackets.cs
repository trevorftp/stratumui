using System;
using System.Collections.Generic;
using ProtoBuf;

#nullable disable

namespace Vintagestory.Stratum.UI;

[Flags]
public enum StratumPlayerActionFlags
{
    None = 0,
    Report = 1 << 0,
    Kick = 1 << 1,
    Ban = 1 << 2,
    Freeze = 1 << 3,
    Jail = 1 << 4,
    Warn = 1 << 5,
    Mute = 1 << 6,
    ViewDetails = 1 << 7
}

[ProtoContract]
public enum StratumPlayerActionKind
{
    [ProtoEnum]
    Report = 1,

    [ProtoEnum]
    Kick = 2,

    [ProtoEnum]
    Ban = 3,

    [ProtoEnum]
    Freeze = 4,

    [ProtoEnum]
    Jail = 5,

    [ProtoEnum]
    Warn = 6,

    [ProtoEnum]
    Mute = 7,

    [ProtoEnum]
    ViewDetails = 8
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class StratumRosterPacket
{
    public StratumRosterEntry[] Players;
    public int OnlineCount;
    public int MaxPlayers;
    public long ServerTimeMilliseconds;
}

[ProtoContract]
public class StratumRosterEntry
{
    // Fields 1-7 preserve the original implicit roster-entry wire layout.
    [ProtoMember(8)]
    public string PlayerUid { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string PlayerName;

    [ProtoMember(5)]
    public string RoleCode;

    [ProtoMember(7)]
    public string RoleName;

    [ProtoMember(6)]
    public string RoleColor;

    [ProtoMember(1)]
    public string GameMode;

    [ProtoMember(3)]
    public int PingMs;

    [ProtoMember(2)]
    public bool IsStaff;

    [ProtoMember(9)]
    public int ActionFlags { get; set; }

    [ProtoMember(10)]
    public bool HasModerationCounts { get; set; }

    [ProtoMember(11)]
    public int ActiveWarnings { get; set; }

    [ProtoMember(12)]
    public int ActiveViolations { get; set; }
}

[ProtoContract]
public class StratumPlayerActionRequest
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)]
    public StratumPlayerActionKind Action { get; set; }

    [ProtoMember(3)]
    public string Reason { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string Duration { get; set; } = string.Empty;
}

[ProtoContract]
public class StratumPlayerActionResultPacket
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; } = string.Empty;
}

[ProtoContract]
public class StratumPlayerDetailRequest
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;
}

[ProtoContract]
public class StratumPlayerModerationRequest
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;
}

[ProtoContract]
public class StratumPlayerModerationPacket
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<StratumModerationEntryPacket> Records { get; set; } = new();
}

[ProtoContract]
public class StratumModerationEntryPacket
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Text { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string ActorName { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string CreatedUtc { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string ExpiresUtc { get; set; } = string.Empty;

    [ProtoMember(7)]
    public bool Active { get; set; }

    [ProtoMember(8)]
    public string ClosedUtc { get; set; } = string.Empty;

    [ProtoMember(9)]
    public string ClosedBy { get; set; } = string.Empty;

    [ProtoMember(10)]
    public string CloseReason { get; set; } = string.Empty;
}

[ProtoContract]
public class StratumPlayerDetailPacket
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string PlayerName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string RoleCode { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string RoleName { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string RoleColor { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string GameMode { get; set; } = string.Empty;

    [ProtoMember(7)]
    public int PingMs { get; set; }

    [ProtoMember(8)]
    public int ActiveWarnings { get; set; }

    [ProtoMember(9)]
    public int ActiveViolations { get; set; }

    [ProtoMember(10)]
    public List<StratumPlayerInventorySlotPacket> InventorySlots { get; set; } = new();

    [ProtoMember(11)]
    public List<StratumPlayerStatPacket> Stats { get; set; } = new();

    [ProtoMember(12)]
    public string Position { get; set; } = string.Empty;

    [ProtoMember(13)]
    public long EntityId { get; set; }

    [ProtoMember(14)]
    public List<StratumPlayerInventoryGroupPacket> InventoryGroups { get; set; } = new();
}

[ProtoContract]
public class StratumPlayerInventoryGroupPacket
{
    [ProtoMember(1)]
    public string InventoryCode { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string ClassName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public int SlotCount { get; set; }
}

[ProtoContract]
public class StratumPlayerInventorySlotPacket
{
    [ProtoMember(1)]
    public string InventoryCode { get; set; } = string.Empty;

    [ProtoMember(2)]
    public int SlotId { get; set; }

    [ProtoMember(3)]
    public string ItemName { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string ItemCode { get; set; } = string.Empty;

    [ProtoMember(5)]
    public int StackSize { get; set; }
}

[ProtoContract]
public class StratumPlayerStatPacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Value { get; set; } = string.Empty;
}