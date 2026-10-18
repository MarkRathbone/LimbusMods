using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;
internal sealed class CombatHooks : IDisposable
{
 readonly List<INativeDetour> hooks=new();readonly List<Delegate> delegates=new();
 readonly CombatProfile profile;
 readonly Func<bool> requested;
 readonly Func<bool> previewRequested;
 readonly Action<string> log;
 bool active;
 int pendingCap,combatCap,uptieCap,threadspinCap;
 bool UseExData=>true;
 public void BeginPreview(){active=false;pendingCap=combatCap=uptieCap=threadspinCap=0;}
 readonly HashSet<int> verifiedSkills=new();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StageInit(IntPtr obj,IntPtr stage,IntPtr formation,byte flag,int stageType,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr SkillGet(IntPtr obj,int id,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr TextGet(IntPtr obj,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FormattedTextGet(IntPtr obj,IntPtr args,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LevelTextGet(IntPtr obj,int level,IntPtr args,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate float SpeedMul(IntPtr obj,IntPtr owner,IntPtr target,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void UnitInit(IntPtr obj,IntPtr data,int level,int sync,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void EgoInit(IntPtr obj,int faction,IntPtr data,int sync,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ZeroAction(IntPtr obj,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void TimingAction(IntPtr obj,int timing,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr StringGet(IntPtr obj,IntPtr id,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int IntGet(IntPtr obj,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ControllerInit(IntPtr obj,IntPtr stage,int type,IntPtr unlocks,byte flag,IntPtr formation,int value,IntPtr method);
 public CombatHooks(CombatProfile profile,Func<bool> requested,Func<bool> previewRequested,Func<CombatCaps> caps,Action<string> log)
 {
  this.profile=profile;this.requested=requested;this.previewRequested=previewRequested;this.log=log;
  try {
  ControllerInit controllerOriginal=null!;
  ControllerInit controllerReplacement=(obj,stage,type,unlocks,flag,formation,value,method)=>{
    BeginPreview();
   try {
    int stageId=U.Read<int>(stage,"id"),recommended=U.Read<int>(stage,"recommendedLevel");
     var limits=caps();
     pendingCap=combatCap=limits.ResolveLevel(recommended);
     bool beforeTier4=(limits.UptieEnabled&&limits.UptieSync||limits.ThreadspinEnabled&&limits.ThreadspinSync)&&NativeHistoricalTiers.BeforeTier4(stage);
     uptieCap=limits.ResolveUptie(beforeTier4);threadspinCap=limits.ResolveThreadspin(beforeTier4);
     if(limits.UptieSync||limits.ThreadspinSync)log($"Tier Sync: stage={stageId}; pre-July-20-2023={beforeTier4}; Uptie={uptieCap}; Threadspin={threadspinCap}");
    active=CombatProfile.SupportedStages.Contains(ReplayCatalog.RestorationStageId(stageId));
    stage=profile.Stage(stage);
     log($"Encounter initialized: {stageId}; difficulty={recommended}; level cap={combatCap}; Uptie cap={uptieCap}; Threadspin cap={threadspinCap}; restorations={active}");
   }catch(Exception e){log("Encounter preparation failed: "+e.Message);}
   controllerOriginal!(obj,stage,type,unlocks,flag,formation,value,method);
  };
  Install(Plugin.Game("StageController"),"InitStageModel","System.Void",new[]{"StageStaticData","STAGE_TYPE","System.Collections.Generic.List<Server.AbnormalityUnlockInformationFormat>","System.Boolean","PlayerUnitFormation","System.Int32"},controllerReplacement,out controllerOriginal);
  ZeroAction clearOriginal=null!;
  ZeroAction clearReplacement=(obj,method)=>{BeginPreview();clearOriginal!(obj,method);};
  Install(Plugin.Game("StageController"),"Clear","System.Void",Array.Empty<string>(),clearReplacement,out clearOriginal);
  StageInit stageOriginal=null!;
  StageInit stageReplacement=(obj,stage,formation,flag,type,method)=>{
   active=false;combatCap=pendingCap;
   try {active=CombatProfile.SupportedStages.Contains(ReplayCatalog.RestorationStageId(U.Read<int>(stage,"id")));stage=profile.Stage(stage);if(active)log($"Restoration profile active: {U.Read<int>(stage,"id")}");}
   catch(Exception e){log("Ex combat preparation failed; keeping Normal: "+e);}
   stageOriginal!(obj,stage,formation,flag,type,method);
  };
  Install(Plugin.Game("StageModel"),"Init","System.Void",new[]{"StageStaticData","PlayerUnitFormation","System.Boolean","STAGE_TYPE"},stageReplacement,out stageOriginal);
  SkillGet stageDataOriginal=null!;
  SkillGet stageDataReplacement=(obj,id,method)=>{
   profile.ObserveStageTable?.Invoke(obj);
   if(ReplayCatalog.Find(id)!=null){var local=profile.LocalStage?.Invoke(id)??IntPtr.Zero;if(local==IntPtr.Zero)log($"Local replay {id} was not prepared; refusing table lookup.");return local;}
   var result=stageDataOriginal!(obj,id,method);try{return profile.Stage(result);}catch(Exception e){log("Stage restoration rejected: "+e.Message);return result;}};
  Install(Plugin.Game("StageStaticDataList"),"GetStage","StageStaticData",new[]{"System.Int32"},stageDataReplacement,out stageDataOriginal);
  UnitInit unitOriginal=null!;
  UnitInit unitReplacement=(obj,data,level,sync,method)=>{
   try {
    bool player=false;for(var klass=IL2CPP.il2cpp_object_get_class(data);klass!=IntPtr.Zero;klass=IL2CPP.il2cpp_class_get_parent(klass))
     if(klass==Plugin.Game("PersonalityStaticData")){player=true;break;}
    int cap=pendingCap>0?pendingCap:combatCap;
    if(player&&cap>0){int capped=RestorationManifest.CappedLevel(level,cap,true);if(capped!=level)log($"Combat identity level capped: {level} -> {capped}");level=capped;}
     if(player&&uptieCap>0){int capped=CombatCaps.Limit(sync,uptieCap);if(capped!=sync)log($"Combat identity Uptie capped: {sync} -> {capped}");sync=capped;}
   }catch(Exception e){log("Level Cap preparation failed: "+e);}
   unitOriginal!(obj,data,level,sync,method);
  };
  Install(Plugin.Game("UnitDataModel_Player"),"InitData","System.Void",new[]{"UnitStaticData","System.Int32","System.Int32"},unitReplacement,out unitOriginal);
   int playerFaction=PlayerFaction();
   EgoInit egoOriginal=null!;
   EgoInit egoReplacement=(obj,faction,data,sync,method)=>{
    if(faction==playerFaction&&threadspinCap>0){int capped=CombatCaps.Limit(sync,threadspinCap);if(capped!=sync)log($"Combat E.G.O Threadspin capped: {sync} -> {capped}");sync=capped;}
    egoOriginal!(obj,faction,data,sync,method);
   };
   Install(Plugin.Game("BattleEgoModel"),".ctor","System.Void",new[]{"UNIT_FACTION","EgoStaticData","System.Int32"},egoReplacement,out egoOriginal);
  foreach(var table in new[]{("EnemyStaticDataList","EnemyStaticData","unit"),("AbnormalityStaticDataList","AbnormalityStaticData","unit"),("AbnormalityPartStaticDataList","AbnormalityPartStaticData","part")})
  {
   SkillGet original=null!;
   SkillGet replacement=(obj,id,method)=>{var result=original!(obj,id,method);if(!UseExData)return result;try{return profile.RestoreData(result,table.Item3,id);}catch(Exception e){log($"Rejected restoration {table.Item3}/{id}: {e.Message}");return result;}};
   Install(Plugin.Game(table.Item1),"GetData",table.Item2,new[]{"System.Int32"},replacement,out original);
  }
  StringGet buffDataOriginal=null!;
  StringGet buffDataReplacement=(obj,id,method)=>{var result=buffDataOriginal!(obj,id,method);if(!active)return result;try{return profile.RestoreData(result,"buff:"+IL2CPP.Il2CppStringToManaged(id),0);}catch(Exception e){log("Rejected buff restoration: "+e.Message);return result;}};
  Install(Plugin.Game("BuffStaticDataList"),"GetData","BuffStaticData",new[]{"System.String"},buffDataReplacement,out buffDataOriginal);
  SkillGet passiveOriginal=null!;
  SkillGet passiveReplacement=(obj,id,method)=>{var result=passiveOriginal!(obj,id,method);try{return profile.RestoreData(result,"passive",id);}catch(Exception e){log("Passive restoration rejected: "+e.Message);return result;}};
  Install(Plugin.Game("PassiveStaticDataList"),"GetData","PassiveStaticData",new[]{"System.Int32"},passiveReplacement,out passiveOriginal);
  FormattedTextGet passiveTextOriginal=null!;
  FormattedTextGet passiveTextReplacement=(obj,args,method)=>{try{obj=profile.RestoreData(obj,"passivetext:"+N.StringField(obj,"id"),0);}catch(Exception e){log("Passive description restoration rejected: "+e.Message);}return passiveTextOriginal!(obj,args,method);};
  Install(Plugin.Game("TextData_Passive"),"GetDesc","System.String",new[]{"System.Object[]"},passiveTextReplacement,out passiveTextOriginal);
  IntGet kongPenaltyOriginal=null!;
  IntGet kongPenaltyReplacement=(obj,method)=>active?15:kongPenaltyOriginal!(obj,method);
  Install(Plugin.Game("SystemAbility_1141LoseDuelPanelty"),"get__STACK","System.Int32",Array.Empty<string>(),kongPenaltyReplacement,out kongPenaltyOriginal);
  ZeroAction staffInit=null!;
  ZeroAction staffInitReplacement=(obj,method)=>{
   staffInit!(obj,method);if(!active)return;
   try {
    U.Write(obj,"_THRESHOLD_STACK",2);U.Write(obj,"_ADD_COIN_SCALE",1);
    U.Write(obj,"_STORONG_THRESHOLD_STACK",4);U.Write(obj,"_ADD_STORONG_COIN_SCALE",2);
    U.Write(obj,"_LOSE_HP_RATIO",.1f);U.Write(obj,"_LEVEL_LIMIT",5);
    U.Write(obj,"_ATTACK_DMG_MULTIPLIER",.2f);U.Write(obj,"_LIMIT_ATTACK_DMG_MULTIPLIER",1f);
   }catch(Exception e){log("Senior Staff passive restoration failed: "+e.Message);}
  };
  Install(Plugin.Game("PassiveAbility_152101"),"Init","System.Void",Array.Empty<string>(),staffInitReplacement,out staffInit);
  void RestoreKromer(IntPtr obj){U.Write(obj,"_hpHealRatio",.15f);U.Write(obj,"_stack",3);U.Write(obj,"_giftCnt",0);U.Write(obj,"_init",(byte)1);}
  ZeroAction kromerInit=null!;
  ZeroAction kromerInitReplacement=(obj,method)=>{kromerInit!(obj,method);if(active)try{RestoreKromer(obj);}catch(Exception e){log("Kromer passive restoration failed: "+e);}};
  Install(Plugin.Game("PassiveAbility_802903"),"InitAbility","System.Void",Array.Empty<string>(),kromerInitReplacement,out kromerInit);
  TimingAction kromerRound=null!;
  TimingAction kromerRoundReplacement=(obj,timing,method)=>{if(active)try{RestoreKromer(obj);}catch(Exception e){log("Kromer passive restoration failed: "+e);}kromerRound!(obj,timing,method);};
  Install(Plugin.Game("PassiveAbility_802903"),"OnRoundStart_After_Event","System.Void",new[]{"BATTLE_EVENT_TIMING"},kromerRoundReplacement,out kromerRound);
  SkillGet skillOriginal=null!;
  SkillGet skillReplacement=(obj,id,method)=>{
   var result=skillOriginal!(obj,id,method);
   if(!UseExData)return result;
   try{var restored=profile.Skill(result);if(restored!=result&&verifiedSkills.Add(id))log($"Restored Ex skill {id}: encounter-specific abilities verified.");return restored;}catch(Exception e){log("Ex skill restoration failed: "+e);active=false;return result;}
  };
  Install(Plugin.Game("SkillStaticDataList"),"GetData","SkillStaticData",new[]{"System.Int32"},skillReplacement,out skillOriginal);
  SkillGet abilityOriginal=null!;
  SkillGet abilityReplacement=(obj,level,method)=>{
   try{if(UseExData)obj=profile.Skill(obj);}catch(Exception e){log("Ex ability restoration failed: "+e);}
   return abilityOriginal!(obj,level,method);
  };
  Install(Plugin.Game("SkillStaticData"),"GetAbilityScript","System.Collections.Generic.List<AbilityData>",new[]{"System.Int32"},abilityReplacement,out abilityOriginal);
  TextGet textOriginal=null!;
  TextGet textReplacement=(obj,method)=>{
   var result=textOriginal!(obj,method);
   try{return UseExData?profile.Text(result,U.Read<int>(obj,"id")):result;}
   catch(Exception e){log("Ex description restoration failed: "+e);return result;}
  };
  Install(Plugin.Game("SkillStaticData"),"GetSkillTextData","TextData_Skill",Array.Empty<string>(),textReplacement,out textOriginal);
  LevelTextGet descOriginal=null!;
  LevelTextGet descReplacement=(obj,level,args,method)=>{
   try{if(UseExData&&int.TryParse(N.StringField(obj,"id"),out int id))obj=profile.Text(obj,id);}
   catch(Exception e){log("Ex preview description restoration failed: "+e);}
   return descOriginal!(obj,level,args,method);
  };
  Install(Plugin.Game("TextData_Skill"),"GetDesc","System.String",new[]{"System.Int32","System.Object[]"},descReplacement,out descOriginal);
  foreach(var name in new[]{"GetRawDesc","GetSkillTextByGaksungLevel"})
  {
   SkillGet original=null!;
   SkillGet replacement=(obj,level,method)=>{try{if(int.TryParse(N.StringField(obj,"id"),out int id))obj=profile.Text(obj,id);}catch(Exception e){log("Skill text restoration failed: "+e.Message);}return original!(obj,level,method);};
   Install(Plugin.Game("TextData_Skill"),name,name=="GetRawDesc"?"System.String":"TextData_SkillPerLevel",new[]{"System.Int32"},replacement,out original);
  }
  foreach(var name in new[]{"GetDesc","GetSummary"})
  {
   FormattedTextGet buffOriginal=null!;
   FormattedTextGet buffReplacement=(obj,args,method)=>{
    try{if(UseExData)obj=profile.BuffText(obj);}catch(Exception e){log("Ex buff description restoration failed: "+e);}
    return buffOriginal!(obj,args,method);
   };
   Install(Plugin.Game("TextData_Buf"),name,"System.String",new[]{"System.Object[]"},buffReplacement,out buffOriginal);
  }
  SpeedMul speedOriginal=null!;
  var capField=U.Field(Plugin.Game("BuffAbility_IrritationDiffSpeedAtkDmgMul"),"_value");
  if(Signature.Type(IL2CPP.il2cpp_field_get_type(capField))!="System.Single")throw new InvalidOperationException("Unexpected damage-cap field type.");
  SpeedMul speedReplacement=(obj,owner,target,method)=>{
   if(!active)return speedOriginal!(obj,owner,target,method);
   float cap=U.Read<float>(obj,"_value");
   try{U.Write(obj,"_value",.4f);return speedOriginal!(obj,owner,target,method);}
   finally{U.Write(obj,"_value",cap);}
  };
  Install(Plugin.Game("BuffAbility_IrritationDiffSpeedAtkDmgMul"),"CalculateMul","System.Single",new[]{"BattleUnitModel","BattleUnitModel"},speedReplacement,out speedOriginal);
  }catch {Dispose();throw;}
 }
 static unsafe int PlayerFaction()
 {
  IntPtr iter=IntPtr.Zero,field;var klass=Plugin.Game("UNIT_FACTION");
  while((field=IL2CPP.il2cpp_class_get_fields(klass,ref iter))!=IntPtr.Zero)
   if(string.Equals(Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field)),"Player",StringComparison.OrdinalIgnoreCase)){int value=0;IL2CPP.il2cpp_field_static_get_value(field,&value);return value;}
  throw new MissingMemberException("UNIT_FACTION.Player");
 }
 void Install<T>(IntPtr klass,string name,string returnType,string[] parameters,T replacement,out T original) where T:Delegate
 {
  var method=Signature.Check(klass,name,returnType,parameters);
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(method),replacement,out original));
  delegates.Add(replacement);delegates.Add(original);
 }
 public void Dispose(){active=false;foreach(var h in hooks.AsEnumerable().Reverse())h.Dispose();hooks.Clear();delegates.Clear();}
}
internal static class Signature
{
 public static string Type(IntPtr t)=>Marshal.PtrToStringAnsi(IL2CPP.il2cpp_type_get_name(t))??"?";
 public static IntPtr Check(IntPtr klass,string name,string result,params string[] args)
 {
  var method=U.Method(klass,name,args.Length);
  var actual=Enumerable.Range(0,args.Length).Select(i=>Type(IL2CPP.il2cpp_method_get_param(method,(uint)i))).ToArray();
  if(Type(IL2CPP.il2cpp_method_get_return_type(method))!=result||!actual.SequenceEqual(args))throw new InvalidOperationException("Game API signature changed: "+name+"("+string.Join(",",actual)+")");
  return method;
 }
}
