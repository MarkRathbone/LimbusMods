using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed class CultivationFade : IDisposable
{
    const int Steps = 64;
    readonly nint root;
    readonly SavedRect backing;
    readonly nint[] bands = new nint[Steps * 2];
    public IntPtr Root => IL2CPP.il2cpp_gchandle_get_target(root);
    public CultivationFade(IntPtr panel, IntPtr room)
    {
        root = IL2CPP.il2cpp_gchandle_new(room, false);
        var black = Make(panel, "Ultrawide cultivation black backing", 1);
        U.Call(black, "SetAsFirstSibling");
        backing = new SavedRect(black);
        for (int side = 0; side < 2; side++)
        for (int step = 0; step < Steps; step++)
        {
            float alpha = (step + .5f) / Steps;
            var band = Make(room, "Ultrawide cultivation fade", alpha);
            bands[side * Steps + step] = IL2CPP.il2cpp_gchandle_new(band, false);
        }
    }
    static IntPtr Make(IntPtr parent, string name, float alpha)
    {
        var go = U.NewGO(name, parent);
        var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
        U.Set(image, "color", new Color(0, 0, 0, alpha));
        U.Set(image, "raycastTarget", (byte)0);
        var rect = U.Transform(image);
        U.Set(rect, "anchorMin", new V2(.5f, .5f));
        U.Set(rect, "anchorMax", new V2(.5f, .5f));
        U.Set(rect, "pivot", new V2(.5f, .5f));
        return rect;
    }
    public void Apply(IntPtr canvas, bool enabled)
    {
        U.ActiveSelf(U.GO(backing.Object), enabled);
        if (enabled) backing.Viewport(canvas);
        var art = U.Get<R4>(Root, "rect");
        float fadeWidth = art.Width * .075f;
        for (int i = 0; i < bands.Length; i++)
        {
            var band = IL2CPP.il2cpp_gchandle_get_target(bands[i]);
            U.ActiveSelf(U.GO(band), enabled);
            if (!enabled) continue;
            int side = i / Steps, step = i % Steps;
            float distance = fadeWidth * (1 - (step + .5f) / Steps);
            float x = side == 0 ? art.X + distance : art.X + art.Width - distance;
            U.Set(band, "sizeDelta", new V2(fadeWidth / Steps + .1f, art.Height));
            U.Set(band, "localPosition", new V3(x, art.Y + art.Height / 2, 0));
        }
    }
    public void Dispose()
    {
        backing.Dispose();
        IL2CPP.il2cpp_gchandle_free(root);
        foreach (var band in bands) IL2CPP.il2cpp_gchandle_free(band);
    }
}
