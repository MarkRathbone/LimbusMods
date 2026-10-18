using Il2CppInterop.Runtime;
namespace UltrawideLimbus;

internal sealed class SettingsControl : IDisposable
{
 readonly Plugin plugin;
 public bool IsUltrawide {get;private set;}
 int lastWidth,lastHeight;
 public SettingsControl(Plugin plugin)
 {
  this.plugin=plugin;
  var klass=U.Class("SettingsPanelGraphic","MainUI","Assembly-CSharp.dll");
  var method=U.Method(klass,"InitResolutionValue",-1);
  int count=(int)IL2CPP.il2cpp_method_get_param_count(method);
  plugin.LogMessage("Resolution initializer parameters: "+count);
  if(count!=1)throw new InvalidOperationException("Resolution initializer signature changed: "+count);
  plugin.HookObjectAction(klass,"InitResolutionValue",(obj,arg)=>{
   try{Extend(obj);}catch(Exception ex){plugin.LogMessage("Resolution dropdown extension failed: "+ex,true);}
  });
  plugin.HookInstance(klass,"ApplySetting",(obj,original)=>{
   var pending=U.Read<IntPtr>(obj,"_resolution");
   int width=U.Read<int>(pending,"width"),height=U.Read<int>(pending,"height");
   bool fullscreen=U.Read<byte>(obj,"_isFullscreen")!=0;
   original();
   ApplyWide(width,height,fullscreen);
  },featureHook:true);
  var assemblyName=System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi("Assembly-CSharp.dll");
  IntPtr assembly;
  try{assembly=IL2CPP.il2cpp_domain_assembly_open(IL2CPP.il2cpp_domain_get(),assemblyName);}
  finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(assemblyName);}
  var image=IL2CPP.il2cpp_assembly_get_image(assembly);
  IntPtr optionsClass=IntPtr.Zero;
  for(uint i=0;i<IL2CPP.il2cpp_image_get_class_count(image);i++)
  {
   var candidate=IL2CPP.il2cpp_image_get_class(image,i);
   if(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(candidate))=="LocalGameOptionData"){optionsClass=candidate;break;}
  }
  if(optionsClass==IntPtr.Zero)throw new MissingMemberException("LocalGameOptionData");
  plugin.HookInstance(optionsClass,"ApplyResolutionAndFullScreen",(obj,original)=>{
   original();
   ApplyWide(U.Read<int>(obj,"_resolutionWidth"),U.Read<int>(obj,"_resolutionHeight"),U.Read<byte>(obj,"_fullScreen")!=0);
  },featureHook:true);
  Refresh();
 }
 static int Screen(string name)=>U.StaticInt("Screen",name);
 public void Refresh()
 {
  int width=Screen("width"),height=Screen("height");
  if(width!=lastWidth||height!=lastHeight){lastWidth=width;lastHeight=height;IsUltrawide=ResolutionChoices.IsWide(width,height);plugin.Discover=true;}
 }
 static unsafe IntPtr Item(IntPtr list,int index)
 {
  void** args=stackalloc void*[1];args[0]=&index;
  return U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(list),"get_Item",1),list,args);
 }
 unsafe void Extend(IntPtr panel)
 {
  var dropdown=U.Read<IntPtr>(panel,"_resolutionDropdown");
  if(!U.Alive(dropdown))return;
  var options=U.Call(dropdown,"get_options");
  int count=U.Get<int>(options,"Count");if(count==0)return;
  var labels=new HashSet<string>();
  for(int i=0;i<count;i++)labels.Add(IL2CPP.Il2CppStringToManaged(U.Call(Item(options,i),"get_text"))??"");
  var display=U.Invoke(U.Method(U.Class("Display"),"get_main"),IntPtr.Zero);
  int maxWidth=U.Get<int>(display,"systemWidth"),maxHeight=U.Get<int>(display,"systemHeight");
  var sample=Item(options,0);
  foreach(var size in ResolutionChoices.ForDisplay(maxWidth,maxHeight,Screen("width"),Screen("height")))
  {
   string label=$"{size.Width}x{size.Height}";
   if(labels.Any(x=>x.Replace(" ","").Replace("×","x")==label))continue;
   var option=U.Call(sample,"MemberwiseClone");
   var handle=IL2CPP.il2cpp_gchandle_new(option,false);
   try{U.ObjectArg(option,"set_text",IL2CPP.ManagedStringToIl2Cpp(label));U.ObjectArg(options,"Add",option);}
   finally{IL2CPP.il2cpp_gchandle_free(handle);}
   labels.Add(label);
  }
  var pending=U.Read<IntPtr>(panel,"_resolution");
  int pendingWidth=U.Read<int>(pending,"width"),pendingHeight=U.Read<int>(pending,"height");
  string current=$"{pendingWidth}x{pendingHeight}";
  int total=U.Get<int>(options,"Count");
  for(int i=0;i<total;i++)
  {
   var label=IL2CPP.Il2CppStringToManaged(U.Call(Item(options,i),"get_text"))??"";
   if(label.Replace(" ","").Replace("×","x")!=current)continue;
   U.ValueArg(dropdown,"SetValueWithoutNotify",i);break;
  }
  U.Call(dropdown,"RefreshShownValue");
  plugin.LogMessage("Extended native resolution dropdown with ultrawide choices.");
 }
 unsafe void ApplyWide(int width,int height,bool fullscreen)
 {
  if(!ResolutionChoices.IsWide(width,height))return;
  try
  {
   IntPtr iterator=IntPtr.Zero,method=IntPtr.Zero,candidate;
   while((candidate=IL2CPP.il2cpp_class_get_methods(U.Class("Screen"),ref iterator))!=IntPtr.Zero)
   {
    if(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(candidate))!="SetResolution"||IL2CPP.il2cpp_method_get_param_count(candidate)!=3)continue;
    var type=IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_method_get_param(candidate,2));
    if(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(type))=="Boolean"){method=candidate;break;}
   }
   if(method==IntPtr.Zero)throw new MissingMethodException("Screen.SetResolution(int,int,bool)");
   byte full=fullscreen?(byte)1:(byte)0;
   void** args=stackalloc void*[3];args[0]=&width;args[1]=&height;args[2]=&full;
   U.Invoke(method,IntPtr.Zero,args);
   plugin.LogMessage($"Applied ultrawide resolution {width}x{height}, fullscreen={fullscreen}.");
  }
  catch(Exception ex){plugin.LogMessage("Ultrawide resolution apply failed: "+ex,true);}
 }
 public void Dispose(){}
}
