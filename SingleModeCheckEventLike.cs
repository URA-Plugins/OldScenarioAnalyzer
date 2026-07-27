using System.Reflection;
using Gallop;

namespace OldScenarioAnalyzer;

internal sealed class SingleModeCheckEventLike
{
    public CommonResponse data = null!;

    public static SingleModeCheckEventLike From(object response)
    {
        var sourceData = Required<object>(response, "data");
        var common = new CommonResponse
        {
            chara_info = Required<SingleModeChara>(sourceData, nameof(CommonResponse.chara_info)),
            home_info = Required<SingleModeHomeInfo>(sourceData, nameof(CommonResponse.home_info)),
            unchecked_event_array = Required<SingleModeEventInfo[]>(sourceData, nameof(CommonResponse.unchecked_event_array)),
            race_condition_array = Required<SingleModeRaceCondition[]>(sourceData, nameof(CommonResponse.race_condition_array)),
            race_start_info = Optional<SingleRaceStartInfo>(sourceData, nameof(CommonResponse.race_start_info)),
            select_index_info_array = Optional<SingleModeSelectIndexInfo[]>(sourceData, nameof(CommonResponse.select_index_info_array)) ?? [],
            ura_data_set = Optional<SingleModeUraDataSet>(sourceData, nameof(CommonResponse.ura_data_set)),
            team_data_set = Optional<SingleModeTeamDataSet>(sourceData, nameof(CommonResponse.team_data_set)),
            live_data_set = Optional<SingleModeLiveDataSet>(sourceData, nameof(CommonResponse.live_data_set)),
            free_data_set = Optional<SingleModeFreeDataSet>(sourceData, nameof(CommonResponse.free_data_set)),
            venus_data_set = Optional<SingleModeVenusDataSet>(sourceData, nameof(CommonResponse.venus_data_set)),
            arc_data_set = Optional<SingleModeArcDataSet>(sourceData, nameof(CommonResponse.arc_data_set))
        };

        return new() { data = common };
    }

    static T Required<T>(object source, string fieldName)
    {
        var field = Field(source, fieldName)
            ?? throw new MissingFieldException(source.GetType().FullName, fieldName);
        return (T)(field.GetValue(source)
            ?? throw new InvalidOperationException($"Gallop field is null: type={source.GetType().FullName}, field={fieldName}"));
    }

    static T Optional<T>(object source, string fieldName)
    {
        var field = Field(source, fieldName);
        return field is null ? default! : (T)field.GetValue(source)!;
    }

    static FieldInfo? Field(object source, string fieldName)
        => source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);

    public sealed class CommonResponse
    {
        public SingleModeChara chara_info = null!;
        public SingleModeHomeInfo home_info = null!;
        public SingleModeEventInfo[] unchecked_event_array = [];
        public SingleModeRaceCondition[] race_condition_array = [];
        public SingleRaceStartInfo? race_start_info;
        public SingleModeSelectIndexInfo[] select_index_info_array = [];

        public SingleModeUraDataSet ura_data_set = null!;
        public SingleModeTeamDataSet team_data_set = null!;
        public SingleModeLiveDataSet live_data_set = null!;
        public SingleModeFreeDataSet free_data_set = null!;
        public SingleModeVenusDataSet venus_data_set = null!;
        public SingleModeArcDataSet arc_data_set = null!;
    }
}
