using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gallop;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UmamusumeResponseAnalyzer.Plugin;
using UmamusumeResponseAnalyzer.TerminalGui;

namespace OldScenarioAnalyzer;

public sealed class OldScenarioAnalyzer : IPlugin
{
    const string WorkspaceTitle = "OldScenarioAnalyzer";
    const string TrainingPanelKey = "training";
    const int DefaultHistoryLimit = 100;
    const int MaximumHistoryLimit = 1000;

    static readonly JsonSerializerOptions SettingsJson = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    readonly object stateGate = new();
    readonly object publishGate = new();
    readonly List<HistoryEntry> history = [];

    IApplication? application;
    Workspace? workspace;
    WorkspaceContent? panelContent;
    HistoryView? historyView;
    string? liveSnapshot;
    long generation;
    int historyLimit = DefaultHistoryLimit;
    int selectedIndex = -1;
    bool settingsLoaded;
    bool accepting;
    bool hasPublishedTrainingPanel;
    bool hasUnreadHistory;

    string SettingsPath => Path.Combine("PluginData", WorkspaceTitle, "settings.json");

    public void Initialize(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureSettingsLoaded();

        lock (stateGate)
        {
            application = context.Application;
            accepting = true;
            generation++;
            hasPublishedTrainingPanel = false;
        }

        context.Analyzers.Register<SingleModeCheckEventResponse>(
            AnalyzerKind.Response,
            [EndpointPattern.Regex(@"^/umamusume/single_mode(?:_[^/]+)?/check_event$")],
            invocation => Analyze(invocation.Payload),
            priority: 1);
    }

    public void Dispose()
    {
        lock (stateGate)
        {
            accepting = false;
            generation++;
        }

        lock (publishGate)
        {
            Workspace? publishedWorkspace;
            HistoryView? publishedView;
            bool removePanel;
            lock (stateGate)
            {
                publishedWorkspace = workspace;
                publishedView = historyView;
                removePanel = hasPublishedTrainingPanel;

                history.Clear();
                liveSnapshot = null;
                selectedIndex = -1;
                hasUnreadHistory = false;
                hasPublishedTrainingPanel = false;
                historyView = null;
                panelContent = null;
                workspace = null;
                application = null;
            }

            publishedView?.StopListening();
            if (removePanel)
                publishedWorkspace!.RemovePanel(TrainingPanelKey);
        }
    }

    public async Task ConfigPromptAsync(
        IApplication application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSettingsLoaded();
        if (application.TopRunnable is null &&
            Environment.CurrentManagedThreadId != application.MainThreadId)
        {
            throw new InvalidOperationException(
                "OldScenarioAnalyzer 无法从非 UI thread 启动配置：Terminal.Gui 当前没有正在运行的 session。");
        }

        int draft;
        lock (stateGate)
            draft = historyLimit;

        int saved;
        if (Environment.CurrentManagedThreadId == application.MainThreadId)
        {
            saved = RunConfigDialog(application, draft, cancellationToken);
        }
        else
        {
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            application.Invoke(() =>
            {
                try
                {
                    completion.SetResult(RunConfigDialog(application, draft, cancellationToken));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
            saved = await completion.Task;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SaveSettings(saved);
        ApplyHistoryLimit(saved);
    }

    ValueTask Analyze(SingleModeCheckEventResponse @event)
        => @event.data.chara_info.scenario_id is >= 1 and <= 6
            ? AnalyzeCore(@event, @event.data.chara_info.scenario_id)
            : ValueTask.CompletedTask;

    ValueTask AnalyzeCore(object response, int scenarioId)
    {
        long activeGeneration;
        lock (stateGate)
        {
            if (!accepting)
                return ValueTask.CompletedTask;
            activeGeneration = generation;
        }

        var @event = SingleModeCheckEventLike.From(response);
        var data = @event.data;
        if (data.chara_info.scenario_id != scenarioId)
            return ValueTask.CompletedTask;

        var state = data.chara_info.state;
        if (data.home_info?.command_info_array is not null && !(state is 2 or 3)) //根据文本简单过滤防止重复、异常输出
        {
            if ((data.unchecked_event_array is { Length: > 0 }) || data.race_start_info is not null)
                return ValueTask.CompletedTask;

            var key = new HistoryKey(data.chara_info.single_mode_chara_id, data.chara_info.turn);
            if (Analyzer.ParseCommandInfo(@event) is { } content)
                PublishTrainingPanel(activeGeneration, key, content);
        }

        return ValueTask.CompletedTask;
    }

    void PublishTrainingPanel(long activeGeneration, HistoryKey key, string content)
    {
        lock (publishGate)
        {
            IApplication targetApplication;
            Workspace target;
            WorkspaceContent stableContent;
            bool firstPublication;
            lock (stateGate)
            {
                if (!accepting || generation != activeGeneration)
                    return;

                targetApplication = application!;
                target = workspace ??= Workspace.Create(WorkspaceTitle);
                stableContent = panelContent ??= new(
                    () => CreateHistoryView(targetApplication, activeGeneration));
                firstPublication = !hasPublishedTrainingPanel;
            }

            target.SetPanel(
                TrainingPanelKey,
                "训练分析",
                stableContent,
                fullBleed: true,
                switchToWorkspace: firstPublication);

            bool notifyUnread;
            lock (stateGate)
            {
                hasPublishedTrainingPanel = true;
                if (!accepting || generation != activeGeneration)
                    return;

                notifyUnread = StoreSnapshot(key, content);
            }

            RequestViewRefresh(
                targetApplication,
                target,
                stableContent,
                activeGeneration);
            if (notifyUnread)
                target.Notify("有新的训练分析记录，按 → 查看最新记录。");
        }
    }

    bool StoreSnapshot(HistoryKey key, string content)
    {
        liveSnapshot = content;
        if (historyLimit == 0)
        {
            history.Clear();
            selectedIndex = -1;
            hasUnreadHistory = false;
            return false;
        }

        var existingIndex = history.FindIndex(entry => entry.Key == key);
        if (existingIndex >= 0)
        {
            history[existingIndex] = new(key, content);
            return false;
        }

        var wasEmpty = history.Count == 0;
        var wasViewingNewest = selectedIndex == history.Count - 1;
        history.Add(new(key, content));

        var notifyUnread = false;
        if (wasEmpty || wasViewingNewest)
        {
            selectedIndex = history.Count - 1;
            hasUnreadHistory = false;
        }
        else if (!hasUnreadHistory)
        {
            hasUnreadHistory = true;
            notifyUnread = true;
        }

        var overflow = history.Count - historyLimit;
        if (overflow <= 0)
            return notifyUnread;

        var selectionEvicted = selectedIndex < overflow;
        history.RemoveRange(0, overflow);
        if (selectionEvicted)
        {
            selectedIndex = history.Count - 1;
            hasUnreadHistory = false;
            return false;
        }

        selectedIndex -= overflow;
        return notifyUnread;
    }

    HistoryView CreateHistoryView(IApplication targetApplication, long activeGeneration)
    {
        string text;
        bool listen;
        lock (stateGate)
        {
            text = DisplayedText();
            listen = accepting && generation == activeGeneration;
        }

        var view = new HistoryView(this, targetApplication, text);
        lock (stateGate)
        {
            if (listen && accepting && generation == activeGeneration)
                historyView = view;
            else
                view.StopListening();
        }
        return view;
    }

    string DisplayedText()
        => selectedIndex >= 0 && selectedIndex < history.Count
            ? history[selectedIndex].Content
            : liveSnapshot ?? string.Empty;

    void RequestViewRefresh(
        IApplication targetApplication,
        Workspace target,
        WorkspaceContent stableContent,
        long activeGeneration)
    {
        void Refresh()
        {
            lock (publishGate)
            {
                HistoryView? view;
                string text;
                lock (stateGate)
                {
                    if (!accepting ||
                        generation != activeGeneration ||
                        !hasPublishedTrainingPanel)
                    {
                        return;
                    }

                    view = historyView;
                    text = DisplayedText();
                }
                view?.Show(text);
                target.SetPanel(
                    TrainingPanelKey,
                    "训练分析",
                    stableContent,
                    fullBleed: true,
                    switchToWorkspace: false);
            }
        }

        if (Environment.CurrentManagedThreadId == targetApplication.MainThreadId)
            Refresh();
        else
            targetApplication.Invoke(Refresh);
    }

    internal bool CanNavigate(HistoryView view)
    {
        lock (stateGate)
        {
            return accepting &&
                   historyLimit > 0 &&
                   history.Count > 0 &&
                   ReferenceEquals(historyView, view) &&
                   workspace is { } target &&
                   ReferenceEquals(Workspace.Current, target);
        }
    }

    internal void Navigate(HistoryView view, HistoryNavigation navigation)
    {
        lock (publishGate)
        {
            IApplication targetApplication;
            Workspace target;
            WorkspaceContent stableContent;
            long activeGeneration;
            int position;
            int count;
            lock (stateGate)
            {
                if (!accepting ||
                    historyLimit == 0 ||
                    !ReferenceEquals(historyView, view) ||
                    history.Count == 0 ||
                    workspace is not { } currentWorkspace ||
                    panelContent is not { } currentContent)
                {
                    return;
                }

                targetApplication = application!;
                target = currentWorkspace;
                stableContent = currentContent;
                activeGeneration = generation;
                var current = selectedIndex < 0 ? history.Count - 1 : selectedIndex;
                selectedIndex = navigation switch
                {
                    HistoryNavigation.Older => Math.Max(0, current - 1),
                    HistoryNavigation.Newer => Math.Min(history.Count - 1, current + 1),
                    HistoryNavigation.Oldest => 0,
                    HistoryNavigation.Newest => history.Count - 1,
                    _ => current,
                };
                if (selectedIndex == history.Count - 1)
                    hasUnreadHistory = false;
                position = selectedIndex + 1;
                count = history.Count;
            }

            RequestViewRefresh(
                targetApplication,
                target,
                stableContent,
                activeGeneration);
            target.Notify($"历史 {position}/{count}");
        }
    }

    internal void Detach(HistoryView view)
    {
        lock (stateGate)
        {
            if (ReferenceEquals(historyView, view))
                historyView = null;
        }
    }

    void ApplyHistoryLimit(int value)
    {
        lock (publishGate)
        {
            IApplication? targetApplication;
            Workspace? target;
            WorkspaceContent? stableContent;
            long activeGeneration;
            lock (stateGate)
            {
                historyLimit = value;
                settingsLoaded = true;
                if (value == 0)
                {
                    history.Clear();
                    selectedIndex = -1;
                    hasUnreadHistory = false;
                }
                else
                {
                    var overflow = history.Count - value;
                    if (overflow > 0)
                    {
                        var selectionEvicted = selectedIndex < overflow;
                        history.RemoveRange(0, overflow);
                        selectedIndex = selectionEvicted
                            ? history.Count - 1
                            : selectedIndex - overflow;
                        if (selectionEvicted)
                            hasUnreadHistory = false;
                    }
                }

                targetApplication = application;
                target = hasPublishedTrainingPanel ? workspace : null;
                stableContent = panelContent;
                activeGeneration = generation;
            }

            if (target is null || stableContent is null || targetApplication is null)
                return;

            RequestViewRefresh(
                targetApplication,
                target,
                stableContent,
                activeGeneration);
        }
    }

    void EnsureSettingsLoaded()
    {
        lock (stateGate)
        {
            if (settingsLoaded)
                return;

            if (!File.Exists(SettingsPath))
            {
                settingsLoaded = true;
                return;
            }

            HistorySettings settings;
            try
            {
                settings = JsonSerializer.Deserialize<HistorySettings>(
                        File.ReadAllText(SettingsPath),
                        SettingsJson)
                    ?? throw new InvalidDataException($"OldScenarioAnalyzer 配置文件反序列化失败: {SettingsPath}");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"OldScenarioAnalyzer 配置文件无效: {SettingsPath}。{ex.Message}",
                    ex);
            }

            ValidateHistoryLimit(settings.HistoryLimit, SettingsPath);
            historyLimit = settings.HistoryLimit;
            settingsLoaded = true;
        }
    }

    void SaveSettings(int value)
    {
        ValidateHistoryLimit(value, "配置对话框");
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(new HistorySettings(value), SettingsJson));
    }

    static void ValidateHistoryLimit(int value, string source)
    {
        if (value is < 0 or > MaximumHistoryLimit)
        {
            throw new InvalidDataException(
                $"OldScenarioAnalyzer historyLimit 必须在 0 到 {MaximumHistoryLimit} 之间，{source} 中的值为 {value}。");
        }
    }

    static int RunConfigDialog(
        IApplication application,
        int draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dialog = new Dialog
        {
            Title = "OldScenarioAnalyzer 配置",
            Width = 58,
            Height = 10,
        };
        var input = new TextField
        {
            X = 1,
            Y = 2,
            Width = 16,
            Text = draft.ToString(CultureInfo.InvariantCulture),
        };
        var validation = new Label
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill(1),
            Height = 1,
        };
        dialog.Add(
            new Label { X = 1, Y = 1, Text = "History 上限 (0-1000)" },
            input,
            validation);

        var accepted = false;
        var result = draft;
        var save = new Button { Text = "保存", IsDefault = true };
        save.Accepting += (_, e) =>
        {
            if (!int.TryParse(
                    input.Text?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value) || value is < 0 or > MaximumHistoryLimit)
            {
                validation.Text = $"请输入 0 到 {MaximumHistoryLimit} 之间的整数。";
                e.Handled = true;
                return;
            }

            result = value;
            accepted = true;
            application.RequestStop(dialog);
            e.Handled = true;
        };
        var cancel = new Button { Text = "取消" };
        cancel.Accepting += (_, e) =>
        {
            application.RequestStop(dialog);
            e.Handled = true;
        };
        dialog.AddButton(cancel);
        dialog.AddButton(save);
        input.SetFocus();

        using (cancellationToken.Register(
                   () => application.Invoke(() => application.RequestStop(dialog))))
        {
            application.Run(dialog);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!accepted)
            throw new OperationCanceledException("OldScenarioAnalyzer 配置已取消。", cancellationToken);

        return result;
    }

    readonly record struct HistoryKey(int SingleModeCharaId, int Turn);

    readonly record struct HistoryEntry(HistoryKey Key, string Content);

    sealed record HistorySettings(
        [property: JsonRequired]
        [property: JsonPropertyName("historyLimit")]
        int HistoryLimit);
}

internal enum HistoryNavigation
{
    Older,
    Newer,
    Oldest,
    Newest,
}

internal sealed class HistoryView : View
{
    readonly OldScenarioAnalyzer owner;
    readonly IApplication application;
    Label content;
    bool listening = true;

    internal HistoryView(OldScenarioAnalyzer owner, IApplication application, string text)
    {
        this.owner = owner;
        this.application = application;
        Width = Dim.Fill();
        Height = Dim.Auto();
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;
        content = CreateLabel(text);
        Add(content);
        application.Keyboard.KeyDown += ApplicationKeyDown;
    }

    internal void Show(string text)
    {
        if (!listening)
            return;

        var next = CreateLabel(text);
        Remove(content);
        content.Dispose();
        content = next;
        Add(content);
        SetNeedsLayout();
        SetNeedsDraw();
    }

    static Label CreateLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            Width = Dim.Fill(),
            Height = Dim.Auto(),
        };
        label.TextFormatter.WordWrap = true;
        return label;
    }

    internal void StopListening()
    {
        if (!listening)
            return;
        listening = false;
        application.Keyboard.KeyDown -= ApplicationKeyDown;
    }

    void ApplicationKeyDown(object? sender, Key key)
    {
        if (key.Handled || key.IsCtrl || key.IsAlt || key.IsShift || !owner.CanNavigate(this))
            return;

        var navigation = key.KeyCode switch
        {
            var code when code == Key.CursorUp.KeyCode => HistoryNavigation.Older,
            var code when code == Key.CursorDown.KeyCode => HistoryNavigation.Newer,
            var code when code == Key.CursorLeft.KeyCode => HistoryNavigation.Oldest,
            var code when code == Key.CursorRight.KeyCode => HistoryNavigation.Newest,
            _ => (HistoryNavigation?)null,
        };
        if (navigation is null || !Contains(application.TopRunnableView?.MostFocused))
            return;

        key.Handled = true;
        owner.Navigate(this, navigation.Value);
    }

    bool Contains(View? view)
    {
        for (var current = view; current is not null; current = current.SuperView)
        {
            if (ReferenceEquals(current, this))
                return true;
        }
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopListening();
            owner.Detach(this);
        }
        base.Dispose(disposing);
    }
}
