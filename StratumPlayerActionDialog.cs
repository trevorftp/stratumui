using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumPlayerActionDialog : GuiDialog
{
    private const double DialogWidth = 260;
    private const double DialogPadding = 12;
    private const double ButtonHeight = 26;
    private const double ButtonGap = 6;
    private const double HeaderHeight = 62;

    private readonly IClientNetworkChannel channel;
    private readonly StratumRosterEntry player;
    private readonly CairoFont titleFont;
    private readonly CairoFont detailFont;
    private readonly double anchorX;
    private readonly double anchorY;
    private StratumPlayerActionPromptDialog promptDialog;

    public StratumPlayerActionDialog(ICoreClientAPI capi, IClientNetworkChannel channel, StratumRosterEntry player, double anchorX, double anchorY) : base(capi)
    {
        this.channel = channel;
        this.player = player;
        this.anchorX = anchorX;
        this.anchorY = anchorY;
        titleFont = CairoFont.WhiteMediumText().WithFontSize(16f).WithWeight(Cairo.FontWeight.Bold);
        detailFont = CairoFont.WhiteDetailText().WithFontSize(12f).WithColor(new double[] { 0.68, 0.63, 0.54, 1 });
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override double DrawOrder => 0.96;

    public override bool PrefersUngrabbedMouse => true;

    public override void Dispose()
    {
        if (promptDialog != null)
        {
            capi.Gui.LoadedGuis.Remove(promptDialog);
            promptDialog.Dispose();
            promptDialog = null;
        }

        base.Dispose();
    }

    private void Compose()
    {
        List<ActionButton> actions = BuildActions();
        int buttonCount = Math.Max(1, actions.Count);
        double contentHeight = HeaderHeight + buttonCount * ButtonHeight + Math.Max(0, buttonCount - 1) * ButtonGap;

        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(DialogPadding, DialogPadding);
        bgBounds.fixedWidth = DialogWidth;
        bgBounds.fixedHeight = contentHeight;

        double maxX = Math.Max(0, capi.Render.FrameWidth - DialogWidth - 60);
        double maxY = Math.Max(0, capi.Render.FrameHeight - contentHeight - 60);
        double x = Math.Min(Math.Max(0, anchorX), maxX);
        double y = Math.Min(Math.Max(0, anchorY), maxY);

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedPosition(x, y);

        double innerWidth = DialogWidth - 30;
        double closeWidth = 22;

        GuiComposer composer = capi.Gui
            .CreateCompo("stratum-player-actions-" + player.PlayerUid, dialogBounds)
            .AddShadedDialogBG(bgBounds, withTitleBar: false)
            .BeginChildElements(bgBounds)
                .AddStaticText(TrimForDisplay(player.PlayerName ?? "Player", 22), titleFont, ElementBounds.Fixed(0, 2, innerWidth - closeWidth - 6, 22))
                .AddSmallButton("x", OnCloseButton, ElementBounds.Fixed(innerWidth - closeWidth, 2, closeWidth, 22))
                .AddStaticText(BuildRoleLine(), detailFont, ElementBounds.Fixed(0, 26, innerWidth, 16))
                .AddStaticText(BuildCountsLine(), detailFont, ElementBounds.Fixed(0, 44, innerWidth, 16));

        double yCursor = HeaderHeight;
        if (actions.Count == 0)
        {
            composer.AddStaticText("No actions available", detailFont, ElementBounds.Fixed(0, yCursor, innerWidth, ButtonHeight));
        }
        else
        {
            foreach (ActionButton action in actions)
            {
                ActionButton captured = action;
                composer.AddSmallButton(captured.Label, () => SendAction(captured.Kind), ElementBounds.Fixed(0, yCursor, innerWidth, ButtonHeight));
                yCursor += ButtonHeight + ButtonGap;
            }
        }

        SingleComposer = composer.EndChildElements().Compose();
    }

    private List<ActionButton> BuildActions()
    {
        StratumPlayerActionFlags flags = (StratumPlayerActionFlags)player.ActionFlags;
        List<ActionButton> actions = new List<ActionButton>();

        AddIf(actions, flags, StratumPlayerActionFlags.ViewDetails, StratumPlayerActionKind.ViewDetails, "View Profile");
        AddIf(actions, flags, StratumPlayerActionFlags.Report, StratumPlayerActionKind.Report, "Report");
        AddIf(actions, flags, StratumPlayerActionFlags.Warn, StratumPlayerActionKind.Warn, "Warn");
        AddIf(actions, flags, StratumPlayerActionFlags.Mute, StratumPlayerActionKind.Mute, "Mute");
        AddIf(actions, flags, StratumPlayerActionFlags.Freeze, StratumPlayerActionKind.Freeze, "Freeze / Unfreeze");
        AddIf(actions, flags, StratumPlayerActionFlags.Jail, StratumPlayerActionKind.Jail, "Jail");
        AddIf(actions, flags, StratumPlayerActionFlags.Kick, StratumPlayerActionKind.Kick, "Kick");
        AddIf(actions, flags, StratumPlayerActionFlags.Ban, StratumPlayerActionKind.Ban, "Ban");

        return actions;
    }

    private static void AddIf(List<ActionButton> actions, StratumPlayerActionFlags flags, StratumPlayerActionFlags requiredFlag, StratumPlayerActionKind kind, string label)
    {
        if ((flags & requiredFlag) != 0)
        {
            actions.Add(new ActionButton(kind, label));
        }
    }

    private string BuildRoleLine()
    {
        string role = string.IsNullOrWhiteSpace(player.RoleName) ? player.RoleCode : player.RoleName;
        return string.IsNullOrWhiteSpace(role) ? "player" : role;
    }

    private string BuildCountsLine()
    {
        if (!player.HasModerationCounts)
        {
            return string.Empty;
        }

        return "Warns " + player.ActiveWarnings + "   \u2022   Viols " + player.ActiveViolations;
    }

    private string BuildModerationLine()
    {
        string role = string.IsNullOrWhiteSpace(player.RoleName) ? player.RoleCode : player.RoleName;
        if (player.HasModerationCounts)
        {
            return role + "  |  warns " + player.ActiveWarnings + "  |  viols " + player.ActiveViolations;
        }

        return string.IsNullOrWhiteSpace(role) ? "player" : role;
    }

    private bool SendAction(StratumPlayerActionKind action)
    {
        if (channel?.Connected != true)
        {
            // Server isn't running StratumUI - swallow the click instead of throwing.
            TryClose();
            return true;
        }

        if (action == StratumPlayerActionKind.ViewDetails)
        {
            channel.SendPacket(new StratumPlayerDetailRequest { TargetPlayerUid = player.PlayerUid });
            TryClose();
            return true;
        }

        if (RequiresPrompt(action))
        {
            OpenPromptDialog(action);
            return true;
        }

        SendActionRequest(action, string.Empty, string.Empty);
        TryClose();
        return true;
    }

    private static bool RequiresPrompt(StratumPlayerActionKind action)
    {
        switch (action)
        {
            case StratumPlayerActionKind.Report:
            case StratumPlayerActionKind.Warn:
            case StratumPlayerActionKind.Mute:
            case StratumPlayerActionKind.Jail:
            case StratumPlayerActionKind.Kick:
            case StratumPlayerActionKind.Ban:
                return true;
            default:
                return false;
        }
    }

    private void OpenPromptDialog(StratumPlayerActionKind action)
    {
        if (promptDialog != null)
        {
            promptDialog.TryClose();
            capi.Gui.LoadedGuis.Remove(promptDialog);
            promptDialog.Dispose();
        }

        promptDialog = new StratumPlayerActionPromptDialog(capi, player, action, (duration, reason) =>
        {
            SendActionRequest(action, duration, reason);
            TryClose();
        });
        promptDialog.TryOpen();
    }

    private void SendActionRequest(StratumPlayerActionKind action, string duration, string reason)
    {
        channel.SendPacket(new StratumPlayerActionRequest
        {
            TargetPlayerUid = player.PlayerUid,
            Action = action,
            Duration = duration ?? string.Empty,
            Reason = reason ?? string.Empty
        });
    }

    private static string TrimForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private bool OnCloseButton()
    {
        TryClose();
        return true;
    }

    private readonly struct ActionButton
    {
        public readonly StratumPlayerActionKind Kind;
        public readonly string Label;

        public ActionButton(StratumPlayerActionKind kind, string label)
        {
            Kind = kind;
            Label = label;
        }
    }
}
