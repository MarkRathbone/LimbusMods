using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace UltrawideLimbus;

internal sealed unsafe class InspectionBuffs : IDisposable
{
    readonly IntPtr rootHandle;
    IntPtr originalHandle, expandedHandle;
    bool expanded, busy, failed;
    public IntPtr Root => IL2CPP.il2cpp_gchandle_get_target(rootHandle);
    public InspectionBuffs(IntPtr root) => rootHandle = IL2CPP.il2cpp_gchandle_new(root, false);

    static IntPtr Element(IntPtr array, int index)
    {
        if ((uint)index >= IL2CPP.il2cpp_array_length(array)) throw new IndexOutOfRangeException();
        return array + sizeof(Il2CppObject) + 2 * IntPtr.Size + index * IntPtr.Size;
    }
    static IntPtr Item(IntPtr array, int index) => Marshal.ReadIntPtr(Element(array, index));
    static void SetItem(IntPtr array, int index, IntPtr value)
    {
        IL2CPP.il2cpp_gc_wbarrier_set_field(array, Element(array, index), value);
    }
    static void Reference(IntPtr obj, string field, IntPtr value)
        => IL2CPP.il2cpp_field_set_value_object(obj, U.Field(IL2CPP.il2cpp_object_get_class(obj), field), value);
    internal static void ValidateArrayInterop()
    {
        var text = IL2CPP.ManagedStringToIl2Cpp("Ultrawide Limbus status row");
        var array = IL2CPP.il2cpp_array_new(IL2CPP.il2cpp_object_get_class(text), 10);
        var handle = IL2CPP.il2cpp_gchandle_new(array, false);
        try
        {
            SetItem(array, 9, text);
            if (IL2CPP.il2cpp_array_length(array) != 10 || Item(array, 9) != text || Item(array, 0) != IntPtr.Zero)
                throw new InvalidOperationException("IL2CPP reference-array interop check failed.");
            SetItem(array, 9, IntPtr.Zero);
        }
        finally { IL2CPP.il2cpp_gchandle_free(handle); }
        Plugin.Instance.LogMessage("Status-row reference-array interop check passed.");
    }
    void Refresh(IntPtr manager)
    {
        var direction = U.Read<IntPtr>(manager, "_uiDirection");
        var data = U.Read<IntPtr>(manager, "_currentBuffInfoList");
        void** args = stackalloc void*[2]; args[0] = (void*)direction; args[1] = (void*)data;
        U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(manager), "SetBufDisplay", 2), manager, args);
    }
    public void Apply(bool enable)
    {
        if (busy || failed) return;
        busy = true;
        try { ApplyReady(enable); }
        catch (Exception ex)
        {
            failed = true;
            Plugin.Instance.LogMessage("Status-row expansion stopped: " + ex, true);
        }
        finally { busy = false; }
    }
    void ApplyReady(bool enable)
    {
        var manager = U.Component(U.GO(Root), U.Class("BuffUIManager", "", "Assembly-CSharp.dll"));
        if (manager == IntPtr.Zero) return;
        if (U.Read<IntPtr>(manager, "_currentBuffInfoList") == IntPtr.Zero ||
            U.Read<IntPtr>(manager, "_uiDirection") == IntPtr.Zero) return;
        if (enable && expandedHandle == IntPtr.Zero)
        {
            var original = U.Read<IntPtr>(manager, "_buffUIList");
            if (original == IntPtr.Zero || IL2CPP.il2cpp_array_length(original) != 5) return;
            var template = Item(original, 0);
            if (!U.Alive(template) || U.Read<IntPtr>(template, "_openBuffPopup") == IntPtr.Zero) return;
            Plugin.Instance.LogMessage("Status row: preparing ten initialized inspection icons.");
            originalHandle = IL2CPP.il2cpp_gchandle_new(original, false);
            var array = IL2CPP.il2cpp_array_new_specific(IL2CPP.il2cpp_object_get_class(original), 10);
            var pendingHandle = IL2CPP.il2cpp_gchandle_new(array, false);
            try
            {
            for (int i = 0; i < 10; i++)
            {
                var icon = i < 5 ? Item(original, i) : Clone(template);
                if (i >= 5) U.ValueArg(U.Transform(icon), "SetSiblingIndex", i + 1);
                SetItem(array, i, icon);
            }
                expandedHandle = pendingHandle;
                Plugin.Instance.LogMessage("Status row: ten inspection slots prepared.");
            }
            catch { IL2CPP.il2cpp_gchandle_free(pendingHandle); throw; }
        }
        if (expanded != enable && expandedHandle != IntPtr.Zero)
        {
            Reference(manager, "_buffUIList", IL2CPP.il2cpp_gchandle_get_target(enable ? expandedHandle : originalHandle));
            if (!enable)
                for (int i = 5; i < 10; i++) U.ActiveSelf(U.GO(Item(IL2CPP.il2cpp_gchandle_get_target(expandedHandle), i)), false);
            try { Refresh(manager); expanded = enable; }
            catch
            {
                Reference(manager, "_buffUIList", IL2CPP.il2cpp_gchandle_get_target(originalHandle));
                for (int i = 5; i < 10; i++) U.ActiveSelf(U.GO(Item(IL2CPP.il2cpp_gchandle_get_target(expandedHandle), i)), false);
                expanded = false;
                throw;
            }
            Plugin.Instance.LogMessage("Status row: " + (enable ? "ten" : "five") + " slots active.");
        }
        if (!expanded) return;
        var list = U.Read<IntPtr>(manager, "_currentBuffInfoList");
        int count = list == IntPtr.Zero ? 0 : U.Get<int>(list, "Count");
        var popup = U.Read<IntPtr>(manager, "btn_buffPopup");
        U.ActiveSelf(U.GO(popup), count > 10);
        if (count > 10) U.Text(U.Read<IntPtr>(popup, "tmp_additionalBuffCount"), "+" + (count - 10));
    }
    static IntPtr Clone(IntPtr template)
    {
        var go = U.GO(template);
        void** args = stackalloc void*[1]; args[0] = (void*)go;
        var clone = U.Invoke(U.MethodTyped(U.Class("Object"), "Instantiate", "Object"), IntPtr.Zero, args);
        var transform = U.Transform(clone);
        var parent = U.Parent(U.Transform(template));
        byte world = 0;
        void** parentArgs = stackalloc void*[2]; parentArgs[0] = (void*)parent; parentArgs[1] = &world;
        U.Invoke(U.Method(U.Class("Transform"), "SetParent", 2), transform, parentArgs);
        U.Set(transform, "localScale", U.Get<V3>(U.Transform(template), "localScale"));
        var icon = U.Component(clone, IL2CPP.il2cpp_object_get_class(template));
        Reference(icon, "_buffDetail", U.Read<IntPtr>(template, "_buffDetail"));
        Reference(icon, "_openBuffPopup", U.Read<IntPtr>(template, "_openBuffPopup"));
        U.ActiveSelf(clone, false);
        return icon;
    }
    public void Dispose()
    {
        if (originalHandle != IntPtr.Zero) IL2CPP.il2cpp_gchandle_free(originalHandle);
        if (expandedHandle != IntPtr.Zero) IL2CPP.il2cpp_gchandle_free(expandedHandle);
        IL2CPP.il2cpp_gchandle_free(rootHandle);
    }
}
