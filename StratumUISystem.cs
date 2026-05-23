using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumUISystem : ModSystem
{
    private const string ChannelName = "stratumui";
    private const string TabListHotKey = "stratumui-tablist";
    private const string ChatHistoryHotKey = "stratumui-chat-history";
    private const string PlayerRecordsKey = "stratum.moderation.records.v1";
    // Roles that get the staff-only action menu. Anything else gets a read-only roster.
    private static readonly HashSet<string> StaffRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "sumod",
        "crmod"
    };

    private ICoreServerAPI sapi;
    private ICoreClientAPI capi;
    private IServerNetworkChannel serverChannel;
    private IClientNetworkChannel clientChannel;
    private long serverTickListenerId;
    private long clientFallbackTickListenerId;
    private StratumTabListDialog tabListDialog;
    private StratumChatHistoryDialog chatHistoryDialog;
    private StratumPlayerDetailDialog playerDetailDialog;
    private StratumPlayerModerationDialog playerModerationDialog;
    private StratumCommandAutocomplete commandAutocomplete;
    private StratumClientConfig clientConfig;

    public override bool ShouldLoad(EnumAppSide side)
    {
        return true;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        // Channel name has to match on both sides or the handshake silently no-ops.
        serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType(typeof(StratumRosterPacket))
            .RegisterMessageType(typeof(StratumPlayerActionRequest))
            .RegisterMessageType(typeof(StratumPlayerActionResultPacket))
            .RegisterMessageType(typeof(StratumPlayerDetailRequest))
            .RegisterMessageType(typeof(StratumPlayerDetailPacket))
            .RegisterMessageType(typeof(StratumPlayerModerationRequest))
            .RegisterMessageType(typeof(StratumPlayerModerationPacket))
            .SetMessageHandler<StratumPlayerActionRequest>(OnPlayerActionRequest)
            .SetMessageHandler<StratumPlayerDetailRequest>(OnPlayerDetailRequest)
            .SetMessageHandler<StratumPlayerModerationRequest>(OnPlayerModerationRequest);

        api.Event.PlayerNowPlaying += OnPlayerRosterChanged;
        api.Event.PlayerLeave += OnPlayerRosterChanged;
        api.Event.PlayerDisconnect += OnPlayerRosterChanged;
        serverTickListenerId = api.Event.RegisterGameTickListener(_ => BroadcastRoster(), 2000, 750);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientConfig = StratumClientConfig.LoadOrCreate(api);
        // On vanilla servers nothing answers this channel; Connected stays false and we degrade gracefully.
        clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType(typeof(StratumRosterPacket))
            .RegisterMessageType(typeof(StratumPlayerActionRequest))
            .RegisterMessageType(typeof(StratumPlayerActionResultPacket))
            .RegisterMessageType(typeof(StratumPlayerDetailRequest))
            .RegisterMessageType(typeof(StratumPlayerDetailPacket))
            .RegisterMessageType(typeof(StratumPlayerModerationRequest))
            .RegisterMessageType(typeof(StratumPlayerModerationPacket))
            .SetMessageHandler<StratumRosterPacket>(OnRosterPacket)
            .SetMessageHandler<StratumPlayerActionResultPacket>(OnPlayerActionResultPacket)
            .SetMessageHandler<StratumPlayerDetailPacket>(OnPlayerDetailPacket)
            .SetMessageHandler<StratumPlayerModerationPacket>(OnPlayerModerationPacket);

        tabListDialog = new StratumTabListDialog(api, clientChannel);
        chatHistoryDialog = new StratumChatHistoryDialog(api);
        commandAutocomplete = new StratumCommandAutocomplete(api);
        api.Gui.RegisterDialog(tabListDialog);
        api.Gui.RegisterDialog(chatHistoryDialog);

        api.Input.RegisterHotKeyFirst(TabListHotKey, "Stratum tab list", GlKeys.Tab, HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler(TabListHotKey, _ =>
        {
            if (commandAutocomplete?.TryCompleteFromChatHotkey() == true)
            {
                return true;
            }

            tabListDialog.Toggle();
            return true;
        });

        api.Input.RegisterHotKey(ChatHistoryHotKey, "Stratum chat history", GlKeys.T, HotkeyType.GUIOrOtherControls, ctrlPressed: true);
        api.Input.SetHotKeyHandler(ChatHistoryHotKey, _ =>
        {
            chatHistoryDialog.Toggle();
            return true;
        });

        api.Event.ChatMessage += OnClientChatMessage;
        api.Event.LeaveWorld += OnClientLeaveWorld;

        // Vanilla / non-Stratum server fallback: the server won't send a roster packet, so we synthesise
        // one from the client's own known-player list. Harmless when the channel IS connected because
        // the server packet arrives more often and just overwrites this.
        clientFallbackTickListenerId = api.Event.RegisterGameTickListener(_ => TryPushClientFallbackRoster(), 1500, 500);
    }

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.PlayerNowPlaying -= OnPlayerRosterChanged;
            sapi.Event.PlayerLeave -= OnPlayerRosterChanged;
            sapi.Event.PlayerDisconnect -= OnPlayerRosterChanged;
            if (serverTickListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(serverTickListenerId);
            }
        }

        if (capi != null)
        {
            capi.Event.ChatMessage -= OnClientChatMessage;
            capi.Event.LeaveWorld -= OnClientLeaveWorld;
            if (clientFallbackTickListenerId != 0)
            {
                capi.Event.UnregisterGameTickListener(clientFallbackTickListenerId);
            }
        }

        tabListDialog?.Dispose();
        chatHistoryDialog?.Dispose();
        playerDetailDialog?.Dispose();
        playerModerationDialog?.Dispose();
        commandAutocomplete?.Dispose();
    }

    private void OnPlayerRosterChanged(IServerPlayer player)
    {
        BroadcastRoster();
    }

    private void BroadcastRoster()
    {
        if (serverChannel == null || sapi?.World == null)
        {
            return;
        }

        foreach (IServerPlayer player in GetOnlineServerPlayers())
        {
            serverChannel.SendPacket(BuildRosterPacket(player), player);
        }
    }

    private StratumRosterPacket BuildRosterPacket(IServerPlayer viewer)
    {
        StratumRosterEntry[] entries = GetOnlineServerPlayers()
            .OrderByDescending(player => StaffRoles.Contains(player.Role?.Code ?? ""))
            .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .Select(player => ToRosterEntry(viewer, player))
            .ToArray();

        return new StratumRosterPacket
        {
            Players = entries,
            OnlineCount = entries.Length,
            MaxPlayers = sapi.Server.Config.MaxClients,
            ServerTimeMilliseconds = sapi.World.ElapsedMilliseconds
        };
    }

    private IEnumerable<IServerPlayer> GetOnlineServerPlayers()
    {
        return sapi.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .Where(player => player.ConnectionState == EnumClientState.Playing);
    }

    private static StratumRosterEntry ToRosterEntry(IServerPlayer viewer, IServerPlayer player)
    {
        string roleCode = player.Role?.Code ?? player.ServerData?.RoleCode ?? "player";
        StratumModerationCounts counts = HasStaffUiAccess(viewer) ? GetModerationCounts(player.ServerData) : default;
        return new StratumRosterEntry
        {
            PlayerUid = player.PlayerUID,
            PlayerName = player.PlayerName,
            RoleCode = roleCode,
            RoleName = player.Role?.Name ?? roleCode,
            RoleColor = ToHexColor(player.Role?.Color),
            GameMode = player.WorldData?.CurrentGameMode.ToString() ?? "Unknown",
            PingMs = ToPingMs(player.Ping),
            IsStaff = StaffRoles.Contains(roleCode),
            ActionFlags = (int)BuildActionFlags(viewer, player),
            HasModerationCounts = HasStaffUiAccess(viewer),
            ActiveWarnings = counts.Warnings,
            ActiveViolations = counts.Violations
        };
    }

    private void OnPlayerActionRequest(IServerPlayer fromPlayer, StratumPlayerActionRequest packet)
    {
        if (!TryGetOnlinePlayer(packet?.TargetPlayerUid, out IServerPlayer targetPlayer))
        {
            SendActionResult(fromPlayer, false, "Target player is no longer online.");
            return;
        }

        StratumPlayerActionFlags requiredFlag = FlagForAction(packet.Action);
        if (requiredFlag == StratumPlayerActionFlags.None || (BuildActionFlags(fromPlayer, targetPlayer) & requiredFlag) == 0)
        {
            SendActionResult(fromPlayer, false, "You are not allowed to use that action.");
            return;
        }

        if (packet.Action == StratumPlayerActionKind.ViewDetails)
        {
            SendPlayerDetails(fromPlayer, targetPlayer);
            return;
        }

        if (!TryBuildActionCommand(packet, targetPlayer, out string command, out string error))
        {
            SendActionResult(fromPlayer, false, error);
            return;
        }

        TextCommandCallingArgs args = new TextCommandCallingArgs
        {
            LanguageCode = fromPlayer.LanguageCode,
            Caller = new Caller
            {
                Player = fromPlayer,
                FromChatGroupId = GlobalConstants.GeneralChatGroup
            }
        };

        sapi.ChatCommands.ExecuteUnparsed(command, args, result =>
        {
            bool success = result?.Status == EnumCommandStatus.Success;
            string message = string.IsNullOrWhiteSpace(result?.StatusMessage)
                ? (success ? "Action completed." : "Action failed.")
                : result.StatusMessage;
            SendActionResult(fromPlayer, success, message);
            BroadcastRoster();
        });
    }

    private void OnPlayerDetailRequest(IServerPlayer fromPlayer, StratumPlayerDetailRequest packet)
    {
        if (!TryGetOnlinePlayer(packet?.TargetPlayerUid, out IServerPlayer targetPlayer))
        {
            SendActionResult(fromPlayer, false, "Target player is no longer online.");
            return;
        }

        if ((BuildActionFlags(fromPlayer, targetPlayer) & StratumPlayerActionFlags.ViewDetails) == 0)
        {
            SendActionResult(fromPlayer, false, "You are not allowed to view that player's details.");
            return;
        }

        SendPlayerDetails(fromPlayer, targetPlayer);
    }

    private void OnPlayerModerationRequest(IServerPlayer fromPlayer, StratumPlayerModerationRequest packet)
    {
        if (serverChannel == null || string.IsNullOrWhiteSpace(packet?.TargetPlayerUid))
        {
            return;
        }

        if (!HasStaffUiAccess(fromPlayer))
        {
            SendActionResult(fromPlayer, false, "You are not allowed to view moderation history.");
            return;
        }

        IServerPlayerData targetData = TryGetOnlinePlayer(packet.TargetPlayerUid, out IServerPlayer onlineTarget)
            ? onlineTarget.ServerData
            : sapi.PlayerData.GetPlayerDataByUid(packet.TargetPlayerUid);

        StratumPlayerModerationPacket response = new StratumPlayerModerationPacket
        {
            TargetPlayerUid = packet.TargetPlayerUid,
            Records = BuildModerationRecordPackets(targetData)
        };

        serverChannel.SendPacket(response, fromPlayer);
    }

    private void SendPlayerDetails(IServerPlayer viewer, IServerPlayer target)
    {
        if (serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildPlayerDetailPacket(target), viewer);
    }

    private StratumPlayerDetailPacket BuildPlayerDetailPacket(IServerPlayer target)
    {
        string roleCode = target.Role?.Code ?? target.ServerData?.RoleCode ?? "player";
        StratumModerationCounts counts = GetModerationCounts(target.ServerData);
        List<StratumPlayerInventoryGroupPacket> groups = new List<StratumPlayerInventoryGroupPacket>();
        return new StratumPlayerDetailPacket
        {
            TargetPlayerUid = target.PlayerUID,
            PlayerName = target.PlayerName,
            RoleCode = roleCode,
            RoleName = target.Role?.Name ?? roleCode,
            RoleColor = ToHexColor(target.Role?.Color),
            GameMode = target.WorldData?.CurrentGameMode.ToString() ?? "Unknown",
            PingMs = ToPingMs(target.Ping),
            ActiveWarnings = counts.Warnings,
            ActiveViolations = counts.Violations,
            InventorySlots = BuildInventorySnapshot(target, groups),
            InventoryGroups = groups,
            Stats = BuildStatSnapshot(target),
            Position = FormatPosition(target),
            EntityId = target.Entity?.EntityId ?? 0
        };
    }

    private static readonly HashSet<string> InventoryClassesToReport = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "character",
        "hotbar",
        "backpack"
    };

    private List<StratumPlayerInventorySlotPacket> BuildInventorySnapshot(IServerPlayer target, List<StratumPlayerInventoryGroupPacket> groups)
    {
        List<StratumPlayerInventorySlotPacket> slots = new List<StratumPlayerInventorySlotPacket>();
        if (target.InventoryManager?.InventoriesOrdered == null)
        {
            return slots;
        }

        foreach (InventoryBase inventory in target.InventoryManager.InventoriesOrdered)
        {
            if (inventory == null)
            {
                continue;
            }

            string className = inventory.ClassName ?? string.Empty;
            if (!InventoryClassesToReport.Contains(className))
            {
                continue;
            }

            AppendInventorySlots(slots, inventory, groups, className);
        }

        return slots;
    }

    private void AppendInventorySlots(List<StratumPlayerInventorySlotPacket> slots, InventoryBase inventory, List<StratumPlayerInventoryGroupPacket> groups, string className)
    {
        int count;
        try
        {
            count = inventory.Count;
        }
        catch (Exception)
        {
            // Some inventories (e.g. InventoryPlayerCreative for non-creative players) throw on Count.
            return;
        }

        string inventoryCode = inventory.InventoryID ?? inventory.ClassName ?? "inventory";

        groups.Add(new StratumPlayerInventoryGroupPacket
        {
            InventoryCode = inventoryCode,
            ClassName = className,
            SlotCount = count
        });

        for (int slotId = 0; slotId < count; slotId++)
        {
            ItemSlot slot;
            try
            {
                slot = inventory[slotId];
            }
            catch (Exception)
            {
                continue;
            }

            if (slot?.Empty != false || slot.Itemstack == null)
            {
                continue;
            }

            ItemStack stack = slot.Itemstack;
            slots.Add(new StratumPlayerInventorySlotPacket
            {
                InventoryCode = inventoryCode,
                SlotId = slotId,
                ItemName = stack.GetName() ?? stack.Collectible?.Code?.ToShortString() ?? "Unknown item",
                ItemCode = stack.Collectible?.Code?.ToShortString() ?? string.Empty,
                StackSize = stack.StackSize
            });
        }
    }

    private static List<StratumPlayerStatPacket> BuildStatSnapshot(IServerPlayer target)
    {
        List<StratumPlayerStatPacket> stats = new List<StratumPlayerStatPacket>();
        if (target.Entity == null)
        {
            return stats;
        }

        var healthTree = target.Entity.WatchedAttributes.GetTreeAttribute("health");
        if (healthTree != null)
        {
            stats.Add(new StratumPlayerStatPacket { Code = "health", Value = FormatFloat(healthTree.GetFloat("currenthealth")) + " / " + FormatFloat(healthTree.GetFloat("maxhealth")) });
        }

        var hungerTree = target.Entity.WatchedAttributes.GetTreeAttribute("hunger");
        if (hungerTree != null)
        {
            stats.Add(new StratumPlayerStatPacket { Code = "saturation", Value = FormatFloat(hungerTree.GetFloat("currentsaturation")) + " / " + FormatFloat(hungerTree.GetFloat("maxsaturation")) });
        }

        foreach (KeyValuePair<string, EntityFloatStats> entry in target.Entity.Stats)
        {
            stats.Add(new StratumPlayerStatPacket
            {
                Code = entry.Key,
                Value = FormatFloat(entry.Value.GetBlended())
            });
        }

        return stats;
    }

    private static string FormatPosition(IServerPlayer target)
    {
        if (target.Entity?.Pos == null)
        {
            return string.Empty;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.0}, {1:0.0}, {2:0.0}", target.Entity.Pos.X, target.Entity.Pos.Y, target.Entity.Pos.Z);
    }

    private bool TryGetOnlinePlayer(string playerUid, out IServerPlayer player)
    {
        player = GetOnlineServerPlayers().FirstOrDefault(entry => string.Equals(entry.PlayerUID, playerUid, StringComparison.Ordinal));
        return player != null;
    }

    private void SendActionResult(IServerPlayer player, bool success, string message)
    {
        serverChannel?.SendPacket(new StratumPlayerActionResultPacket
        {
            Success = success,
            Message = message ?? string.Empty
        }, player);
    }

    private static bool TryBuildActionCommand(StratumPlayerActionRequest packet, IServerPlayer targetPlayer, out string command, out string error)
    {
        string targetName = targetPlayer.PlayerName;
        string reason = NormalizeCommandText(packet.Reason);
        string duration = NormalizeCommandText(packet.Duration);
        command = null;
        error = null;

        switch (packet.Action)
        {
            case StratumPlayerActionKind.Report:
                if (string.IsNullOrWhiteSpace(reason))
                {
                    error = "Report requires a reason.";
                    return false;
                }
                command = "/report " + targetName + " " + reason;
                return true;
            case StratumPlayerActionKind.Kick:
                command = "/kick " + targetName + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " " + reason);
                return true;
            case StratumPlayerActionKind.Ban:
                if (string.IsNullOrWhiteSpace(duration) || string.IsNullOrWhiteSpace(reason))
                {
                    error = "Ban requires an until/duration value and a reason.";
                    return false;
                }
                command = "/ban " + targetName + " " + duration + " " + reason;
                return true;
            case StratumPlayerActionKind.Freeze:
                command = "/freeze " + targetName + " toggle";
                return true;
            case StratumPlayerActionKind.Jail:
                command = "/jail " + targetName + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " " + reason);
                return true;
            case StratumPlayerActionKind.Warn:
                if (string.IsNullOrWhiteSpace(reason))
                {
                    error = "Warn requires a reason.";
                    return false;
                }
                command = "/warn " + targetName + " " + reason;
                return true;
            case StratumPlayerActionKind.Mute:
                if (string.IsNullOrWhiteSpace(duration) || string.IsNullOrWhiteSpace(reason))
                {
                    error = "Mute requires a duration and a reason.";
                    return false;
                }
                command = "/mute " + targetName + " " + duration + " " + reason;
                return true;
            default:
                error = "Unsupported action.";
                return false;
        }
    }

    private static StratumPlayerActionFlags BuildActionFlags(IServerPlayer viewer, IServerPlayer target)
    {
        if (viewer == null || target == null || string.Equals(viewer.PlayerUID, target.PlayerUID, StringComparison.Ordinal))
        {
            return StratumPlayerActionFlags.None;
        }

        StratumPlayerActionFlags flags = StratumPlayerActionFlags.Report;
        if (viewer.HasPrivilege(Privilege.kick))
        {
            flags |= StratumPlayerActionFlags.Kick;
        }

        if (viewer.HasPrivilege(Privilege.ban))
        {
            flags |= StratumPlayerActionFlags.Ban;
        }

        if (HasStaffUiAccess(viewer))
        {
            flags |= StratumPlayerActionFlags.Freeze | StratumPlayerActionFlags.Jail | StratumPlayerActionFlags.Warn | StratumPlayerActionFlags.Mute | StratumPlayerActionFlags.ViewDetails;
        }

        return flags;
    }

    private static StratumPlayerActionFlags FlagForAction(StratumPlayerActionKind action)
    {
        switch (action)
        {
            case StratumPlayerActionKind.Report:
                return StratumPlayerActionFlags.Report;
            case StratumPlayerActionKind.Kick:
                return StratumPlayerActionFlags.Kick;
            case StratumPlayerActionKind.Ban:
                return StratumPlayerActionFlags.Ban;
            case StratumPlayerActionKind.Freeze:
                return StratumPlayerActionFlags.Freeze;
            case StratumPlayerActionKind.Jail:
                return StratumPlayerActionFlags.Jail;
            case StratumPlayerActionKind.Warn:
                return StratumPlayerActionFlags.Warn;
            case StratumPlayerActionKind.Mute:
                return StratumPlayerActionFlags.Mute;
            case StratumPlayerActionKind.ViewDetails:
                return StratumPlayerActionFlags.ViewDetails;
            default:
                return StratumPlayerActionFlags.None;
        }
    }

    private static bool HasStaffUiAccess(IServerPlayer player)
    {
        string roleCode = player?.Role?.Code ?? player?.ServerData?.RoleCode ?? string.Empty;
        return StaffRoles.Contains(roleCode) || player?.HasPrivilege(Privilege.controlserver) == true || player?.HasPrivilege(Privilege.kick) == true || player?.HasPrivilege(Privilege.ban) == true;
    }

    private static List<StratumModerationEntryPacket> BuildModerationRecordPackets(IServerPlayerData data)
    {
        // Records live in CustomPlayerData under the same JSON key the server-side moderation store writes.
        List<StratumModerationEntryPacket> result = new List<StratumModerationEntryPacket>();
        if (data?.CustomPlayerData == null || !data.CustomPlayerData.TryGetValue(PlayerRecordsKey, out string json) || string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            List<LocalModerationRecord> records = JsonSerializer.Deserialize<List<LocalModerationRecord>>(json) ?? new List<LocalModerationRecord>();
            foreach (LocalModerationRecord record in records.OrderByDescending(r => r.CreatedUtc))
            {
                result.Add(new StratumModerationEntryPacket
                {
                    Id = record.Id,
                    Type = record.Type ?? string.Empty,
                    Text = record.Text ?? string.Empty,
                    ActorName = record.ActorName ?? string.Empty,
                    CreatedUtc = FormatUtc(record.CreatedUtc),
                    ExpiresUtc = FormatUtc(record.ExpiresUtc),
                    Active = record.Active,
                    ClosedUtc = FormatUtc(record.ClosedUtc),
                    ClosedBy = record.ClosedBy ?? string.Empty,
                    CloseReason = record.CloseReason ?? string.Empty
                });
            }
        }
        catch
        {
        }

        return result;
    }

    private static string FormatUtc(DateTime? value)
    {
        return value.HasValue ? value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static StratumModerationCounts GetModerationCounts(IServerPlayerData data)
    {
        if (data?.CustomPlayerData == null || !data.CustomPlayerData.TryGetValue(PlayerRecordsKey, out string json) || string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            List<LocalModerationRecord> records = JsonSerializer.Deserialize<List<LocalModerationRecord>>(json) ?? new List<LocalModerationRecord>();
            return new StratumModerationCounts
            {
                Warnings = records.Count(record => record.Active && string.Equals(record.Type, "warning", StringComparison.OrdinalIgnoreCase)),
                Violations = records.Count(record => record.Active && string.Equals(record.Type, "mute", StringComparison.OrdinalIgnoreCase))
            };
        }
        catch
        {
            return default;
        }
    }

    private static string NormalizeCommandText(string text)
    {
        return (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static int ToPingMs(float pingSeconds)
    {
        if (float.IsNaN(pingSeconds) || pingSeconds < 0)
        {
            return -1;
        }

        return Math.Max(0, (int)Math.Round(pingSeconds * 1000));
    }

    private static string ToHexColor(Color? color)
    {
        if (color == null || color.Value.IsEmpty || color.Value.A == 0)
        {
            return "#d7c7a3";
        }

        Color value = color.Value;
        return $"#{value.R:x2}{value.G:x2}{value.B:x2}";
    }

    private void OnRosterPacket(StratumRosterPacket packet)
    {
        tabListDialog?.UpdateRoster(packet);
        commandAutocomplete?.UpdateRoster(packet);
    }

    // Build a minimal roster from the client's own player list. Used on vanilla servers where the
    // StratumUI channel never handshakes, so we have no server-driven roster to show. Most rich data
    // (ping, moderation counts, action flags) is unavailable client-side - those just stay empty.
    private void TryPushClientFallbackRoster()
    {
        if (capi?.World == null)
        {
            return;
        }

        // Channel up = real roster is flowing. Don't fight it.
        if (clientChannel?.Connected == true)
        {
            return;
        }

        IPlayer[] players = capi.World.AllOnlinePlayers ?? Array.Empty<IPlayer>();
        StratumRosterEntry[] entries = players
            .Where(p => p != null && !string.IsNullOrEmpty(p.PlayerUID))
            .OrderBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase)
            .Select(ToClientFallbackEntry)
            .ToArray();

        StratumRosterPacket packet = new StratumRosterPacket
        {
            Players = entries,
            OnlineCount = entries.Length,
            MaxPlayers = 0,
            ServerTimeMilliseconds = capi.World.ElapsedMilliseconds
        };

        tabListDialog?.UpdateRoster(packet);
        commandAutocomplete?.UpdateRoster(packet);
    }

    private static StratumRosterEntry ToClientFallbackEntry(IPlayer player)
    {
        // Vanilla / non-Stratum: we have no reliable role or ping for anyone (including the local
        // player, whose cached role may be stale from another server). Show everyone as "player" and
        // mark ping as unknown (-1) so the dialog hides the pill instead of rendering "0 ms".
        string gameMode;
        try { gameMode = player.WorldData?.CurrentGameMode.ToString() ?? "Unknown"; }
        catch { gameMode = "Unknown"; }

        return new StratumRosterEntry
        {
            PlayerUid = player.PlayerUID,
            PlayerName = player.PlayerName ?? "(unknown)",
            RoleCode = "player",
            RoleName = "player",
            RoleColor = null,
            GameMode = gameMode,
            PingMs = -1,
            IsStaff = false,
            ActionFlags = 0,
            HasModerationCounts = false,
            ActiveWarnings = 0,
            ActiveViolations = 0
        };
    }

    private void OnPlayerActionResultPacket(StratumPlayerActionResultPacket packet)
    {
        // only write chat messages here for debugging
        // can be enabled via stratumui-client.json (ShowActionResultsInChat). off by default
        if (clientConfig?.ShowActionResultsInChat != true)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(packet?.Message))
        {
            capi.ShowChatMessage((packet.Success ? "[StratumUI] " : "[StratumUI] ") + packet.Message);
        }
    }

    private void OnPlayerDetailPacket(StratumPlayerDetailPacket packet)
    {
        if (packet == null)
        {
            return;
        }

        if (playerDetailDialog == null)
        {
            playerDetailDialog = new StratumPlayerDetailDialog(capi, clientChannel);
            capi.Gui.RegisterDialog(playerDetailDialog);
        }

        playerDetailDialog.UpdateDetails(packet);
        playerDetailDialog.TryOpen();
    }

    private void OnPlayerModerationPacket(StratumPlayerModerationPacket packet)
    {
        if (packet == null)
        {
            return;
        }

        if (playerModerationDialog == null)
        {
            playerModerationDialog = new StratumPlayerModerationDialog(capi);
            capi.Gui.RegisterDialog(playerModerationDialog);
        }

        playerModerationDialog.UpdateRecords(packet);
        playerModerationDialog.TryOpen();
    }

    private void OnClientChatMessage(int groupId, string message, EnumChatType chatType, string data)
    {
        chatHistoryDialog?.AddLine(groupId, message, chatType);
    }

    private void OnClientLeaveWorld()
    {
        tabListDialog?.UpdateRoster(new StratumRosterPacket { Players = Array.Empty<StratumRosterEntry>() });
        chatHistoryDialog?.Clear();
        commandAutocomplete?.Clear();
        playerDetailDialog?.TryClose();
    }

    private struct StratumModerationCounts
    {
        public int Warnings;
        public int Violations;
    }

    private sealed class LocalModerationRecord
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public bool Active { get; set; }
        public string Text { get; set; }
        public string ActorName { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public string ClosedBy { get; set; }
        public string CloseReason { get; set; }
    }
}