using System;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumTabListDialog : GuiDialog
{
    private const double DialogWidth = 760;
    private const double DialogHeight = 520;
    private const double HeaderX = 18;
    private const double HeaderY = 38;
    private const double HeaderWidth = 704;
    private const double HeaderHeight = 38;
    private const double SearchWidth = 270;
    private const double ListX = 18;
    private const double ListY = 92;
    private const double ListWidth = 680;
    private const double ListHeight = 390;
    private const double ScrollbarWidth = 18;
    private const double CardHeight = 66;
    private const double CardGap = 8;
    private const double CardPadding = 12;

    private static readonly double[] HeaderTextColor = { 0.92, 0.84, 0.68, 1 };
    private static readonly double[] MutedTextColor = { 0.63, 0.59, 0.52, 1 };
    private static readonly double[] CardFillColor = { 0.055, 0.049, 0.041, 0.88 };
    private static readonly double[] CardBorderColor = { 0.55, 0.46, 0.29, 0.32 };
    private static readonly double[] CardTopLineColor = { 0.86, 0.73, 0.43, 0.08 };
    private static readonly double[] PillFillColor = { 0.13, 0.11, 0.085, 0.86 };
    private static readonly double[] PingGoodColor = { 0.55, 0.86, 0.52, 1 };
    private static readonly double[] PingMediumColor = { 0.9, 0.7, 0.34, 1 };
    private static readonly double[] PingBadColor = { 0.88, 0.45, 0.36, 1 };

    private readonly CairoFont headerFont;
    private readonly CairoFont countFont;
    private readonly CairoFont nameFont;
    private readonly CairoFont roleFont;
    private readonly CairoFont detailFont;
    private readonly CairoFont pingFont;
    private readonly CairoFont searchFont;
    private readonly IClientNetworkChannel clientChannel;

    private StratumRosterPacket roster = new StratumRosterPacket { Players = Array.Empty<StratumRosterEntry>() };
    private StratumRosterEntry[] filteredPlayers = Array.Empty<StratumRosterEntry>();
    private StratumPlayerActionDialog actionDialog;
    private string selectedPlayerUid;
    private string searchText = string.Empty;
    private float scrollOffset;
    private bool suppressSearchTextChanged;

    public StratumTabListDialog(ICoreClientAPI capi, IClientNetworkChannel clientChannel) : base(capi)
    {
        this.clientChannel = clientChannel;
        headerFont = CairoFont.WhiteMediumText().WithFontSize(19f).WithColor(HeaderTextColor).WithWeight(FontWeight.Bold);
        countFont = CairoFont.WhiteDetailText().WithFontSize(15f).WithColor(MutedTextColor);
        nameFont = CairoFont.WhiteSmallishText().WithFontSize(18f).WithColor(HeaderTextColor).WithWeight(FontWeight.Bold);
        roleFont = CairoFont.WhiteDetailText().WithFontSize(14f);
        detailFont = CairoFont.WhiteDetailText().WithFontSize(14f).WithColor(MutedTextColor);
        pingFont = CairoFont.WhiteDetailText().WithFontSize(14f).WithWeight(FontWeight.Bold);
        searchFont = CairoFont.TextInput().WithFontSize(16f);
        ApplyFilter(resetScroll: false);
        Compose();
    }

    public override string ToggleKeyCombinationCode => "stratumui-tablist";

    public override double DrawOrder => 0.92;

    public override void Dispose()
    {
        if (actionDialog != null)
        {
            capi.Gui.LoadedGuis.Remove(actionDialog);
        }

        actionDialog?.Dispose();
    }

    public void UpdateRoster(StratumRosterPacket packet)
    {
        roster = packet ?? new StratumRosterPacket { Players = Array.Empty<StratumRosterEntry>() };
        ApplyFilter(resetScroll: false);
    }

    public override void OnGuiOpened()
    {
        Compose();
        base.OnGuiOpened();
    }

    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        // When the action popup closes, drop the row highlight so the list doesn't keep a stale selection.
        if (selectedPlayerUid != null && (actionDialog == null || !actionDialog.IsOpened()))
        {
            selectedPlayerUid = null;
            RedrawDynamicElements();
        }
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        if (TryOpenActionDialogFromMouse(args))
        {
            args.Handled = true;
            return;
        }

        base.OnMouseDown(args);
    }

    private bool TryOpenActionDialogFromMouse(MouseEvent args)
    {
        if ((args.Button != EnumMouseButton.Left && args.Button != EnumMouseButton.Right) || SingleComposer == null)
        {
            return false;
        }

        GuiElementCustomDraw listElement = SingleComposer.GetCustomDraw("players");
        ElementBounds bounds = listElement?.Bounds;
        if (bounds == null || !bounds.PointInside(args.X, args.Y))
        {
            return false;
        }

        double relativeY = (args.Y - bounds.absY) / RuntimeEnv.GUIScale + scrollOffset;
        double rowHeight = CardHeight + CardGap;
        int index = (int)Math.Floor(relativeY / rowHeight);
        double inRowY = relativeY - index * rowHeight;

        if (index < 0 || index >= filteredPlayers.Length || inRowY > CardHeight)
        {
            return false;
        }

        double cardTopY = bounds.absY + (index * rowHeight - scrollOffset) * RuntimeEnv.GUIScale;
        double cardRightX = bounds.absX + bounds.OuterWidth + 8;
        OpenActionDialog(filteredPlayers[index], cardRightX, cardTopY);
        return true;
    }

    private void Compose()
    {
        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
        ElementBounds headerBounds = ElementBounds.Fixed(HeaderX, HeaderY, HeaderWidth, HeaderHeight);
        ElementBounds searchBounds = ElementBounds.Fixed(DialogWidth - SearchWidth - 36, HeaderY + 4, SearchWidth, 28);
        ElementBounds listInsetBounds = ElementBounds.Fixed(ListX - 5, ListY - 5, ListWidth + 10, ListHeight + 10);
        ElementBounds listBounds = ElementBounds.Fixed(ListX, ListY, ListWidth, ListHeight);
        ElementBounds scrollbarBounds = ElementBounds.Fixed(ListX + ListWidth + 12, ListY - 1, ScrollbarWidth, ListHeight + 2);

        SingleComposer = capi.Gui
            .CreateCompo("stratum-tab-list", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Online Players", OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddDynamicCustomDraw(headerBounds, DrawHeader, "header")
                .AddTextInput(searchBounds, OnSearchTextChanged, searchFont, "search")
                .AddInset(listInsetBounds)
                .AddDynamicCustomDraw(listBounds, DrawPlayerCards, "players")
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .EndChildElements()
            .Compose();

        GuiElementTextInput searchInput = SingleComposer.GetTextInput("search");
        searchInput.SetPlaceHolderText("Search players");
        if (!string.IsNullOrEmpty(searchText))
        {
            suppressSearchTextChanged = true;
            searchInput.SetValue(searchText);
            suppressSearchTextChanged = false;
        }

        UpdateScrollbarHeights();
    }

    private void OnSearchTextChanged(string value)
    {
        if (suppressSearchTextChanged)
        {
            return;
        }

        searchText = value ?? string.Empty;
        ApplyFilter(resetScroll: true);
    }

    private void ApplyFilter(bool resetScroll)
    {
        StratumRosterEntry[] players = roster.Players ?? Array.Empty<StratumRosterEntry>();
        string query = (searchText ?? string.Empty).Trim();

        filteredPlayers = players
            .Where(player => MatchesSearch(player, query))
            .OrderByDescending(player => player.IsStaff)
            .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (resetScroll)
        {
            scrollOffset = 0;
            SingleComposer?.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        UpdateScrollbarHeights();
        RedrawDynamicElements();
    }

    private void DrawHeader(Context context, ImageSurface surface, ElementBounds bounds)
    {
        double scale = RuntimeEnv.GUIScale;
        int online = roster.OnlineCount > 0 ? roster.OnlineCount : (roster.Players?.Length ?? 0);
        string maxText = roster.MaxPlayers > 0 ? " / " + roster.MaxPlayers : "";
        string shownText = string.IsNullOrWhiteSpace(searchText)
            ? online + maxText + " online"
            : filteredPlayers.Length + " shown of " + online + maxText;

        DrawText(context, headerFont, shownText, 0, 0);

        string subline = filteredPlayers.Length == 1 ? "1 player" : filteredPlayers.Length + " players";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            subline += " matching \"" + TrimForDisplay(searchText.Trim(), 28) + "\"";
        }

        DrawText(context, countFont, subline, 0, 21 * scale);
    }

    private void DrawPlayerCards(Context context, ImageSurface surface, ElementBounds bounds)
    {
        double scale = RuntimeEnv.GUIScale;
        double width = ListWidth * scale;
        double cardHeight = CardHeight * scale;
        double cardGap = CardGap * scale;
        double padding = CardPadding * scale;
        double y = -scrollOffset * scale;

        if (filteredPlayers.Length == 0)
        {
            string message = string.IsNullOrWhiteSpace(searchText) ? "Waiting for the server roster..." : "No players match that search.";
            DrawText(context, detailFont, message, padding, padding);
            return;
        }

        foreach (StratumRosterEntry player in filteredPlayers)
        {
            if (y + cardHeight >= 0 && y <= ListHeight * scale)
            {
                DrawPlayerCard(context, player, 0, y, width, cardHeight, padding, scale);
            }

            y += cardHeight + cardGap;
        }
    }

    private void DrawPlayerCard(Context context, StratumRosterEntry player, double x, double y, double width, double height, double padding, double scale)
    {
        double[] roleColor = RoleColor(player, 1);
        double radius = 4 * scale;
        bool isSelected = !string.IsNullOrEmpty(selectedPlayerUid) && string.Equals(selectedPlayerUid, player.PlayerUid, StringComparison.Ordinal);

        double[] fillColor = isSelected ? new double[] { 0.18, 0.14, 0.08, 0.95 } : CardFillColor;
        context.SetSourceRGBA(fillColor);
        GuiElement.RoundRectangle(context, x + scale, y + scale, width - 2 * scale, height - 2 * scale, radius);
        context.Fill();

        if (isSelected)
        {
            context.SetSourceRGBA(roleColor[0], roleColor[1], roleColor[2], 0.18);
            GuiElement.RoundRectangle(context, x + scale, y + scale, width - 2 * scale, height - 2 * scale, radius);
            context.Fill();
        }

        context.SetSourceRGBA(CardTopLineColor);
        context.Rectangle(x + 6 * scale, y + 4 * scale, width - 12 * scale, scale);
        context.Fill();

        if (isSelected)
        {
            context.SetSourceRGBA(roleColor[0], roleColor[1], roleColor[2], 0.9);
            context.LineWidth = 2 * scale;
        }
        else
        {
            context.SetSourceRGBA(CardBorderColor);
            context.LineWidth = scale;
        }
        GuiElement.RoundRectangle(context, x + scale, y + scale, width - 2 * scale, height - 2 * scale, radius);
        context.Stroke();
        context.LineWidth = scale;

        double textX = x + padding + 2 * scale;
        double top = y + 11 * scale;
        DrawText(context, nameFont, TrimForDisplay(player.PlayerName, 28), textX, top);

        string role = DisplayRole(player);
        roleFont.Color = roleColor;
        DrawPill(context, textX, y + 39 * scale, roleFont.GetTextExtents(role).XAdvance + 18 * scale, 20 * scale, roleColor, 0.13, scale);
        DrawText(context, roleFont, role, textX + 9 * scale, y + 41 * scale);

        string mode = string.IsNullOrWhiteSpace(player.GameMode) ? "Unknown" : player.GameMode;
        DrawText(context, detailFont, mode, textX + roleFont.GetTextExtents(role).XAdvance + 38 * scale, y + 42 * scale);

        // PingMs < 0 means the server didn't report one (e.g. vanilla fallback). Hide the pill entirely
        if (player.PingMs >= 0)
        {
            string ping = FormatPing(player.PingMs);
            double pingWidth = Math.Max(72 * scale, pingFont.GetTextExtents(ping).XAdvance + 24 * scale);
            double pingX = x + width - padding - pingWidth;
            double[] pingColor = PingColor(player.PingMs);
            DrawPill(context, pingX, y + 22 * scale, pingWidth, 24 * scale, pingColor, 0.14, scale);
            pingFont.Color = pingColor;
            DrawText(context, pingFont, ping, pingX + 12 * scale, y + 26 * scale);
        }
    }

    private static string FormatPing(int pingMs)
    {
        return pingMs < 0 ? "? ms" : pingMs + " ms";
    }

    private static double[] PingColor(int pingMs)
    {
        if (pingMs < 0)
        {
            return MutedTextColor;
        }

        if (pingMs < 120)
        {
            return PingGoodColor;
        }

        if (pingMs < 250)
        {
            return PingMediumColor;
        }

        return PingBadColor;
    }

    private void DrawPill(Context context, double x, double y, double width, double height, double[] accent, double alpha, double scale)
    {
        context.SetSourceRGBA(PillFillColor);
        GuiElement.RoundRectangle(context, x, y, width, height, 10 * scale);
        context.Fill();

        context.SetSourceRGBA(accent[0], accent[1], accent[2], alpha);
        GuiElement.RoundRectangle(context, x, y, width, height, 10 * scale);
        context.Fill();
    }

    private static void DrawText(Context context, CairoFont font, string text, double x, double top)
    {
        font.SetupContext(context);
        FontExtents extents = font.GetFontExtents();
        context.MoveTo(x, top + extents.Ascent);
        context.ShowText(text ?? string.Empty);
    }

    private static string DisplayRole(StratumRosterEntry player)
    {
        string role = string.IsNullOrWhiteSpace(player.RoleName) ? player.RoleCode : player.RoleName;
        return string.IsNullOrWhiteSpace(role) ? "player" : role;
    }

    private static double[] RoleColor(StratumRosterEntry player, double alpha)
    {
        return IsSafeHexColor(player.RoleColor) ? HexToColor(player.RoleColor, alpha) : new[] { 0.84, 0.76, 0.55, alpha };
    }

    private static double[] HexToColor(string color, double alpha)
    {
        int red = Convert.ToInt32(color.Substring(1, 2), 16);
        int green = Convert.ToInt32(color.Substring(3, 2), 16);
        int blue = Convert.ToInt32(color.Substring(5, 2), 16);
        return new[] { red / 255.0, green / 255.0, blue / 255.0, alpha };
    }

    private static bool MatchesSearch(StratumRosterEntry player, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(player.PlayerName, query)
            || Contains(player.RoleName, query)
            || Contains(player.RoleCode, query)
            || Contains(player.GameMode, query);
    }

    private static bool Contains(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TrimForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private static bool IsSafeHexColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#')
        {
            return false;
        }

        for (int i = 1; i < color.Length; i++)
        {
            char c = color[i];
            bool hex = c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    private float GetContentHeight()
    {
        if (filteredPlayers.Length == 0)
        {
            return (float)ListHeight;
        }

        return (float)(filteredPlayers.Length * CardHeight + Math.Max(0, filteredPlayers.Length - 1) * CardGap);
    }

    private void OnNewScrollbarValue(float value)
    {
        scrollOffset = value;
        SingleComposer?.GetCustomDraw("players")?.Redraw();
    }

    private void OpenActionDialog(StratumRosterEntry player, double anchorX, double anchorY)
    {
        if (player == null || clientChannel == null)
        {
            return;
        }

        // No StratumUI on the server; clicks become no-ops.
        if (!clientChannel.Connected)
        {
            return;
        }

        if (actionDialog != null)
        {
            actionDialog.TryClose();
            capi.Gui.LoadedGuis.Remove(actionDialog);
            actionDialog.Dispose();
        }

        actionDialog = new StratumPlayerActionDialog(capi, clientChannel, player, anchorX, anchorY);
        selectedPlayerUid = player.PlayerUid;
        actionDialog.TryOpen();
        RedrawDynamicElements();
    }

    private void UpdateScrollbarHeights()
    {
        if (SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)ListHeight, Math.Max((float)ListHeight, GetContentHeight()));
    }

    private void RedrawDynamicElements()
    {
        SingleComposer?.GetCustomDraw("header")?.Redraw();
        SingleComposer?.GetCustomDraw("players")?.Redraw();
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}