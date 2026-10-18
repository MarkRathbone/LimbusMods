using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed class ScreenGraphic : IDisposable
{
    readonly nint source, image, rect;
    readonly byte originalEnabled;
    readonly bool solidOnly;
    public IntPtr Source => IL2CPP.il2cpp_gchandle_get_target(source);
    public ScreenGraphic(IntPtr original, bool solidOnly = false)
    {
        this.solidOnly = solidOnly;
        source = IL2CPP.il2cpp_gchandle_new(original, false);
        originalEnabled = U.Get<byte>(original, "enabled");
        var go = U.NewGO("Ultrawide container backing", U.Transform(original));
        var component = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
        image = IL2CPP.il2cpp_gchandle_new(component, false);
        var transform = U.Transform(component);
        U.Call(transform, "SetAsFirstSibling");
        rect = IL2CPP.il2cpp_gchandle_new(transform, false);
    }
    public void Apply(IntPtr canvas, bool enabled)
    {
        var backing = IL2CPP.il2cpp_gchandle_get_target(image);
        enabled = enabled && originalEnabled != 0;
        U.ActiveSelf(U.GO(backing), enabled);
        byte desired = enabled ? (byte)0 : originalEnabled;
        if (U.Get<byte>(Source, "enabled") != desired) U.Set(Source, "enabled", desired);
        if (!enabled) return;
        var color = U.Get<Color>(Source, "color");
        if (U.Get<Color>(backing, "color") != color) U.Set(backing, "color", color);
        foreach (string property in solidOnly ? Array.Empty<string>() : new[] { "sprite", "material" })
        {
            var value = U.Call(Source, "get_" + property);
            if (U.Call(backing, "get_" + property) != value) U.ObjectArg(backing, "set_" + property, value);
        }
        U.Set(backing, "raycastTarget", U.Get<byte>(Source, "raycastTarget"));
        Viewport ??= new SavedRect(IL2CPP.il2cpp_gchandle_get_target(rect));
        Viewport.Viewport(canvas);
    }
    SavedRect? Viewport;
    public void Dispose()
    {
        Viewport?.Dispose();
        IL2CPP.il2cpp_gchandle_free(source); IL2CPP.il2cpp_gchandle_free(image); IL2CPP.il2cpp_gchandle_free(rect);
    }
}
