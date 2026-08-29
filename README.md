# OldScenarioAnalyzer

解析种田杯以前剧本的回合训练信息。

## History

训练分析按 `(single_mode_chara_id, turn)` 保留在当前插件实例的内存中；相同键的输出原位更新，不会新增记录或改变顺序。History 不跨进程重启。

在训练分析面板获得焦点时，使用 `↑` / `↓` 查看较旧 / 较新的记录，使用 `←` / `→` 跳到最旧 / 最新记录。正文滚动使用 `PageUp`、`PageDown`、`Home`、`End` 或鼠标滚轮。

配置文件为 `PluginData/OldScenarioAnalyzer/settings.json`：

```json
{
  "historyLimit": 100
}
```

`historyLimit` 默认值为 `100`，有效范围为 `0` 到 `1000`。设置为 `0` 时只显示最近一次成功输出，不保存 History，也不接管方向键。

## 构建

```powershell
git -c core.longpaths=true submodule update --init --recursive
dotnet build .\OldScenarioAnalyzer.csproj -c Release -m:1 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PlatformTarget=AnyCPU -p:DeployUraPluginToLocalAppDataOnBuild=false
```
