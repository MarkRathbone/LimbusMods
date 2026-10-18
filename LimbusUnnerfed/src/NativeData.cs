using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;
internal static unsafe class N
{
 public static IntPtr BoxedField(IntPtr obj,string field)=>IL2CPP.il2cpp_field_get_value_object(U.Field(IL2CPP.il2cpp_object_get_class(obj),field),obj);
 public static T ScalarField<T>(IntPtr obj,string field) where T:unmanaged
 {
  var boxed=BoxedField(obj,field);
  return boxed==IntPtr.Zero?default:*(T*)IL2CPP.il2cpp_object_unbox(boxed);
 }
 public static void ValueField(IntPtr obj,string field,IntPtr boxed)
 {
  if(boxed==IntPtr.Zero)throw new ArgumentNullException(nameof(boxed));
  IL2CPP.il2cpp_field_set_value(obj,U.Field(IL2CPP.il2cpp_object_get_class(obj),field),(void*)IL2CPP.il2cpp_object_unbox(boxed));
 }
 public static void ScalarField<T>(IntPtr obj,string field,T value) where T:unmanaged
 {
  var f=U.Field(IL2CPP.il2cpp_object_get_class(obj),field);
  var klass=IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(f));
  var name=Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass))??"";
  if(!name.StartsWith("Nullable",StringComparison.Ordinal)){U.Write(obj,field,value);return;}
  var nullable=IL2CPP.il2cpp_object_new(klass);var handle=IL2CPP.il2cpp_gchandle_new(nullable,false);
  try{U.Write(nullable,"hasValue",(byte)1);U.Write(nullable,"value",value);ValueField(obj,field,nullable);}
  finally{IL2CPP.il2cpp_gchandle_free(handle);}
 }
 public static IntPtr Clone(IntPtr obj)=>U.Call(obj,"MemberwiseClone");
 public static IntPtr New(IntPtr klass){var obj=IL2CPP.il2cpp_object_new(klass);var h=IL2CPP.il2cpp_gchandle_new(obj,false);try{U.Call(obj,".ctor");return obj;}finally{IL2CPP.il2cpp_gchandle_free(h);}}
 public static void Reference(IntPtr obj,string field,IntPtr value)=>IL2CPP.il2cpp_field_set_value_object(obj,U.Field(IL2CPP.il2cpp_object_get_class(obj),field),value);
 public static IntPtr Item(IntPtr list,int index){void** args=stackalloc void*[1];args[0]=&index;return U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(list),"get_Item",1),list,args);}
 public static IntPtr IntObject(IntPtr obj,string method,int value){void** a=stackalloc void*[1];a[0]=&value;return U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(obj),method,1),obj,a);}
 public static int EnumValue(string type,string name,string ns="")
 {
  var field=U.Field(Plugin.Game(type,ns),name);int value=0;
  IL2CPP.il2cpp_field_static_get_value(field,&value);return value;
 }
 public static IEnumerable<IntPtr> Items(IntPtr list){var count=U.Get<int>(list,"Count");if(count<0||count>10000)throw new InvalidOperationException("Unexpected list size");for(int i=0;i<count;i++)yield return Item(list,i);}
 public static IntPtr CopyList(IntPtr list){var result=New(IL2CPP.il2cpp_object_get_class(list));var h=IL2CPP.il2cpp_gchandle_new(result,false);try{foreach(var item in Items(list))U.ObjectArg(result,"Add",item);return result;}finally{IL2CPP.il2cpp_gchandle_free(h);}}
 public static IntPtr Instantiate(IntPtr obj){void** a=stackalloc void*[1];a[0]=(void*)obj;return U.Invoke(U.MethodTyped(U.Class("Object"),"Instantiate","Object"),IntPtr.Zero,a);}
 public static void DestroyImmediate(IntPtr obj){void** a=stackalloc void*[1];a[0]=(void*)obj;U.Invoke(U.Method(U.Class("Object"),"DestroyImmediate",1),IntPtr.Zero,a);}
 public static void Parent(IntPtr rect,IntPtr parent){byte world=0;void** a=stackalloc void*[2];a[0]=(void*)parent;a[1]=&world;U.Invoke(U.Method(U.Class("Transform"),"SetParent",2),rect,a);}
 public static string StringField(IntPtr obj,string name)=>IL2CPP.Il2CppStringToManaged(U.Read<IntPtr>(obj,name))??"";
}
