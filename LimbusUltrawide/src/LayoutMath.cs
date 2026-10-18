namespace UltrawideLimbus;

internal static class LayoutMath
{
    public static float TransitionWidthScale(float width, float height)
        => width > 0 && height > 0 ? Math.Max(1, width / (height * (16f / 9f))) : 1;
    public static (float X, float Y) PartProjectionReference((float X, float Y) reference, float width, float height)
        => width > 0 && height > 0 && float.IsFinite(width) && float.IsFinite(height)
            ? (width, height) : reference;
    public static float StoryReferenceHeight(float referenceWidth)
        => referenceWidth / (16f / 9f);
    public static float CornerOffset(float authoredY, float referenceHeight, float viewportHeight)
        => referenceHeight > 0 && viewportHeight > 0
            ? authoredY + Math.Sign(authoredY) * (viewportHeight - referenceHeight) * .5f : authoredY;
    public static float SlideAnchorOffset(float positionX, float designWidth, float viewportWidth)
        => designWidth > 0 && viewportWidth > designWidth
            ? positionX * (viewportWidth / designWidth - 1) / viewportWidth : 0;
    public static bool IsSidePanel(float minX, float maxX, float panelWidth, float designWidth)
        => Math.Abs(minX - maxX) < .00001f && (Math.Abs(minX) < .00001f || Math.Abs(minX - 1) < .00001f)
            && panelWidth > 0 && panelWidth < designWidth * .8f;
    public static float ContentWidth(float viewportWidth, float viewportHeight, float aspect)
        => Math.Min(viewportWidth, viewportHeight * aspect);
    public static float ContentAnchor(float authoredAnchor, float fraction)
        => .5f + (authoredAnchor - .5f) * fraction;
    public static float BackgroundCover(float viewportWidth, float viewportHeight, float imageWidth, float imageHeight)
        => imageWidth > 0 && imageHeight > 0 ? Math.Max(1, Math.Max(viewportWidth / imageWidth, viewportHeight / imageHeight)) : 1;
}
