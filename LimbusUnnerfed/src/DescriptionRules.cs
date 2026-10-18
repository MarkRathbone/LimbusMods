namespace LimbusCompanyUnnerfed;

internal static class DescriptionRules
{
 public static string RestoreBuff(string id,string description)=>id is "Irritation" or "HugeIrritation"
  ?description.Replace("(max 20%)","(max 40%)"):description;
 public static string Restore(int id,string description)
 {
  if(id is <114505 or >114508)return description;
  var lines=description.Replace("\r\n","\n").Split('\n').Where(line=>
   !(id is 114506 or 114507 && ClashLose(line) && (line.Contains("[ResultReduction]")||line.Contains("[Paralysis]")||line.Contains("Power Down")||line.Contains("Paralyze"))) &&
   !(id==114505 && ClashLose(line) && line.Contains("10")));
  var result=string.Join("\n",lines);
  if(id==114508&&!result.Contains("Heal 10 SP"))result="[WhenUse] Heal 10 SP\n- At less than 0 SP, heal 5 additional SP\n"+result;
  return result;
 }
 static bool ClashLose(string line)=>line.Contains("[DefeatDuel]")||line.Contains("[Clash Lose]");
}
