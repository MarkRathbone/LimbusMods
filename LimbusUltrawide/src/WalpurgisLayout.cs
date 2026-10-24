using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed unsafe class WalpurgisLayout : IDisposable
{
    readonly Dictionary<int, EventScreen> screens = new();

    public void Apply(CanvasState canvas, Plugin plugin, bool enabled)
    {
        if (plugin.Discover) Find(canvas.Root, 7);
        foreach (var pair in screens.ToArray())
        {
            if (!U.Alive(pair.Value.Root)) { pair.Value.Dispose(); screens.Remove(pair.Key); continue; }
            if (pair.Value.CanvasId == canvas.Id) pair.Value.Apply(canvas.Root, enabled);
        }
        void Find(IntPtr rect, int depth)
        {
            if (depth < 0 || !U.Alive(rect) || !U.Active(rect)) return;
            if (LayoutMath.IsWalpurgisPanel(U.Name(rect)))
            {
                int id = U.Id(rect);
                if (!screens.ContainsKey(id)) screens[id] = new EventScreen(rect, canvas.Id);
                return;
            }
            foreach (var child in U.Children(rect).ToArray()) Find(child, depth - 1);
        }
    }

    public void Dispose()
    {
        foreach (var screen in screens.Values) screen.Dispose();
        screens.Clear();
    }

    sealed class EventScreen : IDisposable
    {
        readonly nint root, background;
        readonly nint[] edges = new nint[2];
        readonly ScreenGraphic? backing;
        readonly SavedRect? inputBlocker;
        public int CanvasId { get; }
        public IntPtr Root => IL2CPP.il2cpp_gchandle_get_target(root);
        IntPtr Background => background == 0 ? IntPtr.Zero : IL2CPP.il2cpp_gchandle_get_target(background);

        public EventScreen(IntPtr parent, int canvasId)
        {
            CanvasId = canvasId;
            root = IL2CPP.il2cpp_gchandle_new(parent, false);
            var imageClass = U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll");
            var panelImage = U.Component(U.GO(parent), imageClass);
            if (panelImage != IntPtr.Zero) backing = new ScreenGraphic(panelImage);
            var blocker = U.Find(parent, "[Image]InputBlocker", 4);
            if (blocker != IntPtr.Zero) inputBlocker = new SavedRect(blocker);
            var source = U.Find(parent, "[Image]Bg_Slot", 1);
            if (source == IntPtr.Zero) source = U.Find(parent, "[Image]Bg", 1);
            if (source != IntPtr.Zero)
            {
                var graphic = U.Component(U.GO(source), imageClass);
                if (graphic != IntPtr.Zero)
                {
                    background = IL2CPP.il2cpp_gchandle_new(graphic, false);
                    for (int side = 0; side < 2; side++)
                    {
                        var go = U.NewGO("Ultrawide Walpurgis background edge " + side, parent);
                        var image = U.Component(go, imageClass, true);
                        U.Set(image, "raycastTarget", (byte)0);
                        U.Set(U.Transform(image), "anchorMin", new V2(.5f, .5f));
                        U.Set(U.Transform(image), "anchorMax", new V2(.5f, .5f));
                        U.Set(U.Transform(image), "pivot", new V2(.5f, .5f));
                        U.Call(U.Transform(image), "SetAsFirstSibling");
                        edges[side] = IL2CPP.il2cpp_gchandle_new(image, false);
                    }
                }
            }
            Plugin.Instance.LogMessage("Native Walpurgisnacht composition: " + U.Name(parent));
        }

        public void Apply(IntPtr canvas, bool enabled)
        {
            if (!U.Active(Root)) return;
            backing?.Apply(canvas, enabled);
            if (inputBlocker != null)
            {
                if (enabled) inputBlocker.Viewport(canvas); else inputBlocker.Restore();
            }
            if (Background == IntPtr.Zero || !U.Alive(Background)) return;
            var source = Background;
            var sourceRect = U.Transform(source);
            var sprite = U.Call(source, "get_sprite");
            bool visible = enabled && U.Active(source) && sprite != IntPtr.Zero && U.Get<byte>(source, "enabled") != 0;
            var rect = U.Get<R4>(sourceRect, "rect");
            var scale = U.Get<V3>(sourceRect, "localScale");
            var pivot = U.Get<V2>(sourceRect, "pivot");
            var position = U.Get<V3>(sourceRect, "localPosition");
            float aspect = 1;
            if (sprite != IntPtr.Zero)
            {
                var pixels = U.Get<R4>(sprite, "rect");
                if (pixels.Height > 0) aspect = pixels.Width / pixels.Height;
            }
            bool preserve = U.Get<byte>(source, "preserveAspect") != 0;
            var drawing = LayoutMath.SpriteDrawingSize(rect.Width, rect.Height, aspect, preserve);
            float width = drawing.Width * Math.Abs(scale.X);
            var viewport = U.Get<R4>(canvas, "rect");
            var left = U.Map(Root, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(viewport.X, 0, 0)));
            var right = U.Map(Root, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(viewport.X + viewport.Width, 0, 0)));
            visible &= float.IsFinite(width) && width > 0 && Math.Abs(right.X - left.X) > width + .1f;
            for (int side = 0; side < 2; side++)
            {
                if (edges[side] == 0) continue;
                var edge = IL2CPP.il2cpp_gchandle_get_target(edges[side]);
                U.ActiveSelf(U.GO(edge), visible);
                if (!visible) continue;
                U.ObjectArg(edge, "set_sprite", sprite);
                U.ObjectArg(edge, "set_material", U.Call(source, "get_material"));
                U.Set(edge, "color", U.Get<Color>(source, "color"));
                U.Set(edge, "preserveAspect", U.Get<byte>(source, "preserveAspect"));
                U.Set(edge, "raycastTarget", (byte)0);
                var target = U.Transform(edge);
                U.Set(target, "sizeDelta", new V2(rect.Width, rect.Height));
                U.Set(target, "localScale", new V3(-scale.X, scale.Y, scale.Z));
                float centerX = position.X + (.5f - pivot.X) * rect.Width * scale.X;
                float centerY = position.Y + (.5f - pivot.Y) * rect.Height * scale.Y;
                U.Set(target, "localPosition", new V3(centerX + (side == 0 ? -width : width), centerY, position.Z));
            }
        }

        public void Dispose()
        {
            if (backing != null && U.Alive(backing.Source)) backing.Apply(Root, false);
            backing?.Dispose();
            if (inputBlocker != null && U.Alive(inputBlocker.Object)) inputBlocker.Restore();
            inputBlocker?.Dispose();
            void** args = stackalloc void*[1];
            foreach (var handle in edges)
            {
                if (handle == 0) continue;
                var image = IL2CPP.il2cpp_gchandle_get_target(handle);
                if (U.Alive(image))
                {
                    var go = U.GO(image);
                    U.ActiveSelf(go, false);
                    args[0] = (void*)go;
                    U.Invoke(U.Method(U.Class("Object"), "Destroy", 1), IntPtr.Zero, args);
                }
                IL2CPP.il2cpp_gchandle_free(handle);
            }
            if (background != 0) IL2CPP.il2cpp_gchandle_free(background);
            IL2CPP.il2cpp_gchandle_free(root);
        }
    }
}


