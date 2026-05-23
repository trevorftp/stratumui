using System;
using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumPlayerActionPromptDialog : GuiDialog
{
    private const double DialogWidth = 280;
    private const double DialogPadding = 10;
    private const double RowHeight = 26;
    private const double RowGap = 6;

    private readonly StratumRosterEntry player;
    private readonly StratumPlayerActionKind action;
    private readonly Action<string, string> onConfirm;
    private readonly bool needsDuration;
    private readonly bool needsReason;
    private readonly CairoFont titleFont;
    private readonly CairoFont detailFont;
    private readonly CairoFont inputFont;

    public StratumPlayerActionPromptDialog(ICoreClientAPI capi, StratumRosterEntry player, StratumPlayerActionKind action, Action<string, string> onConfirm) : base(capi)
    {
        this.player = player;
        this.action = action;
        this.onConfirm = onConfirm;
        needsDuration = action == StratumPlayerActionKind.Mute || action == StratumPlayerActionKind.Ban;
        needsReason = true;
        titleFont = CairoFont.WhiteMediumText().WithFontSize(15f).WithWeight(Cairo.FontWeight.Bold);
        detailFont = CairoFont.WhiteDetailText().WithFontSize(12f).WithColor(new double[] { 0.68, 0.63, 0.54, 1 });
        inputFont = CairoFont.TextInput().WithFontSize(13f);
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override double DrawOrder => 0.97;

    public override bool PrefersUngrabbedMouse => true;

    private void Compose()
    {
        double y = 30;
        double labelHeight = 22;
        double descriptionHeight = 32;
        double height = 0;

        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(DialogPadding, DialogPadding);
        bgBounds.fixedWidth = DialogWidth;

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        GuiComposer composer = capi.Gui
            .CreateCompo("stratum-player-action-prompt-" + player.PlayerUid + "-" + action, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(ActionTitle() + " - " + (player.PlayerName ?? "Player"), OnTitleBarClose)
            .BeginChildElements(bgBounds);

        composer.AddStaticText(ActionDescription(), detailFont, ElementBounds.Fixed(0, y, DialogWidth - 30, descriptionHeight));
        y += descriptionHeight + 10;

        if (needsDuration)
        {
            composer.AddStaticText("Duration (e.g. 30m, 2h, 7d)", detailFont, ElementBounds.Fixed(0, y, DialogWidth - 30, labelHeight));
            y += labelHeight + 2;
            composer.AddTextInput(ElementBounds.Fixed(0, y, DialogWidth - 30, RowHeight), _ => { }, inputFont, "duration");
            y += RowHeight + RowGap + 4;
        }

        if (needsReason)
        {
            composer.AddStaticText("Reason", detailFont, ElementBounds.Fixed(0, y, DialogWidth - 30, labelHeight));
            y += labelHeight + 2;
            composer.AddTextInput(ElementBounds.Fixed(0, y, DialogWidth - 30, RowHeight), _ => { }, inputFont, "reason");
            y += RowHeight + RowGap + 4;
        }

        y += 6;
        double btnWidth = (DialogWidth - 30 - 10) / 2;
        composer.AddSmallButton("Cancel", OnCancel, ElementBounds.Fixed(0, y, btnWidth, RowHeight));
        composer.AddSmallButton("Confirm", OnConfirm, ElementBounds.Fixed(btnWidth + 10, y, btnWidth, RowHeight));
        y += RowHeight + 8;
        height = y;

        bgBounds.fixedHeight = height;
        SingleComposer = composer.EndChildElements().Compose();

        if (needsDuration)
        {
            SingleComposer.GetTextInput("duration")?.SetPlaceHolderText("30m");
            SingleComposer.GetTextInput("duration")?.SetValue("30m");
        }

        if (needsReason)
        {
            SingleComposer.GetTextInput("reason")?.SetPlaceHolderText("Reason");
        }
    }

    private string ActionTitle()
    {
        switch (action)
        {
            case StratumPlayerActionKind.Report: return "Report";
            case StratumPlayerActionKind.Warn: return "Warn";
            case StratumPlayerActionKind.Mute: return "Mute";
            case StratumPlayerActionKind.Jail: return "Jail";
            case StratumPlayerActionKind.Kick: return "Kick";
            case StratumPlayerActionKind.Ban: return "Ban";
            default: return action.ToString();
        }
    }

    private string ActionDescription()
    {
        switch (action)
        {
            case StratumPlayerActionKind.Report: return "Submit a report against this player.";
            case StratumPlayerActionKind.Warn: return "Issue a formal warning to this player.";
            case StratumPlayerActionKind.Mute: return "Prevent this player from chatting.";
            case StratumPlayerActionKind.Jail: return "Confine this player to the jail area.";
            case StratumPlayerActionKind.Kick: return "Disconnect this player from the server.";
            case StratumPlayerActionKind.Ban: return "Ban this player from the server.";
            default: return string.Empty;
        }
    }

    private bool OnConfirm()
    {
        string duration = needsDuration ? (SingleComposer.GetTextInput("duration")?.GetText() ?? string.Empty) : string.Empty;
        string reason = needsReason ? (SingleComposer.GetTextInput("reason")?.GetText() ?? string.Empty) : string.Empty;
        TryClose();
        onConfirm?.Invoke(duration, reason);
        return true;
    }

    private bool OnCancel()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}
