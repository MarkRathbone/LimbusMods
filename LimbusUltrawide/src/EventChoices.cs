namespace UltrawideLimbus;

internal sealed class EventChoices : IDisposable
{
    readonly SavedRect root, story, choices, content;
    readonly SavedRect? skillCheck;
    readonly List<SavedRect> checkFollowers = new();
    readonly float originalCheckX;
    readonly IntPtr scroll, viewport, layout, scrollbar;
    readonly byte originalVertical;
    readonly bool originalScrollbar;
    readonly float originalHeight;
    bool adjusted;
    public IntPtr Root => root.Object;
    public int CanvasId { get; }
    public EventChoices(IntPtr panel, int canvasId)
    {
        CanvasId = canvasId;
        var storyRect = U.Find(panel, "[Rect]ScreenUI", 4);
        var choiceRect = U.Find(panel, "[Script]ActionChoiceButtons", 4);
        var scrollRect = U.Find(choiceRect, "Scroll View", 2);
        viewport = U.Find(scrollRect, "Viewport", 1);
        var buttons = U.Find(viewport, "Buttons", 1);
        if (storyRect == IntPtr.Zero || choiceRect == IntPtr.Zero || scrollRect == IntPtr.Zero || viewport == IntPtr.Zero || buttons == IntPtr.Zero)
            throw new InvalidOperationException("Event choice hierarchy is not initialized yet");
        scroll = U.Component(U.GO(scrollRect), U.Class("ScrollRect", "UnityEngine.UI", "UnityEngine.UI.dll"));
        layout = U.Component(U.GO(buttons), U.Class("VerticalLayoutGroup", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (storyRect == IntPtr.Zero || choiceRect == IntPtr.Zero || buttons == IntPtr.Zero || scroll == IntPtr.Zero || layout == IntPtr.Zero)
            throw new InvalidOperationException("Event choice layout components unavailable");
        root = new(panel); story = new(storyRect); choices = new(choiceRect); content = new(buttons);
        var checkRect = U.Find(panel, "[Rect]PersonalityDescriptionField", 4);
        if (checkRect != IntPtr.Zero)
        {
            skillCheck = new(checkRect);
            originalCheckX = U.Get<V2>(checkRect, "anchoredPosition").X;
            var followers = new List<IntPtr>();
            foreach (var name in new[] { "[Script]EventResultScreen", "[Script]SelectionsSuccessRate",
                "[Rect]PersonalityResultField", "[Rect]ActiveResultField" })
            {
                var follower = U.Find(panel, name, 4);
                if (follower != IntPtr.Zero) followers.Add(follower);
            }
            foreach (var follower in followers)
            {
                bool nested = false;
                for (var ancestor = U.Parent(follower); ancestor != IntPtr.Zero && ancestor != panel; ancestor = U.Parent(ancestor))
                    if (followers.Contains(ancestor)) { nested = true; break; }
                if (!nested) checkFollowers.Add(new(follower));
            }
        }
        originalHeight = U.Get<R4>(choiceRect, "rect").Height;
        originalVertical = U.Get<byte>(scroll, "vertical");
        scrollbar = U.Find(scrollRect, "Scrollbar Vertical", 1);
        originalScrollbar = scrollbar != IntPtr.Zero && U.Active(scrollbar);
    }
    public void Apply(CanvasState canvas, bool enabled)
    {
        if (!U.Active(Root)) return;
        if (!enabled)
        {
            if (!adjusted) return;
            root.Restore(); story.Restore(); choices.Restore(); content.Restore();
            skillCheck?.Restore();
            foreach (var follower in checkFollowers) follower.Restore();
            U.Set(scroll, "vertical", originalVertical);
            if (U.Alive(scrollbar)) U.ActiveSelf(U.GO(scrollbar), originalScrollbar);
            adjusted = false; return;
        }
        var bounds = U.Get<R4>(canvas.Root, "rect");
        float storyWidth = U.Get<R4>(story.Object, "rect").Width;
        float choiceWidth = U.Get<R4>(choices.Object, "rect").Width;
        if (bounds.Width < storyWidth + choiceWidth + 40) { Apply(canvas, false); return; }
        root.Viewport(canvas.Root);
        float gap = Math.Clamp(bounds.Width - storyWidth - choiceWidth - 240, 40, 200);
        float groupWidth = storyWidth + choiceWidth + gap;
        story.CenterX(canvas.Root, -groupWidth * .5f + storyWidth * .5f);
        choices.CenterX(canvas.Root, groupWidth * .5f - choiceWidth * .5f);
        skillCheck?.CenterX(canvas.Root, groupWidth * .5f - choiceWidth * .5f);
        if (skillCheck != null)
        {
            float shift = U.Get<V2>(skillCheck.Object, "anchoredPosition").X - originalCheckX;
            foreach (var follower in checkFollowers) follower.OffsetX(shift);
        }
        var items = U.Children(content.Object).Where(U.Active).ToArray();
        var padding = U.Call(layout, "get_padding");
        float required = U.Get<int>(padding, "top") + U.Get<int>(padding, "bottom")
            + Math.Max(0, items.Length - 1) * U.Get<float>(layout, "spacing")
            + items.Sum(item => U.Get<R4>(item, "rect").Height);
        float viewportInset = U.Get<R4>(choices.Object, "rect").Height - U.Get<R4>(viewport, "rect").Height;
        float height = Math.Min(Math.Max(originalHeight, required + viewportInset), Math.Max(originalHeight, bounds.Height - 380));
        choices.Height(height);
        content.Size(U.Get<V2>(content.Object, "sizeDelta") with { Y = required - U.Get<R4>(viewport, "rect").Height });
        bool overflow = required > height - viewportInset + .5f;
        U.Set(scroll, "vertical", (byte)(overflow ? 1 : 0));
        if (U.Alive(scrollbar)) U.ActiveSelf(U.GO(scrollbar), overflow && originalScrollbar);
        if (!adjusted || !overflow) U.Set(scroll, "verticalNormalizedPosition", 1f);
        adjusted = true;
    }
    public void Dispose()
    {
        root.Dispose(); story.Dispose(); choices.Dispose(); content.Dispose(); skillCheck?.Dispose();
        foreach (var follower in checkFollowers) follower.Dispose();
    }
}
