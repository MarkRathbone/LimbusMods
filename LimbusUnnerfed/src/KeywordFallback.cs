using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;

internal sealed unsafe class KeywordFallback : IDisposable
{
 readonly INativeDetour hook;
 readonly List<Delegate> delegates=new();
 readonly Dictionary<(IntPtr,IntPtr),IntPtr> cached=new();
 readonly List<IntPtr> handles=new();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetData(IntPtr obj,IntPtr id,IntPtr method);
 public KeywordFallback(Action<string> log)
 {
  var dataSetClass=Plugin.Game("TextDataSet");
  var singletonGetter=ResolveTextDataSetSingleton(dataSetClass);
  var field=U.Field(dataSetClass,"_keywordList");
  var keywordListClass=IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(field));
  var get=U.MethodTyped(keywordListClass,"GetData","String");
  if(Signature.Type(IL2CPP.il2cpp_method_get_return_type(get))!="TextData_BattleKeywords")
   throw new InvalidOperationException("Unexpected battle keyword lookup type.");
  GetData original=null!;
  GetData replacement=(obj,id,method)=>{
   var result=original(obj,id,method);
   if(obj==IntPtr.Zero||IL2CPP.il2cpp_object_get_class(obj)!=keywordListClass||
      IL2CPP.Il2CppStringToManaged(id)!="LittleCourage"||result==IntPtr.Zero)return result;
   try
   {
    if(!string.IsNullOrEmpty(N.StringField(result,"id")))return result;
    var set=U.Invoke(singletonGetter,IntPtr.Zero);
    var buffs=U.Call(set,"get_BufList");
    void** args=stackalloc void*[1];args[0]=(void*)id;
    var source=U.Invoke(U.MethodTyped(IL2CPP.il2cpp_object_get_class(buffs),"GetData","String"),buffs,args);
    if(source==IntPtr.Zero||N.StringField(source,"id")!="LittleCourage")return result;
    if(cached.TryGetValue((result,source),out var existing))return existing;
    var copy=N.Clone(result);handles.Add(IL2CPP.il2cpp_gchandle_new(copy,false));
    foreach(var name in new[]{"id","name","desc"})N.Reference(copy,name,U.Read<IntPtr>(source,name));
    cached.Add((result,source),copy);
    log("Repaired A Sign keyword tooltip from native localized buff text.");
    return copy;
   }
   catch(Exception e){log("A Sign tooltip fallback rejected: "+e.Message);return result;}
  };
  hook=INativeDetour.CreateAndApply(Marshal.ReadIntPtr(get),replacement,out original);
  delegates.Add(replacement);delegates.Add(original);
  log("A Sign keyword fallback ready; Singleton<TextDataSet> accessor verified.");
 }
 static IntPtr ResolveTextDataSetSingleton(IntPtr dataSetClass)
 {
  var definition=IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(Plugin.Game("Singleton`1")));
  var argument=IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(dataSetClass));
  var typeClass=U.Class("Type","System","mscorlib.dll");
  var arguments=IL2CPP.il2cpp_array_new(typeClass,1);
  var handle=IL2CPP.il2cpp_gchandle_new(arguments,false);
  try
  {
   IL2CPP.il2cpp_gc_wbarrier_set_field(arguments,arguments+32,argument);
   void** args=stackalloc void*[1];args[0]=(void*)arguments;
   var closed=U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(definition),"MakeGenericType",1),definition,args);
   var klass=IL2CPP.il2cpp_class_from_system_type(closed);
   var getter=U.Method(klass,"get_Instance");
   if(Signature.Type(IL2CPP.il2cpp_method_get_return_type(getter))!="TextDataSet")
    throw new InvalidOperationException("Unexpected localization singleton type.");
   return getter;
  }
  finally{IL2CPP.il2cpp_gchandle_free(handle);}
 }
 public void Dispose(){hook.Dispose();foreach(var handle in handles)IL2CPP.il2cpp_gchandle_free(handle);handles.Clear();cached.Clear();}
}
