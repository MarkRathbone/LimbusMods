using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed unsafe class TransitionDiagnostics
{
    readonly Dictionary<int, string> previous = new();
    long until, next;
    int lines;
    bool stopped, sample;
    public void BeginFrame(IEnumerable<CanvasState> canvases, Plugin plugin)
    {
        sample = false;
        if (!plugin.TraceTransition.Value || stopped || lines >= 2000) return;
        long now = Environment.TickCount64;
        if (canvases.Any(c => U.Alive(c.Object) && U.Active(c.Object)
            && U.Name(c.Object) == "Prefab_DungeonFormationUIPanel_RpgScene(Clone)"))
            until = now + 10000;
        if (now > until || now < next) return;
        next = now + 100;
        sample = true;
    }
    public void Observe(CanvasState canvas, Plugin plugin)
    {
        if (!sample || lines >= 2000) return;
        try
        {
            var native = U.Component(U.GO(canvas.Root), U.Class("Canvas", "UnityEngine", "UnityEngine.UIModule.dll"));
            if (native == IntPtr.Zero) return;
            string header = U.Name(canvas.Object) + " pixel=" + U.Get<R4>(native, "pixelRect")
                + " factor=" + U.Get<float>(native, "scaleFactor") + " mode=" + U.Get<int>(native, "renderMode");
            var camera = U.Call(native, "get_worldCamera");
            if (U.Alive(camera)) header += " camera=" + U.Name(camera) + " viewport=" + U.Get<R4>(camera, "rect")
                + " cameraPixel=" + U.Get<R4>(camera, "pixelRect");
            Search(canvas.Root, header, "", 6, plugin);
        }
        catch (Exception ex)
        {
            stopped = true;
            plugin.LogMessage("Transition trace stopped (layout remains enabled): " + ex.Message);
        }
    }
    void Search(IntPtr node, string header, string path, int depth, Plugin plugin)
    {
        if (depth < 0 || lines >= 2000 || !U.Alive(node) || !U.Active(node)) return;
        path += "/" + U.Name(node);
        var go = U.GO(node);
        var group = U.Component(go, U.Class("CanvasGroup", "UnityEngine", "UnityEngine.UIModule.dll"));
        if (group != IntPtr.Zero) path += "(group=" + U.Get<float>(group, "alpha").ToString("0.###") + ")";
        var rect = U.Component(go, U.Class("RectTransform"));
        if (rect != IntPtr.Zero)
        {
            var bounds = U.Get<R4>(rect, "rect");
            if (bounds.Width >= 1000 && bounds.Height >= 600)
            {
                var graphic = U.Component(go, U.Class("Graphic", "UnityEngine.UI", "UnityEngine.UI.dll"));
                if (graphic != IntPtr.Zero)
                {
                    var renderer = U.Call(graphic, "get_canvasRenderer");
                    float inherited = *(float*)IL2CPP.il2cpp_object_unbox(U.Call(renderer, "GetInheritedAlpha"));
                    var color = *(Color*)IL2CPP.il2cpp_object_unbox(U.Call(renderer, "GetColor"));
                    string value = header + path + " bounds=" + bounds + " position=" + U.Get<V3>(rect, "position")
                        + " scale=" + U.Get<V3>(rect, "lossyScale") + " enabled=" + U.Get<byte>(graphic, "enabled")
                        + " color=" + U.Get<Color>(graphic, "color") + " rendererColor=" + color
                        + " inheritedAlpha=" + inherited.ToString("0.###") + " culled=" + U.Get<byte>(renderer, "cull");
                    int id = U.Id(graphic);
                    if (!previous.TryGetValue(id, out var old) || value != old)
                    {
                        previous[id] = value;
                        plugin.LogMessage("Transition trace: " + value);
                        lines++;
                    }
                }
            }
        }
        foreach (var child in U.Children(node)) Search(child, header, path, depth - 1, plugin);
    }
}
