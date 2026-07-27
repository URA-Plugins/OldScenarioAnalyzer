using Gallop;
using Gallop.Endpoints;
using UmamusumeResponseAnalyzer.TerminalGui;
using UmamusumeResponseAnalyzer.Plugin;

namespace OldScenarioAnalyzer
{
    public class OldScenarioAnalyzer : IPlugin
    {
        const string WorkspaceTitle = "OldScenarioAnalyzer";
        const string TrainingPanelKey = "training";

        Workspace? workspace;
        bool hasPublishedTrainingPanel;

        public void Initialize(IPluginContext context)
        {
            hasPublishedTrainingPanel = false;
        }

        public void Dispose()
        {
            if (!hasPublishedTrainingPanel || workspace is not { } publishedWorkspace)
                return;

            publishedWorkspace.RemovePanel(TrainingPanelKey);
            hasPublishedTrainingPanel = false;
        }

        [ResponseAnalyzer<GameApi.SingleMode.CheckEvent>(1)]
        ValueTask Analyze(SingleModeCheckEventResponse @event) => AnalyzeCore(@event, 1);

        [ResponseAnalyzer<GameApi.SingleModeTeam.CheckEvent>(1)]
        ValueTask Analyze(SingleModeTeamCheckEventResponse @event) => AnalyzeCore(@event, 2);

        [ResponseAnalyzer<GameApi.SingleModeLive.CheckEvent>(1)]
        ValueTask Analyze(SingleModeLiveCheckEventResponse @event) => AnalyzeCore(@event, 3);

        [ResponseAnalyzer<GameApi.SingleModeFree.CheckEvent>(1)]
        ValueTask Analyze(SingleModeFreeCheckEventResponse @event) => AnalyzeCore(@event, 4);

        [ResponseAnalyzer<GameApi.SingleModeVenus.CheckEvent>(1)]
        ValueTask Analyze(SingleModeVenusCheckEventResponse @event) => AnalyzeCore(@event, 5);

        [ResponseAnalyzer<GameApi.SingleModeArc.CheckEvent>(1)]
        ValueTask Analyze(SingleModeArcCheckEventResponse @event) => AnalyzeCore(@event, 6);

        ValueTask AnalyzeCore(object response, int scenarioId)
        {
            var @event = SingleModeCheckEventLike.From(response);
            var data = @event.data;
            if (data.chara_info.scenario_id != scenarioId) return ValueTask.CompletedTask;
            var state = data.chara_info.state;
            if (data.home_info?.command_info_array is not null && !(state is 2 or 3)) //根据文本简单过滤防止重复、异常输出
            {
                if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0) || @event.data.race_start_info != null) return ValueTask.CompletedTask;
                if (Analyzer.ParseCommandInfo(@event) is { } content)
                    PublishTrainingPanel(content);
            }

            return ValueTask.CompletedTask;
        }

        void PublishTrainingPanel(string content)
        {
            var workspace = this.workspace ??= Workspace.Create(WorkspaceTitle);
            workspace.SetPanel(
                TrainingPanelKey,
                "训练分析",
                WorkspaceContent.Text(content),
                fullBleed: true,
                switchToWorkspace: !hasPublishedTrainingPanel);
            hasPublishedTrainingPanel = true;
        }
    }
}
