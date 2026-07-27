using Gallop;

namespace OldScenarioAnalyzer;

internal enum ScenarioType
{
    MakeANewTrack = 4,
    GrandMasters = 5,
    LArc = 6
}

internal enum PartnerPriority
{
    友人 = 0,
    闪 = 1,
    羁绊不足 = 2,
    其他 = 3,
    需要充电 = 4,
    关键NPC = 5,
    无用NPC = 6,
    默认 = 7
}

internal static class ScenarioExtensions
{
    public static bool IsScenario(object response, ScenarioType type)
    {
        var data = response.GetType().GetField("data")?.GetValue(response)
            ?? throw new MissingFieldException(response.GetType().FullName, "data");
        var charaInfo = data.GetType().GetField("chara_info")?.GetValue(data)
            ?? throw new MissingFieldException(data.GetType().FullName, "chara_info");
        var scenarioId = (int)(charaInfo.GetType().GetField("scenario_id")?.GetValue(charaInfo)
            ?? throw new MissingFieldException(charaInfo.GetType().FullName, "scenario_id"));

        return type switch
        {
            ScenarioType.MakeANewTrack => scenarioId == 4 && FieldValue(data, "free_data_set") is not null,
            ScenarioType.GrandMasters => scenarioId == 5 && FieldValue(data, "venus_data_set") is not null,
            ScenarioType.LArc => scenarioId == 6 && FieldValue(data, "arc_data_set") is not null,
            _ => false
        };
    }

    static object? FieldValue(object source, string fieldName)
        => source.GetType().GetField(fieldName)?.GetValue(source);
}
