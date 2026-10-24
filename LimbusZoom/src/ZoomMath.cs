namespace LimbusZoom;

internal static class ZoomMath
{
    public static float Next(float current, float wheel, float step, float minimum, float maximum)
    {
        if (!float.IsFinite(current) || current <= 0) current = 1;
        if (!float.IsFinite(wheel) || !float.IsFinite(step)) return current;
        return Math.Clamp(current * MathF.Exp(Math.Clamp(wheel, -10, 10) * step), minimum, maximum);
    }

    public static float Cover(float viewWidth, float viewHeight, float artWidth, float artHeight)
    {
        if (artWidth <= 0 || artHeight <= 0 || !float.IsFinite(artWidth) || !float.IsFinite(artHeight)) return 1;
        return Math.Max(viewWidth / artWidth, viewHeight / artHeight);
    }

    public static float Correction(float artStart, float artSize, float viewStart, float viewSize)
    {
        if (artSize <= viewSize) return viewStart + viewSize / 2 - artStart - artSize / 2;
        return Math.Clamp(0, viewStart + viewSize - artStart - artSize, viewStart - artStart);
    }

    public static (float X, float Y) Position(float x, float y, float anchorX, float anchorY, float ratio)
        => (anchorX + (x - anchorX) * ratio, anchorY + (y - anchorY) * ratio);
}
