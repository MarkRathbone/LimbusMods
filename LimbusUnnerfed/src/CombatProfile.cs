using Il2CppInterop.Runtime;
namespace LimbusCompanyUnnerfed;
internal sealed class CombatProfile : IDisposable
{
 readonly NativeRestorations restorations=new();
 public Action<IntPtr>? ObserveStageTable;
 public Func<int,IntPtr>? LocalStage;
 public static readonly HashSet<int> SupportedStages=RestorationManifest.Embedded().StageIds;
 public IntPtr RestoreData(IntPtr original,string kind,int id)=>restorations.Restore(original,kind,id);
 readonly Dictionary<IntPtr,IntPtr> stages=new();
 readonly Dictionary<IntPtr,IntPtr> skills=new();
 readonly Dictionary<(IntPtr,int),IntPtr> texts=new();
 readonly Dictionary<IntPtr,IntPtr> buffTexts=new();
 readonly List<IntPtr> handles=new();
 IntPtr Keep(IntPtr obj){handles.Add(IL2CPP.il2cpp_gchandle_new(obj,false));return obj;}
 public static bool IsStoryLeiHeng(IntPtr stage)
 {
  if(stage==IntPtr.Zero||U.Read<int>(stage,"id")!=10830)return false;
  var scripts=U.Read<IntPtr>(stage,"stageScriptList");
  return N.Items(scripts).Any(x=>IL2CPP.Il2CppStringToManaged(x)=="1145");
 }
 public IntPtr Stage(IntPtr original)
 {
  if(original==IntPtr.Zero)return original;
  if(stages.TryGetValue(original,out var cached))return cached;
  if(stages.ContainsValue(original))return original;
  var result=restorations.Restore(original,"stage",U.Read<int>(original,"id"));
  if(result==original)return original;
  N.Reference(result,"_battleWaveData",IntPtr.Zero);
  N.Reference(result,"_enemyDataDictionary",IntPtr.Zero);
  U.Call(result,"SetEnemyData");
  stages.Add(original,result);
  return result;
 }
 static unsafe void SetItem(IntPtr list,int index,IntPtr value){void** a=stackalloc void*[2];a[0]=&index;a[1]=(void*)value;U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(list),"set_Item",2),list,a);}
 public IntPtr Skill(IntPtr original)
 {
  if(original==IntPtr.Zero)return original;
  int id=U.Read<int>(original,"id");
  original=restorations.Restore(original,"skill",id);
  if(id is <114505 or >114508)return original;
  if(skills.ContainsValue(original))return original;
  if(skills.TryGetValue(original,out var cached))return cached;
  var result=Keep(N.Clone(original));
  var levels=Keep(N.CopyList(U.Read<IntPtr>(original,"skillData")));
  for(int index=0;index<U.Get<int>(levels,"Count");index++)
  {
   var level=Keep(N.Clone(N.Item(levels,index)));
   var originalAbilities=U.Read<IntPtr>(level,"abilityScriptList");
   var abilities=Keep(N.New(IL2CPP.il2cpp_object_get_class(originalAbilities)));
   IntPtr empty=IntPtr.Zero;int removed=0;
   foreach(var ability in N.Items(originalAbilities))
   {
    var script=N.StringField(ability,"scriptName");
    if(script=="EmptyBody")empty=ability;
    if((id==114505&&script=="GiveMpDmgOnLoseDuelSelf10") || (id is 114506 or 114507 && script=="GiveBuffOnLoseDuel")) {removed++;continue;}
    U.ObjectArg(abilities,"Add",ability);
   }
   int expected=id==114505?1:id==114508?0:2;
   if(removed!=expected)throw new InvalidOperationException($"Skill {id} does not match the verified hotfix data ({removed}/{expected}).");
   if(id==114508)
   {
    if(empty==IntPtr.Zero)throw new MissingMemberException("Reload ability template");
    var heal=Keep(N.Clone(empty));
    N.Reference(heal,"scriptName",IL2CPP.ManagedStringToIl2Cpp("HealMpOnUseSelf10IfUnderZeroThenMore"));
    U.Write(heal,"value",5f);U.Write(heal,"turnLimit",1);
    U.ObjectArg(abilities,"Add",heal);
   }
   N.Reference(level,"abilityScriptList",abilities);SetItem(levels,index,level);
  }
  N.Reference(result,"skillData",levels);skills.Add(original,result);return result;
 }
 public unsafe IntPtr Preview(IntPtr original)
 {
  var stage=U.Read<IntPtr>(original,"_stageStaticData");
  var exStage=Stage(stage);
  if(exStage==stage)throw new InvalidOperationException("Ex preview is only available for story 8-30.");
  var result=Keep(N.Clone(original));
  N.Reference(result,"_stageStaticData",exStage);
  var source=U.Read<IntPtr>(original,"_stageEnemyInfoData");
  var klass=Plugin.Game("StageEnemyInfoUIData","LimbusCompany.GameUILibs.StageUILibs.EnemyDataLibs");
  var ctor=Signature.Check(klass,".ctor","System.Void","StageStaticData","STAGE_BATTLE_TYPE","STAGE_ENEMY_TYPE","System.Boolean","System.Boolean","UnitInformationOptionData");
  var enemyInfo=Keep(IL2CPP.il2cpp_object_new(klass));
  int battleType=U.Read<int>(source,"_battleType"),enemyType=U.Read<int>(source,"_enemyType");
  byte cleared=UnboxByte(U.Call(source,"IsClearedStage")),entered=UnboxByte(U.Call(source,"IsEnteredStage"));
  var options=U.Read<IntPtr>(source,"OptionData");
  void** a=stackalloc void*[6];a[0]=(void*)exStage;a[1]=&battleType;a[2]=&enemyType;a[3]=&cleared;a[4]=&entered;a[5]=(void*)options;
  U.Invoke(ctor,enemyInfo,a);
  int enemyCount=U.Get<int>(U.Call(enemyInfo,"GetStageEnemyInfoDatas"),"Count");
  if(enemyCount<1)throw new InvalidOperationException("Unnerfed preview has no enemies.");
  N.Reference(result,"_stageEnemyInfoData",enemyInfo);
  var continueData=U.Read<IntPtr>(original,"_continueData");
  if(continueData!=IntPtr.Zero){continueData=Keep(N.Clone(continueData));U.Write(continueData,"canContinue",(byte)0);N.Reference(result,"_continueData",continueData);}
  var title=N.StringField(original,"_title");N.Reference(result,"_title",IL2CPP.ManagedStringToIl2Cpp(title+"  [Unnerfed]"));
  return result;
 }
 static unsafe byte UnboxByte(IntPtr obj)=>*(byte*)IL2CPP.il2cpp_object_unbox(obj);
 public IntPtr Text(IntPtr original,int id)
 {
  if(original==IntPtr.Zero)return original;
  original=restorations.Restore(original,"text",id);
  if(id is <114505 or >114508)return original;
  if(texts.ContainsValue(original))return original;
  if(texts.TryGetValue((original,id),out var cached))return cached;
  var result=Keep(N.Clone(original));
  var levels=Keep(N.CopyList(U.Read<IntPtr>(original,"levelList")));
  for(int i=0;i<U.Get<int>(levels,"Count");i++)
  {
   var level=Keep(N.Clone(N.Item(levels,i)));
   string desc=N.StringField(level,"desc");
   desc=DescriptionRules.Restore(id,desc);
   N.Reference(level,"desc",IL2CPP.ManagedStringToIl2Cpp(desc));
   var raw=N.StringField(level,"rawDesc");
   N.Reference(level,"rawDesc",IL2CPP.ManagedStringToIl2Cpp(DescriptionRules.Restore(id,string.IsNullOrEmpty(raw)?desc:raw)));
   SetItem(levels,i,level);
  }
  N.Reference(result,"levelList",levels);texts.Add((original,id),result);return result;
 }
 public IntPtr BuffText(IntPtr original)
 {
  if(original==IntPtr.Zero)return original;
  var id=N.StringField(original,"id");
  original=restorations.Restore(original,"bufftext:"+id,0);
  if(id is not ("Irritation" or "HugeIrritation"))return original;
  if(buffTexts.ContainsValue(original))return original;
  if(buffTexts.TryGetValue(original,out var cached))return cached;
  var result=Keep(N.Clone(original));
  foreach(var field in new[]{"desc","summary"})
   N.Reference(result,field,IL2CPP.ManagedStringToIl2Cpp(DescriptionRules.RestoreBuff(id,N.StringField(original,field))));
  buffTexts.Add(original,result);return result;
 }
 public void Dispose(){restorations.Dispose();foreach(var h in handles)IL2CPP.il2cpp_gchandle_free(h);handles.Clear();stages.Clear();skills.Clear();texts.Clear();buffTexts.Clear();}
}
