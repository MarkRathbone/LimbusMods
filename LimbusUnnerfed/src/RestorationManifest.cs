using System.Text.Json;
namespace LimbusCompanyUnnerfed;

internal sealed record DataReversal(string Kind, int Id, string[] Path, JsonElement Expected, JsonElement Restored, string Source);
internal sealed class RestorationManifest
{
 public IReadOnlyList<DataReversal> Patches { get; }
 public HashSet<int> StageIds {get;}
 readonly Dictionary<(string,int),DataReversal[]> records;
 public RestorationManifest(Stream stream)
 {
  using var doc=JsonDocument.Parse(stream);
  if(doc.RootElement.GetProperty("schemaVersion").GetInt32()!=1)throw new InvalidDataException("Unknown restoration schema.");
  StageIds=doc.RootElement.GetProperty("stageIds").EnumerateArray().Select(x=>x.GetInt32()).ToHashSet();
  Patches=doc.RootElement.GetProperty("patches").EnumerateArray().Select(p=>new DataReversal(
   p.GetProperty("kind").GetString()!,p.GetProperty("id").GetInt32(),
   p.GetProperty("path").EnumerateArray().Select(x=>x.GetString()!).ToArray(),
   p.GetProperty("expected").Clone(),p.GetProperty("restored").Clone(),p.GetProperty("source").GetString()!)).ToArray();
  records=Patches.GroupBy(p=>(p.Kind,p.Id)).ToDictionary(g=>g.Key,g=>g.ToArray());
  foreach(var group in records.Values)
   if(group.Select(p=>string.Join('/',p.Path)).Distinct().Count()!=group.Length)throw new InvalidDataException("Duplicate reversal path.");
 }
 public IReadOnlyList<DataReversal> For(string kind,int id)=>records.TryGetValue((kind,id),out var p)?p:Array.Empty<DataReversal>();
 public static RestorationManifest Embedded()=>new(typeof(RestorationManifest).Assembly.GetManifestResourceStream("LimbusCompanyUnnerfed.restorations.json")??throw new FileNotFoundException("Restoration manifest"));
 public static int CappedLevel(int actual,int difficulty,bool enabled)=>enabled&&difficulty>0?Math.Min(actual,difficulty):actual;
}
