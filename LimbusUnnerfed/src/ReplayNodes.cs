using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;

internal sealed unsafe class ReplayNodes : IDisposable
{
 readonly List<INativeDetour> hooks=new();readonly List<Delegate> delegates=new();
 readonly List<IntPtr> handles=new();readonly Dictionary<IntPtr,ReplayEncounter> slots=new();
 readonly NativeReplayStages stages;readonly CombatProfile profile;readonly Action<string> log;
 readonly Dictionary<int,IntPtr> sources=new();readonly HashSet<IntPtr> indexedTables=new();
 IntPtr map,presenter,formationData,formationPanel,selectedStage;
 readonly List<(IntPtr Rect,V2 Position,V3 Scale)> mapView=new();
 string buildState="";
 ReplayEncounter? selected;bool selecting,building,openingMap,localBattle,localFormation;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Zero(IntPtr obj,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void One(IntPtr obj,IntPtr arg,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ClickAction(IntPtr obj,byte longClick,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Two(IntPtr obj,IntPtr a,IntPtr b,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void MapOpen(IntPtr obj,IntPtr owner,int part,int chapter,byte flag,IntPtr action,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Leave(IntPtr obj,IntPtr battle,IntPtr result,IntPtr formation,IntPtr method);
 IntPtr Keep(IntPtr obj){if(obj!=IntPtr.Zero)handles.Add(IL2CPP.il2cpp_gchandle_new(obj,false));return obj;}
 void Guard(Action action){try{action();}catch(Exception e){log("Local replay stopped: "+e);}}
 void Hook<T>(IntPtr klass,string name,int[] kinds,T replacement,out T original) where T:Delegate
 {
  var method=U.Method(klass,name,kinds.Length);
  if(((Marshal.ReadInt32(IL2CPP.il2cpp_method_get_return_type(method),8)>>16)&255)!=1)throw new InvalidOperationException(name+" is no longer void.");
  for(uint i=0;i<kinds.Length;i++)if(((Marshal.ReadInt32(IL2CPP.il2cpp_method_get_param(method,i),8)>>16)&255)!=kinds[i])throw new InvalidOperationException(name+" ABI changed.");
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(method),replacement,out original));delegates.Add(replacement);delegates.Add(original);
 }
 public ReplayNodes(CombatProfile profile,Action<string> log,Func<bool> modalOpen)
 {
  this.profile=profile;this.log=log;stages=new(profile);
  try {
   profile.ObserveStageTable=obj=>Guard(()=>IndexSources(obj));
   profile.LocalStage=id=>stages.TryGet(id,out var stage)?stage:IntPtr.Zero;
   One initOriginal=null!;
   One init=(obj,data,method)=>{initOriginal!(obj,data,method);Guard(()=>IndexSources(obj));};
   Hook(Plugin.Game("StageStaticDataList"),"Init",new[]{18},init,out initOriginal);
   MapOpen openOriginal=null!;
   MapOpen open=(obj,owner,part,chapter,flag,action,method)=>{
    ClearSlots();selected=null;localFormation=false;selectedStage=IntPtr.Zero;map=Keep(obj);presenter=Keep(owner);
    log($"Replay map opened: part={part}; chapter={chapter}; sources={sources.Count}/{ReplayCatalog.Encounters.Count}.");
    openingMap=true;
    try{openOriginal!(obj,owner,part,chapter,flag,action,method);}finally{openingMap=false;}
    Guard(BuildSlots);
   };
   Hook(Plugin.Game("StageMapUIPanel","MainUI"),"SetDataOpen",new[]{18,8,8,2,21},open,out openOriginal);
   Zero updateOriginal=null!;
   Zero update=(obj,method)=>{updateOriginal!(obj,method);if(presenter!=obj)presenter=Keep(obj);var actual=U.Read<IntPtr>(obj,"stageMapUIPanel");if(actual!=IntPtr.Zero&&map!=actual){ClearSlots();map=Keep(actual);selected=null;selectedStage=IntPtr.Zero;localFormation=false;}Guard(BuildSlots);};
   Hook(Plugin.Game("StageUIPresenter","MainUI"),"UpdateUI",Array.Empty<int>(),update,out updateOriginal);
   Two slotDataOriginal=null!;
   Two slotData=(obj,data,callback,method)=>{slotDataOriginal!(obj,data,callback,method);if(!building&&map!=IntPtr.Zero)Guard(BuildSlots);};
   Hook(Plugin.Game("StageIconUISlot","MainUI"),"SetData",new[]{18,21},slotData,out slotDataOriginal);
   ClickAction clickOriginal=null!;
   ClickAction click=(obj,longClick,method)=>{
    if(modalOpen())return;
    if(slots.TryGetValue(obj,out var entry)){Guard(()=>Select(obj,entry));return;}
    clickOriginal!(obj,longClick,method);
   };
   Hook(Plugin.Game("StageIconUISlot","MainUI"),"OnClick",new[]{2},click,out clickOriginal);
   Two infoOriginal=null!;
   Two info=(obj,data,callback,method)=>{if(!selecting){selected=null;localFormation=false;selectedStage=IntPtr.Zero;}infoOriginal!(obj,data,callback,method);};
   Hook(Plugin.Game("StageInfoUI","MainUI"),"SetDataOpen",new[]{18,21},info,out infoOriginal);
   Zero statisticsOriginal=null!;
   Zero statistics=(obj,method)=>{if(selected==null)statisticsOriginal!(obj,method);};
   Hook(Plugin.Game("StageInfoDisplayRenewal"),"OnClickStatisticButton",Array.Empty<int>(),statistics,out statisticsOriginal);
   IntPtr nestedIter=IntPtr.Zero,confirmListener=IntPtr.Zero,nested;
   while((nested=IL2CPP.il2cpp_class_get_nested_types(Plugin.Game("FormationUIPanel","MainUI"),ref nestedIter))!=IntPtr.Zero)
    if(IL2CPP.il2cpp_class_get_method_from_name(nested,"<OnConfirm>b__0",0)!=IntPtr.Zero){confirmListener=nested;break;}
   if(confirmListener==IntPtr.Zero)throw new MissingMethodException("Formation final confirmation listener");
   Zero confirmOriginal=null!;
   Zero confirmed=(obj,method)=>{
    if(localFormation&&U.Read<IntPtr>(obj,"<>4__this")==formationPanel){
     Guard(()=>{
      var current=Keep(U.Call(formationData,"GetCurrentFormation"));
      IntPtr iter=IntPtr.Zero,field,formation=IntPtr.Zero;
      while((field=IL2CPP.il2cpp_class_get_fields(IL2CPP.il2cpp_object_get_class(current),ref iter))!=IntPtr.Zero){
       var type=IL2CPP.il2cpp_field_get_type(field);
       if(((Marshal.ReadInt32(type,8)>>16)&255)!=18)continue;
       var klass=IL2CPP.il2cpp_class_from_type(type);
       if(Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass))!="PlayerUnitFormation")continue;
       formation=IL2CPP.il2cpp_field_get_value_object(field,current);break;
      }
      if(formation==IntPtr.Zero)throw new InvalidOperationException("Native formation selection is unavailable.");
      log("Local replay To Battle: native formation accepted.");
      Launch(formation);
     });return;
    }
    confirmOriginal!(obj,method);
   };
   Hook(confirmListener,"<OnConfirm>b__0",Array.Empty<int>(),confirmed,out confirmOriginal);
   Zero mapObjectsOpenOriginal=null!;
   Zero mapObjectsOpen=(obj,method)=>{
    if(localFormation){
     Guard(()=>{
      log("Local replay Back: closing formation and restoring the stage map.");
      localFormation=false;
      U.Call(formationPanel,"Close");
      ReturnToMap();
     });
     return;
    }
    mapObjectsOpenOriginal!(obj,method);
   };
   Hook(Plugin.Game("StageMapObjectManager"),"Open",Array.Empty<int>(),mapObjectsOpen,out mapObjectsOpenOriginal);
   Leave leaveOriginal=null!;
   Leave leave=(obj,battle,result,formation,method)=>{
    if(!localBattle){leaveOriginal!(obj,battle,result,formation,method);return;}
    Guard(()=>{
     U.Call(Singleton("StageController"),"Clear");
     int phase=EnumContaining("MAINUI_PHASE_TYPE","STAGE","MainUI");
     void** a=stackalloc void*[3];a[0]=&phase;a[1]=null;a[2]=null;
     U.Invoke(U.Method(Plugin.Game("GlobalGameManager"),"LoadMainScenePhase",3),obj,a);
     localBattle=false;selected=null;selectedStage=IntPtr.Zero;
    });
   };
   Hook(Plugin.Game("GlobalGameManager"),"LeaveStage",new[]{18,18,18},leave,out leaveOriginal);
  }catch{Dispose();throw;}
 }
 void IndexSources(IntPtr stageTable)
 {
  if(stageTable==IntPtr.Zero||indexedTables.Contains(stageTable))return;
  var list=U.Read<IntPtr>(stageTable,"list");if(list==IntPtr.Zero||U.Get<int>(list,"Count")==0)return;
  Keep(stageTable);indexedTables.Add(stageTable);
  foreach(var stage in N.Items(list))
  {
   var entry=ReplayCatalog.Encounters.FirstOrDefault(x=>x.SourceId==U.Read<int>(stage,"id"));
   if(entry==null||sources.ContainsKey(entry.SourceId))continue;
   var scripts=U.Read<IntPtr>(stage,"stageScriptList");
   if(scripts==IntPtr.Zero||!ReplayCatalog.MatchesSource(entry,U.Read<int>(stage,"id"),N.StringField(stage,"stageType"),N.Items(scripts).Select(x=>IL2CPP.Il2CppStringToManaged(x)??"")))continue;
   sources.Add(entry.SourceId,Keep(stage));log($"Replay source indexed: {entry.Name} ({entry.SourceId}).");
  }
 }
 static IntPtr Singleton(string name)=>U.Invoke(U.Method(Plugin.Game(name),"get_Instance"),IntPtr.Zero);
 static int EnumContaining(string type,string text,string ns="")
 {
  return EnumContaining(Plugin.Game(type,ns),text);
 }
 static int EnumContaining(IntPtr klass,string text)
 {
  IntPtr iter=IntPtr.Zero,field;var matches=new List<IntPtr>();
  while((field=IL2CPP.il2cpp_class_get_fields(klass,ref iter))!=IntPtr.Zero){var name=Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field))!;if(name.Equals(text,StringComparison.OrdinalIgnoreCase)){int exact=0;IL2CPP.il2cpp_field_static_get_value(field,&exact);return exact;}if(name.Contains(text,StringComparison.OrdinalIgnoreCase))matches.Add(field);}
  if(matches.Count!=1)throw new MissingMemberException($"Ambiguous enum value: {text}");
  int value=0;IL2CPP.il2cpp_field_static_get_value(matches[0],&value);return value;
 }
 void BuildSlots()
 {
  if(building||openingMap||map==IntPtr.Zero||!U.Alive(map)||!U.Active(map))return;
  if(slots.Count>0&&!slots.Keys.All(x=>U.Alive(x)&&U.Active(x)))
  {
   log("Replay map slots expired or were hidden; rebuilding on the active map.");
   ClearSlots();selected=null;selectedStage=IntPtr.Zero;
  }
  var state=$"map={(map!=IntPtr.Zero)}; sources={sources.Count}/{ReplayCatalog.Encounters.Count}; slots={slots.Count}";
  if(state!=buildState){buildState=state;log("Replay map readiness: "+state);}
  if(sources.Count==0||slots.Count==ReplayCatalog.Encounters.Count)return;
  var originals=U.Read<IntPtr>(map,"_stageSlots");if(originals==IntPtr.Zero)return;
  var candidates=N.Items(originals).Where(x=>U.Alive(x)&&U.Active(x)).Where(x=>{
   var data=U.Read<IntPtr>(x,"_currentStageData");if(data==IntPtr.Zero)return false;
   var node=U.Read<IntPtr>(data,"_stageNodeLocalData");if(node==IntPtr.Zero)return false;
   int id=U.Read<int>(node,"stageId");return id==10830;
  }).ToArray();
  if(candidates.Length==0)return;
  building=true;
  try {
   var template=candidates.Last();var templateRect=U.Transform(template);
   var parent=U.Parent(templateRect);
   var regular=N.Items(originals).Where(x=>U.Alive(x)&&U.Active(x)).ToArray();
   var last=regular.OrderBy(x=>U.Read<int>(x,"_nodeUIIdx")).Last();
   var pos=U.Get<V2>(U.Transform(last),"anchoredPosition");
   float right=regular.Max(x=>U.Get<V2>(U.Transform(x),"anchoredPosition").X);
   int index=0;
   foreach(var entry in ReplayCatalog.Encounters)
   {
    ++index;
    if(slots.Values.Any(x=>x.Id==entry.Id)||!sources.TryGetValue(entry.SourceId,out var source))continue;
    stages.Build(entry,source);
    var slot=Keep(N.Instantiate(template));var rect=U.Transform(slot);N.Parent(rect,parent);
    N.Reference(slot,"_currentStageData",U.Read<IntPtr>(template,"_currentStageData"));
    foreach(var field in new[]{"_nodeId","_nodeUIIdx","_type"})U.Write(slot,field,U.Read<int>(template,field));
    U.Write(slot,"_isUpper",U.Read<byte>(template,"_isUpper"));
    U.Set(slot,"interactable",(byte)1);
    U.Set(rect,"anchoredPosition",new V2(right+450*index,pos.Y));
    slots.Add(slot,entry);
    U.Text(U.Read<IntPtr>(slot,"tmp_stageid"),$"T{index}");
    U.Text(U.Read<IntPtr>(slot,"tmp_stageName"),entry.Name+" [Unnerfed Test]");
    U.ValueArg(slot,"SetSelected",(byte)0);
   }
   var mapRect=U.Read<IntPtr>(map,"rect_stageMap");var size=U.Get<V2>(mapRect,"sizeDelta");
   U.Set(mapRect,"sizeDelta",new V2(Math.Max(size.X,right+450*(index+1)),size.Y));
   U.ValueArg(map,"RestrictMovableArea",right+450*(index+1));
   log("Prepared 5 local Unnerfed replay nodes on the Canto VIII map.");
  }catch{ClearSlots();throw;}finally{building=false;}
 }
 void Select(IntPtr slot,ReplayEncounter entry)
 {
  foreach(var item in slots.Keys)U.ValueArg(item,"SetSelected",(byte)(item==slot?1:0));
  selected=entry;localFormation=false;selectedStage=stages.TryGet(entry.Id,out var stage)?stage:throw new InvalidOperationException("Replay not prepared.");
  CaptureMapView();
  log($"Selecting local replay: {entry.Name} ({entry.Id}).");
  var data=U.Read<IntPtr>(slot,"_currentStageData");var info=U.Read<IntPtr>(map,"stageInfoUIPanel");
  var action=U.Read<IntPtr>(map,"_enterEvent");
  selecting=true;
  try{void** a=stackalloc void*[2];a[0]=(void*)data;a[1]=(void*)action;U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(info),"SetDataOpen",2),info,a);}
  finally{selecting=false;}
 }
 public bool Click(IntPtr button)
 {
  if(slots.Count==0)return false;
  var rect=U.Transform(button);
  for(int depth=0;depth<12&&rect!=IntPtr.Zero;depth++,rect=U.Parent(rect))
   foreach(var item in slots)
    if(U.Alive(item.Key)&&U.Transform(item.Key)==rect){Guard(()=>Select(item.Key,item.Value));return true;}
  return false;
 }
 public IntPtr Preview(IntPtr original)
 {
  if(selected==null)return original;
  log($"Preparing replay preview: {selected.Name}.");
  var result=Keep(N.Clone(original));N.ScalarField(result,"_stageID",selected.Id);
  N.Reference(result,"_stageStaticData",selectedStage);
  N.Reference(result,"_title",IL2CPP.ManagedStringToIl2Cpp(selected.Name+" [Unnerfed Test]"));
  N.Reference(result,"_stageDesc",IL2CPP.ManagedStringToIl2Cpp("Local test encounter"));
  N.Reference(result,"_recommendLevel",IL2CPP.ManagedStringToIl2Cpp(U.Read<int>(selectedStage,"recommendedLevel").ToString()));
  U.Write(result,"_hasStory",(byte)0);
  var button=Keep(N.BoxedField(result,"_enterButtonData"));
  N.ScalarField(button,"staminaCost",0);N.ScalarField(button,"canEnterStage",(byte)1);N.ValueField(result,"_enterButtonData",button);
  var rewards=Keep(N.Clone(U.Read<IntPtr>(result,"_stageRewards")));
  foreach(var field in new[]{"_normalClearReward","_exClearReward","_firstClearReward"}){var list=U.Read<IntPtr>(rewards,field);if(list!=IntPtr.Zero)N.Reference(rewards,field,Keep(N.New(IL2CPP.il2cpp_object_get_class(list))));}
  N.Reference(result,"_stageRewards",rewards);
  var continuation=U.Read<IntPtr>(result,"_continueData");if(continuation!=IntPtr.Zero){continuation=Keep(N.Clone(continuation));N.ScalarField(continuation,"canContinue",(byte)0);N.Reference(result,"_continueData",continuation);}
  var source=U.Read<IntPtr>(result,"_stageEnemyInfoData");
  var klass=Plugin.Game("StageEnemyInfoUIData","LimbusCompany.GameUILibs.StageUILibs.EnemyDataLibs");
  var enemy=Keep(IL2CPP.il2cpp_object_new(klass));
  int battleType=U.Get<int>(selectedStage,"StageType"),enemyType=U.Get<int>(selectedStage,"StageEnemyType");byte yes=1;
  var options=U.Read<IntPtr>(source,"OptionData");void** args=stackalloc void*[6];
  args[0]=(void*)selectedStage;args[1]=&battleType;args[2]=&enemyType;args[3]=&yes;args[4]=&yes;args[5]=(void*)options;
  U.Invoke(U.Method(klass,".ctor",6),enemy,args);N.Reference(result,"_stageEnemyInfoData",enemy);
  log($"Replay preview prepared: {selected.Name}; free entry; rewards removed.");
  return result;
 }
 public bool Enter()
 {
  if(selected==null)return false;
  Guard(()=>{
   var boxedConfirm=Keep(CreateConfirm());formationData=Keep(IL2CPP.il2cpp_object_new(Plugin.Game("StageBattleFormationPanelData","MainUI")));
   var info=U.Read<IntPtr>(map,"stageInfoUIPanel");
    var backAction=Keep(U.Read<IntPtr>(info,"_openStageMapUI"));
    if(backAction==IntPtr.Zero)throw new InvalidOperationException("The stage map return callback is not initialized; replay formation was not opened.");
    int enterType=EnumContaining("ENTER_STAGE_TYPE","NORMAL_BATTLE");void** a=stackalloc void*[4];a[0]=(void*)selectedStage;a[1]=&enterType;a[2]=(void*)IL2CPP.il2cpp_object_unbox(boxedConfirm);a[3]=(void*)backAction;
   U.Invoke(U.Method(Plugin.Game("StageBattleFormationPanelData","MainUI"),".ctor",4),formationData,a);
   var panel=Keep(U.Call(presenter,"get_formationUIPanel"));formationPanel=panel;void** f=stackalloc void*[2];f[0]=(void*)presenter;f[1]=(void*)formationData;
   U.Invoke(U.Method(Plugin.Game("FormationUIPanel","MainUI"),"SetDataOpen",2),panel,f);
   localFormation=true;
   log($"Local replay formation opened: {selected.Name}; native map-return callback supplied.");
  });return true;
 }
 static IntPtr CreateConfirm(){byte yes=1;void** a=stackalloc void*[2];a[0]=null;a[1]=&yes;return U.Invoke(U.Method(Plugin.Game("FormationPanelConfirm","MainUI"),"GetBattleConfirmData",2),IntPtr.Zero,a);}
 void Launch(IntPtr formation)
 {
  if(selectedStage==IntPtr.Zero||selected==null)throw new InvalidOperationException("Local selection expired.");
  var start=Keep(N.New(Plugin.Game("BattleStartData","LimbusCompany.BattleLibrary.StartData")));
  var combatStage=stages.ForCombat(selected);
  int type=EnumContaining("STAGE_TYPE","NORMAL_BATTLE");byte skip=1;
  void** a=stackalloc void*[7];a[0]=(void*)start;a[1]=&skip;a[2]=(void*)combatStage;a[3]=&type;a[4]=null;a[5]=(void*)formation;a[6]=null;
  localBattle=true;
  try{U.Invoke(U.Method(Plugin.Game("GlobalGameManager"),"EnterStageWithoutPartData",7),Singleton("GlobalGameManager"),a);localFormation=false;log($"Local replay launched: {selected.Name}; source={selected.SourceId}; combat ID={U.Read<int>(combatStage,"id")}; no story entry request.");}
  catch{localBattle=false;throw;}
 }
 void CaptureMapView()
 {
  mapView.Clear();
  foreach(var name in new[]{"rect_stageMap","rect_slotParent","rect_lineParent","rect_objectInStageMap","rect_backGround"}){
   var rect=U.Read<IntPtr>(map,name);
   if(U.Alive(rect))mapView.Add((Keep(rect),U.Get<V2>(rect,"anchoredPosition"),U.Get<V3>(rect,"localScale")));
  }
 }
 void RestoreMapView()
 {
  U.Call(map,"StopCurrentZoomRoutine");
  U.Call(map,"StopMapScrollMovement");
  void** a=stackalloc void*[1];
  foreach(var name in new[]{"_startMoveCor","_zoomCor"}){
   var coroutine=U.Read<IntPtr>(map,name);
   if(coroutine==IntPtr.Zero)continue;
   a[0]=(void*)coroutine;
   U.Invoke(U.MethodTyped(U.Class("MonoBehaviour"),"StopCoroutine","Coroutine"),map,a);
   N.Reference(map,name,IntPtr.Zero);
  }
  foreach(var view in mapView)if(U.Alive(view.Rect)){
   U.Set(view.Rect,"localScale",view.Scale);U.Set(view.Rect,"anchoredPosition",view.Position);
  }
  mapView.Clear();
 }
 void ReturnToMap()
 {
  int part=U.Read<int>(map,"_partId"),chapter=U.Read<int>(map,"_subchapterId");byte no=0;
  void** a=stackalloc void*[5];a[0]=&part;a[1]=&chapter;a[2]=&no;a[3]=&no;a[4]=null;
  U.Invoke(U.Method(Plugin.Game("StageUIPresenter","MainUI"),"ReturnStageMap",5),presenter,a);
  RestoreMapView();
  formationData=IntPtr.Zero;localFormation=false;selected=null;selectedStage=IntPtr.Zero;
  log("Local replay Back: stage map restored.");
 }
 void ClearSlots(){foreach(var slot in slots.Keys)if(U.Alive(slot))N.DestroyImmediate(U.GO(slot));slots.Clear();}
 public void Dispose(){profile.ObserveStageTable=null;profile.LocalStage=null;foreach(var hook in hooks.AsEnumerable().Reverse())hook.Dispose();hooks.Clear();ClearSlots();stages.Dispose();foreach(var handle in handles)IL2CPP.il2cpp_gchandle_free(handle);handles.Clear();}
}
