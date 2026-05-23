using System;
using System.Collections.Generic;
using System.Globalization;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.Stratum.UI;

public class StratumPlayerDetailDialog : GuiDialog
{
    private const double DialogWidth = 940;
    private const double DialogHeight = 620;
    private const int HotbarColumns = 10;
    private const int BackpackColumns = 4;

    // Character slot order taken from GuiDialogCharacter so the layout matches the in-game inventory.
    private static readonly int[] CharacterLeftSlots = new[] { 0, 1, 2, 11, 3, 4 };
    private static readonly int[] CharacterRightSlots = new[] { 6, 7, 8, 10, 5, 9 };
    private static readonly int[] CharacterArmorSlots = new[] { 12, 13, 14 };

    private static readonly double[] HeaderColor = { 0.92, 0.84, 0.68, 1 };
    private static readonly double[] MutedColor = { 0.72, 0.66, 0.55, 1 };
    private static readonly double[] AccentColor = { 0.83, 0.74, 0.46, 1 };

    private readonly CairoFont titleFont;
    private readonly CairoFont sectionFont;
    private readonly CairoFont labelFont;
    private readonly CairoFont valueFont;
    private readonly Vec4f lightPos = new Vec4f(-1f, -1f, 0f, 0f).NormalizeXYZ();
    private readonly Matrixf matrix = new Matrixf();

    private StratumPlayerDetailPacket details;
    private readonly IClientNetworkChannel channel;
    private DummyInventory characterView;
    private DummyInventory hotbarView;
    private DummyInventory backpackView;
    private ElementBounds modelInsetBounds;
    private float yaw = -1.2707963f;
    private bool rotateCharacter;

    public StratumPlayerDetailDialog(ICoreClientAPI capi, IClientNetworkChannel channel) : base(capi)
    {
        this.channel = channel;
        titleFont = CairoFont.WhiteMediumText().WithFontSize(20f).WithColor(HeaderColor).WithWeight(FontWeight.Bold);
        sectionFont = CairoFont.WhiteSmallishText().WithFontSize(16f).WithColor(HeaderColor).WithWeight(FontWeight.Bold);
        labelFont = CairoFont.WhiteDetailText().WithFontSize(14f).WithColor(MutedColor);
        valueFont = CairoFont.WhiteDetailText().WithFontSize(14f).WithColor(AccentColor).WithWeight(FontWeight.Bold);
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override double DrawOrder => 0.95;

    public override bool PrefersUngrabbedMouse => true;

    public override float ZSize => RuntimeEnv.GUIScale * 400f;

    public void UpdateDetails(StratumPlayerDetailPacket packet)
    {
        details = packet;
        RebuildInventoryViews();
        Compose();
    }

    public override void OnMouseDown(MouseEvent args)
    {
        base.OnMouseDown(args);
        rotateCharacter = modelInsetBounds != null && modelInsetBounds.PointInside(args.X, args.Y);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);
        rotateCharacter = false;
    }

    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);
        if (rotateCharacter)
        {
            yaw -= (float)args.DeltaX / 100f;
        }
    }

    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        Entity entity = details?.EntityId > 0 ? capi.World.GetEntityById(details.EntityId) : null;
        if (entity != null && modelInsetBounds != null)
        {
            capi.Render.GlPushMatrix();
            if (focused)
            {
                capi.Render.GlTranslate(0f, 0f, 150f);
            }

            capi.Render.GlRotate(-14f, 1f, 0f, 0f);
            matrix.Identity();
            matrix.RotateXDeg(-14f);
            Vec4f light = matrix.TransformVector(lightPos);
            capi.Render.CurrentActiveShader.Uniform("lightPosition", light.X, light.Y, light.Z);

            double centerX = modelInsetBounds.renderX + modelInsetBounds.OuterWidth / 2.0;
            // Eye line sits slightly above the inset top; small negative offset keeps the head visible without cropping.
            double topY = modelInsetBounds.renderY + GuiElement.scaled(-4);
            capi.Render.RenderEntityToGui(deltaTime, entity, centerX - GuiElement.scaled(145), topY, GuiElement.scaled(255), yaw, (float)GuiElement.scaled(145), -1);
            capi.Render.GlPopMatrix();
            capi.Render.CurrentActiveShader.Uniform("lightPosition", 0.7071068f, -0.7071068f, 0f);
        }

        if (modelInsetBounds != null && !modelInsetBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY) && !rotateCharacter)
        {
            yaw += (float)(Math.Sin(capi.World.ElapsedMilliseconds / 1000f) / 220.0);
        }
    }

    private void RebuildInventoryViews()
    {
        characterView = BuildView("character");
        hotbarView = BuildView("hotbar");
        backpackView = BuildView("backpack");
    }

    private DummyInventory BuildView(string className)
    {
        int slotCount = GetSlotCount(className);
        DummyInventory inv = new DummyInventory(capi, Math.Max(1, slotCount));
        if (details?.InventorySlots == null || details.InventoryGroups == null)
        {
            return inv;
        }

        string inventoryCode = null;
        foreach (StratumPlayerInventoryGroupPacket group in details.InventoryGroups)
        {
            if (string.Equals(group.ClassName, className, StringComparison.OrdinalIgnoreCase))
            {
                inventoryCode = group.InventoryCode;
                break;
            }
        }

        if (inventoryCode == null)
        {
            return inv;
        }

        foreach (StratumPlayerInventorySlotPacket slot in details.InventorySlots)
        {
            if (!string.Equals(slot.InventoryCode, inventoryCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (slot.SlotId < 0 || slot.SlotId >= inv.Count)
            {
                continue;
            }

            ItemStack stack = ResolveStack(slot);
            if (stack != null)
            {
                inv[slot.SlotId].Itemstack = stack;
            }
        }

        return inv;
    }

    private int GetSlotCount(string className)
    {
        if (details?.InventoryGroups != null)
        {
            foreach (StratumPlayerInventoryGroupPacket group in details.InventoryGroups)
            {
                if (string.Equals(group.ClassName, className, StringComparison.OrdinalIgnoreCase))
                {
                    return group.SlotCount;
                }
            }
        }

        return className switch
        {
            "character" => 15,
            "hotbar" => HotbarColumns,
            "backpack" => BackpackColumns,
            _ => 1
        };
    }

    private ItemStack ResolveStack(StratumPlayerInventorySlotPacket entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ItemCode))
        {
            return null;
        }

        AssetLocation code;
        try { code = new AssetLocation(entry.ItemCode); }
        catch { return null; }

        Item item = capi.World.GetItem(code);
        if (item != null)
        {
            return new ItemStack(item, Math.Max(1, entry.StackSize));
        }

        Block block = capi.World.GetBlock(code);
        if (block != null)
        {
            return new ItemStack(block, Math.Max(1, entry.StackSize));
        }

        return null;
    }

    private void Compose()
    {
        if (characterView == null)
        {
            RebuildInventoryViews();
        }

        ElementBounds bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
        bgBounds.fixedWidth = DialogWidth;
        bgBounds.fixedHeight = DialogHeight;

        double slotPad = GuiElementItemSlotGridBase.unscaledSlotPadding;
        double slotStep = GuiElementPassiveItemSlot.unscaledSlotSize + slotPad;

        // Left column of character slots (6 rows)
        double leftColX = 22;
        double leftColY = 60;
        ElementBounds leftSlotsBounds = ElementBounds.Fixed(leftColX, leftColY, slotStep, 6 * slotStep);

        // Model inset between columns
        double modelX = leftColX + slotStep + 6;
        double modelW = 200;
        double modelH = 6 * slotStep;
        modelInsetBounds = ElementBounds.Fixed(modelX, leftColY, modelW, modelH);

        // Right column of character slots
        double rightColX = modelX + modelW + 6;
        ElementBounds rightSlotsBounds = ElementBounds.Fixed(rightColX, leftColY, slotStep, 6 * slotStep);

        // Armor row below
        double armorY = leftColY + 6 * slotStep + 14;
        ElementBounds armorSlotsBounds = ElementBounds.Fixed(leftColX, armorY, CharacterArmorSlots.Length * slotStep, slotStep);

        double leftBlockHeight = armorY + slotStep + 8 - leftColY + 8;
        ElementBounds characterInset = ElementBounds.Fixed(leftColX - 8, leftColY - 12, rightColX + slotStep - leftColX + 16, leftBlockHeight);

        // Right side panels
        double rightX = rightColX + slotStep + 22;
        double rightW = DialogWidth - rightX - 22;

        ElementBounds identityInset = ElementBounds.Fixed(rightX, leftColY - 12, rightW, 116);
        ElementBounds recordsBtnBounds = ElementBounds.Fixed(rightX + rightW - 130, leftColY - 12 + 84, 120, 24);

        const int InvCols = 10;
        int hotbarCount = Math.Max(1, hotbarView?.Count ?? HotbarColumns);
        int backpackCount = Math.Max(1, backpackView?.Count ?? BackpackColumns);
        int hotbarRows = (int)Math.Ceiling(hotbarCount / (double)InvCols);
        int backpackRows = (int)Math.Ceiling(backpackCount / (double)InvCols);
        double titleH = 22;
        double sectionPad = 10;

        double hotbarInsetY = identityInset.fixedY + identityInset.fixedHeight + 10;
        double hotbarInsetH = titleH + hotbarRows * slotStep + sectionPad * 2;
        ElementBounds hotbarInset = ElementBounds.Fixed(rightX, hotbarInsetY, rightW, hotbarInsetH);
        ElementBounds hotbarTitleBounds = ElementBounds.Fixed(rightX + 12, hotbarInsetY + 6, rightW - 24, titleH);
        ElementBounds hotbarSlotsBounds = ElementBounds.Fixed(rightX + 12, hotbarInsetY + 6 + titleH, InvCols * slotStep, hotbarRows * slotStep);

        double backpackInsetY = hotbarInsetY + hotbarInsetH + 8;
        double backpackInsetH = titleH + backpackRows * slotStep + sectionPad * 2;
        ElementBounds backpackInset = ElementBounds.Fixed(rightX, backpackInsetY, rightW, backpackInsetH);
        ElementBounds backpackTitleBounds = ElementBounds.Fixed(rightX + 12, backpackInsetY + 6, rightW - 24, titleH);
        ElementBounds backpackSlotsBounds = ElementBounds.Fixed(rightX + 12, backpackInsetY + 6 + titleH, InvCols * slotStep, backpackRows * slotStep);

        // Stats inset across bottom
        double statsY = Math.Max(armorY + slotStep + 18, backpackInsetY + backpackInsetH + 10);
        double statsH = Math.Max(110, (Math.Max(0, (details?.Stats?.Count ?? 0) - 1) / 4 + 1) * 22 + 36);
        double dialogH = Math.Max(DialogHeight, statsY + statsH + 50);
        bgBounds.fixedHeight = dialogH;
        ElementBounds statsInset = ElementBounds.Fixed(10, statsY, DialogWidth - 20, statsH);
        ElementBounds statsTitleBounds = ElementBounds.Fixed(20, statsY + 6, 200, 22);
        ElementBounds closeBtnBounds = ElementBounds.Fixed(DialogWidth - 120, dialogH - 40, 90, 26);

        SingleComposer = capi.Gui
            .CreateCompo("stratum-player-detail", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(BuildTitle(), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddInset(characterInset)
                .AddItemSlotGrid(characterView, _ => { }, 1, CharacterLeftSlots, leftSlotsBounds, "char-left")
                .AddInset(modelInsetBounds, 0)
                .AddDynamicCustomDraw(modelInsetBounds, DrawModelOverlay, "model-overlay")
                .AddItemSlotGrid(characterView, _ => { }, 1, CharacterRightSlots, rightSlotsBounds, "char-right")
                .AddItemSlotGrid(characterView, _ => { }, CharacterArmorSlots.Length, CharacterArmorSlots, armorSlotsBounds, "char-armor")
                .AddInset(identityInset)
                .AddDynamicCustomDraw(identityInset, DrawIdentityPanel, "identity")
                .AddSmallButton("View Records", OnViewRecordsClicked, recordsBtnBounds)
                .AddInset(hotbarInset)
                .AddStaticText("Hotbar", sectionFont, hotbarTitleBounds)
                .AddItemSlotGrid(hotbarView, _ => { }, InvCols, hotbarSlotsBounds, "hotbar-grid")
                .AddInset(backpackInset)
                .AddStaticText("Backpack", sectionFont, backpackTitleBounds)
                .AddItemSlotGrid(backpackView, _ => { }, InvCols, backpackSlotsBounds, "backpack-grid")
                .AddInset(statsInset)
                .AddStaticText("Stats", sectionFont, statsTitleBounds)
                .AddDynamicCustomDraw(statsInset, DrawStatsPanel, "stats")
                .AddSmallButton("Close", () => TryClose(), closeBtnBounds)
            .EndChildElements()
            .Compose();
    }

    private string BuildTitle()
    {
        return string.IsNullOrWhiteSpace(details?.PlayerName) ? "Player Details" : "Player Details: " + details.PlayerName;
    }

    private void DrawModelOverlay(Context context, ImageSurface surface, ElementBounds bounds)
    {
        if (details == null || details.EntityId <= 0 || capi.World.GetEntityById(details.EntityId) == null)
        {
            double scale = RuntimeEnv.GUIScale;
            DrawCenteredText(context, labelFont, "Model loads when\nthe player entity\nis visible", bounds.fixedWidth * scale, (bounds.fixedHeight - 80) * scale);
        }
    }

    private void DrawIdentityPanel(Context context, ImageSurface surface, ElementBounds bounds)
    {
        double scale = RuntimeEnv.GUIScale;
        double padX = 16 * scale;
        double topY = 8 * scale;

        DrawText(context, titleFont, details?.PlayerName ?? "Player", padX, topY);
        topY += 28 * scale;

        double colW = (bounds.fixedWidth - 32) * scale / 3.0;
        DrawLabelValue(context, "Role", DisplayRole(), padX, topY);
        DrawLabelValue(context, "Mode", details?.GameMode ?? "Unknown", padX + colW, topY);
        DrawLabelValue(context, "Ping", FormatPing(details?.PingMs ?? -1), padX + 2 * colW, topY);

        topY += 22 * scale;
        DrawLabelValue(context, "Warns", (details?.ActiveWarnings ?? 0).ToString(CultureInfo.InvariantCulture), padX, topY);
        DrawLabelValue(context, "Viols", (details?.ActiveViolations ?? 0).ToString(CultureInfo.InvariantCulture), padX + colW, topY);

        topY += 22 * scale;
        DrawLabelValue(context, "Pos", FormatPosition(details?.Position), padX, topY);
    }

    private void DrawStatsPanel(Context context, ImageSurface surface, ElementBounds bounds)
    {
        double scale = RuntimeEnv.GUIScale;
        double padX = 16 * scale;
        double topY = 32 * scale;

        List<StratumPlayerStatPacket> stats = details?.Stats ?? new List<StratumPlayerStatPacket>();
        if (stats.Count == 0)
        {
            DrawText(context, labelFont, "No stats available", padX, topY);
            return;
        }

        int columns = 4;
        double colW = (bounds.fixedWidth - 32) * scale / columns;
        double rowH = 20 * scale;
        int col = 0;
        double y = topY;
        foreach (StratumPlayerStatPacket stat in stats)
        {
            double x = padX + col * colW;
            DrawLabelValue(context, FormatStatLabel(stat.Code), stat.Value, x, y);
            col++;
            if (col >= columns)
            {
                col = 0;
                y += rowH;
                if (y + rowH > bounds.fixedHeight * scale - 6 * scale)
                {
                    break;
                }
            }
        }
    }

    private void DrawLabelValue(Context context, string label, string value, double x, double y)
    {
        double scale = RuntimeEnv.GUIScale;
        DrawText(context, labelFont, label, x, y);
        double labelWidth = labelFont.GetTextExtents(label).Width + 8 * scale;
        DrawText(context, valueFont, value ?? string.Empty, x + labelWidth, y);
    }

    private static void DrawText(Context context, CairoFont font, string text, double x, double top)
    {
        font.SetupContext(context);
        FontExtents extents = font.GetFontExtents();
        context.MoveTo(x, top + extents.Ascent);
        context.ShowText(text ?? string.Empty);
    }

    private static void DrawCenteredText(Context context, CairoFont font, string text, double width, double top)
    {
        font.SetupContext(context);
        FontExtents extents = font.GetFontExtents();
        string[] lines = (text ?? string.Empty).Split('\n');
        double y = top;
        foreach (string line in lines)
        {
            TextExtents te = font.GetTextExtents(line);
            context.MoveTo((width - te.Width) / 2.0, y + extents.Ascent);
            context.ShowText(line);
            y += extents.Height;
        }
    }

    private string DisplayRole()
    {
        string role = string.IsNullOrWhiteSpace(details?.RoleName) ? details?.RoleCode : details.RoleName;
        return string.IsNullOrWhiteSpace(role) ? "player" : role;
    }

    private static string FormatPing(int pingMs)
    {
        return pingMs < 0 ? "? ms" : pingMs + " ms";
    }

    private static string FormatPosition(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return "unknown";
        }

        // Round to integers so the position fits in the panel.
        string[] parts = position.Split(',');
        if (parts.Length != 3)
        {
            return position.Trim();
        }

        try
        {
            int x = (int)Math.Round(double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture));
            int y = (int)Math.Round(double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
            int z = (int)Math.Round(double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture));
            return x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + ", " + z.ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return position.Trim();
        }
    }

    private static string FormatStatLabel(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(code.Length + 4);
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(code[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }

        return sb.ToString();
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private bool OnViewRecordsClicked()
    {
        if (channel?.Connected != true || string.IsNullOrWhiteSpace(details?.TargetPlayerUid))
        {
            return true;
        }

        channel.SendPacket(new StratumPlayerModerationRequest { TargetPlayerUid = details.TargetPlayerUid });
        return true;
    }
}
