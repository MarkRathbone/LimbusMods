using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed class ArtworkCurtains
{
    readonly Dictionary<int, Curtain> curtains = new();
    public void Apply(CanvasState canvas, Plugin p)
    {
        if (p.Discover) Find(canvas.Root, 8);
        foreach (var pair in curtains.ToArray())
        {
            if (!U.Alive(pair.Value.Root)) { pair.Value.Dispose(); curtains.Remove(pair.Key); continue; }
            if (pair.Value.CanvasId == canvas.Id)
                pair.Value.Apply(canvas.Root, p.ActiveLayout && p.ArtworkConstrain.Value, p.MenuAspect.Value);
        }
        void Find(IntPtr root, int depth)
        {
            if (depth < 0 || !U.Active(root)) return;
            if (U.Name(root) == "[Script]UnitInformationController_Renewal")
            {
                int id = U.Id(root);
                if (!curtains.ContainsKey(id)) curtains[id] = new Curtain(root, canvas.Id);
                return;
            }
            foreach (var child in U.Children(root).ToArray()) Find(child, depth - 1);
        }
    }
    sealed class Curtain : IDisposable
    {
        readonly nint root;
        readonly nint[] sides = new nint[2];
        public int CanvasId { get; }
        public IntPtr Root => IL2CPP.il2cpp_gchandle_get_target(root);
        public Curtain(IntPtr parent, int canvasId)
        {
            CanvasId = canvasId;
            root = IL2CPP.il2cpp_gchandle_new(parent, false);
            for (int i = 0; i < 2; i++)
            {
                var go = U.NewGO("Ultrawide artwork side cover " + i, parent);
                var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
                U.Set(image, "color", new Color(0, 0, 0, 1)); U.Set(image, "raycastTarget", (byte)0);
                var rect = U.Transform(image);
                U.Set(rect, "anchorMin", new V2(.5f, .5f)); U.Set(rect, "anchorMax", new V2(.5f, .5f));
                U.Set(rect, "pivot", new V2(.5f, .5f));
                sides[i] = IL2CPP.il2cpp_gchandle_new(rect, false);
            }
            Plugin.Instance.LogMessage("Default artwork framing with opaque content-area side covers.");
        }
        public void Apply(IntPtr canvas, bool enabled, float aspect)
        {
            if (!U.Active(Root)) return;
            var viewport = U.Get<R4>(canvas, "rect");
            float contentWidth = LayoutMath.ContentWidth(viewport.Width, viewport.Height, aspect);
            float margin = (viewport.Width - contentWidth) / 2;
            for (int i = 0; i < 2; i++)
            {
                var rect = IL2CPP.il2cpp_gchandle_get_target(sides[i]);
                U.ActiveSelf(U.GO(rect), enabled && margin > .01f);
                if (!enabled || margin <= .01f) continue;
                float x = i == 0 ? viewport.X : viewport.X + viewport.Width - margin;
                var low = U.Map(Root, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(x, viewport.Y, 0)));
                var high = U.Map(Root, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(x + margin, viewport.Y + viewport.Height, 0)));
                var size = new V2(Math.Abs(high.X - low.X), Math.Abs(high.Y - low.Y));
                var position = new V3((low.X + high.X) / 2, (low.Y + high.Y) / 2, low.Z);
                if (U.Get<V2>(rect, "sizeDelta") != size) U.Set(rect, "sizeDelta", size);
                if (U.Get<V3>(rect, "localPosition") != position) U.Set(rect, "localPosition", position);
                if (Plugin.Instance.Discover) U.Call(rect, "SetAsLastSibling");
            }
        }
        public void Dispose()
        {
            IL2CPP.il2cpp_gchandle_free(root);
            foreach (var side in sides) IL2CPP.il2cpp_gchandle_free(side);
        }
    }
}
