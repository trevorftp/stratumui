using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumChatHistoryDialog : GuiDialog
{
    private static readonly Regex PlainUrlRegex = new Regex(@"\b(?:https?://|www\.)[^\s<>'""\]]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly Queue<ChatLine> lines = new Queue<ChatLine>();
    private readonly CairoFont bodyFont;

    public StratumChatHistoryDialog(ICoreClientAPI capi) : base(capi)
    {
        bodyFont = CairoFont.WhiteSmallText();
        Compose();
    }

    public override string ToggleKeyCombinationCode => "stratumui-chat-history";

    public override double DrawOrder => 0.93;

    public void AddLine(int groupId, string message, EnumChatType chatType)
    {
        lines.Enqueue(new ChatLine
        {
            GroupId = groupId,
            Message = message ?? "",
            ChatType = chatType,
            CreatedAt = DateTime.Now
        });

        while (lines.Count > 200)
        {
            lines.Dequeue();
        }

        RefreshText();
    }

    public void Clear()
    {
        lines.Clear();
        RefreshText();
    }

    public override void OnGuiOpened()
    {
        Compose();
        base.OnGuiOpened();
    }

    private void Compose()
    {
        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
        ElementBounds textBounds = ElementBounds.Fixed(0, 0, 740, 470);
        ElementBounds titleBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, 0, 830, 30);
        ElementBounds insetBounds = textBounds.ForkBoundingParent(5, 5, 5, 5).FixedUnder(titleBounds);
        ElementBounds clippingBounds = textBounds.CopyOffsetedSibling();
        ElementBounds scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);

        SingleComposer = capi.Gui
            .CreateCompo("stratum-chat-history", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Stratum Chat", OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddInset(insetBounds)
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .BeginClip(clippingBounds)
                    .AddRichtext(BuildChatText(), bodyFont, textBounds, null, "chat")
                .EndClip()
            .EndChildElements()
            .Compose();

        clippingBounds.CalcWorldBounds();
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)clippingBounds.fixedHeight, (float)textBounds.fixedHeight);
    }

    private void RefreshText()
    {
        if (SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetRichtext("chat")?.SetNewText(BuildChatText(), bodyFont);
        UpdateScrollbarHeights();
        SingleComposer.ReCompose();
    }

    private string BuildChatText()
    {
        if (lines.Count == 0)
        {
            return "<font color=\"#b8afa0\">Chat history will appear here after the next server message.</font>";
        }

        StringBuilder text = new StringBuilder();
        foreach (ChatLine line in lines)
        {
            if (text.Length > 0)
            {
                text.Append("\n");
            }

            text.Append("<font color=\"#8f8779\">").Append(line.CreatedAt.ToString("HH:mm")).Append("</font> ");
            text.Append(TypeBadge(line.ChatType));
            text.Append(" ").Append(Linkify(line.Message));
        }

        return text.ToString();
    }

    private static string TypeBadge(EnumChatType chatType)
    {
        switch (chatType)
        {
            case EnumChatType.CommandSuccess:
                return "<font color=\"#92df8b\"><strong>[ok]</strong></font>";
            case EnumChatType.CommandError:
                return "<font color=\"#e47d68\"><strong>[error]</strong></font>";
            case EnumChatType.Notification:
                return "<font color=\"#e6c15f\"><strong>[notice]</strong></font>";
            case EnumChatType.OthersMessage:
            case EnumChatType.OwnMessage:
                return "<font color=\"#8bd5ff\"><strong>[chat]</strong></font>";
            default:
                return "<font color=\"#b8afa0\"><strong>[server]</strong></font>";
        }
    }

    private static string Linkify(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "";
        }

        if (message.IndexOf("<a", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return message;
        }

        StringBuilder text = new StringBuilder();
        int index = 0;
        foreach (Match match in PlainUrlRegex.Matches(message))
        {
            text.Append(message.Substring(index, match.Index - index));
            string url = match.Value;
            string href = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url
                : "https://" + url;
            text.Append("<a href=\"").Append(EscapeAttribute(href)).Append("\">").Append(EscapeText(url)).Append("</a>");
            index = match.Index + match.Length;
        }

        text.Append(message.Substring(index));
        return text.ToString();
    }

    private static string EscapeText(string text)
    {
        return (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string EscapeAttribute(string text)
    {
        return EscapeText(text).Replace("\"", "&quot;");
    }

    private void OnNewScrollbarValue(float value)
    {
        ElementBounds bounds = SingleComposer.GetRichtext("chat").Bounds;
        bounds.fixedY = 10 - value;
        bounds.CalcWorldBounds();
    }

    private void UpdateScrollbarHeights()
    {
        if (SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetScrollbar("scrollbar")?.SetHeights(470, (float)Math.Max(470, SingleComposer.GetRichtext("chat").Bounds.fixedHeight));
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private class ChatLine
    {
        public int GroupId;
        public string Message;
        public EnumChatType ChatType;
        public DateTime CreatedAt;
    }
}