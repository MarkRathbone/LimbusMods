using Il2CppInterop.Runtime;
namespace LimbusCompanyUnnerfed;

internal sealed class NativeReplayStages : IDisposable
{
 readonly CombatProfile profiles;
 readonly Dictionary<int,IntPtr> stages=new();
 readonly List<IntPtr> handles=new();
 public NativeReplayStages(CombatProfile profiles)=>this.profiles=profiles;
 public bool TryGet(int id,out IntPtr stage)=>stages.TryGetValue(id,out stage);
 IntPtr Keep(IntPtr value){if(value!=IntPtr.Zero)handles.Add(IL2CPP.il2cpp_gchandle_new(value,false));return value;}
 void EmptyList(IntPtr owner,string field)
 {
  var list=U.Read<IntPtr>(owner,field);
  if(list!=IntPtr.Zero)N.Reference(owner,field,Keep(N.New(IL2CPP.il2cpp_object_get_class(list))));
 }
 public IntPtr Build(ReplayEncounter encounter,IntPtr source)
 {
  if(source==IntPtr.Zero||U.Read<int>(source,"id")!=encounter.SourceId)
   throw new InvalidDataException("Replay source stage does not match its catalog entry.");
  if(!CombatProfile.SupportedStages.Contains(encounter.SourceId))
   throw new InvalidDataException("Replay source has no reviewed Unnerfed profile.");
  if(stages.TryGetValue(encounter.Id,out var result))return result;
  result=Keep(N.Clone(profiles.Stage(source)));
  U.Write(result,"id",encounter.Id);U.Write(result,"staminaCost",0);
  foreach(var field in new[]{"hasGoldenBough","hasGoldenBoughGray","unlockDanteAbility"})
   U.Write(result,field,(byte)0);
  EmptyList(result,"rewardList");EmptyList(result,"questlist");
  var story=U.Read<IntPtr>(result,"story");
  if(story!=IntPtr.Zero)
  {
   story=Keep(N.Clone(story));EmptyList(story,"enter");EmptyList(story,"exit");
   N.Reference(result,"story",story);
  }
  var continuation=U.Read<IntPtr>(result,"stageContinueInfo");
  if(continuation!=IntPtr.Zero)
  {
   continuation=Keep(N.Clone(continuation));
   U.Write(continuation,"isContinueEnabled",(byte)0);U.Write(continuation,"isContinueAssist",(byte)0);
   U.Write(continuation,"continueDataId",0);U.Write(continuation,"ver",0);
   N.Reference(result,"stageContinueInfo",continuation);
  }
  N.Reference(result,"_battleWaveData",IntPtr.Zero);
  N.Reference(result,"_enemyDataDictionary",IntPtr.Zero);
  U.Call(result,"SetEnemyData");
  stages.Add(encounter.Id,result);return result;
 }
 public IntPtr ForCombat(ReplayEncounter encounter)
 {
  if(!stages.TryGetValue(encounter.Id,out var preview))throw new InvalidOperationException("Replay preview expired.");
  var combat=Keep(N.Clone(preview));
  U.Write(combat,"id",encounter.SourceId);
  N.Reference(combat,"_battleWaveData",IntPtr.Zero);
  N.Reference(combat,"_enemyDataDictionary",IntPtr.Zero);
  U.Call(combat,"SetEnemyData");
  return combat;
 }
 public void Dispose(){foreach(var handle in handles)IL2CPP.il2cpp_gchandle_free(handle);handles.Clear();stages.Clear();}
}
