using System.Globalization;
using System.Text.Json;
namespace LimbusCompanyUnnerfed;

internal static class HistoricalTiers
{
 internal sealed record Entry(string Key,string Source);
 public static readonly IReadOnlyList<Entry> Entries=Load();
 static readonly HashSet<string> keys=Entries.Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
 static readonly HashSet<int> ids=Entries.Select(x=>int.Parse(x.Key.Split(':')[0],CultureInfo.InvariantCulture)).ToHashSet();
 static Entry[] Load()
 {
  using var stream=typeof(HistoricalTiers).Assembly.GetManifestResourceStream("LimbusCompanyUnnerfed.pre-tier4-encounters.json")!;
  return JsonSerializer.Deserialize<Entry[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 }
 public static bool MayMatch(int id)=>ids.Contains(ReplayCatalog.RestorationStageId(id));
 public static string Key(int id,IEnumerable<IEnumerable<int>> waves)=>
  ReplayCatalog.RestorationStageId(id).ToString(CultureInfo.InvariantCulture)+":"+
  string.Join(";",waves.Select(w=>string.Join(",",w.Select(x=>x.ToString(CultureInfo.InvariantCulture)))));
 public static bool BeforeTier4(int id,IEnumerable<IEnumerable<int>> waves)=>keys.Contains(Key(id,waves));
}
