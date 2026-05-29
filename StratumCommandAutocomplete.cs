using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.Stratum.UI;

internal sealed class StratumCommandAutocomplete : IDisposable
{
    private const int MaxMatches = 80;
    private const int MaxVisibleMatches = 8;
    private const double MinPopupWidth = 230;
    private const double MaxPopupWidth = 560;
    private const int MaxKindLength = 48;

    private static readonly string[] GameModes =
    {
        "survival", "creative", "spectator", "guest"
    };

    private static readonly string[] RoleCodes =
    {
        "player", "limitedsuplayer", "limitedcrplayer", "suplayer", "crplayer", "sumod", "crmod", "admin"
    };

    private static readonly string[] QuantitySuggestions =
    {
        "1", "8", "16", "32", "64"
    };

    private static readonly string[] EntitySelectorSuggestions =
    {
        "p[]", "e[]", "e[type=]"
    };

    private static readonly HashSet<string> PlayerFirstArgumentCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ban", "freeze", "groupinvite", "hardban", "jail", "jailstatus", "kick", "msg", "mute", "mutestatus", "note",
        "notes", "op", "player", "role", "seen", "tell", "tp", "tpa", "tpahere", "unban", "unjail", "unmute", "w",
        "warn", "warnings", "whois"
    };

    private static readonly HashSet<string> PlayerSecondArgumentCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gamemode", "gm", "tp"
    };

    private static readonly HashSet<string> ItemFirstArgumentCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "giveitem"
    };

    private static readonly HashSet<string> BlockFirstArgumentCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "giveblock", "setblock"
    };

    private readonly ICoreClientAPI capi;
    private readonly StratumCommandSuggestionPopup suggestionPopup;
    private readonly CairoFont measureFont;
    private readonly List<string> rosterPlayerNames = new List<string>();
    private readonly StratumCompletionSuggestion[] cachedServerCommandSuggestions;

    private long tickListenerId;
    private string lastSnapshot;
    private CompletionContext currentContext;
    private StratumCompletionSuggestion[] currentMatches = Array.Empty<StratumCompletionSuggestion>();
    private int selectedIndex;
    private bool previewActive;
    private string previewBaseText;
    private int previewTokenStart;
    private int previewTokenEnd;

    public StratumCommandAutocomplete(ICoreClientAPI capi)
    {
        this.capi = capi;
        suggestionPopup = new StratumCommandSuggestionPopup(capi);
        measureFont = CairoFont.WhiteDetailText().WithFontSize(17f);
        capi.Gui.RegisterDialog(suggestionPopup);
        capi.Event.KeyDown += OnKeyDown;

        cachedServerCommandSuggestions = new StratumCompletionSuggestion[StratumCommandCatalog.ServerCommands.Length];
        for (int i = 0; i < StratumCommandCatalog.ServerCommands.Length; i++)
        {
            StratumCommandCatalog.Entry entry = StratumCommandCatalog.ServerCommands[i];
            cachedServerCommandSuggestions[i] = new StratumCompletionSuggestion(entry.Name, TruncateKind(entry.Description));
        }

        // Tab/Up/Down also forces a refresh, so 200ms idle is plenty.
        tickListenerId = capi.Event.RegisterGameTickListener(deltaTime => RefreshFromChatInput(), 200, 75);
    }

    public void UpdateRoster(StratumRosterPacket packet)
    {
        rosterPlayerNames.Clear();
        if (packet?.Players != null)
        {
            rosterPlayerNames.AddRange(packet.Players
                .Select(player => player.PlayerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        }

        RefreshFromChatInput(force: true);
    }

    public void Clear()
    {
        rosterPlayerNames.Clear();
        ClearSuggestions();
    }

    public bool TryCompleteFromChatHotkey()
    {
        if (!TryGetChatInput(out GuiElementChatInput chatInput))
        {
            ClearSuggestions();
            return false;
        }

        RefreshFromChatInput(chatInput, force: true);
        string text = chatInput.GetText() ?? string.Empty;
        bool isCommandInput = text.Length > 0 && (text[0] == '/' || text[0] == '.');
        if (!isCommandInput)
        {
            ClearSuggestions();
            return false;
        }

        if (currentContext == null || currentMatches.Length == 0)
        {
            return true;
        }

        StratumCompletionSuggestion target = currentMatches[Math.Clamp(selectedIndex, 0, currentMatches.Length - 1)];
        if (!target.IsSelectable) return true;
        ApplySuggestion(chatInput, currentContext, target, appendSpace: true);
        return true;
    }

    public void Dispose()
    {
        if (tickListenerId != 0)
        {
            capi.Event.UnregisterGameTickListener(tickListenerId);
            tickListenerId = 0;
        }

        capi.Event.KeyDown -= OnKeyDown;

        suggestionPopup?.Dispose();
    }

    private static string TruncateKind(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = text.Trim();
        if (text.Length <= MaxKindLength) return text;
        return text.Substring(0, MaxKindLength - 1).TrimEnd() + "\u2026";
    }

    private void OnKeyDown(KeyEvent args)
    {
        if (args.Handled || args.CtrlPressed || args.AltPressed || args.CommandPressed)
        {
            return;
        }

        if (TryHandleChatKey(args))
        {
            args.Handled = true;
        }
    }

    private bool TryHandleChatKey(KeyEvent args)
    {
        int keyCode = args.KeyCode;
        int triggerKey = capi.Input.GetHotKeyByCode(StratumUISystem.TabListHotKey)?.CurrentMapping?.KeyCode ?? (int)GlKeys.Tab;
        if (keyCode != triggerKey && keyCode != (int)GlKeys.Up && keyCode != (int)GlKeys.Down)
        {
            return false;
        }

        if (!TryGetChatInput(out GuiElementChatInput chatInput))
        {
            ClearSuggestions();
            return false;
        }

        string text = chatInput.GetText() ?? string.Empty;
        bool isCommandInput = text.Length > 0 && (text[0] == '/' || text[0] == '.');
        if (!isCommandInput)
        {
            ClearSuggestions();
            return false;
        }

        EnsureCurrentSuggestions(chatInput);

        if (keyCode == triggerKey)
        {
            if (currentContext != null && currentMatches.Length > 0)
            {
                StratumCompletionSuggestion target = currentMatches[Math.Clamp(selectedIndex, 0, currentMatches.Length - 1)];
                if (target.IsSelectable)
                {
                    ApplySuggestion(chatInput, currentContext, target, appendSpace: true);
                }
            }

            return true;
        }

        if (currentContext == null || currentMatches.Length == 0)
        {
            return false;
        }

        MoveSelection(chatInput, keyCode == (int)GlKeys.Up ? -1 : 1);
        return true;
    }

    private void RefreshFromChatInput()
    {
        RefreshFromChatInput(force: false);
    }

    private void RefreshFromChatInput(bool force)
    {
        if (!TryGetChatInput(out GuiElementChatInput chatInput))
        {
            ClearSuggestions();
            return;
        }

        RefreshFromChatInput(chatInput, force);
    }

    private void RefreshFromChatInput(GuiElementChatInput chatInput, bool force)
    {
        string text = chatInput.GetText() ?? string.Empty;
        int caret = Math.Clamp(chatInput.CaretPosWithoutLineBreaks, 0, text.Length);
        string snapshot = text + "\u001f" + caret;
        if (previewActive && IsCurrentPreviewText(text, caret))
        {
            lastSnapshot = snapshot;
            return;
        }

        ClearPreview();

        if (!force && string.Equals(snapshot, lastSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        lastSnapshot = snapshot;
        CompletionContext context = CompletionContext.From(text, caret);
        if (context == null)
        {
            ClearSuggestions(keepSnapshot: true);
            return;
        }

        string previousSelectedValue = selectedIndex >= 0 && selectedIndex < currentMatches.Length ? currentMatches[selectedIndex].Value : null;
        StratumCompletionSuggestion[] matches = FindMatches(context).ToArray();
        currentContext = context;
        currentMatches = matches;
        selectedIndex = ResolveSelectedIndex(matches, previousSelectedValue);

        // Unknown top-level command: show a non-selectable error row for feedback.
        if (matches.Length == 0 && context.TokenIndex == 0 && context.HasTypedToken && context.Prefix == '/')
        {
            matches = new[] { new StratumCompletionSuggestion(context.Token, "unknown command") { IsError = true } };
            currentMatches = matches;
            selectedIndex = 0;
        }

        if (matches.Length == 0)
        {
            suggestionPopup.Hide();
            return;
        }

        StratumCommandSuggestionPlacement placement = CreatePopupPlacement(chatInput, context, matches);
        suggestionPopup.Show(placement, matches, selectedIndex);
    }

    private void EnsureCurrentSuggestions(GuiElementChatInput chatInput)
    {
        string text = chatInput.GetText() ?? string.Empty;
        int caret = Math.Clamp(chatInput.CaretPosWithoutLineBreaks, 0, text.Length);
        if (previewActive && IsCurrentPreviewText(text, caret))
        {
            return;
        }

        RefreshFromChatInput(chatInput, force: true);
    }

    private bool TryGetChatInput(out GuiElementChatInput chatInput)
    {
        chatInput = null;

        foreach (GuiDialog dialog in capi.Gui.LoadedGuis)
        {
            if (!dialog.IsOpened() || !dialog.Focused)
            {
                continue;
            }

            GuiComposer composer = dialog.Composers["chat"];
            if (composer == null)
            {
                continue;
            }

            chatInput = composer.GetChatInput("chatinput");
            if (chatInput != null)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<StratumCompletionSuggestion> FindMatches(CompletionContext context)
    {
        List<RankedSuggestion> rankedSuggestions = new List<RankedSuggestion>();
        HashSet<string> seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (StratumCompletionSuggestion suggestion in GetCandidates(context))
        {
            if (string.IsNullOrWhiteSpace(suggestion.Value) || !seenValues.Add(suggestion.Value))
            {
                continue;
            }

            int rank = GetMatchRank(suggestion.Value, context.Token);
            if (rank >= 0)
            {
                rankedSuggestions.Add(new RankedSuggestion(suggestion, rank));
            }
        }

        // Empty token: alphabetical so the list is browseable. Typed token: rank-then-length.
        bool emptyToken = string.IsNullOrEmpty(context.Token);
        IOrderedEnumerable<RankedSuggestion> ordered = emptyToken
            ? rankedSuggestions.OrderBy(s => s.Suggestion.Value, StringComparer.OrdinalIgnoreCase)
            : rankedSuggestions
                .OrderBy(s => s.Rank)
                .ThenBy(s => s.Suggestion.Value.Length)
                .ThenBy(s => s.Suggestion.Value, StringComparer.OrdinalIgnoreCase);

        return ordered.Take(MaxMatches).Select(s => s.Suggestion);
    }

    private IEnumerable<StratumCompletionSuggestion> GetCandidates(CompletionContext context)
    {
        if (context.TokenIndex == 0)
        {
            if (context.Prefix == '.')
            {
                return ToSuggestions(GetClientCommandNames(), "client command");
            }
            return cachedServerCommandSuggestions;
        }

        if (context.CommandEquals("tp"))
        {
            return GetTeleportCandidates(context);
        }

        if (context.CommandEquals("giveitem"))
        {
            return GetGiveCandidates(context, isBlock: false);
        }

        if (context.CommandEquals("giveblock"))
        {
            return GetGiveCandidates(context, isBlock: true);
        }

        if (context.CommandEquals("setblock"))
        {
            return GetSetBlockCandidates(context);
        }

        if (context.CommandEquals("gamemode") || context.CommandEquals("gm"))
        {
            return GetGameModeCandidates(context);
        }

        if (context.CommandEquals("role") && context.ArgumentIndex == 1)
        {
            return ToSuggestions(RoleCodes, "role");
        }

        if ((context.CommandEquals("entity") || context.CommandEquals("e")) && context.ArgumentIndex == 2 && context.PreviousTokenEquals("spawn", "spawnat"))
        {
            return ToSuggestionsIfTyped(context, GetEntityTypeCodes(), "entity");
        }

        if (ItemFirstArgumentCommands.Contains(context.Command) && context.ArgumentIndex == 1)
        {
            return ToSuggestionsIfTyped(context, GetItemCodes(), "item");
        }

        if (BlockFirstArgumentCommands.Contains(context.Command) && context.ArgumentIndex == 1)
        {
            return ToSuggestionsIfTyped(context, GetBlockCodes(), "block");
        }

        if (PlayerFirstArgumentCommands.Contains(context.Command) && context.ArgumentIndex == 1)
        {
            return GetEntityTargetCandidates(context);
        }

        if (PlayerSecondArgumentCommands.Contains(context.Command) && context.ArgumentIndex == 2)
        {
            return GetEntityTargetCandidates(context);
        }

        // Catalog-driven subcommands (stratum, entity, wgen, etc.).
        if (context.ArgumentIndex == 1
            && !string.IsNullOrEmpty(context.Command)
            && StratumCommandCatalog.Subcommands.TryGetValue(context.Command, out string[] subs))
        {
            return ToSuggestions(subs, "subcommand");
        }

        return Array.Empty<StratumCompletionSuggestion>();
    }

    private IEnumerable<StratumCompletionSuggestion> GetTeleportCandidates(CompletionContext context)
    {
        if (context.ArgumentIndex == 1)
        {
            return GetEntityTargetCandidates(context);
        }

        if (context.ArgumentIndex == 2)
        {
            return IsCoordinateLike(context.Token) ? Array.Empty<StratumCompletionSuggestion>() : GetEntityTargetCandidates(context);
        }

        return Array.Empty<StratumCompletionSuggestion>();
    }

    private IEnumerable<StratumCompletionSuggestion> GetGiveCandidates(CompletionContext context, bool isBlock)
    {
        if (context.ArgumentIndex == 1)
        {
            return isBlock
                ? ToSuggestionsIfTyped(context, GetBlockCodes(), "block")
                : ToSuggestionsIfTyped(context, GetItemCodes(), "item");
        }

        if (context.ArgumentIndex == 2)
        {
            return ToSuggestionsIfTyped(context, QuantitySuggestions, "quantity");
        }

        if (context.ArgumentIndex == 3)
        {
            return GetEntityTargetCandidates(context);
        }

        return Array.Empty<StratumCompletionSuggestion>();
    }

    private IEnumerable<StratumCompletionSuggestion> GetSetBlockCandidates(CompletionContext context)
    {
        if (context.ArgumentIndex == 1)
        {
            return ToSuggestionsIfTyped(context, GetBlockCodes(), "block");
        }

        if (context.ArgumentIndex == 2)
        {
            return Array.Empty<StratumCompletionSuggestion>();
        }

        return Array.Empty<StratumCompletionSuggestion>();
    }

    private IEnumerable<StratumCompletionSuggestion> GetGameModeCandidates(CompletionContext context)
    {
        if (context.ArgumentIndex == 1)
        {
            IEnumerable<StratumCompletionSuggestion> gameModes = ToSuggestions(GameModes, "mode");
            return context.HasTypedToken
                ? gameModes.Concat(ToSuggestions(GetPlayerNames(), "player"))
                : gameModes;
        }

        if (context.ArgumentIndex == 2)
        {
            return ToSuggestions(GameModes, "mode");
        }

        return Array.Empty<StratumCompletionSuggestion>();
    }

    private IEnumerable<StratumCompletionSuggestion> GetEntityTargetCandidates(CompletionContext context)
    {
        if (!context.HasTypedToken)
        {
            return Array.Empty<StratumCompletionSuggestion>();
        }

        return ToSuggestions(GetPlayerNames(), "player")
            .Concat(ToSuggestions(EntitySelectorSuggestions, "selector"));
    }

    private IEnumerable<string> GetClientCommandNames()
    {
        return capi.ChatCommands.Select(command => command.Key);
    }

    private IEnumerable<string> GetPlayerNames()
    {
        if (rosterPlayerNames.Count > 0)
        {
            return rosterPlayerNames;
        }

        return capi.World.AllOnlinePlayers
            .Select(player => player.PlayerName)
            .Where(name => !string.IsNullOrWhiteSpace(name));
    }

    private IEnumerable<string> GetBlockCodes()
    {
        return capi.World.Blocks
            .Select(block => block?.Code?.ToShortString())
            .Where(code => !string.IsNullOrWhiteSpace(code));
    }

    private IEnumerable<string> GetItemCodes()
    {
        return capi.World.Items
            .Select(item => item?.Code?.ToShortString())
            .Where(code => !string.IsNullOrWhiteSpace(code));
    }

    private IEnumerable<string> GetEntityTypeCodes()
    {
        return capi.World.EntityTypeCodes != null ? capi.World.EntityTypeCodes : Array.Empty<string>();
    }

    private void ApplySuggestion(GuiElementChatInput chatInput, CompletionContext context, StratumCompletionSuggestion suggestion, bool appendSpace)
    {
        string text = previewActive ? previewBaseText : chatInput.GetText() ?? string.Empty;
        int caret = previewActive ? previewTokenEnd : Math.Clamp(chatInput.CaretPosWithoutLineBreaks, 0, text.Length);
        string suffix = text.Substring(caret);
        bool shouldAppendSpace = appendSpace && (suffix.Length == 0 || char.IsWhiteSpace(suffix[0]));
        string replacement = suggestion.Value + (shouldAppendSpace ? " " : string.Empty);
        string updated = text.Substring(0, context.TokenStart) + replacement + suffix;

        ClearPreview();
        chatInput.SetValue(updated, setCaretPosToEnd: false);
        chatInput.SetCaretPos(context.TokenStart + replacement.Length);
        RefreshFromChatInput(chatInput, force: true);
    }

    private void MoveSelection(GuiElementChatInput chatInput, int delta)
    {
        if (currentMatches.Length == 0)
        {
            return;
        }

        // Skip hint/error rows when cycling.
        int attempts = 0;
        do
        {
            selectedIndex = (selectedIndex + delta + currentMatches.Length) % currentMatches.Length;
            attempts++;
        }
        while (attempts < currentMatches.Length && !currentMatches[selectedIndex].IsSelectable);

        if (!currentMatches[selectedIndex].IsSelectable) return;
        PreviewSuggestion(chatInput, currentMatches[selectedIndex]);
    }

    private void PreviewSuggestion(GuiElementChatInput chatInput, StratumCompletionSuggestion suggestion)
    {
        if (currentContext == null)
        {
            return;
        }

        if (!previewActive)
        {
            previewBaseText = chatInput.GetText() ?? string.Empty;
            previewTokenStart = currentContext.TokenStart;
            previewTokenEnd = Math.Clamp(chatInput.CaretPosWithoutLineBreaks, 0, previewBaseText.Length);
            previewActive = true;
        }

        string updated = BuildPreviewText(suggestion.Value);
        int previewCaret = previewTokenStart + suggestion.Value.Length;
        chatInput.SetValue(updated, setCaretPosToEnd: false);
        chatInput.SetCaretPos(previewCaret);
        lastSnapshot = updated + "\u001f" + previewCaret;

        StratumCommandSuggestionPlacement placement = CreatePopupPlacement(chatInput, currentContext, currentMatches);
        suggestionPopup.Show(placement, currentMatches, selectedIndex);
    }

    private bool IsCurrentPreviewText(string text, int caret)
    {
        if (!previewActive || selectedIndex < 0 || selectedIndex >= currentMatches.Length)
        {
            return false;
        }

        string suggestionValue = currentMatches[selectedIndex].Value;
        return caret == previewTokenStart + suggestionValue.Length && string.Equals(text, BuildPreviewText(suggestionValue), StringComparison.Ordinal);
    }

    private string BuildPreviewText(string suggestionValue)
    {
        return previewBaseText.Substring(0, previewTokenStart) + suggestionValue + previewBaseText.Substring(previewTokenEnd);
    }

    private void ClearPreview()
    {
        previewActive = false;
        previewBaseText = null;
        previewTokenStart = 0;
        previewTokenEnd = 0;
    }

    private StratumCommandSuggestionPlacement CreatePopupPlacement(GuiElementChatInput chatInput, CompletionContext context, IReadOnlyList<StratumCompletionSuggestion> matches)
    {
        int visibleCount = Math.Min(MaxVisibleMatches, matches.Count);
        double popupHeight = StratumCommandSuggestionPopup.VerticalPadding * 2 + visibleCount * StratumCommandSuggestionPopup.RowHeight;
        double measuredWidth = MeasurePopupWidth(matches.Take(visibleCount));
        double popupWidth = Math.Clamp(measuredWidth, MinPopupWidth, MaxPopupWidth);
        double scale = RuntimeEnv.GUIScale;

        chatInput.Bounds.CalcWorldBounds();
        double inputX = chatInput.Bounds.renderX / scale;
        double inputY = chatInput.Bounds.renderY / scale;
        double inputWidth = chatInput.Bounds.OuterWidth / scale;
        double inputHeight = chatInput.Bounds.OuterHeight / scale;
        double screenWidth = capi.Render.FrameWidth / scale;
        double screenHeight = capi.Render.FrameHeight / scale;
        string textBeforeToken = (chatInput.GetText() ?? string.Empty).Substring(0, context.TokenStart);
        double tokenOffset = measureFont.GetTextExtents(textBeforeToken).XAdvance / scale;
        double desiredX = inputX + 8 + tokenOffset - 10;
        double maxX = Math.Max(inputX, Math.Min(screenWidth - popupWidth - 4, inputX + inputWidth - popupWidth));
        double popupX = Math.Clamp(desiredX, inputX, maxX);
        double popupY = inputY - popupHeight - 4;

        if (popupY < 4)
        {
            popupY = inputY + inputHeight + 4;
        }

        if (popupY + popupHeight > screenHeight - 4)
        {
            popupY = Math.Max(4, screenHeight - popupHeight - 4);
        }

        return new StratumCommandSuggestionPlacement(popupX, popupY, popupWidth, popupHeight, visibleCount);
    }

    private double MeasurePopupWidth(IEnumerable<StratumCompletionSuggestion> suggestions)
    {
        double maxWidth = 0;
        foreach (StratumCompletionSuggestion suggestion in suggestions)
        {
            string displayText = string.IsNullOrWhiteSpace(suggestion.Kind)
                ? suggestion.Value
                : suggestion.Value + "  " + suggestion.Kind;
            maxWidth = Math.Max(maxWidth, measureFont.GetTextExtents(displayText).XAdvance / RuntimeEnv.GUIScale);
        }

        return maxWidth + 34;
    }

    private int ResolveSelectedIndex(IReadOnlyList<StratumCompletionSuggestion> matches, string previousSelectedValue)
    {
        if (matches.Count == 0)
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(previousSelectedValue))
        {
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
            {
                if (string.Equals(matches[matchIndex].Value, previousSelectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return matchIndex;
                }
            }
        }

        return 0;
    }

    private void ClearSuggestions(bool keepSnapshot = false)
    {
        currentContext = null;
        currentMatches = Array.Empty<StratumCompletionSuggestion>();
        selectedIndex = 0;
        suggestionPopup.Hide();
        if (!keepSnapshot)
        {
            lastSnapshot = null;
        }
    }

    private static IEnumerable<StratumCompletionSuggestion> ToSuggestions(IEnumerable<string> values, string kind)
    {
        return values.Select(value => new StratumCompletionSuggestion(value, kind));
    }

    private static IEnumerable<StratumCompletionSuggestion> ToSuggestionsIfTyped(CompletionContext context, IEnumerable<string> values, string kind)
    {
        return context.HasTypedToken ? ToSuggestions(values, kind) : Array.Empty<StratumCompletionSuggestion>();
    }

    private static bool IsCoordinateLike(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        char first = token[0];
        return first == '~' || first == '-' || first == '+' || char.IsDigit(first);
    }

    private static int GetMatchRank(string value, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return 0;
        }

        if (value.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string unqualifiedValue = GetUnqualifiedCode(value);
        if (!string.Equals(unqualifiedValue, value, StringComparison.Ordinal) && unqualifiedValue.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (token.Length >= 2 && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 2;
        }

        return -1;
    }

    private static string GetUnqualifiedCode(string value)
    {
        int colonIndex = value.IndexOf(':');
        return colonIndex >= 0 && colonIndex < value.Length - 1 ? value.Substring(colonIndex + 1) : value;
    }

    private sealed class RankedSuggestion
    {
        public RankedSuggestion(StratumCompletionSuggestion suggestion, int rank)
        {
            Suggestion = suggestion;
            Rank = rank;
        }

        public StratumCompletionSuggestion Suggestion { get; }
        public int Rank { get; }
    }

    private sealed class CompletionContext
    {
        public char Prefix { get; private set; }
        public string Command { get; private set; }
        public string Token { get; private set; }
        public string[] TokensBeforeCurrent { get; private set; }
        public int TokenStart { get; private set; }
        public int TokenIndex { get; private set; }
        public int ArgumentIndex => TokenIndex;
        public bool HasTypedToken => Token.Length > 0;

        public static CompletionContext From(string text, int caret)
        {
            if (text.Length == 0 || caret == 0 || (text[0] != '/' && text[0] != '.'))
            {
                return null;
            }

            int tokenStart = caret;
            while (tokenStart > 1 && !char.IsWhiteSpace(text[tokenStart - 1]))
            {
                tokenStart--;
            }

            string[] tokensBeforeCurrent = text.Substring(1, tokenStart - 1)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string token = text.Substring(tokenStart, caret - tokenStart);

            return new CompletionContext
            {
                Prefix = text[0],
                Command = tokensBeforeCurrent.Length == 0 ? token : tokensBeforeCurrent[0],
                Token = token,
                TokensBeforeCurrent = tokensBeforeCurrent,
                TokenStart = tokenStart,
                TokenIndex = tokensBeforeCurrent.Length
            };
        }

        public bool CommandEquals(string command)
        {
            return string.Equals(Command, command, StringComparison.OrdinalIgnoreCase);
        }

        public bool PreviousTokenEquals(params string[] tokens)
        {
            if (TokensBeforeCurrent.Length == 0)
            {
                return false;
            }

            string previous = TokensBeforeCurrent[TokensBeforeCurrent.Length - 1];
            return tokens.Any(token => string.Equals(previous, token, StringComparison.OrdinalIgnoreCase));
        }
    }
}