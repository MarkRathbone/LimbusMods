using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;

namespace UltrawideLimbus;

internal sealed class ExtractionLayout : IDisposable
{
    readonly Dictionary<int, Scene> scenes = new();
    static IntPtr controllerClass;

    public static void ValidateController()
    {
        var klass = ControllerClass();
        foreach (var name in new[] { "_effectChainCamera", "img_space" })
            if (IL2CPP.il2cpp_class_get_field_from_name(klass, name) == IntPtr.Zero)
                throw new MissingFieldException("GachaResultUI", name);
    }

    static IntPtr ControllerClass()
    {
        if (controllerClass != IntPtr.Zero) return controllerClass;
        var known = U.Class("BattleCamManager", "", "Assembly-CSharp.dll");
        var image = IL2CPP.il2cpp_class_get_image(known);
        IntPtr found = IntPtr.Zero;
        for (uint i = 0; i < IL2CPP.il2cpp_image_get_class_count(image); i++)
        {
            var candidate = IL2CPP.il2cpp_image_get_class(image, i);
            if (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(candidate)) != "GachaResultUI") continue;
            if (found != IntPtr.Zero) throw new InvalidOperationException("Multiple extraction controller types.");
            found = candidate;
        }
        if (found == IntPtr.Zero) throw new MissingMemberException("Extraction controller GachaResultUI");
        controllerClass = found;
        Plugin.Instance.LogMessage("Extraction controller resolved: " + Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(found)) + ".GachaResultUI");
        return found;
    }


    public void Apply(CanvasState canvas, Plugin plugin, bool enabled)
    {
        if (enabled && (plugin.Discover || scenes.Count == 0)) Find(canvas.Root, 4);
        foreach (var pair in scenes.ToArray())
        {
            if (!U.Alive(pair.Value.Root)) { pair.Value.Dispose(); scenes.Remove(pair.Key); continue; }
            if (pair.Value.CanvasId == canvas.Id) pair.Value.Apply(canvas.Root, enabled);
        }
        void Find(IntPtr rect, int depth)
        {
            if (depth < 0 || !U.Alive(rect) || !U.Active(rect)) return;
            if (U.Name(rect) == "GachaResultUI(Clone)")
            {
                int id = U.Id(rect);
                if (!scenes.ContainsKey(id) && Scene.Ready(rect)) scenes[id] = new Scene(rect, canvas.Id);
                return;
            }
            foreach (var child in U.Children(rect)) Find(child, depth - 1);
        }
    }

    public void Dispose()
    {
        foreach (var scene in scenes.Values) scene.Dispose();
        scenes.Clear();
    }

    sealed unsafe class Scene : IDisposable
    {
        readonly nint root, camera, image, fitter;
        readonly SavedRect background, backdrop;
        readonly nint backdropHandle;
        readonly float originalAspect;
        readonly byte fitterEnabled;
        bool applied, logged;
        public int CanvasId { get; }
        public IntPtr Root => IL2CPP.il2cpp_gchandle_get_target(root);
        IntPtr Camera => IL2CPP.il2cpp_gchandle_get_target(camera);
        IntPtr Image => IL2CPP.il2cpp_gchandle_get_target(image);
        IntPtr Fitter => fitter == 0 ? IntPtr.Zero : IL2CPP.il2cpp_gchandle_get_target(fitter);

        public static bool Ready(IntPtr rect)
        {
            var ui = U.Component(U.GO(rect), ControllerClass());
            if (ui == IntPtr.Zero) return false;
            var cam = U.Read<IntPtr>(ui, "_effectChainCamera");
            var raw = U.Read<IntPtr>(ui, "img_space");
            return U.Alive(cam) && U.Alive(raw) && U.Alive(U.Call(cam, "get_targetTexture")) && U.Alive(U.Call(raw, "get_texture"));
        }
        public Scene(IntPtr rect, int canvasId)
        {
            var ui = U.Component(U.GO(rect), ControllerClass());
            var cam = U.Read<IntPtr>(ui, "_effectChainCamera");
            var raw = U.Read<IntPtr>(ui, "img_space");
            if (!U.Alive(cam) || !U.Alive(raw)) throw new InvalidOperationException("Extraction camera/background unavailable.");
            var target = U.Call(cam, "get_targetTexture");
            var texture = U.Call(raw, "get_texture");
            if (!U.Alive(target) || !U.Alive(texture) || U.Id(target) != U.Id(texture))
                throw new InvalidOperationException("Extraction background no longer uses the chain camera texture.");
            originalAspect = U.Get<float>(cam, "aspect");
            if (!float.IsFinite(originalAspect) || originalAspect <= 0) throw new InvalidOperationException("Invalid extraction camera aspect.");
            var aspectFitter = U.Component(U.GO(raw), U.Class("AspectRatioFitter", "UnityEngine.UI", "UnityEngine.UI.dll"));
            background = new SavedRect(U.Transform(raw));
            root = IL2CPP.il2cpp_gchandle_new(rect, false);
            camera = IL2CPP.il2cpp_gchandle_new(cam, false);
            image = IL2CPP.il2cpp_gchandle_new(raw, false);
            if (aspectFitter != IntPtr.Zero)
            {
                fitter = IL2CPP.il2cpp_gchandle_new(aspectFitter, false);
                fitterEnabled = U.Get<byte>(aspectFitter, "enabled");
            }
            var backing = U.NewGO("Ultrawide extraction backdrop", rect);
            U.Set(backing, "layer", U.Get<int>(U.GO(rect), "layer"));
            var backingImage = U.Component(backing, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
            U.Set(backingImage, "color", new Color(0, 0, 0, 1));
            U.Set(backingImage, "raycastTarget", (byte)1);
            var group = U.Component(backing, U.Class("CanvasGroup", "UnityEngine", "UnityEngine.UIModule.dll"), true);
            U.Set(group, "alpha", 1f); U.Set(group, "ignoreParentGroups", (byte)1);
            U.Set(group, "blocksRaycasts", (byte)1);
            U.Call(U.Transform(backing), "SetAsFirstSibling");
            backdrop = new SavedRect(U.Transform(backing));
            backdropHandle = IL2CPP.il2cpp_gchandle_new(backing, false);
            U.ActiveSelf(backing, false);
            CanvasId = canvasId;
        }

        public void Apply(IntPtr canvas, bool enabled)
        {
            if (!enabled || !U.Active(Root) || !U.Alive(Camera) || !U.Alive(Image)) { Restore(); return; }
            var bounds = U.Get<R4>(canvas, "rect");
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width / bounds.Height <= 16f / 9f + .001f) { Restore(); return; }
            U.ActiveSelf(IL2CPP.il2cpp_gchandle_get_target(backdropHandle), true);
            backdrop.Viewport(canvas);
            var view = LayoutMath.ExtractionView(bounds.Width, bounds.Height, originalAspect);
            if (U.Alive(Fitter) && U.Get<byte>(Fitter, "enabled") != 0) U.Set(Fitter, "enabled", (byte)0);
            background.Viewport(canvas);
            var rect = U.Transform(Image);
            var viewportHeight = U.Get<R4>(rect, "rect").Height;
            background.Height(viewportHeight * view.Height / bounds.Height);
            if (Math.Abs(U.Get<float>(Camera, "aspect") - view.Aspect) > .00001f) U.Set(Camera, "aspect", view.Aspect);
            applied = true;
            if (!logged)
            {
                Plugin.Instance.LogMessage($"Extraction framing: {bounds.Width} x {view.Height}; camera aspect {originalAspect} -> {view.Aspect}; native vertical framing retained; opaque viewport backing active.");
                logged = true;
            }
        }

        void Restore()
        {
            var backing = IL2CPP.il2cpp_gchandle_get_target(backdropHandle);
            if (U.Alive(backing)) U.ActiveSelf(backing, false);
            if (!applied) return;
            if (U.Alive(Camera)) U.Set(Camera, "aspect", originalAspect);
            if (U.Alive(background.Object)) background.Restore();
            if (U.Alive(Fitter)) U.Set(Fitter, "enabled", fitterEnabled);
            applied = false;
        }

        public void Dispose()
        {
            Restore(); background.Dispose(); backdrop.Dispose();
            var backing = IL2CPP.il2cpp_gchandle_get_target(backdropHandle);
            if (U.Alive(backing))
            {
                void** args = stackalloc void*[1]; args[0] = (void*)backing;
                U.Invoke(U.Method(U.Class("Object"), "Destroy", 1), IntPtr.Zero, args);
            }
            IL2CPP.il2cpp_gchandle_free(backdropHandle);
            foreach (var handle in new[] { root, camera, image, fitter })
                if (handle != 0) IL2CPP.il2cpp_gchandle_free(handle);
        }
    }
}
