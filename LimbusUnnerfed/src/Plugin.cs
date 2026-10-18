using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;
[BepInPlugin("local.limbus.unnerfed", "Limbus Unnerfed", "0.4.20")]
public sealed class Plugin : BasePlugin
{
 readonly List<INativeDetour> hooks = new();
 readonly List<Delegate> delegates = new();
 ModeControl? control;
 CapSettings capSettings=null!;
 CapEditor? capEditor;
 readonly Dictionary<IntPtr,ModeControl> luxControls=new();
 readonly Dictionary<IntPtr,IntPtr> pendingLux=new();
 bool observingLux;
 bool observing;
 readonly Dictionary<IntPtr,string> luxStates=new();
 bool selectedEx,launchEx;
 int difficulty;
 IntPtr uiHandle,dataHandle;
 CombatProfile? profile;
 CombatHooks? combat;
 PrivacyTextHooks? privacy;
 KeywordFallback? keywordFallback;
 ReplayNodes? replays;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate ushort ValidateAction(IntPtr obj,IntPtr text,int position,ushort character,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ZeroAction(IntPtr obj,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void IntAction(IntPtr obj,IntPtr arg,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void TwoObjects(IntPtr obj,IntPtr first,IntPtr second,IntPtr method);
 internal static IntPtr Game(string name,string ns="")=>U.Class(name,ns,"Assembly-CSharp.dll");
 public override void Load()
 {
  try {
  try{keywordFallback=new KeywordFallback(message=>Log.LogInfo(message));}
  catch(Exception e){Log.LogWarning("A Sign keyword fallback unavailable: "+e.Message);}
  privacy=new PrivacyTextHooks(Config.Bind("Privacy","HideManagerId",true,"Hide your manager number in displayed UI text."),Config.Bind("Privacy","ManagerId","","Manager number to hide."));
  Signature.Check(Game("StageInfoUI","MainUI"),"OpenStageInfoDisplay","System.Void","StageInfoUIDataBase");
  Signature.Check(Game("StageInfoUI","MainUI"),"SetEnterState","System.Void","UnlockCondition");
  Signature.Check(U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),"OnPointerClick","System.Void","UnityEngine.EventSystems.PointerEventData");
  profile=new CombatProfile();
  capSettings=new CapSettings(Config);
  combat=new CombatHooks(profile,()=>launchEx,()=>selectedEx,()=>capSettings.Value,message=>Log.LogInfo(message));
  if(Config.Bind("Local Testing","EnableReplayNodes",false,"Add local, free Unnerfed dungeon-boss test nodes to the Canto VIII map.").Value)
   try{replays=new ReplayNodes(profile,message=>Log.LogInfo(message),()=>capEditor!=null);}
   catch(Exception e){Log.LogError("Local replay UI disabled after API validation failure: "+e);}
  var ui=Game("StageInfoUI","MainUI");
  IntAction openOriginal=null!;
  IntAction openReplacement=(obj,arg,method)=>{
   combat.BeginPreview();
   var displayData=arg;
   try{displayData=replays?.Preview(arg)??arg;}
   catch(Exception e){Log.LogError("Replay preview could not be prepared: "+e);return;}
   arg=displayData;
   Guard(()=>{CloseCapEditor();control?.Dispose();control=null;});
   Guard(()=>{
    ReplaceHandle(ref uiHandle,obj);ReplaceHandle(ref dataHandle,arg);
    var stageField=IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_object_get_class(arg),"_stageID");
    selectedEx=stageField!=IntPtr.Zero&&CombatProfile.SupportedStages.Contains(ReplayCatalog.RestorationStageId(N.ScalarField<int>(arg,"_stageID")));
    difficulty=0;
    var staticField=IL2CPP.il2cpp_class_get_field_from_name(IL2CPP.il2cpp_object_get_class(arg),"_stageStaticData");
    if(staticField!=IntPtr.Zero){var stage=U.Read<IntPtr>(arg,"_stageStaticData");if(stage!=IntPtr.Zero)difficulty=U.Read<int>(stage,"recommendedLevel");}
   });
   openOriginal!(obj,displayData,method);
   Guard(()=>Observe(obj));
  };
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(ui,"OpenStageInfoDisplay",1)),openReplacement,out openOriginal));
  delegates.Add(openReplacement);delegates.Add(openOriginal);
  BoolAction closeOriginal=null!;
  BoolAction closeReplacement=(obj,flag,method)=>{if(capEditor==null)closeOriginal!(obj,flag,method);};
  var closeMethod=Signature.Check(ui,"Close","System.Void","System.Boolean");
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(closeMethod),closeReplacement,out closeOriginal));
  delegates.Add(closeReplacement);delegates.Add(closeOriginal);
  HookZero(ui,"OnClickEnterButton",(obj,original)=>{if(replays?.Enter()==true)return;launchEx=selectedEx;Log.LogInfo($"Entering stage {U.Read<int>(obj,"_stageId")}; restorations={launchEx}; caps={capSettings.Value}");original();});
  HookInt(ui,"SetEnterState",(obj,arg,original)=>{ original(); Guard(()=>Observe(obj)); });
  Signature.Check(Game("StageInfoDisplayRenewal"),"SetActiveVisual","System.Void","System.Boolean");
  BoolAction visualOriginal=null!;
  BoolAction visualReplacement=(obj,show,method)=>{if(show==0&&capEditor!=null)return;visualOriginal!(obj,show,method);if(show!=0&&uiHandle!=IntPtr.Zero)Guard(()=>{var owner=IL2CPP.il2cpp_gchandle_get_target(uiHandle);if(U.Alive(owner)&&U.Read<IntPtr>(owner,"_stageInfoDisplay")==obj)Observe(owner);});};
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(Game("StageInfoDisplayRenewal"),"SetActiveVisual",1)),visualReplacement,out visualOriginal));delegates.Add(visualReplacement);delegates.Add(visualOriginal);
  HookInt(U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),"OnPointerClick",(obj,arg,original)=>{
    if(capEditor!=null){Guard(()=>capEditor.Click(obj));return;}
    var capControl=FindControl(obj);
    if(capControl!=null) { Log.LogInfo("Combat Caps control clicked: "+U.Name(capControl.Owner));Guard(()=>OpenCapEditor(capControl));return; }
    if(replays?.Click(obj)==true)return;
    original();
  });
  var inputClass=U.Class("TMP_InputField","TMPro","Unity.TextMeshPro.dll");
  Signature.Check(inputClass,"Validate","System.Char","System.String","System.Int32","System.Char");
  ValidateAction validateOriginal=null!;
  ValidateAction validateReplacement=(obj,text,position,character,method)=>{
   if(capEditor?.OwnsInput(obj)==true)return capEditor.ValidateInput(obj,IL2CPP.Il2CppStringToManaged(text)??"",position,character);
   return validateOriginal(obj,text,position,character,method);
  };
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(inputClass,"Validate",3)),validateReplacement,out validateOriginal));
  delegates.Add(validateReplacement);delegates.Add(validateOriginal);
  foreach(var entry in new[]{("OnPointerEnter",true),("OnPointerExit",false)})
   HookInt(U.Class("Selectable","UnityEngine.UI","UnityEngine.UI.dll"),entry.Item1,(obj,arg,original)=>{original();if(capEditor!=null){Guard(()=>capEditor.Hover(obj,entry.Item2));return;}var target=FindControl(obj);if(target!=null)Guard(()=>target.Hover(entry.Item2));});
  var rewardPanel=Game("RewardDungeonUIPanel","MainUI");
  var openPanel=U.Method(rewardPanel,"OpenPanel",1);
  var panelType=IL2CPP.il2cpp_method_get_param(openPanel,0);
  if(((Marshal.ReadInt32(panelType,8)>>16)&255)!=17)throw new InvalidOperationException("RewardDungeonUIPanel.OpenPanel signature changed");
  PanelAction panelOriginal=null!;
  PanelAction panelReplacement=(obj,tab,info)=>{panelOriginal!(obj,tab,info);Guard(()=>ObserveLux(obj));};
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(openPanel),panelReplacement,out panelOriginal));
  delegates.Add(panelReplacement);delegates.Add(panelOriginal);
  var openReward=U.Method(rewardPanel,"SetDataOpen",3);
  var expectedKinds=new[]{17,18,18};
  for(uint i=0;i<3;i++)
   if(((Marshal.ReadInt32(IL2CPP.il2cpp_method_get_param(openReward,i),8)>>16)&255)!=expectedKinds[i])
    throw new InvalidOperationException("RewardDungeonUIPanel.SetDataOpen signature changed");
  RewardOpenAction rewardOriginal=null!;
  RewardOpenAction rewardReplacement=(obj,tab,presenter,data,info)=>{rewardOriginal!(obj,tab,presenter,data,info);Guard(()=>ObserveLux(obj));};
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(openReward),rewardReplacement,out rewardOriginal));
  delegates.Add(rewardReplacement);delegates.Add(rewardOriginal);
  foreach(var setter in new[]{"set_sprite","set_overrideSprite"})
   HookInt(U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"),setter,(obj,sprite,original)=>{
    original();
    if(sprite!=IntPtr.Zero&&!observingLux&&pendingLux.Count>0)Guard(RetryLuxArtwork);
   });
  Log.LogInfo("Limbus Company Unnerfed restorations and Level Cap ready.");
  }catch {Unload();throw;}
 }
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void BoolAction(IntPtr obj,byte value,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void PanelAction(IntPtr obj,int tab,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void RewardOpenAction(IntPtr obj,int tab,IntPtr presenter,IntPtr data,IntPtr method);
 void CheckLuxVisibility()
 {
  foreach(var c in luxControls.Values)
  {
   if(!c.Alive)continue;
   var path=new List<string>();
   for(var t=U.Transform(c.Button);t!=IntPtr.Zero;t=U.Parent(t))
   {
    var group=U.Component(U.GO(t),U.Class("CanvasGroup"));
    var rect=U.Component(U.GO(t),U.Class("RectTransform"));
    path.Add($"{U.Name(t)}(self={U.Get<byte>(U.GO(t),"activeSelf")}"+(rect==IntPtr.Zero?"":$",rect={U.Get<R4>(rect,"rect")}")+(group==IntPtr.Zero?")":$",alpha={U.Get<float>(group,"alpha")},interactable={U.Get<byte>(group,"interactable")})"));
   }
   var state=string.Join(" / ",path);
   if(!luxStates.TryGetValue(c.Owner,out var old)||old!=state){luxStates[c.Owner]=state;Log.LogInfo("Lux visibility: "+state);}
  }
 }
 static void ReplaceHandle(ref IntPtr handle,IntPtr obj){if(handle!=IntPtr.Zero)IL2CPP.il2cpp_gchandle_free(handle);handle=IL2CPP.il2cpp_gchandle_new(obj,false);}
 void Observe(IntPtr ui)
 {
  if(observing)return;
  observing=true;
  try {
  bool visible=difficulty>0;
  if(!visible) { control?.SetVisible(false); return; }
  if(control==null || !control.Alive || control.Owner!=ui) { control?.Dispose(); control=new ModeControl(ui); }
   control.SetVisible(true);control.Refresh(capSettings.Value,difficulty);
  }finally{observing=false;}
 }
 ModeControl? FindControl(IntPtr button)=>control!=null&&control.IsButton(button)?control:luxControls.Values.FirstOrDefault(x=>x.IsButton(button));
 void OpenCapEditor(ModeControl source)
 {
  CloseCapEditor();capEditor=new CapEditor(source,capSettings.Value,value=>{
   capSettings.Save(value);Log.LogInfo($"Combat caps saved: {value}");
   if(control?.Alive==true)control.Refresh(value,difficulty);
   foreach(var c in luxControls.Values)if(c.Alive)c.Refresh(value);
  },CloseCapEditor);
  Log.LogInfo("Combat Caps modal opened; underlying button input blocked.");
 }
 void CloseCapEditor(){var editor=capEditor;capEditor=null;editor?.Dispose();}
 void ObserveLux(IntPtr ui)
 {
  if(observingLux)return;
  observingLux=true;
  try {
  if(luxControls.TryGetValue(ui,out var c)&&c.Alive){c.SetVisible(true);c.Refresh(capSettings.Value);return;}
  c?.Dispose();
  var source=U.Read<IntPtr>(ui,"btn_exp");
  if(source==IntPtr.Zero)throw new MissingMemberException("Luxcavation button artwork");
  if(!ModeControl.ArtworkReady(source))
  {
   if(!pendingLux.ContainsKey(ui)){pendingLux[ui]=IL2CPP.il2cpp_gchandle_new(ui,false);Log.LogInfo("Waiting for Luxcavation tab artwork.");}
   return;
  }
  c=new ModeControl(ui,source,true);luxControls[ui]=c;c.Refresh(capSettings.Value);c.SetVisible(true);
  if(pendingLux.Remove(ui,out var handle))IL2CPP.il2cpp_gchandle_free(handle);
  var rect=U.Transform(c.Button);
  Log.LogInfo($"Lux Level Cap: owner={U.Name(ui)} active={U.Active(c.Button)} position={U.Get<V3>(rect,"position")} parentRect={U.Get<R4>(U.Parent(rect),"rect")} layer={U.Get<int>(U.GO(c.Button),"layer")}");
  }finally{observingLux=false;}
 }
 void RetryLuxArtwork()
 {
  foreach(var entry in pendingLux.ToArray())
  {
   var ui=IL2CPP.il2cpp_gchandle_get_target(entry.Value);
   if(U.Alive(ui))ObserveLux(ui);
   else {pendingLux.Remove(entry.Key);IL2CPP.il2cpp_gchandle_free(entry.Value);}
  }
 }
 void Guard(Action action){try{action();}catch(Exception e){Log.LogWarning(e);}}
 void HookZero(IntPtr klass,string method,Action<IntPtr,Action> action)
 {
  ZeroAction? original=null;
  ZeroAction replacement=(obj,info)=>action(obj,()=>original!(obj,info));
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass,method)),replacement,out original));
  delegates.Add(replacement);delegates.Add(original!);
 }
 void HookInt(IntPtr klass,string method,Action<IntPtr,IntPtr,Action> action)
 {
  IntAction? original=null;
  IntAction replacement=(obj,arg,info)=>action(obj,arg,()=>original!(obj,arg,info));
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass,method,1)),replacement,out original));
  delegates.Add(replacement);delegates.Add(original!);
 }
 void Dump(IntPtr klass)
 {
  var name=Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass));
  string TypeName(IntPtr t)=>Marshal.PtrToStringAnsi(IL2CPP.il2cpp_type_get_name(t))??"?";
  IntPtr iter=IntPtr.Zero, method;
  while((method=IL2CPP.il2cpp_class_get_methods(klass,ref iter))!=IntPtr.Zero)
  {
   var args=Enumerable.Range(0,(int)IL2CPP.il2cpp_method_get_param_count(method)).Select(i=>TypeName(IL2CPP.il2cpp_method_get_param(method,(uint)i))+" "+Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_param_name(method,(uint)i)));
   Log.LogInfo(name+"."+Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method))+"("+string.Join(",",args)+") -> "+TypeName(IL2CPP.il2cpp_method_get_return_type(method)));
  }
  iter=IntPtr.Zero;IntPtr f;
  while((f=IL2CPP.il2cpp_class_get_fields(klass,ref iter))!=IntPtr.Zero)
   Log.LogInfo(name+" field "+Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(f))+": "+TypeName(IL2CPP.il2cpp_field_get_type(f)));
 }
 public override bool Unload(){CloseCapEditor();replays?.Dispose();privacy?.Dispose();keywordFallback?.Dispose();combat?.Dispose();profile?.Dispose();if(uiHandle!=IntPtr.Zero)IL2CPP.il2cpp_gchandle_free(uiHandle);if(dataHandle!=IntPtr.Zero)IL2CPP.il2cpp_gchandle_free(dataHandle);control?.Dispose();foreach(var c in luxControls.Values)c.Dispose();foreach(var handle in pendingLux.Values)IL2CPP.il2cpp_gchandle_free(handle);pendingLux.Clear();foreach(var hook in hooks.AsEnumerable().Reverse())hook.Dispose();return true;}
}
