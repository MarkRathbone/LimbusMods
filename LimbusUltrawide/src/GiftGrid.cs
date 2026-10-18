namespace UltrawideLimbus;

internal sealed class GiftGrid : IDisposable
{
    readonly SavedRect root, scrollRect, description;
    readonly IntPtr scroll, content, grid;
    readonly int originalConstraint, originalColumns;
    readonly byte originalVertical;
    readonly V2 originalContentSize;
    readonly List<(IntPtr Image, byte Enabled)> arrows = new();
    public IntPtr Root => root.Object;
    public int CanvasId { get; private set; }
    bool adjusted;

    public GiftGrid(IntPtr panel)
    {
        root = new(panel);
        var scrollTransform = U.Find(panel, "Scroll View", 1);
        scrollRect = new(scrollTransform);
        description = new(U.Find(panel, "[Rect]Description", 1));
        content = U.Find(scrollTransform, "Content", 2);
        grid = U.Component(U.GO(content), U.Class("GridLayoutGroup", "UnityEngine.UI", "UnityEngine.UI.dll"));
        scroll = U.Component(U.GO(scrollTransform), U.Class("ScrollRect", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (grid == IntPtr.Zero || scroll == IntPtr.Zero) throw new InvalidOperationException("Starting gift grid components unavailable");
        originalConstraint = U.Get<int>(grid, "constraint");
        originalColumns = U.Get<int>(grid, "constraintCount");
        originalVertical = U.Get<byte>(scroll, "vertical");
        originalContentSize = U.Get<V2>(content, "sizeDelta");
        foreach (var child in U.Children(scrollTransform))
            if (U.Name(child).Contains("Arrow", StringComparison.OrdinalIgnoreCase))
            {
                var image = U.Component(U.GO(child), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
                if (image != IntPtr.Zero) arrows.Add((image, U.Get<byte>(image, "enabled")));
            }
    }
    public void Apply(CanvasState canvas, Plugin p, bool enabled)
    {
        CanvasId = canvas.Id;
        if (!U.Active(Root)) return;
        if (!enabled)
        {
            if (!adjusted) return;
            root.Restore(); scrollRect.Restore(); description.Restore();
            U.Set(grid, "constraint", originalConstraint); U.Set(grid, "constraintCount", originalColumns);
            U.Set(scroll, "vertical", originalVertical); U.Set(content, "sizeDelta", originalContentSize);
            foreach (var arrow in arrows) if (U.Alive(arrow.Image)) U.Set(arrow.Image, "enabled", arrow.Enabled);
            adjusted = false; return;
        }
        var viewport = U.Get<R4>(canvas.Root, "rect");
        float detailWidth = U.Get<R4>(description.Object, "rect").Width;
        float width = viewport.Width - p.GiftMargin.Value * 2 - detailWidth - p.GiftGap.Value;
        var cell = U.Get<V2>(grid, "cellSize"); var spacing = U.Get<V2>(grid, "spacing");
        if (width < cell.X) { Apply(canvas, p, false); return; }
        var padding = U.Call(grid, "get_padding");
        int horizontalPadding = U.Get<int>(padding, "left") + U.Get<int>(padding, "right");
        int verticalPadding = U.Get<int>(padding, "top") + U.Get<int>(padding, "bottom");
        int count = U.Children(content).Count(c => U.Active(c));
        int maxColumns = Math.Max(1, (int)Math.Floor((width - horizontalPadding + spacing.X) / (cell.X + spacing.X)));
        int columns = Math.Min(maxColumns, Math.Max(1, (count + p.GiftRows.Value - 1) / p.GiftRows.Value));
        int rows = (count + columns - 1) / columns;
        float required = rows * cell.Y + Math.Max(0, rows - 1) * spacing.Y + verticalPadding;
        float height = Math.Max(cell.Y, viewport.Height - p.GiftMargin.Value * 2);
        float gridWidth = columns * cell.X + Math.Max(0, columns - 1) * spacing.X + horizontalPadding;
        float scale = Math.Clamp(Math.Min(1, height / Math.Max(1, required + 17)), p.GiftMinimumScale.Value, 1);
        float logicalHeight = Math.Min(required + 17, height / scale);
        float displayedWidth = gridWidth * scale;
        float groupWidth = displayedWidth + p.GiftGap.Value + detailWidth;
        root.Viewport(canvas.Root);
        description.Anchors(.5f);
        description.PositionX(groupWidth / 2 - detailWidth / 2);
        scrollRect.Anchors(.5f);
        scrollRect.PositionX(-groupWidth / 2 + displayedWidth / 2);
        scrollRect.Size(new(gridWidth, logicalHeight));
        scrollRect.UniformScale(scale);
        U.Set(grid, "constraint", 1); // FixedColumnCount
        U.Set(grid, "constraintCount", columns);
        U.Set(content, "sizeDelta", U.Get<V2>(content, "sizeDelta") with { Y = required });
        bool overflow = required > logicalHeight - 17 + .1f;
        U.Set(scroll, "vertical", (byte)(overflow ? 1 : 0));
        foreach (var arrow in arrows)
            if (U.Alive(arrow.Image)) U.Set(arrow.Image, "enabled", (byte)(overflow ? arrow.Enabled : 0));
        if (!adjusted) U.Set(scroll, "verticalNormalizedPosition", 1f);
        adjusted = true;
    }
    public void Dispose() { root.Dispose(); scrollRect.Dispose(); description.Dispose(); }
}
