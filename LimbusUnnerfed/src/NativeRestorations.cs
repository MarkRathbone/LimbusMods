using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace LimbusCompanyUnnerfed;

internal sealed unsafe class NativeRestorations : IDisposable
{
 readonly RestorationManifest manifest=RestorationManifest.Embedded();
 readonly Dictionary<(IntPtr,string),IntPtr> cache=new();
 readonly HashSet<IntPtr> copies=new();
 readonly List<IntPtr> roots=new();
 IntPtr Keep(IntPtr value){if(value!=IntPtr.Zero)roots.Add(IL2CPP.il2cpp_gchandle_new(value,false));return value;}
 static string TypeName(IntPtr type)=>Marshal.PtrToStringAnsi(IL2CPP.il2cpp_type_get_name(type))??"";
 static IntPtr Field(IntPtr obj,string name)
 {
  for(var c=IL2CPP.il2cpp_object_get_class(obj);c!=IntPtr.Zero;c=IL2CPP.il2cpp_class_get_parent(c))
  {var f=IL2CPP.il2cpp_class_get_field_from_name(c,name);if(f!=IntPtr.Zero)return f;}
  throw new MissingFieldException(name);
 }
 public IntPtr Restore(IntPtr original,string kind,int id)
 {
  if(original==IntPtr.Zero||copies.Contains(original))return original;
  if(cache.TryGetValue((original,kind),out var result))return result;
  var changes=manifest.For(kind,id);if(changes.Count==0)return original;
  try {foreach(var p in changes)CheckPath(original,p.Path,0,p.Expected);}
  catch {cache[(original,kind)]=original;throw;}
  try {
   result=Keep(N.Clone(original));
   foreach(var p in changes)WritePath(result,p.Path,0,p.Restored);
   if(kind.StartsWith("buff:",StringComparison.Ordinal))
    foreach(var ability in N.Items(U.Read<IntPtr>(result,"list")))
     if(U.Read<IntPtr>(ability,"buffData")==IntPtr.Zero)
      throw new InvalidDataException("Restored buff ability is missing required BuffReferenceData: "+kind);
  }catch {cache[(original,kind)]=original;throw;}
  cache.Add((original,kind),result);copies.Add(result);return result;
 }
 static bool IsList(IntPtr obj)=>TypeName(IL2CPP.il2cpp_class_get_type(IL2CPP.il2cpp_object_get_class(obj))).StartsWith("System.Collections.Generic.List<",StringComparison.Ordinal);
 static IntPtr ItemType(IntPtr list)=>IL2CPP.il2cpp_method_get_return_type(U.Method(IL2CPP.il2cpp_object_get_class(list),"get_Item",1));
 static bool Primitive(IntPtr type)=>TypeName(type) is "System.Int32" or "System.Single" or "System.Double" or "System.Boolean" || IL2CPP.il2cpp_class_is_enum(IL2CPP.il2cpp_class_from_type(type));
 static int EnumValue(IntPtr type,JsonElement value)
 {
  if(value.ValueKind==JsonValueKind.Number)return value.GetInt32();
  var field=IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_class_from_type(type),value.GetString()!);
  if(field==IntPtr.Zero)throw new InvalidDataException("Unknown enum value: "+value);
  int result=0;IL2CPP.il2cpp_field_static_get_value(field,&result);return result;
 }
 static void CheckPath(IntPtr obj,string[] path,int offset,JsonElement expected)
 {
  if(obj==IntPtr.Zero)throw new InvalidDataException("Null native path: "+string.Join('/',path));
  if(IsList(obj))
  {
   int index=int.Parse(path[offset]);if(index>=U.Get<int>(obj,"Count"))throw new InvalidDataException("Native list changed.");
   var item=N.Item(obj,index);var type=ItemType(obj);
   if(offset+1<path.Length)CheckPath(item,path,offset+1,expected);
   else CheckValue(item,type,expected,true);
  }
  else
  {
   var field=Field(obj,path[offset]);var type=IL2CPP.il2cpp_field_get_type(field);
   if(offset+1==path.Length){byte* storage=stackalloc byte[16];IL2CPP.il2cpp_field_get_value(obj,field,storage);CheckValue((IntPtr)storage,type,expected,false);}
   else {IntPtr child=IntPtr.Zero;IL2CPP.il2cpp_field_get_value(obj,field,&child);CheckPath(child,path,offset+1,expected);}
  }
 }
 static void CheckValue(IntPtr value,IntPtr type,JsonElement expected,bool boxed)
 {
  string name=TypeName(type);var data=boxed&&Primitive(type)?IL2CPP.il2cpp_object_unbox(value):value;
  if(IL2CPP.il2cpp_class_is_enum(IL2CPP.il2cpp_class_from_type(type)))
  {if(*(int*)data!=EnumValue(type,expected))throw new InvalidDataException("Installed enum differs from reviewed value.");return;}
  if(!Primitive(type)&&IL2CPP.il2cpp_class_is_valuetype(IL2CPP.il2cpp_class_from_type(type)))throw new InvalidDataException("Unsupported native value type: "+name);
  bool match=name switch {
   "System.Int32"=>expected.ValueKind==JsonValueKind.Number&&*(int*)data==expected.GetInt32(),
   "System.Single"=>expected.ValueKind==JsonValueKind.Number&&Math.Abs(*(float*)data-expected.GetDouble())<0.0001,
   "System.Double"=>expected.ValueKind==JsonValueKind.Number&&Math.Abs(*(double*)data-expected.GetDouble())<0.0001,
   "System.Boolean"=>*(byte*)data==(expected.GetBoolean()?1:0),
   "System.String"=>CheckString(boxed?value:*(IntPtr*)value,expected),
   _=>CheckObject(boxed?value:*(IntPtr*)value,expected)
  };
  if(!match)throw new InvalidDataException($"Installed {name} differs from reviewed post-nerf value {expected}.");
 }
 static bool CheckString(IntPtr value,JsonElement expected)
 {
  var actual=IL2CPP.Il2CppStringToManaged(value);
  if(expected.ValueKind is JsonValueKind.True or JsonValueKind.False)return bool.TryParse(actual,out var b)&&b==expected.GetBoolean();
  return actual==(expected.ValueKind==JsonValueKind.Null?null:expected.GetString());
 }
 static bool CheckObject(IntPtr obj,JsonElement expected)
 {
  if(expected.ValueKind==JsonValueKind.Null)return obj==IntPtr.Zero;
  if(obj==IntPtr.Zero)return false;
  if(expected.ValueKind==JsonValueKind.Array)
  {
   if(U.Get<int>(obj,"Count")!=expected.GetArrayLength())return false;
   for(int i=0;i<expected.GetArrayLength();i++)CheckValue(N.Item(obj,i),ItemType(obj),expected[i],true);
   return true;
  }
  foreach(var property in expected.EnumerateObject())CheckPath(obj,new[]{property.Name},0,property.Value);
  return true;
 }
 void WritePath(IntPtr obj,string[] path,int offset,JsonElement restored)
 {
  if(IsList(obj))
  {
   int index=int.Parse(path[offset]);var type=ItemType(obj);var item=N.Item(obj,index);
   if(offset+1<path.Length){item=Keep(N.Clone(item));WritePath(item,path,offset+1,restored);SetItem(obj,index,item,type);}
   else SetItem(obj,index,Build(restored,type,item),type);
  }
  else
  {
   var field=Field(obj,path[offset]);var type=IL2CPP.il2cpp_field_get_type(field);IntPtr child=IntPtr.Zero;
   if(!Primitive(type))IL2CPP.il2cpp_field_get_value(obj,field,&child);
   if(offset+1<path.Length){child=Keep(IsList(child)?CopyList(child):N.Clone(child));WritePath(child,path,offset+1,restored);IL2CPP.il2cpp_field_set_value_object(obj,field,child);}
   else
   {
    var value=Build(restored,type,child);
    if(Primitive(type))IL2CPP.il2cpp_field_set_value(obj,field,(void*)IL2CPP.il2cpp_object_unbox(value));
    else IL2CPP.il2cpp_field_set_value_object(obj,field,value);
    if(path[offset]=="desc")
    {
     var raw=IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_object_get_class(obj),"rawDesc");
     if(raw!=IntPtr.Zero&&TypeName(IL2CPP.il2cpp_field_get_type(raw))=="System.String")IL2CPP.il2cpp_field_set_value_object(obj,raw,value);
    }
    var parsed=IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_object_get_class(obj),"_"+path[offset]);
    if(parsed!=IntPtr.Zero&&TypeName(IL2CPP.il2cpp_field_get_type(parsed))=="System.Nullable<System.Boolean>")
    {long empty=0;IL2CPP.il2cpp_field_set_value(obj,parsed,&empty);}
   }
  }
 }
 IntPtr Build(JsonElement value,IntPtr type,IntPtr template)
 {
  if(value.ValueKind==JsonValueKind.Null)return IntPtr.Zero;
  var klass=IL2CPP.il2cpp_class_from_type(type);string name=TypeName(type);
  if(name=="System.String")return Keep(IL2CPP.ManagedStringToIl2Cpp(value.ValueKind==JsonValueKind.String?value.GetString()!:value.ToString()));
  byte* storage=stackalloc byte[16];
  if(IL2CPP.il2cpp_class_is_enum(klass)){*(int*)storage=EnumValue(type,value);return Keep(IL2CPP.il2cpp_value_box(klass,(IntPtr)storage));}
  switch(name){case "System.Int32":*(int*)storage=value.GetInt32();break;case "System.Single":*(float*)storage=value.GetSingle();break;case "System.Double":*(double*)storage=value.GetDouble();break;case "System.Boolean":*storage=(byte)(value.GetBoolean()?1:0);break;default:goto ObjectValue;}
  return Keep(IL2CPP.il2cpp_value_box(klass,(IntPtr)storage));
 ObjectValue:
  var obj=Keep(N.New(klass));
  if(value.ValueKind==JsonValueKind.Array)
  {
   obj=Keep(N.New(klass));var itemType=ItemType(obj);int i=0;void** args=stackalloc void*[1];
   foreach(var element in value.EnumerateArray())
   {
    var old=template!=IntPtr.Zero&&i<U.Get<int>(template,"Count")?N.Item(template,i):IntPtr.Zero;
    var item=Build(element,itemType,old);args[0]=Primitive(itemType)?(void*)IL2CPP.il2cpp_object_unbox(item):(void*)item;
    U.Invoke(U.Method(klass,"Add",1),obj,args);i++;
   }
  }
  else
  {
   foreach(var property in value.EnumerateObject())WritePath(obj,new[]{property.Name},0,property.Value);
   if(Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass))=="BuffAbilityStaticData")
   {
    var field=Field(obj,"buffData");IntPtr data=IntPtr.Zero;
    IL2CPP.il2cpp_field_get_value(obj,field,&data);
    if(data==IntPtr.Zero)
    {
     data=Keep(N.New(IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(field))));
     IL2CPP.il2cpp_field_set_value_object(obj,field,data);
    }
   }
  }
  return obj;
 }
 static void SetItem(IntPtr list,int index,IntPtr item,IntPtr type)
 {void** args=stackalloc void*[2];args[0]=&index;args[1]=Primitive(type)?(void*)IL2CPP.il2cpp_object_unbox(item):(void*)item;U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(list),"set_Item",2),list,args);}
 IntPtr CopyList(IntPtr source)
 {
  var klass=IL2CPP.il2cpp_object_get_class(source);var result=Keep(N.New(klass));var type=ItemType(source);void** args=stackalloc void*[1];
  for(int i=0;i<U.Get<int>(source,"Count");i++){var item=N.Item(source,i);args[0]=Primitive(type)?(void*)IL2CPP.il2cpp_object_unbox(item):(void*)item;U.Invoke(U.Method(klass,"Add",1),result,args);}
  return result;
 }
 public void Dispose(){foreach(var root in roots)IL2CPP.il2cpp_gchandle_free(root);roots.Clear();cache.Clear();copies.Clear();}
}
