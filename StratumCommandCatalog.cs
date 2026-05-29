using System;
using System.Collections.Generic;

#nullable disable

namespace Vintagestory.Stratum.UI;

// Static catalog for the '/' command autocomplete popup. Client commands ('.') still come
// live from capi.ChatCommands.
//
// Add to ServerCommands for top-level entries; add to Subcommands for known subcommand trees
// shown at arg index 1.
internal static class StratumCommandCatalog
{
    internal sealed class Entry
    {
        public Entry(string name, string description)
        {
            Name = name;
            Description = description ?? string.Empty;
        }

        public string Name { get; }
        public string Description { get; }
    }

    // Top-level server commands, alphabetical.
    public static readonly Entry[] ServerCommands =
    {
        // Movement / teleport
        new("back", "TP to your previous location or death point"),
        new("delhome", "Delete one of your homes"),
        new("home", "Teleport to one of your homes"),
        new("homes", "List your homes"),
        new("near", "List nearby players"),
        new("sethome", "Set a home at your current location"),
        new("setspawn", "Set the server spawn here"),
        new("spawn", "Teleport to server spawn"),
        new("tp", "Teleport entity/player to a target or coords"),
        new("tpa", "Request to teleport to a player"),
        new("tpacancel", "Cancel your outgoing TP request"),
        new("tpaccept", "Accept a pending TP request"),
        new("tpadecline", "Decline a pending TP request"),
        new("tpadeny", "Deny a pending TP request"),
        new("tpahere", "Request a player to teleport to you"),
        new("tpwp", "Teleport to a waypoint by name"),

        // Communication
        new("announce", "Broadcast a message to all players"),
        new("announcenear", "Broadcast a message to nearby players"),
        new("chatclear", "Clear visible chat history"),
        new("clearchat", "Clear visible chat history"),
        new("discord", "Show the Discord link"),
        new("lockchat", "Lock or unlock player chat"),
        new("motd", "Show the message of the day"),
        new("msg", "Private message a player"),
        new("r", "Reply to the last PM"),
        new("reply", "Reply to the last PM"),
        new("rules", "Show server rules"),
        new("sbc", "Staff broadcast (highlighted)"),
        new("slowmode", "Set or clear chat slowmode"),
        new("staffbroadcast", "Staff broadcast (highlighted)"),
        new("staffchat", "Send a message to staff only"),
        new("tell", "Private message a player"),
        new("w", "Private message a player"),
        new("website", "Show the website link"),

        // Info / status
        new("clients", "List connected players"),
        new("help", "Show command help"),
        new("info", "Server information"),
        new("list", "List clients, banned players, roles, or privileges"),
        new("mystats", "Show your player stats"),
        new("online", "List connected players"),
        new("seen", "Show when a player was last seen"),
        new("serverinfo", "Stratum server info"),
        new("stats", "Server statistics"),
        new("stratum", "Stratum server info & diagnostics"),
        new("tps", "Show server tick rate (TPS/MSPT)"),
        new("uptime", "Show server uptime"),
        new("whois", "Show staff investigation details"),

        // Moderation
        new("ban", "Ban a player"),
        new("delwarn", "Delete a warning"),
        new("freeze", "Freeze / unfreeze a player"),
        new("hardban", "Permanent ban"),
        new("jail", "Jail a player"),
        new("jailstatus", "Check a player's jail status"),
        new("kick", "Kick a player"),
        new("mute", "Mute a player's chat"),
        new("mutestatus", "Check a player's mute status"),
        new("note", "Add a staff note on a player"),
        new("notes", "List staff notes on a player"),
        new("report", "Report a player"),
        new("reports", "List player reports"),
        new("revive", "Revive a dead player in place"),
        new("setjail", "Set the jail location"),
        new("unban", "Remove a player ban"),
        new("unjail", "Release a jailed player"),
        new("unmute", "Unmute a player"),
        new("vanish", "Toggle staff vanish mode"),
        new("warn", "Warn a player"),
        new("warnings", "List a player's warnings"),

        // Player / role / privs
        new("gamemode", "Get or set a player's gamemode"),
        new("gm", "Get or set a player's gamemode"),
        new("group", "Player group management"),
        new("groupinvite", "Toggle group invites"),
        new("op", "Give a player admin status"),
        new("player", "Player control (movespeed, privs, role, stats, etc.)"),
        new("role", "Manage roles (privs, claim limits, spawn point)"),
        new("self", "Show or change your own player state"),
        new("whitelist", "Whitelist management"),

        // Items / blocks / inventory
        new("activate", "Activate a targeted block"),
        new("clear", "Clear an inventory"),
        new("giveblock", "Give blocks to a player"),
        new("giveitem", "Give items to a player"),
        new("setblock", "Set a block at a location"),

        // Time / weather / world
        new("setambient", "Set the server ambient (JSON)"),
        new("time", "Get or set world time"),
        new("weather", "Show or set weather"),
        new("whenwillitstopraining", "Query when the rain will stop"),

        // Waypoints / land / chunks
        new("chunk", "Chunk management"),
        new("land", "Land claims and rights"),
        new("waypoint", "Manage waypoints"),

        // Entities
        new("e", "Entity control (alias of /entity)"),
        new("entity", "Entity control (cmd, debug, spawn, etc.)"),
        new("executeas", "Execute a command as an entity"),

        // World generation / debug
        new("debug", "Server debug utilities"),
        new("wgen", "World generation tools"),

        // ID remappers
        new("bir", "Block ID remapper"),
        new("eir", "Entity code remapper"),
        new("fixmapping", "Remapper assistant"),
        new("iir", "Item ID remapper"),

        // Macros / mods
        new("macro", "Server-side macros"),
        new("moddb", "Mod DB utility"),

        // Server / network admin
        new("allowlan", "Toggle external LAN connections"),
        new("autosavenow", "Trigger an autosave"),
        new("hardstop", "Hard shutdown the server"),
        new("sc", "Server config (alias)"),
        new("serverconfig", "Read or set server configuration"),
        new("stop", "Graceful server shutdown"),
        new("upnp", "UPnP port forwarding control"),
        new("wc", "World config (alias)"),
        new("wcc", "Create a new world config entry"),
        new("worldconfig", "Read or set world configuration"),
        new("worldconfigcreate", "Create a new world config entry"),

        // Nimbus / multi-server
        new("nimbus", "Nimbus network management"),
        new("server", "Transfer to another backend"),

        // Survival mod
        new("bre", "Block reinforcement"),
        new("chb", "Handbook"),
        new("chbr", "Handbook reader"),
        new("chisel", "Microblock chiseling utilities"),
        new("dweather", "Drift weather (rifts)"),
        new("elevator", "Elevator block control"),
        new("gbre", "Global block reinforcement"),
        new("microblock", "Microblock bulk operations"),
        new("nexttempstorm", "Show next temporal storm"),
        new("npc", "NPC control"),
        new("npcs", "Global NPC control"),
        new("story", "Story structure commands"),
        new("storylock", "Lock doors for story"),
        new("timeswitch", "Temporal event control"),
        new("tutorial", "Tutorial control"),

        // Essentials mod
        new("emote", "Play an emote"),
        new("errorreporter", "Error reporting"),
        new("jsonexport", "Export entities as JSON"),
        new("leafdecaydebug", "Leaf decay debug"),
        new("rooms", "Room registry"),
        new("worldmap", "World map controls"),
    };

    // Subcommands shown at arg index 1. Unlisted commands fall through to argument-type logic
    // in StratumCommandAutocomplete (player names, item codes, etc).
    public static readonly Dictionary<string, string[]> Subcommands = BuildSubcommands();

    private static Dictionary<string, string[]> BuildSubcommands()
    {
        string[] entitySubs = { "cmd", "debug", "list", "spawn", "spawnat", "spawndebug", "export", "wipeall" };
        string[] idRemapperSubs = { "list", "getcode", "getid", "map", "remap" };

        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["bir"] = idRemapperSubs,
            ["bre"] = new[] { "grant", "revoke", "grantgroup", "revokegroup" },
            ["chbr"] = new[] { "expcmds" },
            ["chisel"] = new[] { "genshape" },
            ["chunk"] = new[] { "forceload" },
            ["cp"] = new[] { "posi", "aposi", "apos", "chat" },
            ["debug"] = new[]
            {
                "blockcodes", "itemcodes", "blockstats", "helddurability", "heldtemperature",
                "heldcoattr", "heldstattr", "netbench", "rebuildlandclaimpartitions", "privileges",
                "cloh", "logticks", "profiler", "connectionQueueDebug"
            },
            ["dweather"] = new[] { "reset" },
            ["e"] = entitySubs,
            ["eir"] = new[] { "list", "map", "remap" },
            ["elevator"] = new[] { "set-entity-net", "set-block-net" },
            ["entity"] = entitySubs,
            ["fixmapping"] = new[] { "doremap" },
            ["gbre"] = new[] { "grant", "revoke", "grantgroup", "revokegroup" },
            ["help"] = AllServerCommandNames(),
            ["iir"] = idRemapperSubs,
            ["land"] = new[] { "free", "adminfree", "adminfreehere", "list", "info", "claim" },
            ["macro"] = new[] { "addcmd", "setcmd", "desc", "priv", "discard", "save", "delete", "list", "show", "showwip" },
            ["microblock"] = new[] { "recalc", "editable" },
            ["moddb"] = new[] { "install", "remove", "list", "search", "searchcompatible", "searchfor", "searchforc" },
            ["nexttempstorm"] = new[] { "now" },
            ["nimbus"] = new[] { "status", "servers", "send", "reload" },
            ["npc"] = new[]
            {
                // Stagehand additions
                "create", "remove", "rename", "list", "cmd",
                // Vanilla survival NPC system
                "enqueue", "upd", "start", "stop", "clear", "exec", "loop", "setname", "copyskin"
            },
            ["npcs"] = new[] { "startall", "stopall", "loopall" },
            ["player"] = new[]
            {
                "movespeed", "whitelist", "privilege", "role", "stats", "entity", "wipedata",
                "clearinv", "gamemode", "allowcharselonce", "landclaimallowance", "landclaimmaxareas"
            },
            ["role"] = new[] { "landclaimallowance", "landclaimminsize", "landclaimmaxareas", "privilege", "spawnpoint" },
            ["rooms"] = new[] { "list", "hi", "unhi" },
            ["self"] = new[] { "stats", "privileges", "role", "gamemode", "clearinv", "kill" },
            ["stratum"] = new[]
            {
                "status", "health", "reload", "preflight", "packets", "violations", "players", "access",
                "chat", "pregen", "player", "chunks", "entities", "queues", "performance", "perf", "timings"
            },
            ["story"] = new[] { "tp", "setpos", "removeschematiccount", "listmissing" },
            ["storylock"] = new[] { "set", "clear" },
            ["time"] = new[] { "stop", "resume", "speed", "set", "setmonth", "add", "calendarspeedmul", "hoursperday" },
            ["timeswitch"] = new[] { "toggle", "start", "setpos", "copy", "relight" },
            ["tutorial"] = new[] { "hud", "restart", "skip" },
            ["waypoint"] = new[] { "deathwp", "add", "addp", "addat", "addati", "modify", "remove", "list" },
            ["weather"] = new[]
            {
                "setprecip", "setprecipa", "cloudypos", "stoprain", "acp", "lp", "t", "c",
                "setw", "randomevent", "setev", "setevr"
            },
            ["wgen"] = new[]
            {
                "tree", "treelineup", "structures", "testvillage", "resolve-meta", "decopass", "autogen",
                "gt", "regenk", "regen", "regenc", "pregen", "delrock", "delrockc", "del", "delr",
                "delrange", "regenrange", "treemap", "testmap", "genmap", "stitchclimate", "region",
                "regions", "pos", "testnoise"
            },
            ["whitelist"] = new[] { "add", "remove", "on", "off" },
            ["worldmap"] = new[] { "worldmapsize", "purgedb", "redraw" },
        };
    }

    private static string[] AllServerCommandNames()
    {
        var names = new string[ServerCommands.Length];
        for (int i = 0; i < ServerCommands.Length; i++) names[i] = ServerCommands[i].Name;
        return names;
    }
}
