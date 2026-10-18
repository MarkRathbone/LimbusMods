using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace RetainAutoplay;
[BepInPlugin("local.limbus.retainautoplay", "Limbus Autodialogue", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    readonly List<INativeDetour> hooks = new();
    readonly List<Delegate> delegates = new();
    StoryPreferences? preferences;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstanceAction(IntPtr obj, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ObjectAction(IntPtr obj, IntPtr arg, IntPtr method);
    public override void Load() { preferences = new StoryPreferences(this); preferences.Install(); Log.LogInfo("Limbus Autodialogue ready."); }
    internal void HookInstance(IntPtr klass, string method, Action<IntPtr, Action> action, bool featureHook = false)
    {
        InstanceAction? original = null;
        InstanceAction replacement = (obj, info) => action(obj, () => original!(obj, info));
        hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass, method)), replacement, out original));
        delegates.Add(replacement); delegates.Add(original!);
    }
    internal void HookObjectAction(IntPtr klass, string method, Action<IntPtr, IntPtr> after)
    {
        ObjectAction? original = null;
        ObjectAction replacement = (obj, arg, info) => { original!(obj, arg, info); after(obj, arg); };
        hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass, method, 1)), replacement, out original));
        delegates.Add(replacement); delegates.Add(original!);
    }
    internal void LogMessage(string message, bool warning = false) { if(warning) Log.LogWarning(message); else Log.LogInfo(message); }
    public override bool Unload() { foreach(var hook in hooks.AsEnumerable().Reverse()) hook.Dispose(); hooks.Clear(); delegates.Clear(); return true; }
}
