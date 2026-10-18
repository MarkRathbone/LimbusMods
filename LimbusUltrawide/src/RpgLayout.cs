namespace UltrawideLimbus;

internal sealed class RpgLayout
{
    static readonly HashSet<string> screens = new(StringComparer.Ordinal)
    {
        "Prefab_RpgUiPanel_main", "Prefab_RpgUiPanel_main_Dialogue_fix",
        "Prefab_RpgUiPanel_theater", "Prefab_RpgUiPanel_CinematicSetting",
        "Prefab_RpgUiPanel_sisyphe_CinematicSetting", "Prefab_RpgUiPanel_EndingSkip",
        "Prefab_RpgUiPanel_LocationBanner", "Popup_RpgUiPanel_SideNoticePopup",
        "Popup_RpgUiPanel_Menu", "Popup_RpgUiPanel_Shop", "Popup_RpgUiPanel_FloorSelect",
        "Popup_RpgUiPanel_SuicideBox", "Prefab_RpgUiPanel_GameChoice",
        "Prefab_RpgUiPanel_TheaterSetting", "Prefab_RpgUiPanel_TheaterSettingSound",
        "Prefab_RpgUiPanel_logPopup", "Prefab_RpgUi_ProgressPopup",
        "Prefab_RpgUiPanel_sisyphe_PopupUI", "Prefab_sisyphe_manualPopup",
        "Prefab_sisyphe_ProgressPopup"
    };
    readonly Dictionary<int, (int Canvas, SavedRect Rect)> panels = new();
    readonly Dictionary<int, (int Canvas, SavedRect Rect)> dimmers = new();
    readonly HashSet<int> pending = new();
    readonly Dictionary<int, SavedRect> details = new();
    readonly Dictionary<int, SavedRect> backings = new();
    readonly Dictionary<int, SavedTransformScale> effectScales = new();
    SavedRect Detail(IntPtr rect)
    {
        int id = U.Id(rect);
        if (!details.TryGetValue(id, out var saved)) details[id] = saved = new SavedRect(rect);
        return saved;
    }
    static string PrefabName(IntPtr root) => U.Name(root).Replace("(Clone)", "").Trim();
    public bool Apply(CanvasState canvas, Plugin plugin, bool enabled)
    {
        foreach (var pair in details.Where(p => !U.Alive(p.Value.Object)).ToArray())
        { pair.Value.Dispose(); details.Remove(pair.Key); }
        foreach (var pair in backings.Where(p => !U.Alive(p.Value.Object)).ToArray())
        { pair.Value.Dispose(); backings.Remove(pair.Key); }
        foreach (var pair in effectScales.Where(p => !U.Alive(p.Value.Object)).ToArray())
        { pair.Value.Dispose(); effectScales.Remove(pair.Key); }
        if (plugin.Discover || pending.Remove(canvas.Id)) Find(canvas.Root, canvas.Id, 6, plugin);
        bool active = false;
        foreach (var pair in panels.ToArray())
        {
            var entry = pair.Value;
            if (!U.Alive(entry.Rect.Object)) { entry.Rect.Dispose(); panels.Remove(pair.Key); continue; }
            if (entry.Canvas != canvas.Id || !U.Active(entry.Rect.Object)) continue;
            active = true;
            var scale = U.Get<V3>(entry.Rect.Object, "localScale");
            var bounds = U.Get<R4>(entry.Rect.Object, "rect");
            if (Math.Abs(scale.X) < .001f || Math.Abs(scale.Y) < .001f || bounds.Width <= 0 || bounds.Height <= 0) continue;
            if (enabled) entry.Rect.Viewport(canvas.Root); else entry.Rect.Restore();
            string name = PrefabName(entry.Rect.Object);
            if (name == "Popup_RpgUiPanel_SuicideBox")
            {
                var viewport = U.Get<R4>(canvas.Root, "rect");
                float horizontal = LayoutMath.TransitionWidthScale(viewport.Width, viewport.Height);
                foreach (var effectName in new[] { "FX_UI_Main_Cp10_Start", "FX_UI_Main_Cp10_End", "FX_UI_Main_Cp10_End2" })
                {
                    var effect = U.Find(entry.Rect.Object, effectName, 1);
                    var shade = effect == IntPtr.Zero ? IntPtr.Zero : U.Find(effect, "MapDarker", 3);
                    if (shade == IntPtr.Zero) continue;
                    int shadeId = U.Id(shade);
                    if (!effectScales.TryGetValue(shadeId, out var correction))
                    {
                        effectScales[shadeId] = correction = new SavedTransformScale(shade);
                        plugin.LogMessage("Sisyphe machine viewport shade: " + effectName + "/MapDarker");
                    }
                    if (enabled) correction.Apply(horizontal, 1); else correction.Restore();
                }
            }
            if (name == "Prefab_RpgUiPanel_main")
            {
                var boss = U.Find(entry.Rect.Object, "Image_boss_health", 4);
                if (boss != IntPtr.Zero)
                {
                    var saved = Detail(boss);
                    var gauge = U.Find(boss, "Image_gaugebg", 1);
                    if (enabled && gauge != IntPtr.Zero) saved.CenterVisualX(canvas.Root, gauge);
                    else saved.Restore();
                }
                var tracker = U.Find(entry.Rect.Object, "Quest_title_group", 5);
                if (tracker != IntPtr.Zero)
                {
                    var saved = Detail(tracker);
                    var viewport = U.Get<R4>(canvas.Root, "rect");
                    if (enabled) saved.OffsetX(viewport.Width - canvas.Original.Reference.X); else saved.Restore();
                }
            }
            if (name == "Prefab_RpgUiPanel_main_Dialogue_fix")
            {
                var art = U.Find(entry.Rect.Object, "[Image] Dialogue Box_BG", 3);
                if (art != IntPtr.Zero)
                {
                    var saved = Detail(art);
                    if (enabled) saved.WidthToViewport(canvas.Root); else saved.Restore();
                }
            }
            if (name == "Popup_RpgUiPanel_Menu")
            {
                var viewport = U.Get<R4>(canvas.Root, "rect");
                float fraction = LayoutMath.ContentWidth(viewport.Width, viewport.Height, plugin.MenuAspect.Value) / viewport.Width;
                foreach (var child in U.Children(entry.Rect.Object))
                {
                    if (U.Name(child) is not ("Left_tap" or "btn_CloseButton" or "Status")) continue;
                    var saved = Detail(child);
                    if (enabled) { saved.AcceptGameAnchors(); saved.SafeRange(fraction); } else saved.Restore();
                }
                if (!backings.TryGetValue(pair.Key, out var backing))
                {
                    var go = U.NewGO("Ultrawide RPG menu black backing", entry.Rect.Object);
                    var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
                    U.Set(image, "color", new Color(0, 0, 0, 1)); U.Set(image, "raycastTarget", (byte)0);
                    var rect = U.Transform(image); U.Call(rect, "SetAsFirstSibling");
                    backings[pair.Key] = backing = new SavedRect(rect);
                }
                U.ActiveSelf(U.GO(backing.Object), enabled);
                if (enabled) backing.Viewport(canvas.Root);
            }
        }
        foreach (var pair in dimmers.ToArray())
        {
            var entry = pair.Value;
            if (!U.Alive(entry.Rect.Object)) { entry.Rect.Dispose(); dimmers.Remove(pair.Key); continue; }
            if (entry.Canvas != canvas.Id || !U.Active(entry.Rect.Object)) continue;
            if (enabled) entry.Rect.Viewport(canvas.Root); else entry.Rect.Restore();
        }
        return active;
    }
    void Find(IntPtr root, int canvas, int depth, Plugin plugin)
    {
        if (depth < 0 || !U.Alive(root)) return;
        string name = PrefabName(root);
        if (screens.Contains(name) && U.Component(U.GO(root), U.Class("RectTransform")) != IntPtr.Zero)
        {
            if (!U.Active(root)) return;
            var bounds = U.Get<R4>(root, "rect");
            var scale = U.Get<V3>(root, "localScale");
            if (bounds.Width <= 0 || bounds.Height <= 0 || Math.Abs(scale.X) < .001f || Math.Abs(scale.Y) < .001f)
            { pending.Add(canvas); return; }
            int id = U.Id(root);
            if (!panels.ContainsKey(id))
            {
                panels[id] = (canvas, new SavedRect(root));
                plugin.LogMessage("RPG viewport panel: " + name);
            }
            foreach (var child in U.Children(root))
            {
                string childName = U.Name(child);
                bool dimmer = name is "Popup_RpgUiPanel_Menu" or "Popup_RpgUiPanel_Shop" && childName == "Image"
                    || name == "Prefab_sisyphe_manualPopup" && childName == "Background";
                if (!dimmer || U.Component(U.GO(child), U.Class("RectTransform")) == IntPtr.Zero) continue;
                int childId = U.Id(child);
                if (!dimmers.ContainsKey(childId)) dimmers[childId] = (canvas, new SavedRect(child));
            }
            return;
        }
        foreach (var child in U.Children(root)) Find(child, canvas, depth - 1, plugin);
    }
}
