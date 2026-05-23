using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumPlayerModerationDialog : GuiDialog
{
    private const double DialogWidth = 720;
    private const double TitleBarHeight = 32;
    private const double SectionTitleHeight = 24;
    private const double RowHeight = 60;
    private const double SectionGap = 14;
    private const double Padding = 16;

    private static readonly double[] HeaderColor = { 0.92, 0.84, 0.68, 1 };
    private static readonly double[] MutedColor = { 0.72, 0.66, 0.55, 1 };
    private static readonly double[] AccentColor = { 0.83, 0.74, 0.46, 1 };
    private static readonly double[] WarnColor = { 1.00, 0.78, 0.30, 1 };
    private static readonly double[] ViolColor = { 1.00, 0.45, 0.40, 1 };
    private static readonly double[] InactiveColor = { 0.55, 0.50, 0.42, 1 };

    private readonly CairoFont titleFont;
    private readonly CairoFont sectionFont;
    private readonly CairoFont labelFont;
    private readonly CairoFont valueFont;
    private readonly CairoFont reasonFont;

    private StratumPlayerModerationPacket records;
    private List<StratumModerationEntryPacket> warnings = new();
    private List<StratumModerationEntryPacket> violations = new();

    public StratumPlayerModerationDialog(ICoreClientAPI capi) : base(capi)
    {
        titleFont = CairoFont.WhiteMediumText().WithFontSize(18f).WithColor(HeaderColor).WithWeight(FontWeight.Bold);
        sectionFont = CairoFont.WhiteSmallishText().WithFontSize(15f).WithColor(HeaderColor).WithWeight(FontWeight.Bold);
        labelFont = CairoFont.WhiteDetailText().WithFontSize(13f).WithColor(MutedColor);
        valueFont = CairoFont.WhiteDetailText().WithFontSize(13f).WithColor(AccentColor).WithWeight(FontWeight.Bold);
        reasonFont = CairoFont.WhiteDetailText().WithFontSize(13f).WithColor(HeaderColor);
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override double DrawOrder => 0.97;

    public override bool PrefersUngrabbedMouse => true;

    public void UpdateRecords(StratumPlayerModerationPacket packet)
    {
        records = packet;
        // "warning" and "mute" are the two record types the moderation store writes today.
        warnings = packet?.Records?.Where(r => string.Equals(r.Type, "warning", System.StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<StratumModerationEntryPacket>();
        violations = packet?.Records?.Where(r => string.Equals(r.Type, "mute", System.StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<StratumModerationEntryPacket>();
        Compose();
    }

    private void Compose()
    {
        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
        bgBounds.fixedWidth = DialogWidth;

        int warnCount = warnings?.Count ?? 0;
        int violCount = violations?.Count ?? 0;
        double warnSectionH = SectionTitleHeight + System.Math.Max(1, warnCount) * RowHeight + 8;
        double violSectionH = SectionTitleHeight + System.Math.Max(1, violCount) * RowHeight + 8;

        double warnY = TitleBarHeight + Padding;
        double violY = warnY + warnSectionH + SectionGap;
        double closeBtnY = violY + violSectionH + SectionGap;
        double dialogH = closeBtnY + 32 + Padding;
        bgBounds.fixedHeight = dialogH;

        double innerX = Padding;
        double innerW = DialogWidth - Padding * 2;

        ElementBounds warnsInset = ElementBounds.Fixed(innerX, warnY, innerW, warnSectionH);
        ElementBounds warnsTitle = ElementBounds.Fixed(innerX + 12, warnY + 4, innerW - 24, SectionTitleHeight);
        ElementBounds violsInset = ElementBounds.Fixed(innerX, violY, innerW, violSectionH);
        ElementBounds violsTitle = ElementBounds.Fixed(innerX + 12, violY + 4, innerW - 24, SectionTitleHeight);
        ElementBounds closeBtn = ElementBounds.Fixed(DialogWidth - 120, closeBtnY, 90, 26);

        SingleComposer = capi.Gui
            .CreateCompo("stratum-player-moderation", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(BuildTitle(), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddInset(warnsInset)
                .AddStaticText("Warnings (" + warnCount + ")", sectionFont, warnsTitle)
                .AddDynamicCustomDraw(warnsInset, (ctx, surf, b) => DrawRecords(ctx, b, warnings, WarnColor), "warnings-list")
                .AddInset(violsInset)
                .AddStaticText("Violations (" + violCount + ")", sectionFont, violsTitle)
                .AddDynamicCustomDraw(violsInset, (ctx, surf, b) => DrawRecords(ctx, b, violations, ViolColor), "violations-list")
                .AddSmallButton("Close", () => TryClose(), closeBtn)
            .EndChildElements()
            .Compose();
    }

    private string BuildTitle()
    {
        return "Moderation Records";
    }

    private void DrawRecords(Context context, ElementBounds bounds, List<StratumModerationEntryPacket> entries, double[] accent)
    {
        double scale = RuntimeEnv.GUIScale;
        double padX = 16 * scale;
        double y = (SectionTitleHeight + 4) * scale;

        if (entries == null || entries.Count == 0)
        {
            DrawText(context, labelFont, "None on record", padX, y);
            return;
        }

        double rowH = RowHeight * scale;
        foreach (StratumModerationEntryPacket entry in entries)
        {
            double[] color = entry.Active ? accent : InactiveColor;

            string status = entry.Active ? "ACTIVE" : "CLOSED";
            DrawColoredText(context, valueFont, status, color, padX, y);

            double extentsW = valueFont.GetTextExtents(status).Width + 12 * scale;
            string header = entry.CreatedUtc + "  by " + (string.IsNullOrWhiteSpace(entry.ActorName) ? "?" : entry.ActorName);
            DrawText(context, labelFont, header, padX + extentsW, y);

            if (!string.IsNullOrEmpty(entry.ExpiresUtc))
            {
                string expires = "expires " + entry.ExpiresUtc;
                double w = labelFont.GetTextExtents(expires).Width;
                DrawText(context, labelFont, expires, bounds.fixedWidth * scale - padX - w, y);
            }
            else if (string.Equals(entry.Type, "mute", System.StringComparison.OrdinalIgnoreCase))
            {
                string perm = "permanent";
                double w = labelFont.GetTextExtents(perm).Width;
                DrawText(context, labelFont, perm, bounds.fixedWidth * scale - padX - w, y);
            }

            double textLine = y + 20 * scale;
            DrawText(context, reasonFont, string.IsNullOrWhiteSpace(entry.Text) ? "(no reason)" : entry.Text, padX, textLine);

            if (!entry.Active)
            {
                double closedLine = textLine + 18 * scale;
                string closedBy = string.IsNullOrWhiteSpace(entry.ClosedBy) ? "" : " by " + entry.ClosedBy;
                string closeReason = string.IsNullOrWhiteSpace(entry.CloseReason) ? "" : ": " + entry.CloseReason;
                DrawText(context, labelFont, "closed " + entry.ClosedUtc + closedBy + closeReason, padX, closedLine);
            }

            y += rowH;
            if (y > bounds.fixedHeight * scale - 4 * scale)
            {
                break;
            }
        }
    }

    private static void DrawText(Context context, CairoFont font, string text, double x, double top)
    {
        font.SetupContext(context);
        FontExtents extents = font.GetFontExtents();
        context.MoveTo(x, top + extents.Ascent);
        context.ShowText(text ?? string.Empty);
    }

    private static void DrawColoredText(Context context, CairoFont font, string text, double[] color, double x, double top)
    {
        font.SetupContext(context);
        context.SetSourceRGBA(color[0], color[1], color[2], color[3]);
        FontExtents extents = font.GetFontExtents();
        context.MoveTo(x, top + extents.Ascent);
        context.ShowText(text ?? string.Empty);
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}
