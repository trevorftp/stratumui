using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.Stratum.UI;

internal sealed class StratumCommandSuggestionPopup : GuiDialog
{
    internal const double RowHeight = 26;
    internal const double VerticalPadding = 7;
    private const double HorizontalPadding = 10;

    private static readonly double[] BackgroundColor = { 0.018, 0.016, 0.013, 0.9 };
    private static readonly double[] BorderColor = { 0.58, 0.48, 0.3, 0.65 };
    private static readonly double[] SelectionColor = { 0.46, 0.34, 0.16, 0.58 };
    private static readonly double[] ValueColor = { 0.84, 0.78, 0.64, 1 };
    private static readonly double[] SelectedValueColor = { 1, 0.94, 0.76, 1 };
    private static readonly double[] KindColor = { 0.56, 0.53, 0.47, 1 };

    private readonly CairoFont valueFont;
    private readonly CairoFont selectedValueFont;
    private readonly CairoFont kindFont;
    private StratumCompletionSuggestion[] visibleSuggestions = Array.Empty<StratumCompletionSuggestion>();
    private StratumCommandSuggestionPlacement currentPlacement;
    private int selectedIndex;
    private string lastSignature;

    public StratumCommandSuggestionPopup(ICoreClientAPI capi) : base(capi)
    {
        valueFont = CairoFont.WhiteDetailText().WithFontSize(16f).WithColor(ValueColor);
        selectedValueFont = valueFont.Clone().WithColor(SelectedValueColor).WithWeight(FontWeight.Bold);
        kindFont = valueFont.Clone().WithColor(KindColor).WithFontSize(14f);
    }

    public override bool Focusable => false;

    public override string ToggleKeyCombinationCode => null;

    public override EnumDialogType DialogType => EnumDialogType.HUD;

    public override double DrawOrder => 0.035;

    public void Show(StratumCommandSuggestionPlacement placement, IReadOnlyList<StratumCompletionSuggestion> suggestions, int selectedIndex)
    {
        if (suggestions == null || suggestions.Count == 0 || placement.VisibleCount == 0)
        {
            Hide();
            return;
        }

        int clampedSelectedIndex = Math.Clamp(selectedIndex, 0, suggestions.Count - 1);
        int visibleStartIndex = Math.Clamp(clampedSelectedIndex - placement.VisibleCount / 2, 0, Math.Max(0, suggestions.Count - placement.VisibleCount));
        visibleSuggestions = suggestions.Skip(visibleStartIndex).Take(placement.VisibleCount).ToArray();
        currentPlacement = placement;
        this.selectedIndex = clampedSelectedIndex - visibleStartIndex;

        string signature = BuildSignature();
        if (!string.Equals(signature, lastSignature, StringComparison.Ordinal))
        {
            lastSignature = signature;
            Compose();
        }

        if (!IsOpened())
        {
            TryOpen(withFocus: false);
        }
    }

    public void Hide()
    {
        lastSignature = null;
        if (IsOpened())
        {
            TryClose();
        }
    }

    private void Compose()
    {
        Composers.ClearComposers();

        ElementBounds dialogBounds = ElementBounds.Fixed(currentPlacement.Left, currentPlacement.Top, currentPlacement.Width, currentPlacement.Height);

        SingleComposer = capi.Gui
            .CreateCompo("stratum-command-suggestions", dialogBounds)
            .AddGameOverlay(ElementBounds.Fill, BackgroundColor)
            .AddStaticCustomDraw(ElementBounds.Fill, DrawPopup)
            .Compose(false);
    }

    private void DrawPopup(Context context, ImageSurface surface, ElementBounds bounds)
    {
        double scale = RuntimeEnv.GUIScale;
        double width = currentPlacement.Width * scale;
        double height = currentPlacement.Height * scale;
        double rowHeight = RowHeight * scale;
        double paddingY = VerticalPadding * scale;
        double paddingX = HorizontalPadding * scale;
        FontExtents fontExtents = valueFont.GetFontExtents();

        for (int suggestionIndex = 0; suggestionIndex < visibleSuggestions.Length; suggestionIndex++)
        {
            if (suggestionIndex == selectedIndex)
            {
                double selectedY = paddingY + suggestionIndex * rowHeight;
                context.SetSourceRGBA(SelectionColor);
                context.Rectangle(2 * scale, selectedY, width - 4 * scale, rowHeight);
                context.Fill();
            }

            StratumCompletionSuggestion suggestion = visibleSuggestions[suggestionIndex];
            double rowTop = paddingY + suggestionIndex * rowHeight;
            double baseline = rowTop + (rowHeight + fontExtents.Ascent - fontExtents.Descent) / 2 - scale;
            CairoFont rowFont = suggestionIndex == selectedIndex ? selectedValueFont : valueFont;

            rowFont.SetupContext(context);
            context.MoveTo(paddingX, baseline);
            context.ShowText(suggestion.Value);

            if (!string.IsNullOrWhiteSpace(suggestion.Kind))
            {
                TextExtents valueExtents = context.TextExtents(suggestion.Value);
                kindFont.SetupContext(context);
                context.MoveTo(paddingX + valueExtents.XAdvance + 10 * scale, baseline);
                context.ShowText(suggestion.Kind);
            }
        }

        context.SetSourceRGBA(BorderColor);
        GuiElement.RoundRectangle(context, 0.5, 0.5, width - 1, height - 1, 2 * scale);
        context.Stroke();
    }

    private string BuildSignature()
    {
        StringBuilder signature = new StringBuilder();
        signature.Append(currentPlacement.Left).Append('|')
            .Append(currentPlacement.Top).Append('|')
            .Append(currentPlacement.Width).Append('|')
            .Append(currentPlacement.Height).Append('|')
            .Append(selectedIndex);

        foreach (StratumCompletionSuggestion suggestion in visibleSuggestions)
        {
            signature.Append('|').Append(suggestion.Value).Append(':').Append(suggestion.Kind);
        }

        return signature.ToString();
    }

}

internal sealed class StratumCompletionSuggestion
{
    public StratumCompletionSuggestion(string value, string kind)
    {
        Value = value ?? string.Empty;
        Kind = kind ?? string.Empty;
    }

    public string Value { get; }

    public string Kind { get; }
}

internal readonly struct StratumCommandSuggestionPlacement
{
    public StratumCommandSuggestionPlacement(double left, double top, double width, double height, int visibleCount)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        VisibleCount = visibleCount;
    }

    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
    public double Height { get; }
    public int VisibleCount { get; }
}