namespace UltrawideLimbus;

internal sealed class EgoDiagnostics
{
    readonly Dictionary<int, string> previous = new();
    long nextSample;
    int lines;
    public void Observe(CanvasState canvas, Plugin plugin)
    {
        if (!plugin.TraceEgo.Value || lines >= 2500) return;
        long now = Environment.TickCount64;
        if (now < nextSample) return;
        try { Search(canvas.Root, U.Name(canvas.Object), 4, plugin); }
        catch (Exception ex) { plugin.LogMessage("EGO trace skipped: " + ex.Message); }
    }
    public void EndFrame()
    {
        long now = Environment.TickCount64;
        if (now >= nextSample) nextSample = now + 100;
    }
    void Search(IntPtr node, string canvas, int depth, Plugin plugin)
    {
        if (!U.Alive(node) || depth < 0) return;
        string name = U.Name(node);
        if (name is "BattleCutSceneUI" or "[Script]SkillViewCanvas")
        { Snapshot(node, canvas, 7, plugin); return; }
        foreach (var child in U.Children(node)) Search(child, canvas, depth - 1, plugin);
    }
    void Snapshot(IntPtr node, string path, int depth, Plugin plugin)
    {
        if (!U.Alive(node) || depth < 0 || lines >= 2500) return;
        path += "/" + U.Name(node);
        bool active = U.Active(node);
        string value = path + " active=" + active + " scale=" + U.Get<V3>(node, "localScale")
            + " local=" + U.Get<V3>(node, "localPosition");
        var rect = U.Component(U.GO(node), U.Class("RectTransform"));
        if (rect != IntPtr.Zero)
            value += " rect=" + U.Get<R4>(rect, "rect") + " min=" + U.Get<V2>(rect, "anchorMin")
                + " max=" + U.Get<V2>(rect, "anchorMax") + " pos=" + U.Get<V2>(rect, "anchoredPosition");
        var raw = U.Component(U.GO(node), U.Class("RawImage", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (raw != IntPtr.Zero)
        {
            var texture = U.Call(raw, "get_texture");
            if (U.Alive(texture)) value += " texture=" + U.Get<int>(texture, "width") + "x" + U.Get<int>(texture, "height")
                + " uv=" + U.Get<R4>(raw, "uvRect");
        }
        int id = U.Id(node);
        if (!previous.TryGetValue(id, out var old) || old != value)
        { previous[id] = value; plugin.LogMessage("EGO trace: " + value); lines++; }
        if (active) foreach (var child in U.Children(node)) Snapshot(child, path, depth - 1, plugin);
    }
}
