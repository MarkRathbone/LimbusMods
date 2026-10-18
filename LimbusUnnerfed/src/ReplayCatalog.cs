using System.Text.Json.Nodes;
namespace LimbusCompanyUnnerfed;

internal sealed record ReplayEncounter(int Id,int SourceId,string Name,string RequiredScript,string StageType="Abnormality");

internal static class ReplayCatalog
{
 public static readonly IReadOnlyList<ReplayEncounter> Encounters=Array.AsReadOnly(new[]{
  new ReplayEncounter(880001,10348,"Kromer","Cromer"),
  new ReplayEncounter(880002,10515,"Ahab — Pequod Crew","8105"),
  new ReplayEncounter(880003,10516,"Ahab — Gasharpoon","8106"),
  new ReplayEncounter(880004,10827,"Kong Qiu","1137"),
  new ReplayEncounter(880005,11041,"Buffet","1517"),
  new ReplayEncounter(880006,10218,"Aida","Casino_Eyed","Normal"),
  new ReplayEncounter(880007,10509,"Ambling Pearl","8101"),
  new ReplayEncounter(880008,10506,"Queequeg ? Solo","a1c5p1_4_2"),
  new ReplayEncounter(880009,10635,"Heartbroken Heathcliff","8166"),
  new ReplayEncounter(880010,10644,"Erlking ? 6-44","8172"),
  new ReplayEncounter(880011,10645,"Nelly","8171"),
  new ReplayEncounter(880012,10648,"Erlking / Every Catherine","8173"),
  new ReplayEncounter(880013,10830,"Lei Heng","1145"),
  new ReplayEncounter(880014,10641,"The Wild Hunt","WH")
 });
 public static ReplayEncounter? Find(int id)=>Encounters.FirstOrDefault(x=>x.Id==id);
 public static int RestorationStageId(int id)=>Find(id)?.SourceId??id;
 public static bool MatchesSource(ReplayEncounter encounter,int id,string stageType,IEnumerable<string> scripts)
  =>id==encounter.SourceId&&stageType==encounter.StageType&&scripts.Contains(encounter.RequiredScript);

 public static JsonObject LocalData(ReplayEncounter encounter,JsonObject source)
 {
  if(!MatchesSource(encounter,source["id"]?.GetValue<int>()??0,source["stageType"]?.GetValue<string>()??"",
   (source["stageScriptList"] as JsonArray)?.Select(x=>x!.GetValue<string>())??Array.Empty<string>()))
   throw new InvalidDataException($"Wrong source for {encounter.Name}.");
  var clone=JsonNode.Parse(source.ToJsonString())!.AsObject();
  clone["id"]=encounter.Id;
  clone["staminaCost"]=0;
  clone["rewardList"]=new JsonArray();
  clone["questlist"]=new JsonArray();
  clone["hasGoldenBough"]=false;
  clone["hasGoldenBoughGray"]=false;
  clone["unlockDanteAbility"]=false;
  clone["stageContinueInfo"]=new JsonObject{
   ["continueDataId"]=0,["isContinueEnabled"]=false,
   ["isContinueAssist"]=false,["ver"]=0
  };
  if(clone["story"] is JsonObject story)
  {
   story["enter"]=new JsonArray();story["exit"]=new JsonArray();
  }
  return clone;
 }
}
