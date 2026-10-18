using System.Text.Json.Nodes;
using LimbusCompanyUnnerfed;

internal static class TestFixtures
{
    public static JsonObject Replay(ReplayEncounter encounter)
    {
        var key = HistoricalTiers.Entries.FirstOrDefault(e => e.Key.StartsWith(encounter.SourceId + ":"))?.Key;
        var waves = key == null ? new[] { new[] { 900001, 900002 }, new[] { 900003 } } : Waves(key);
        return new JsonObject
        {
            ["id"] = encounter.SourceId, ["stageType"] = encounter.StageType,
            ["waveList"] = WaveList(waves), ["stageScriptList"] = new JsonArray(encounter.RequiredScript),
            ["participantInfo"] = new JsonObject { ["maxParticipant"] = 6 },
            ["recommendedLevel"] = 55, ["eventScriptName"] = "test-event",
            ["abnormalityEventList"] = new JsonArray(new JsonObject { ["id"] = 1 }),
            ["isBatonPassOn"] = encounter.SourceId is 10827 or 11041,
            ["staminaCost"] = 20, ["rewardList"] = new JsonArray(1), ["questlist"] = new JsonArray(2),
            ["hasGoldenBough"] = true, ["hasGoldenBoughGray"] = true, ["unlockDanteAbility"] = true,
            ["stageContinueInfo"] = new JsonObject { ["isContinueEnabled"] = true },
            ["story"] = new JsonObject { ["enter"] = new JsonArray(1), ["exit"] = new JsonArray(2) }
        };
    }

    public static JsonObject HistoricalTable(string file)
    {
        var entries = HistoricalTiers.Entries.Where(e => e.Source == file).ToArray();
        if (entries.Length == 0) throw new Exception("Historical fixture has no entries: " + file);
        return new JsonObject { ["list"] = new JsonArray(entries.Select(e => (JsonNode)new JsonObject
        {
            ["id"] = int.Parse(e.Key.Split(':')[0]), ["waveList"] = WaveList(Waves(e.Key))
        }).ToArray()) };
    }

    static int[][] Waves(string key) => key.Split(':')[1].Split(';')
        .Select(w => w.Split(',').Select(int.Parse).ToArray()).ToArray();

    static JsonArray WaveList(int[][] waves) => new(waves.Select(w => (JsonNode)new JsonObject
    {
        ["unitList"] = new JsonArray(w.Select(id => (JsonNode)new JsonObject { ["unitID"] = id }).ToArray())
    }).ToArray());
}
