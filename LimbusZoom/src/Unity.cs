using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;

namespace LimbusZoom;

[StructLayout(LayoutKind.Sequential)]
internal record struct V2(float X, float Y)
{
    public static V2 operator /(V2 v, float scale) => new(v.X / scale, v.Y / scale);
}
[StructLayout(LayoutKind.Sequential)]
internal record struct V3(float X, float Y, float Z);
[StructLayout(LayoutKind.Sequential)]
internal record struct R4(float X, float Y, float Width, float Height);
[StructLayout(LayoutKind.Sequential)]
internal record struct Color(float R, float G, float B, float A);

internal static unsafe class U
{
    static readonly Dictionary<(IntPtr, string, int), IntPtr> methods = new();
    static readonly Dictionary<string, IntPtr> classes = new();
    public static IntPtr Class(string name, string ns = "UnityEngine", string assembly = "UnityEngine.CoreModule.dll")
    {
        var key = assembly + ":" + ns + ":" + name;
        if (!classes.TryGetValue(key, out var value))
        {
            value = IL2CPP.GetIl2CppClass(assembly, ns, name);
            if (value == IntPtr.Zero) throw new MissingMemberException(key);
            classes[key] = value;
        }
        return value;
    }
    public static IntPtr Method(IntPtr klass, string name, int count = 0)
    {
        var key = (klass, name, count);
        if (methods.TryGetValue(key, out var method)) return method;
        for (var c = klass; c != IntPtr.Zero; c = IL2CPP.il2cpp_class_get_parent(c))
        {
            method = IL2CPP.il2cpp_class_get_method_from_name(c, name, count);
            if (method != IntPtr.Zero) { methods[key] = method; return method; }
        }
        throw new MissingMethodException(name);
    }
    public static IntPtr Invoke(IntPtr method, IntPtr obj, void** args = null)
    {
        var exception = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, obj, args, ref exception);
        if (exception != IntPtr.Zero) throw new InvalidOperationException($"Unity invocation failed: {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method))}");
        return result;
    }
    public static IntPtr Call(IntPtr obj, string method) => Invoke(Method(IL2CPP.il2cpp_object_get_class(obj), method), obj);
    public static T Get<T>(IntPtr obj, string property) where T : unmanaged
        => *(T*)IL2CPP.il2cpp_object_unbox(Call(obj, "get_" + property));
    public static void Set<T>(IntPtr obj, string property, T value) where T : unmanaged
    {
        void** args = stackalloc void*[1]; args[0] = &value;
        Invoke(Method(IL2CPP.il2cpp_object_get_class(obj), "set_" + property, 1), obj, args);
    }
    public static T Map<T>(IntPtr obj, string method, T value) where T : unmanaged
    {
        void** args = stackalloc void*[1]; args[0] = &value;
        var result = Invoke(Method(IL2CPP.il2cpp_object_get_class(obj), method, 1), obj, args);
        return *(T*)IL2CPP.il2cpp_object_unbox(result);
    }
    public static void ObjectArg(IntPtr obj, string method, IntPtr arg)
    {
        void** args = stackalloc void*[1]; args[0] = (void*)arg;
        Invoke(Method(IL2CPP.il2cpp_object_get_class(obj), method, 1), obj, args);
    }
    public static void ValueArg<T>(IntPtr obj, string method, T value) where T : unmanaged
    {
        void** args = stackalloc void*[1]; args[0] = &value;
        Invoke(Method(IL2CPP.il2cpp_object_get_class(obj), method, 1), obj, args);
    }
    public static void Text(IntPtr obj, string value) => ObjectArg(obj, "set_text", IL2CPP.ManagedStringToIl2Cpp(value));
    public static void ActiveSelf(IntPtr go, bool active)
    {
        byte value = (byte)(active ? 1 : 0);
        if (Get<byte>(go, "activeSelf") == value) return;
        void** args = stackalloc void*[1]; args[0] = &value;
        Invoke(Method(Class("GameObject"), "SetActive", 1), go, args);
    }
    public static string Name(IntPtr obj) => Alive(obj) ? IL2CPP.Il2CppStringToManaged(Call(obj, "get_name")) ?? "" : "";
    public static IntPtr GO(IntPtr component) => Call(component, "get_gameObject");
    public static IntPtr Transform(IntPtr component) => Call(component, "get_transform");
    public static IntPtr Parent(IntPtr transform) => Call(transform, "get_parent");
    public static bool Active(IntPtr component) => Get<byte>(GO(component), "activeInHierarchy") != 0;
    public static bool Alive(IntPtr obj)
    {
        void** args = stackalloc void*[1]; args[0] = (void*)obj;
        var result = Invoke(Method(Class("Object"), "op_Implicit", 1), IntPtr.Zero, args);
        return *(byte*)IL2CPP.il2cpp_object_unbox(result) != 0;
    }
    public static int Id(IntPtr obj) => *(int*)IL2CPP.il2cpp_object_unbox(Call(obj, "GetInstanceID"));
    public static IEnumerable<IntPtr> Children(IntPtr parent)
    {
        if (!Alive(parent)) yield break;
        int count = Get<int>(parent, "childCount");
        for (int i = 0; i < count; i++) yield return Child(parent, i);
    }
    static IntPtr Child(IntPtr parent, int index)
    {
        void** args = stackalloc void*[1]; args[0] = &index;
        return Invoke(Method(Class("Transform"), "GetChild", 1), parent, args);
    }
    public static IntPtr Find(IntPtr root, string name, int depth = 6)
    {
        if (!Alive(root)) return IntPtr.Zero;
        if (Name(root) == name) return root;
        if (depth <= 0) return IntPtr.Zero;
        foreach (var child in Children(root))
        {
            var found = Find(child, name, depth - 1);
            if (found != IntPtr.Zero) return found;
        }
        return IntPtr.Zero;
    }
    public static IntPtr Component(IntPtr go, IntPtr klass, bool add = false)
    {
        var type = IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(klass));
        void** args = stackalloc void*[1]; args[0] = (void*)type;
        return Invoke(MethodTyped(Class("GameObject"), add ? "AddComponent" : "GetComponent", "Type"), go, args);
    }
    public static IntPtr MethodTyped(IntPtr klass, string name, string parameterClass)
    {
        IntPtr iter = IntPtr.Zero, method;
        while ((method = IL2CPP.il2cpp_class_get_methods(klass, ref iter)) != IntPtr.Zero)
        {
            if (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method)) != name || IL2CPP.il2cpp_method_get_param_count(method) != 1) continue;
            var type = IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_method_get_param(method, 0));
            if (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(type)) == parameterClass) return method;
        }
        throw new MissingMethodException(name + "(" + parameterClass + ")");
    }
    public static IntPtr NewGO(string name, IntPtr parent)
    {
        var klass = Class("GameObject");
        var go = IL2CPP.il2cpp_object_new(klass);
        var text = IL2CPP.ManagedStringToIl2Cpp(name);
        void** args = stackalloc void*[1]; args[0] = (void*)text;
        Invoke(Method(klass, ".ctor", 1), go, args);
        var rect = Component(go, Class("RectTransform"), true);
        byte worldPositionStays = 0;
        void** parentArgs = stackalloc void*[2]; parentArgs[0] = (void*)parent; parentArgs[1] = &worldPositionStays;
        Invoke(Method(Class("Transform"), "SetParent", 2), rect, parentArgs);
        Set(rect, "localScale", new V3(1, 1, 1));
        return go;
    }
    public static IntPtr Field(IntPtr klass, string name)
    {
        var f = IL2CPP.il2cpp_class_get_field_from_name(klass, name);
        if (f == IntPtr.Zero) throw new MissingFieldException(name);
        return f;
    }
    public static T Read<T>(IntPtr obj, string name) where T : unmanaged
    {
        T value = default;
        IL2CPP.il2cpp_field_get_value(obj, Field(IL2CPP.il2cpp_object_get_class(obj), name), &value);
        return value;
    }
    public static void Write<T>(IntPtr obj, string name, T value) where T : unmanaged
        => IL2CPP.il2cpp_field_set_value(obj, Field(IL2CPP.il2cpp_object_get_class(obj), name), &value);
}
