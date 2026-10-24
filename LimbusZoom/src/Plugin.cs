using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;

namespace LimbusZoom;

[BepInPlugin("local.limbus.zoom", "Limbus Zoom", "0.1.1")]
public sealed unsafe class Plugin : BasePlugin
{
    readonly List<INativeDetour> hooks = new();
    readonly List<Delegate> delegates = new();
    readonly Dictionary<IntPtr, View> views = new();
    readonly HashSet<IntPtr> hooked = new();
    ConfigEntry<bool> enabled = null!, story = null!, dungeons = null!;
    ConfigEntry<float> minimum = null!, maximum = null!, step = null!;
    IntPtr storyPanel, mirrorFloor, mirrorStart, input, keyDown;
    int errors;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void EventAction(IntPtr obj, IntPtr data, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FocusAction(IntPtr obj, IntPtr slot, byte first, byte second, byte third, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstanceAction(IntPtr obj, IntPtr method);

    sealed class View
    {
        public IntPtr Scroll, Content, Background;
        public V3 OriginalScale;
        public float Factor = 1;
        public byte? Boundary;
        public IntPtr ScrollHandle, ContentHandle;
    }

    public override void Load()
    {
        enabled = Config.Bind("General", "Enabled", true, "Enable mouse-wheel map zoom. Drag to pan; Home restores the original scale.");
        story = Config.Bind("Maps", "Story", true, "Enable zoom on the story stage-selection map.");
        dungeons = Config.Bind("Maps", "Dungeons", true, "Enable zoom on story-dungeon and Mirror Dungeon map scroll views.");
        minimum = Number("MinimumScale", .25f, .1f, 1, "Requested minimum scale; the map artwork can impose a higher minimum.");
        maximum = Number("MaximumScale", 1, 1, 2, "Largest map scale relative to the game's normal view.");
        step = Number("WheelStep", .12f, .01f, .5f, "Zoom speed per mouse-wheel notch.");
        storyPanel = U.Class("StageMapUIPanel", "MainUI", "Assembly-CSharp.dll");
        mirrorFloor = U.Class("MirrorDungeonMapFloorScrollRect", "Dungeon.Mirror.Map", "Assembly-CSharp.dll");
        mirrorStart = U.Class("MirrorDungeonStartBuffMapController", "MainUI", "Assembly-CSharp.dll");
        input = U.Class("Input", "UnityEngine", "UnityEngine.InputLegacyModule.dll");
        keyDown = U.MethodTyped(input, "GetKeyDown", "KeyCode");
        try
        {
            Install(U.Class("ScrollRect", "UnityEngine.UI", "UnityEngine.UI.dll"));
            Install(U.Class("CustomScrollRect", "CustomUnityCode", "Assembly-CSharp.dll"));
            InstallFocus();
        }
        catch
        {
            foreach (var hook in hooks.AsEnumerable().Reverse()) hook.Dispose();
            hooks.Clear(); delegates.Clear(); throw;
        }
        Log.LogInfo("Map zoom ready: wheel to zoom, drag to pan, Home to reset.");
    }

    ConfigEntry<float> Number(string name, float value, float low, float high, string description)
        => Config.Bind("Zoom", name, value, new ConfigDescription(description, new AcceptableValueRange<float>(low, high)));

    void Install(IntPtr klass)
    {
        var address = Marshal.ReadIntPtr(U.Method(klass, "OnScroll", 1));
        if (hooked.Add(address))
        {
            EventAction? original = null;
            EventAction replacement = (obj, data, info) =>
            {
                bool consumed = false;
                try { consumed = Scroll(obj, data); }
                catch (Exception ex) { Warn(ex); }
                if (!consumed) original!(obj, data, info);
            };
            hooks.Add(INativeDetour.CreateAndApply(address, replacement, out original));
            delegates.Add(replacement); delegates.Add(original!);
        }
        Hook(klass, "LateUpdate", (obj, original) =>
        {
            original();
            try { Tick(obj); } catch (Exception ex) { Warn(ex); }
        });
        Hook(klass, "OnDisable", (obj, original) =>
        {
            try { Forget(obj, true); } catch (Exception ex) { Warn(ex); }
            original();
        });
    }

    void Hook(IntPtr klass, string name, Action<IntPtr, Action> action)
    {
        var address = Marshal.ReadIntPtr(U.Method(klass, name));
        if (!hooked.Add(address)) return;
        InstanceAction? original = null;
        InstanceAction replacement = (obj, info) => action(obj, () => original!(obj, info));
        hooks.Add(INativeDetour.CreateAndApply(address, replacement, out original));
        delegates.Add(replacement); delegates.Add(original!);
    }

    bool IsMap(IntPtr scroll)
    {
        var klass = IL2CPP.il2cpp_object_get_class(scroll);
        for (var c = klass; c != IntPtr.Zero; c = IL2CPP.il2cpp_class_get_parent(c))
        {
            string name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(c)) ?? "";
            if (name == "StageMapScrollRect") return story.Value;
            if (name == "DungeonSceneScrollRect") return dungeons.Value;
        }
        var current = U.Transform(scroll);
        for (int depth = 0; depth < 8 && U.Alive(current); depth++, current = U.Parent(current))
        {
            var go = U.GO(current);
            if (story.Value)
            {
                var panel = U.Component(go, storyPanel);
                if (panel != IntPtr.Zero && U.Read<IntPtr>(panel, "_scrollRect") == scroll) return true;
            }
            if (dungeons.Value)
            {
                foreach (var ownerClass in new[] { mirrorFloor, mirrorStart })
                {
                    var owner = U.Component(go, ownerClass);
                    if (owner != IntPtr.Zero && U.Read<IntPtr>(owner, "_scrollRect") == scroll) return true;
                }
            }
        }
        return false;
    }

    bool Scroll(IntPtr scroll, IntPtr data)
    {
        if (!enabled.Value || data == IntPtr.Zero || !U.Active(scroll) || !IsMap(scroll)) return false;
        var delta = U.Get<V2>(data, "scrollDelta");
        if (!float.IsFinite(delta.Y) || Math.Abs(delta.Y) < .0001f) return false;
        var content = U.Call(scroll, "get_content");
        if (!U.Alive(content)) return false;
        if (views.TryGetValue(scroll, out var existing) && existing.Content != content) Forget(scroll, true);
        if (!views.TryGetValue(scroll, out var view))
        {
            view = new View { Scroll = scroll, Content = content, Background = FindBackground(scroll, content), OriginalScale = U.Get<V3>(content, "localScale") };
            view.ScrollHandle = IL2CPP.il2cpp_gchandle_new(scroll, false);
            view.ContentHandle = IL2CPP.il2cpp_gchandle_new(content, false);
            var field = IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_object_get_class(scroll), "_useBoundary");
            if (field != IntPtr.Zero) view.Boundary = U.Read<byte>(scroll, "_useBoundary");
            views[scroll] = view;
            Log.LogInfo($"Zoom enabled for {U.Name(scroll)} ({Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(scroll)))}).");
        }
        var parent = U.Parent(content);
        if (!U.Alive(parent)) return false;
        var screen = U.Get<V2>(data, "position");
        var camera = U.Call(data, "get_enterEventCamera");
        if (!LocalPoint(parent, screen, camera, out var anchor)) return true;
        float floor = MinimumScale(view);
        SetScale(view, ZoomMath.Next(view.Factor, delta.Y, step.Value, floor, maximum.Value), anchor);
        U.Call(data, "Use");
        return true;
    }

    void SetScale(View view, float factor, V2 anchor)
    {
        float current = U.Get<V3>(view.Content, "localScale").X;
        float target = view.OriginalScale.X * factor;
        if (!float.IsFinite(current) || current <= 0 || !float.IsFinite(target) || target <= 0) return;
        var position = U.Get<V3>(view.Content, "localPosition");
        var next = ZoomMath.Position(position.X, position.Y, anchor.X, anchor.Y, target / current);
        U.Call(view.Scroll, "StopMovement");
        U.Set(view.Content, "localScale", new V3(target, view.OriginalScale.Y * factor, view.OriginalScale.Z));
        U.Set(view.Content, "localPosition", new V3(next.X, next.Y, position.Z));
        // The story map's saved boundary assumes its original scale.
        if (view.Boundary.HasValue) U.Write(view.Scroll, "_useBoundary", factor == 1 ? view.Boundary.Value : (byte)0);
        view.Factor = factor;
        Constrain(view);
    }

    void Tick(IntPtr scroll)
    {
        if (!views.TryGetValue(scroll, out var view)) return;
        if (!U.Alive(view.Content)) { Forget(scroll, false); return; }
        if (!enabled.Value || !IsMap(scroll)) { Forget(scroll, true); return; }
        float floor = MinimumScale(view);
        if (view.Factor < floor - .0001f) SetScale(view, floor, ViewCenter(view));
        Constrain(view);
        int home = 278;
        void** args = stackalloc void*[1]; args[0] = &home;
        if (*(byte*)IL2CPP.il2cpp_object_unbox(U.Invoke(keyDown, IntPtr.Zero, args)) != 0)
            SetScale(view, 1, ViewCenter(view));
    }

    static bool LocalPoint(IntPtr parent, V2 screen, IntPtr camera, out V2 point)
    {
        V2 local = default;
        void** args = stackalloc void*[4];
        args[0] = (void*)parent; args[1] = &screen; args[2] = (void*)camera; args[3] = &local;
        var method = U.Method(U.Class("RectTransformUtility", "UnityEngine", "UnityEngine.UIModule.dll"), "ScreenPointToLocalPointInRectangle", 4);
        bool success = *(byte*)IL2CPP.il2cpp_object_unbox(U.Invoke(method, IntPtr.Zero, args)) != 0;
        point = local;
        return success;
    }

    void Forget(IntPtr scroll, bool restore)
    {
        if (!views.Remove(scroll, out var view)) return;
        try
        {
            if (restore && U.Alive(view.Content)) SetScale(view, 1, ViewCenter(view));
            if (restore && view.Boundary.HasValue && U.Alive(scroll)) U.Write(scroll, "_useBoundary", view.Boundary.Value);
        }
        finally
        {
            if (view.ScrollHandle != IntPtr.Zero) IL2CPP.il2cpp_gchandle_free(view.ScrollHandle);
            if (view.ContentHandle != IntPtr.Zero) IL2CPP.il2cpp_gchandle_free(view.ContentHandle);
        }
    }

    void InstallFocus()
    {
        var method = U.Method(storyPanel, "StartFocus", 4);
        for (uint index = 1; index < 4; index++)
        {
            var type = IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_method_get_param(method, index));
            if (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(type)) != "Boolean")
                throw new MissingMethodException("Stage-map focus signature changed.");
        }
        FocusAction? original = null;
        FocusAction replacement = (obj, slot, first, second, third, info) =>
        {
            try
            {
                var scroll = U.Read<IntPtr>(obj, "_scrollRect");
                if (views.ContainsKey(scroll))
                {
                    Forget(scroll, true);
                    Log.LogInfo("Restored normal map coordinates before node focus.");
                }
            }
            catch (Exception ex) { Warn(ex); }
            original!(obj, slot, first, second, third, info);
        };
        hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(method), replacement, out original));
        delegates.Add(replacement); delegates.Add(original!);
        Hook(storyPanel, "ResetData", (obj, originalReset) =>
        {
            try { Forget(U.Read<IntPtr>(obj, "_scrollRect"), true); }
            catch (Exception ex) { Warn(ex); }
            originalReset();
        });
    }

    IntPtr FindBackground(IntPtr scroll, IntPtr content)
    {
        var current = U.Transform(scroll);
        for (int depth = 0; depth < 8 && U.Alive(current); depth++, current = U.Parent(current))
        {
            var panel = U.Component(U.GO(current), storyPanel);
            if (panel == IntPtr.Zero || U.Read<IntPtr>(panel, "_scrollRect") != scroll) continue;
            var background = U.Read<IntPtr>(panel, "rect_backGround");
            if (U.Alive(background))
            {
                for (var node = background; U.Alive(node); node = U.Parent(node))
                    if (node == content) return background;
            }
        }
        return content;
    }

    static IntPtr Viewport(View view)
    {
        var viewport = U.Call(view.Scroll, "get_viewport");
        return U.Alive(viewport) ? viewport : U.Transform(view.Scroll);
    }

    static R4 Bounds(IntPtr rect, IntPtr relativeTo)
    {
        var area = U.Get<R4>(rect, "rect");
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        for (int corner = 0; corner < 4; corner++)
        {
            var point = new V3(area.X + ((corner & 1) == 0 ? 0 : area.Width),
                area.Y + ((corner & 2) == 0 ? 0 : area.Height), 0);
            var world = U.Map(rect, "TransformPoint", point);
            var local = U.Map(relativeTo, "InverseTransformPoint", world);
            left = Math.Min(left, local.X); right = Math.Max(right, local.X);
            bottom = Math.Min(bottom, local.Y); top = Math.Max(top, local.Y);
        }
        return new R4(left, bottom, right - left, top - bottom);
    }

    static V2 ViewCenter(View view)
    {
        var viewport = Bounds(Viewport(view), U.Parent(view.Content));
        return new V2(viewport.X + viewport.Width / 2, viewport.Y + viewport.Height / 2);
    }

    float MinimumScale(View view)
    {
        if (!U.Alive(view.Background)) view.Background = view.Content;
        var parent = U.Parent(view.Content);
        var art = Bounds(view.Background, parent);
        var viewport = Bounds(Viewport(view), parent);
        float actual = U.Get<V3>(view.Content, "localScale").X / view.OriginalScale.X;
        float cover = ZoomMath.Cover(viewport.Width, viewport.Height, art.Width / actual, art.Height / actual);
        return Math.Max(minimum.Value, Math.Min(1, cover));
    }

    static void Constrain(View view)
    {
        if (!U.Alive(view.Background)) return;
        var parent = U.Parent(view.Content);
        var art = Bounds(view.Background, parent);
        var viewport = Bounds(Viewport(view), parent);
        float x = ZoomMath.Correction(art.X, art.Width, viewport.X, viewport.Width);
        float y = ZoomMath.Correction(art.Y, art.Height, viewport.Y, viewport.Height);
        if (Math.Abs(x) < .01f && Math.Abs(y) < .01f) return;
        var position = U.Get<V3>(view.Content, "localPosition");
        U.Call(view.Scroll, "StopMovement");
        U.Set(view.Content, "localPosition", new V3(position.X + x, position.Y + y, position.Z));
    }

    void Warn(Exception ex)
    {
        if (errors++ < 5) Log.LogWarning("Map zoom skipped: " + ex);
    }

    public override bool Unload()
    {
        foreach (var scroll in views.Keys.ToArray())
            try { Forget(scroll, true); } catch (Exception ex) { Warn(ex); }
        foreach (var hook in hooks.AsEnumerable().Reverse()) hook.Dispose();
        hooks.Clear(); delegates.Clear(); hooked.Clear();
        return true;
    }
}




