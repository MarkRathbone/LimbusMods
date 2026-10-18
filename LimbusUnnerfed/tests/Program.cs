using LimbusCompanyUnnerfed;

void Equal(string expected,string actual){if(expected!=actual)throw new Exception($"Expected: {expected}\nActual: {actual}");}
const string retained="[DefeatDuel] Deal -80% damage\n[EndSkill] [FullReload]";
Equal(retained,DescriptionRules.Restore(114506,"[DefeatDuel] Gain 4 [ResultReduction]\n[DefeatDuel] Gain 1 [Paralysis]\n"+retained));
Equal(retained,DescriptionRules.Restore(114507,"[DefeatDuel] Gain 2 [ResultReduction]\n[DefeatDuel] Gain 6 [Paralysis]\n"+retained));
Equal("Coin Power +1",DescriptionRules.Restore(114505,"[DefeatDuel] Lose 10 SP\nCoin Power +1"));
var reload=DescriptionRules.Restore(114508,"[DuelCounter]\n[DefeatDuel] Lose 1 [BulletPropellant]");
Equal(reload,DescriptionRules.Restore(114508,reload));
if(!reload.Contains("Heal 10 SP")||!reload.Contains("5 additional SP")||!reload.Contains("Lose 1 [BulletPropellant]"))throw new Exception("Reload restoration incomplete.");
const string normal="[DefeatDuel] Gain 4 [ResultReduction]";
Equal(normal,DescriptionRules.Restore(124106,normal));
Equal("every 20% missing HP; Speed bonus (max 40%)",DescriptionRules.RestoreBuff("Irritation","every 20% missing HP; Speed bonus (max 20%)"));
Equal("Speed bonus (max 40%)",DescriptionRules.RestoreBuff("HugeIrritation","Speed bonus (max 20%)"));
Equal("Speed bonus (max 20%)",DescriptionRules.RestoreBuff("HugeIrritationReflectrial","Speed bonus (max 20%)"));
Equal(retained,DescriptionRules.Restore(114506,"<color=red>[Clash Lose]</color> Gain 4 <link=ResultReduction>Power Down</link>\n<color=red>[Clash Lose]</color> Gain 1 <link=Paralysis>Paralyze</link>\n"+retained));
Console.WriteLine("10 description regression checks passed.");
var manifest=RestorationManifest.Embedded();
if(manifest.Patches.Count<227)throw new Exception("Missing reviewed restoration coverage.");
if(manifest.Patches.Any(p=>p.Id==817104||p.Id==1143||p.Id==10645))throw new Exception("Excluded repair/encounter was restored.");
if(manifest.Patches.Any(p=>p.Path.Contains("attributeResistList")&&int.Parse(p.Path[Array.IndexOf(p.Path,"attributeResistList")+1])>=7))throw new Exception("Unused WHITE/BLACK resistances included.");
if(manifest.For("skill",9002401).Count!=3||manifest.For("part",817301).Count==0)throw new Exception("Missing verified Aida/Erlking changes.");
foreach(var p in manifest.Patches)
 if(p.Path.Length==0||p.Expected.GetRawText()==p.Restored.GetRawText())throw new Exception("Invalid no-op reversal.");
foreach(var example in new[]{(65,55,true,55),(40,55,true,40),(65,55,false,65),(65,0,true,65)})
 if(RestorationManifest.CappedLevel(example.Item1,example.Item2,example.Item3)!=example.Item4)throw new Exception("Level cap changed an uncapped/lower-level unit.");
Console.WriteLine("Restoration manifest coverage/exclusions and level-cap boundary checks passed.");

if(ReplayCatalog.Encounters.Select(x=>x.Id).Distinct().Count()!=ReplayCatalog.Encounters.Count)
 throw new Exception("Replay IDs collide.");
foreach(var encounter in ReplayCatalog.Encounters)
{
 var source=TestFixtures.Replay(encounter);
 var before=source.ToJsonString();
 if(ReplayCatalog.MatchesSource(encounter,encounter.SourceId,"Human",new[]{encounter.RequiredScript})||
    ReplayCatalog.MatchesSource(encounter,encounter.SourceId,"Abnormality",new[]{"unrelated-stage-script"}))
  throw new Exception("Replay source lookup accepts a reused stage ID from the wrong encounter table.");
 var local=ReplayCatalog.LocalData(encounter,source);
 if(source.ToJsonString()!=before)throw new Exception("Replay construction mutated source data.");
 foreach(var field in new[]{"waveList","stageScriptList","abnormalityEventList","participantInfo","recommendedLevel","eventScriptName","isBatonPassOn"})
  if(source[field]?.ToJsonString()!=local[field]?.ToJsonString())throw new Exception($"Replay lost encounter behavior: {encounter.Name}/{field}");
 if(local["staminaCost"]!.GetValue<int>()!=0||local["rewardList"]!.AsArray().Count!=0||local["questlist"]!.AsArray().Count!=0)
  throw new Exception("Replay inherited progression or cost.");
 if(local["stageContinueInfo"]!["isContinueEnabled"]!.GetValue<bool>())
  throw new Exception("Replay inherited paid continuation.");
 if(local["story"] is System.Text.Json.Nodes.JsonObject story&&(story["enter"]!.AsArray().Count!=0||story["exit"]!.AsArray().Count!=0))
  throw new Exception("Replay inherited story entry/exit progression.");
 if(!manifest.StageIds.Contains(ReplayCatalog.RestorationStageId(encounter.Id)))
  throw new Exception("Synthetic replay ID bypasses native restoration hooks.");
 try {ReplayCatalog.LocalData(encounter,new System.Text.Json.Nodes.JsonObject{["id"]=0});throw new Exception("Wrong source accepted.");}
 catch(InvalidDataException){}
}
if(ReplayCatalog.RestorationStageId(10830)!=10830||ReplayCatalog.Find(10830)!=null)
 throw new Exception("Real story stage interpreted as a replay.");
Console.WriteLine($"{ReplayCatalog.Encounters.Count} replay encounters: source isolation, wave/script fidelity, free/no-reward policy and Unnerfed routing passed.");

var customCaps=new CombatCaps(true,27,true,2,true,1);
if(!customCaps.Valid||customCaps.ResolveLevel(55)!=27||customCaps.ResolveLevel(63)!=27)
 throw new Exception("Custom level cap followed node difficulty instead of the selected value.");
var autoCaps=customCaps with{Level=0};
if(autoCaps.ResolveLevel(55)!=55||autoCaps.ResolveLevel(63)!=63)
 throw new Exception("Automatic level cap retained a previous node difficulty.");
if((customCaps with{LevelEnabled=false}).ResolveLevel(55)!=0)
 throw new Exception("Disabled level cap is still active.");
foreach(var example in new[]{(65,27,27),(20,27,20),(5,2,2),(1,2,1),(4,5,4),(4,0,4)})
 if(CombatCaps.Limit(example.Item1,example.Item2)!=example.Item3)
  throw new Exception("A cap raised an owned level/tier or changed an uncapped value.");
foreach(var tier in new[]{1,2,3,4,5})
 if(!(customCaps with{Uptie=tier,Threadspin=tier}).Valid)throw new Exception("Supported cap tier rejected.");
if((customCaps with{Level=-1}).Valid||(customCaps with{Uptie=0}).Valid||(customCaps with{Threadspin=6}).Valid)
 throw new Exception("Invalid combat caps accepted.");
if(!(customCaps with{Level=int.MaxValue}).Valid)
 throw new Exception("Arbitrary positive level cap rejected.");
Console.WriteLine("Custom/automatic level caps, independent tier bounds, and never-upgrade cap checks passed.");

foreach(var sample in new[]{("",0,'0',false),("",0,'-',false),("",0,'5',true),("5",1,'0',true),("5",0,'0',false),("214748364",9,'8',false)})
 if(CombatCaps.ValidLevelInsertion(sample.Item1,sample.Item2,sample.Item3)!=sample.Item4)throw new Exception("Positive level input validation failed.");
Console.WriteLine("Positive-only manual level input checks passed; Sync remains independent.");

foreach(var stage in new[]{10218,10348,10506,10509,10515,10516,10635,10641,10644,10645,10648,10827,10830,11041})
 if(!ReplayCatalog.Encounters.Any(x=>x.SourceId==stage))throw new Exception($"Restored boss has no test node: {stage}");
var aida=ReplayCatalog.Encounters.Single(x=>x.SourceId==10218);
if(ReplayCatalog.MatchesSource(aida,10218,"Abnormality",new[]{"Casino_Eyed"})||!ReplayCatalog.MatchesSource(aida,10218,"Normal",new[]{"Casino_Eyed"}))throw new Exception("Aida accepted the wrong encounter table.");
Console.WriteLine("All reviewed restored boss encounters have local test stages, including Aida's Normal battle type.");

var syncedCaps=new CombatCaps(true,0,true,5,true,5,true,true);
if(syncedCaps.ResolveUptie(true)!=3||syncedCaps.ResolveThreadspin(true)!=3||
   syncedCaps.ResolveUptie(false)!=4||syncedCaps.ResolveThreadspin(false)!=4)
 throw new Exception("Historical Sync selected the wrong encounter era.");
if((syncedCaps with{UptieEnabled=false}).ResolveUptie(true)!=0||
   (syncedCaps with{ThreadspinEnabled=false}).ResolveThreadspin(true)!=0||
   (syncedCaps with{UptieSync=false}).ResolveUptie(true)!=5||
   (syncedCaps with{ThreadspinSync=false}).ResolveThreadspin(true)!=5)
 throw new Exception("Sync ignored disabled caps or lost the saved manual tier.");
foreach(var encounter in ReplayCatalog.Encounters)
{
 var source=TestFixtures.Replay(encounter);
 var waves=source["waveList"]!.AsArray().Select(w=>w!["unitList"]!.AsArray().Select(u=>u!["unitID"]!.GetValue<int>()).ToArray()).ToArray();
 bool expected=encounter.SourceId is 10218 or 10348;
 if(HistoricalTiers.BeforeTier4(encounter.SourceId,waves)!=expected||HistoricalTiers.BeforeTier4(encounter.Id,waves)!=expected)
  throw new Exception($"Historical Sync mismatch for real/replay {encounter.Name}.");
}
foreach(var file in new[]{"battle-exp-dungeon-01.json","battle-thread-dungeon-01.json","storybattle91-3.json","battle-railway-dungeon-01.json"})
{
 using var doc=System.Text.Json.JsonDocument.Parse(TestFixtures.HistoricalTable(file).ToJsonString());
 foreach(var stage in doc.RootElement.GetProperty("list").EnumerateArray())
 {
  int id=stage.GetProperty("id").GetInt32();
  var waves=stage.GetProperty("waveList").EnumerateArray().Select(w=>w.GetProperty("unitList").EnumerateArray().Select(u=>u.GetProperty("unitID").GetInt32()).ToArray()).ToArray();
  if(!HistoricalTiers.BeforeTier4(id,waves)||HistoricalTiers.BeforeTier4(id,new[]{new[]{int.MaxValue}}))
   throw new Exception($"Historical original Lux/event/Railway classification failed for {file}:{id}.");
 }
}
Console.WriteLine("Historical tier Sync: early bosses/replays, Lux, Hell's Chicken, Railway, reused-ID isolation, manual values and disabled caps passed.");

foreach(var patch in manifest.Patches)
{
 if(patch.Kind=="stage"&&patch.Id!=10218&&patch.Path[0]=="stageScriptList")
  throw new Exception("Restoration replaced a native boss/identity interaction stage script.");
 if((patch.Kind=="unit"||patch.Kind=="part")&&patch.Path.Any(x=>x.Contains("passive",StringComparison.OrdinalIgnoreCase)))
  throw new Exception("Restoration replaced native passive assignments.");
 if(patch.Kind=="skill"&&patch.Id is >=800000 and <820000)
 {
  var before=InteractionScripts(patch.Expected).GroupBy(x=>x).ToDictionary(x=>x.Key,x=>x.Count());
  var after=InteractionScripts(patch.Restored).GroupBy(x=>x).ToDictionary(x=>x.Key,x=>x.Count());
  foreach(var script in before)
   if(!after.TryGetValue(script.Key,out int count)||count<script.Value)
    throw new Exception($"Canto III/V/VI restoration removed a native skill callback: {patch.Id}/{script.Key}");
 }
}
foreach(var entry in new[]{(10348,"Cromer"),(10516,"8106"),(10648,"8173")})
{
 var source=TestFixtures.Replay(ReplayCatalog.Encounters.Single(e=>e.SourceId==entry.Item1));
 var replay=ReplayCatalog.LocalData(ReplayCatalog.Encounters.Single(e=>e.SourceId==entry.Item1),source);
 if(!replay["stageScriptList"]!.AsArray().Any(x=>x!.GetValue<string>()==entry.Item2))
  throw new Exception("Replay lost its native identity-interaction controller.");
}
Console.WriteLine("Capstone interaction preservation: native stage scripts/passive assignments and Canto III/V/VI skill callbacks retained.");
static IEnumerable<string> InteractionScripts(System.Text.Json.JsonElement element)
{
 if(element.ValueKind==System.Text.Json.JsonValueKind.Object)
  foreach(var property in element.EnumerateObject())
  {
   if(property.Name is "scriptName" or "ability")yield return property.Value.GetString()!;
   else foreach(var script in InteractionScripts(property.Value))yield return script;
  }
 else if(element.ValueKind==System.Text.Json.JsonValueKind.Array)
  foreach(var item in element.EnumerateArray())foreach(var script in InteractionScripts(item))yield return script;
}
