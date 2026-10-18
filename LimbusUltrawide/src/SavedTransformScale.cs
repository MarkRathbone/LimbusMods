using Il2CppInterop.Runtime;
namespace UltrawideLimbus;

internal sealed class SavedTransformScale : IDisposable
{
    readonly nint handle;
    V3 authored, last;
    bool applied;
    public IntPtr Object => IL2CPP.il2cpp_gchandle_get_target(handle);
    public SavedTransformScale(IntPtr transform)
    {
        handle = IL2CPP.il2cpp_gchandle_new(transform, false);
        authored = last = U.Get<V3>(transform, "localScale");
    }
    public void Apply(float horizontal, float vertical)
    {
        var current = U.Get<V3>(Object, "localScale");
        if (!applied || current != last) authored = current;
        last = new V3(authored.X * horizontal, authored.Y * vertical, authored.Z);
        if (current != last) U.Set(Object, "localScale", last);
        applied = true;
    }
    public void Restore()
    {
        if (!applied) return;
        if (U.Get<V3>(Object, "localScale") == last) U.Set(Object, "localScale", authored);
        applied = false;
    }
    public void Dispose() => IL2CPP.il2cpp_gchandle_free(handle);
}
