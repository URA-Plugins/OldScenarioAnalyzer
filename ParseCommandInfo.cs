using EventLoggerPlugin;
using Gallop;
using System.Text;
using UmamusumeResponseAnalyzer;

namespace OldScenarioAnalyzer
{
    internal static class Analyzer
    {
        public static string? ParseCommandInfo(SingleModeCheckEventLike @event)
        {
            if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0) || @event.data.race_start_info != null) return null;
            var rows = new List<string>();
            var turnNum = @event.data.chara_info.turn;
            var LArcIsAbroad = (turnNum >= 37 && turnNum <= 43) || (turnNum >= 61 && turnNum <= 67);
            var eventLoggerSnapshot = new EventLoggerSnapshot(
                @event.data.chara_info,
                @event.data.unchecked_event_array,
                @event.data.select_index_info_array);
            var round = EventLogger.Current;

            var currentFiveValue = new int[]
            {
                @event.data.chara_info.speed,
                @event.data.chara_info.stamina,
                @event.data.chara_info.power ,
                @event.data.chara_info.guts ,
                @event.data.chara_info.wiz ,
            };
            var fiveValueMaxRevised = new int[]
            {
                EventLoggerPlugin.ScoreUtils.ReviseOver1200(@event.data.chara_info.max_speed),
                EventLoggerPlugin.ScoreUtils.ReviseOver1200(@event.data.chara_info.max_stamina),
                EventLoggerPlugin.ScoreUtils.ReviseOver1200(@event.data.chara_info.max_power) ,
                EventLoggerPlugin.ScoreUtils.ReviseOver1200(@event.data.chara_info.max_guts) ,
                EventLoggerPlugin.ScoreUtils.ReviseOver1200(@event.data.chara_info.max_wiz) ,
            };
            var currentFiveValueRevised = currentFiveValue.Select(x => EventLoggerPlugin.ScoreUtils.ReviseOver1200(x)).ToArray();
            var totalValue = currentFiveValueRevised.Sum();
            rows.Add(string.Empty);

            if (round.CurrentTurn != turnNum - 1 //正常情况
                && round.CurrentTurn != turnNum //重复显示
                && turnNum != 1 //第一个回合
                )
            {
                rows.Add($"警告：回合数不正确，上一个回合为{round.CurrentTurn}，当前回合为{turnNum}");
                EventLogger.ResetSession(eventLoggerSnapshot, isFullGame: false);
                round = EventLogger.Current;
            }
            else if (turnNum == 1)
            {
                EventLogger.ResetSession(eventLoggerSnapshot, isFullGame: true);
                round = EventLogger.Current;
            }

            //买技能，大师杯剧本年末比赛，会重复显示
            var isRepeat = @event.data.chara_info.playing_state != 1;

            //初始化TurnStats
            if (isRepeat)
            {
                rows.Add("******此回合为重复显示******");
            }
            else
            {
                EventLogger.BeginScenarioTurn(
                    eventLoggerSnapshot,
                    @event.data.chara_info.scenario_id,
                    turnNum);
                round = EventLogger.Current;
            }

            //为了避免写判断，对于重复回合，直接让turnStat指向一个无用的TurnStats类
            var turnStat = isRepeat ? new TurnStats() : round.NewTurnBuilder(turnNum);
            Dictionary<int, int>? ssRivalsSpecialBuffs = null;
            var gameYear = (turnNum - 1) / 24 + 1;
            var gameMonth = ((turnNum - 1) % 24) / 2 + 1;
            var halfMonth = (turnNum % 2 == 0) ? "后半" : "前半";
            var totalTurns = ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) ? 67 : 78;

            rows.Add("------------------------------------------------------------------------------------");
            rows.Add($"回合数：{@event.data.chara_info.turn}/{totalTurns}, 第{gameYear}年{gameMonth}月{halfMonth}");

            var motivation = @event.data.chara_info.motivation;
            turnStat.motivation = motivation;
            var currentVital = @event.data.chara_info.vital;
            var maxVital = @event.data.chara_info.max_vital;
            switch (currentVital)
            {
                case < 30:
                    rows.Add($"体力：{currentVital}/{maxVital}");
                    break;
                case < 50:
                    rows.Add($"体力：{currentVital}/{maxVital}");
                    break;
                case < 70:
                    rows.Add($"体力：{currentVital}/{maxVital}");
                    break;
                default:
                    rows.Add($"体力：{currentVital}/{maxVital}");
                    break;
            }

            switch (motivation)
            {
                case 5:
                    rows.Add("干劲：绝好调");
                    break;
                case 4:
                    rows.Add("干劲：好调");
                    break;
                case 3:
                    rows.Add("干劲：普通");
                    break;
                case 2:
                    rows.Add("干劲：不调");
                    break;
                case 1:
                    rows.Add("干劲：绝不调");
                    break;
            }

            var totalValueWithPt = totalValue + @event.data.chara_info.skill_point;
            var totalValueWithHalfPt = totalValue + 0.5 * @event.data.chara_info.skill_point;
            rows.Add($"总属性：{totalValue}\t总属性+0.5*pt：{totalValueWithHalfPt}");

            #region LArc
            //计算训练等级
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc))//预测训练等级
            {
                for (var i = 0; i < 5; i++)
                {
                    if (turnNum == 1)
                    {
                        turnStat.trainLevel[i] = 1;
                        turnStat.trainLevelCount[i] = 0;
                    }
                    else
                    {
                        var previousTurn = round.Turns[turnNum - 1];
                        var lastTrainLevel = previousTurn?.TrainLevel[i] ?? 1;
                        var lastTrainLevelCount = previousTurn?.TrainLevelCount[i] ?? 0;

                        turnStat.trainLevel[i] = lastTrainLevel;
                        turnStat.trainLevelCount[i] = lastTrainLevelCount;
                        if (previousTurn is not null &&
                            previousTurn.PlayerChoice == GameGlobal.TrainIds[i] &&
                            !previousTurn.IsTrainingFailed &&
                            !((turnNum - 1 >= 37 && turnNum - 1 <= 43) || (turnNum - 1 >= 61 && turnNum - 1 <= 67))
                            )//上回合点的这个训练，计数+1
                            turnStat.trainLevelCount[i] += 1;
                        if (turnStat.trainLevelCount[i] >= 4)
                        {
                            turnStat.trainLevelCount[i] -= 4;
                            turnStat.trainLevel[i] += 1;
                        }
                        //检查是否有期待度上升
                        var appRate = @event.data.arc_data_set.arc_info.approval_rate;
                        var oldAppRate = previousTurn is not null ? (previousTurn.LArcTotalApproval + 85) / 170 : 0;
                        if (oldAppRate < 200 && appRate >= 200)
                            turnStat.trainLevel[i] += 1;
                        if (oldAppRate < 600 && appRate >= 600)
                            turnStat.trainLevel[i] += 1;
                        if (oldAppRate < 1000 && appRate >= 1000)
                            turnStat.trainLevel[i] += 1;

                        if (turnStat.trainLevel[i] >= 5)
                        {
                            turnStat.trainLevel[i] = 5;
                            turnStat.trainLevelCount[i] = 0;
                        }
                    }
                }
            }
            //额外显示LArc信息
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc))
            {
                turnStat.larc_isSSS = @event.data.arc_data_set.selection_info?.is_special_match == 1;
                turnStat.larc_totalApproval = @event.data.arc_data_set.arc_rival_array.Sum(x => x.approval_point);
                var totalSSLevel = @event.data.arc_data_set.arc_rival_array.Sum(x => x.star_lv);
                var rivalBoostCount = new int[] { 0, 0, 0, 0 };
                var effectCount = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                foreach (var rival in @event.data.arc_data_set.arc_rival_array)
                {
                    if (rival.selection_peff_array == null)
                        continue; ///马娘自身
                    rivalBoostCount[rival.rival_boost] += 1;
                    foreach (var ef in rival.selection_peff_array)
                    {
                        effectCount[ef.effect_group_id] += 1;
                    }
                }
                var approval_rate = @event.data.arc_data_set.arc_info.approval_rate;
                var approval_rate_level = approval_rate / 50;
                var approval_training_bonus = GameGlobal.LArcTrainBonusEvery5Percent[approval_rate_level > 40 ? 40 : approval_rate_level];
                var lastTurnTotalApproval = round.Turns[turnNum - 1]?.LArcTotalApproval ?? 0;
                rows.Add($"期待度：{approval_rate / 10}.{approval_rate % 10}%（训练+{approval_training_bonus}%）    适性pt：{@event.data.arc_data_set.arc_info.global_exp}    总支援pt：{turnStat.larc_totalApproval}(+{turnStat.larc_totalApproval - lastTurnTotalApproval})");

                var totalCount = totalSSLevel * 3 + rivalBoostCount[1] * 1 + rivalBoostCount[2] * 2 + rivalBoostCount[3] * 3;
                rows.Add($"总格数：{totalCount}    总SS数：{totalSSLevel}    0123格：{rivalBoostCount[0]} {rivalBoostCount[1]} {rivalBoostCount[2]} {rivalBoostCount[3]}");

                var toPrint = string.Empty;
                //每个人头（包括支援卡）每3级一定有一个属性，一个pt，一个特殊词条。其中特殊词条在一局内是固定的
                //每局15个人头的每种特殊词条的总数是固定的。但是除了几个特殊的（体力最大值-茶座、爱娇-黄金船、练习上手-神鹰），其他都会随机分配给支援卡和路人
                //支援卡相比路人点的次数更多，如果第三回合的支援卡随机分配的特殊词条不好，就可以重开了

                ssRivalsSpecialBuffs = turnNum <= 2
                    ? []
                    : round.SsRivalsSpecialBuffs.ToDictionary();
                if (turnNum > 2)
                {
                    ssRivalsSpecialBuffs[1014] = 9;
                    ssRivalsSpecialBuffs[1007] = 8;

                    foreach (var arc_data in @event.data.arc_data_set.arc_rival_array)
                    {
                        if (arc_data.selection_peff_array == null)//马娘自身
                            continue;

                        if (!ssRivalsSpecialBuffs.ContainsKey(arc_data.chara_id))
                            ssRivalsSpecialBuffs[arc_data.chara_id] = 0; //未知状态

                        foreach (var ef in arc_data.selection_peff_array)
                        {
                            var efid = ef.effect_group_id;
                            if (efid != 1 && efid != 11) //特殊buff
                            {
                                var efid_old = ssRivalsSpecialBuffs[arc_data.chara_id];
                                if (efid_old == 0)
                                    ssRivalsSpecialBuffs[arc_data.chara_id] = efid;
                                else if (efid_old != efid)//要么是出错，要么是神鹰的练习上手+适性pt
                                {
                                    if (efid_old == 7 && efid == 9)
                                    {
                                        ssRivalsSpecialBuffs[arc_data.chara_id] = 9;
                                    }
                                    else if (efid_old == 9 && efid == 7)
                                    {
                                        //什么都不用做
                                    }
                                    else
                                    {
                                        rows.Add($"警告：larc的ss特殊buff错误，{arc_data.chara_id} {efid} {efid_old}");
                                    }
                                }
                            }
                        }
                    }
                }

                var supportCards1 = @event.data.chara_info.support_card_array.ToDictionary(x => x.position, x => x.support_card_id); //当前S卡卡组
                for (var cardCount = 0; cardCount < 8; cardCount++)
                {
                    if (supportCards1.Any(x => x.Key == cardCount))
                    {

                        var name = Database.Names.DisplayNickname(supportCards1[cardCount]); //partner是当前S卡卡组的index（1~6，7是啥？我忘了）或者charaId（10xx)
                        var charaTrainingType = string.Empty;
                        var specialBuffs = string.Empty;
                        var chara_id = @event.data.arc_data_set.evaluation_info_array.First(x => x.target_id == cardCount).chara_id;
                        if (@event.data.arc_data_set.arc_rival_array.Any(x => x.chara_id == chara_id))
                        {
                            var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == chara_id);

                            charaTrainingType = $"({GameGlobal.TrainNames[arc_data.command_id]})";

                            if (ssRivalsSpecialBuffs[arc_data.chara_id] != 0)
                                specialBuffs = GameGlobal.LArcSSEffectNamesFull[ssRivalsSpecialBuffs[arc_data.chara_id]];
                            else
                                specialBuffs = "?";
                        }
                        toPrint += $"{name}:{charaTrainingType}{specialBuffs} ";
                    }
                }
                rows.Add(toPrint);
            }
            #endregion

            #region Grand Masters
            //额外显示GM杯信息
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.GrandMasters))
            {
                var outputLine = "当前碎片组：";
                var spiritColors = new int[8]; //0空，1红，2蓝，3黄
                for (var spiritPlace = 1; spiritPlace < 9; spiritPlace++)
                {
                    var spiritId =
                        @event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_num == spiritPlace)
                        ? @event.data.venus_data_set.spirit_info_array.First(x => x.spirit_num == spiritPlace).spirit_id
                        : -1;
                    spiritColors[spiritPlace - 1] = (8 + spiritId) / 8;  //0空，1红，2蓝，3黄
                    if (GameGlobal.GrandMastersSpiritNames.TryGetValue(spiritId, out var spiritStr))
                    {
                        outputLine += (spiritPlace == 1 || spiritPlace == 5) ? $"{{{spiritStr}}} " : $"{spiritStr} ";
                    }
                }
                rows.Add(outputLine);

                //看看有没有凑齐的女神
                if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9040)) rows.Add("当前女神睿智：红");
                else if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9041)) rows.Add("当前女神睿智：蓝");
                else if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9042)) rows.Add("当前女神睿智：黄");
                else //预测下一个女神
                {
                    var colorStrs = new string[] { "⚪", "红", "蓝", "黄" };
                    if (spiritColors[0] == 0)
                    {
                        rows.Add("下一个女神：⚪ vs ⚪");
                    }
                    else if (spiritColors[0] != 0 && spiritColors[4] == 0)
                    {
                        var color1 = spiritColors[0];
                        var color1count = spiritColors.Count(x => x == color1);
                        rows.Add($"下一个女神：{colorStrs[color1]}x{color1count} vs ⚪");
                    }
                    else
                    {
                        var color1 = spiritColors[0];
                        var color1count = spiritColors.Count(x => x == color1);
                        var color2 = spiritColors[4];
                        var color2count = spiritColors.Count(x => x == color2);
                        var emptycount = spiritColors.Count(x => x == 0);
                        if (color1 == color2 || color1count > color2count + emptycount)
                            rows.Add($"下一个女神：{colorStrs[color1]}");
                        else if (color2count > color1count + emptycount)
                            rows.Add($"下一个女神：{colorStrs[color2]}");
                        else
                            rows.Add($"下一个女神：{colorStrs[color1]}x{color1count} vs {colorStrs[color2]}x{color2count}");
                    }
                }

                if (@event.data.venus_data_set.venus_chara_info_array != null && @event.data.venus_data_set.venus_chara_info_array.Any(x => x.chara_id == 9042))
                {
                    var venusLevels = @event.data.venus_data_set.venus_chara_info_array;
                    turnStat.venus_yellowVenusLevel = venusLevels.First(x => x.chara_id == 9042).venus_level;
                    turnStat.venus_redVenusLevel = venusLevels.First(x => x.chara_id == 9040).venus_level;
                    turnStat.venus_blueVenusLevel = venusLevels.First(x => x.chara_id == 9041).venus_level;
                    rows.Add($"女神等级：黄{turnStat.venus_yellowVenusLevel} 红{turnStat.venus_redVenusLevel} 蓝{turnStat.venus_blueVenusLevel}");
                }
                // 是否开蓝了
                if (@event.data.venus_data_set.venus_spirit_active_effect_info_array.Any(x => x.chara_id == 9041))
                {
                    turnStat.venus_isVenusCountConcerned = false;
                }
            }
            //女神情热状态，不统计女神召唤次数
            if (@event.data.chara_info.chara_effect_id_array.Any(x => x == 102))
            {
                turnStat.venus_isVenusCountConcerned = false;
                turnStat.venus_isEffect102 = true;
                //统计一下女神情热持续了几回合
                var continuousTurnNum = 1;
                for (var i = turnNum - 1; i >= 1; i--)
                {
                    if (round.Turns[i] is not { VenusIsEffect102: true })
                        break;
                    continuousTurnNum++;
                }
                rows.Add($"女神彩圈已持续{continuousTurnNum}回合");
            }
            #endregion

            var trainItems = new Dictionary<int, SingleModeCommandInfo>();
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc))
            {
                //LArc的合宿ID不一样，所以要单独处理
                trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 1101) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1101) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 1102) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1102) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 1103) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1103) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 1104) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1104) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 1105) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1105) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
            }
            else
            {
                //速耐力根智，6xx为合宿时ID
                trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 601) ? @event.data.home_info.command_info_array.First(x => x.command_id == 601) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 602) ? @event.data.home_info.command_info_array.First(x => x.command_id == 602) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 603) ? @event.data.home_info.command_info_array.First(x => x.command_id == 603) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 604) ? @event.data.home_info.command_info_array.First(x => x.command_id == 604) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 605) ? @event.data.home_info.command_info_array.First(x => x.command_id == 605) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
            }

            var trainStats = new TrainStats[5];
            var failureRate = new Dictionary<int, int>();
            for (var i = 0; i < 5; i++)
            {
                var trainId = GameGlobal.TrainIds[i];
                failureRate[trainId] = trainItems[trainId].failure_rate;
                var trainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                dynamic commandInfoArray = @event.data.home_info.command_info_array;
                //去掉剧本加成的训练值（游戏里的下层显示）
                foreach (var item in commandInfoArray)
                    if (GameGlobal.ToTrainId.TryGetValue(item.command_id, out int value) && value == trainId)
                        foreach (var trainParam in item.params_inc_dec_info_array)
                            trainParams[trainParam.target_type] += trainParam.value;
                var nonScenarioTrainParams = new Dictionary<int, int>(trainParams);
                if (@event.data.team_data_set != null) // 青春杯
                    commandInfoArray = @event.data.team_data_set.command_info_array;
                else if (@event.data.free_data_set != null) // 巅峰杯
                    commandInfoArray = @event.data.free_data_set.command_info_array;
                else if (@event.data.live_data_set != null) // 偶像杯
                    commandInfoArray = @event.data.live_data_set.command_info_array;
                else if (ScenarioExtensions.IsScenario(@event, ScenarioType.GrandMasters)) // 女神杯
                    commandInfoArray = @event.data.venus_data_set.command_info_array;
                else if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc)) // 凯旋门
                    commandInfoArray = @event.data.arc_data_set.command_info_array;
                if (commandInfoArray is System.Collections.IEnumerable and not null)
                    foreach (var item in commandInfoArray)
                        if (GameGlobal.ToTrainId.TryGetValue(item.command_id, out int value) && value == trainId)
                            foreach (var trainParam in item.params_inc_dec_info_array)
                                trainParams[trainParam.target_type] += trainParam.value;

                var stats = new TrainStats
                {
                    FailureRate = trainItems[trainId].failure_rate,
                    VitalGain = trainParams[10]
                };
                if (currentVital + stats.VitalGain > maxVital)
                    stats.VitalGain = maxVital - currentVital;
                if (stats.VitalGain < -currentVital)
                    stats.VitalGain = -currentVital;
                stats.FiveValueGain = [trainParams[1], trainParams[2], trainParams[3], trainParams[4], trainParams[5]];
                for (var j = 0; j < 5; j++)
                    stats.FiveValueGain[j] = ScoreUtils.ReviseOver1200(currentFiveValue[j] + stats.FiveValueGain[j]) - ScoreUtils.ReviseOver1200(currentFiveValue[j]);
                stats.PtGain = trainParams[30];
                stats.FiveValueGainNonScenario = [nonScenarioTrainParams[1], nonScenarioTrainParams[2], nonScenarioTrainParams[3], nonScenarioTrainParams[4], nonScenarioTrainParams[5]];
                for (var j = 0; j < 5; j++)
                    stats.FiveValueGainNonScenario[j] = ScoreUtils.ReviseOver1200(currentFiveValue[j] + stats.FiveValueGainNonScenario[j]) - ScoreUtils.ReviseOver1200(currentFiveValue[j]);
                stats.PtGainNonScenario = nonScenarioTrainParams[30];
                trainStats[i] = stats;
            }
            turnStat.fiveTrainStats = trainStats;
            var failureRateStr = new string[5];
            //失败率>=40%标红、>=20%(有可能大失败)标DarkOrange、>0%标黄
            for (var i = 0; i < 5; i++)
            {
                var thisFailureRate = failureRate[GameGlobal.TrainIds[i]];
                failureRateStr[i] = thisFailureRate switch
                {
                    > 0 => $"({thisFailureRate}%)",
                    _ => string.Empty
                };
            }
            var columnCount = ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) ? 6 : 5;
            var headers = new string[columnCount];
            var footers = new string[columnCount];
            var tableRows = Enumerable.Range(0, 14).Select(_ => new string[columnCount]).ToArray();
            var separatorLine = Enumerable.Repeat("---------------", 5).ToArray();
            var separatorLineSSMatch = "---------------";
            headers[0] = $"速{failureRateStr[0]}";
            headers[1] = $"耐{failureRateStr[1]}";
            headers[2] = $"力{failureRateStr[2]}";
            headers[3] = $"根{failureRateStr[3]}";
            headers[4] = $"智{failureRateStr[4]}";
            if (columnCount == 6)
                headers[5] = "SS Match";

            var outputItems = new string[5];
            Enumerable.Repeat("当前:可获得", 5).ToArray().CopyTo(tableRows[0], 0);
            //显示此属性的当前属性及还差多少属性达到上限
            for (var i = 0; i < 5; i++)
            {
                var remainValue = fiveValueMaxRevised[i] - currentFiveValueRevised[i];
                outputItems[i] = $"{currentFiveValueRevised[i]}: {remainValue}属性";
            }
            outputItems.CopyTo(tableRows[1], 0);
            separatorLine.CopyTo(tableRows[2], 0);
            //显示训练后的剩余体力
            for (var i = 0; i < 5; i++)
            {
                var tid = GameGlobal.TrainIds[i];
                var VitalGain = trainStats[i].VitalGain;
                var newVital = VitalGain + currentVital;
                outputItems[i] = $"体力:{newVital}/{maxVital}";
            }
            outputItems.CopyTo(tableRows[3], 0);

            //显示此训练的训练等级
            for (var i = 0; i < 5; i++)
            {
                var normalId = GameGlobal.TrainIds[i];
                if (@event.data.home_info.command_info_array.Any(x => x.command_id == GameGlobal.XiahesuIds[normalId]))
                {
                    outputItems[i] = "夏合宿";
                }
                else if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) && LArcIsAbroad)
                {
                    outputItems[i] = "远征";
                }
                else
                {
                    var lv = @event.data.chara_info.training_level_info_array.First(x => x.command_id == normalId).level;
                    if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) && turnStat.trainLevel[i] != lv && !isRepeat)
                    {
                        //可能是半途开启小黑板，也可能是有未知bug
                        rows.Add($"警告：训练等级预测错误，预测{GameGlobal.TrainNames[normalId]}为lv{turnStat.trainLevel[i]}(+{turnStat.trainLevelCount[i]})，实际为lv{lv}");
                        turnStat.trainLevel[i] = lv;
                        turnStat.trainLevelCount[i] = 0;//如果是半途开启小黑板，则会在下一次升级时变成正确的计数
                    }
                    if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc))
                        outputItems[i] = lv < 5 ? $"Lv{lv}(+{turnStat.trainLevelCount[i]})" : $"Lv{lv}";
                    else
                        outputItems[i] = $"Lv{lv}";
                }
            }
            outputItems.CopyTo(tableRows[4], 0);
            separatorLine.CopyTo(tableRows[5], 0);

            //显示此次训练可获得的属性和Pt
            var bestScore = -100;
            var bestTrain = -1;
            for (var i = 0; i < 5; i++)
            {
                var tid = GameGlobal.TrainIds[i];
                var stats = trainStats[i];
                var score = stats.FiveValueGain.Sum();
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTrain = i;
                }
                outputItems[i] = $"{score}";
            }
            for (var i = 0; i < 5; i++)
            {
                if (i == bestTrain)
                    outputItems[i] = $"属性:{outputItems[i]}(最高)|Pt:{trainStats[i].PtGain}";
                else
                    outputItems[i] = $"属性:{outputItems[i]}|Pt:{trainStats[i].PtGain}";
            }
            outputItems.CopyTo(tableRows[6], 0);

            //以下几项用于计算单次训练能充多少格
            var LArcRivalBoostCount = new int[,] { { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 } };// 五种训练的充电槽为0,1,2格的个数
            var LArcShiningCount = new int[] { 0, 0, 0, 0, 0 };//彩圈个数
            var LArcfriendAppear = new bool[] { false, false, false, false, false };//友人在不在

            // 当前S卡卡组
            var supportCards = @event.data.chara_info.support_card_array.ToDictionary(x => x.position, x => x.support_card_id);
            var commandInfo = new Dictionary<int, string[]>();
            foreach (var command in @event.data.home_info.command_info_array)
            {
                if (!GameGlobal.ToTrainIndex.ContainsKey(command.command_id)) continue;
                var trainIdx = GameGlobal.ToTrainIndex[command.command_id];

                var tips = command.tips_event_partner_array.Intersect(command.training_partner_array); //红感叹号 || Hint
                var partners = command.training_partner_array
                    .Select(partner =>
                    {
                        turnStat.isTraining = true;
                        var priority = PartnerPriority.默认;

                        // partner是当前S卡卡组的index（1~6，7是啥？我忘了）或者charaId（10xx)
                        var name = partner >= 1 && partner <= 7
                            ? Database.Names.DisplayNickname(supportCards[partner])
                            : Database.Names.DisplayNickname(partner);
                        var friendship = @event.data.chara_info.evaluation_info_array.First(x => x.target_id == partner).evaluation;
                        var isArcPartner = ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) && (partner > 1000 || (partner >= 1 && partner <= 7)) && @event.data.arc_data_set.evaluation_info_array.Any(x => x.target_id == partner);
                        var nameAppend = "";
                        var shouldShining = false; // 是不是友情训练
                        if (partner >= 1 && partner <= 7)
                        {
                            priority = PartnerPriority.其他;
                            if (name.Contains("[友]")) // 友人单独标绿
                            {
                                priority = PartnerPriority.友人;

                                switch (supportCards[partner])
                                {
                                    case 30137: // 三女神团队卡的友情训练
                                        turnStat.venus_venusTrain = GameGlobal.ToTrainId[command.command_id];
                                        break;
                                    case 30160 or 10094: // 佐岳友人卡
                                        LArcfriendAppear[trainIdx] = true;
                                        turnStat.larc_zuoyueAtTrain[trainIdx] = true;
                                        break;
                                    case 30188 or 10104:    // 都留岐涼花
                                        turnStat.uaf_friendAtTrain[trainIdx] = true;
                                        break;
                                    case 30207 or 10109:    // 理事长
                                        turnStat.cook_friendAtTrain[trainIdx] = true;
                                        break;
                                }
                            }
                            else if (friendship < 80) // 羁绊不满80，无法触发友情训练标黄
                            {
                                priority = PartnerPriority.羁绊不足;
                            }

                            //闪彩标蓝
                            {
                                //在得意位置上
                                var commandId1 = GameGlobal.ToTrainId[command.command_id];
                                shouldShining = friendship >= 80 &&
                                    name.Contains(commandId1 switch
                                    {
                                        101 => "[速]",
                                        105 => "[耐]",
                                        102 => "[力]",
                                        103 => "[根]",
                                        106 => "[智]",
                                        _ => string.Empty
                                    });
                                //GM杯检查
                                if (ScenarioExtensions.IsScenario(@event, ScenarioType.GrandMasters) && @event.data.venus_data_set.venus_spirit_active_effect_info_array.Any(x => x.chara_id == 9042 && x.effect_group_id == 421)
                                    && (name.Contains("[速]") || name.Contains("[耐]") || name.Contains("[力]") || name.Contains("[根]") || name.Contains("[智]")))
                                {
                                    shouldShining = true;
                                }

                                if ((supportCards[partner] == 30137 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 102)) || //神团
                                (supportCards[partner] == 30067 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 101)) || //皇团
                                (supportCards[partner] == 30081 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 100)) //天狼星
                                )
                                {
                                    shouldShining = true;
                                }
                            }

                            if (shouldShining)
                            {
                                LArcShiningCount[trainIdx] += 1;
                                if (name.Contains("[友]"))
                                {
                                    priority = PartnerPriority.友人;
                                }
                                else
                                {
                                    priority = PartnerPriority.闪;
                                }
                            }
                        }
                        else
                        {
                            if (partner >= 100 && partner < 1000)//理事长、记者等
                            {
                                priority = PartnerPriority.关键NPC;
                            }
                            else if (isArcPartner) // 凯旋门的其他人
                            {
                                priority = PartnerPriority.无用NPC;
                            }
                        }

                        if ((partner >= 1 && partner <= 7) || (partner >= 100 && partner < 1000))//支援卡，理事长，记者，佐岳
                            if (friendship < 100) //羁绊不满100，显示羁绊
                                nameAppend += $"({friendship})";

                        if (isArcPartner && !LArcIsAbroad)
                        {
                            var chara_id = @event.data.arc_data_set.evaluation_info_array.First(x => x.target_id == partner).chara_id;
                            if (@event.data.arc_data_set.arc_rival_array.Any(x => x.chara_id == chara_id))
                            {
                                var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == chara_id);
                                var rival_boost = arc_data.rival_boost;
                                var effectId = arc_data.selection_peff_array.First(x => x.effect_num == arc_data.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                                if (rival_boost != 3)
                                {
                                    if (priority > PartnerPriority.需要充电) priority = PartnerPriority.需要充电;
                                    LArcRivalBoostCount[trainIdx, rival_boost] += 1;

                                    nameAppend += $":{rival_boost}{GameGlobal.LArcSSEffectNamesShort[effectId]}";
                                }
                            }
                        }

                        name += nameAppend;
                        name = tips.Contains(partner) ? $"!{name}" : name; //有Hint就加个红感叹号，和游戏内表现一样

                        return (priority, name);
                    }).ToArray();

                // 按照优先级排序
                commandInfo.Add(command.command_id, partners.OrderBy(s => s.priority).Select(x => x.name).ToArray());
            }
            if (!commandInfo.SelectMany(x => x.Value).Any())
                return string.Join(Environment.NewLine, rows);
            //LArc充电槽计数
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) && !LArcIsAbroad)
            {
                for (var i = 0; i < 5; i++)
                {
                    var chargedNum = LArcRivalBoostCount[i, 0] + LArcRivalBoostCount[i, 1] + LArcRivalBoostCount[i, 2];
                    var chargedFullNum = LArcRivalBoostCount[i, 2];
                    if (LArcShiningCount[i] >= 1)
                    {
                        chargedNum += LArcRivalBoostCount[i, 0] + LArcRivalBoostCount[i, 1];
                        chargedFullNum += LArcRivalBoostCount[i, 1];
                    }
                    if (LArcShiningCount[i] >= 2)
                    {
                        chargedNum += LArcRivalBoostCount[i, 0];
                        chargedFullNum += LArcRivalBoostCount[i, 0];
                    }
                    outputItems[i] = $"格数{chargedNum}{(LArcfriendAppear[i] ? "+友" : string.Empty)}|满数{chargedFullNum}";
                }
                outputItems.CopyTo(tableRows[7], 0);
            }

            separatorLine.CopyTo(tableRows[8], 0);
            for (var i = 0; i < 5; ++i)
            {
                commandInfo.Select(x => x.Value.Length > i ? x.Value[i] : string.Empty).ToArray().CopyTo(tableRows[9 + i], 0);//第8行预留位置
            }

            if (ScenarioExtensions.IsScenario(@event, ScenarioType.MakeANewTrack) && @event.data.free_data_set != null)
            {
                var freeDataSet = @event.data.free_data_set;
                var coinNum = freeDataSet.coin_num;
                var inventory = freeDataSet.user_item_info_array?.ToDictionary(x => x.item_id, x => x.num) ?? [];
                var shouldPromoteTea = inventory.ContainsKey(2301) ||  //包里或者商店里有加干劲的道具
                    inventory.ContainsKey(2302) ||
                    freeDataSet.pick_up_item_info_array.Any(x => x.item_id == 2301) ||
                    freeDataSet.pick_up_item_info_array.Any(x => x.item_id == 2302);
                var currentTurn = @event.data.chara_info.turn;

                var itemRows = new List<List<string>> { new(), new(), new(), new(), new() };
                var k = 0;
                foreach (var j in freeDataSet.pick_up_item_info_array
                    .Where(x => x.item_buy_num != 1)
                    .GroupBy(x => x.item_id))
                {
                    if (k == 5) k = 0;
                    var name = Database.ClimaxItem[j.First().item_id];
                    if (name.Contains("+15") ||
                        name.Contains("体力+") ||
                        (name == "苦茶" && shouldPromoteTea) ||
                        name == "BBQ" ||
                        name == "切者" ||
                        name == "哨子" ||
                        name == "60%喇叭" ||
                        name == "御守" ||
                        name == "蹄铁・極"
                        )
                        name = $"*{name}";
                    var itemCount = j.Count();
                    var remainTurn = j.First().limit_turn == 0 ? ((currentTurn - 1) / 6 + 1) * 6 + 1 - currentTurn : j.First().limit_turn + 1 - currentTurn;
                    var remainTurnRemind = $"{remainTurn}T";
                    if (remainTurn == 3)
                        remainTurnRemind = $"!{remainTurnRemind}";
                    else if (remainTurn == 2)
                        remainTurnRemind = $"!{remainTurnRemind}";
                    else if (remainTurn == 1)
                        remainTurnRemind = $"!{remainTurnRemind}";
                    itemRows[k].Add($"{name}:{itemCount}/{remainTurnRemind}");
                    k++;
                }
                for (var i = 0; i < 5; ++i)
                {
                    footers[i] = string.Join(" / ", itemRows[i]);
                }
            }
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.GrandMasters))
            {
                foreach (var i in @event.data.venus_data_set.venus_chara_command_info_array)
                {
                    switch (i.command_type)
                    {
                        case 1:
                            switch (i.command_id)
                            {
                                case 101 or 601:
                                    headers[0] = $"速{failureRateStr[0]} | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}"; break;
                                case 102 or 603:
                                    headers[2] = $"力{failureRateStr[2]} | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}"; break;
                                case 103 or 604:
                                    headers[3] = $"根{failureRateStr[3]} | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}"; break;
                                case 105 or 602:
                                    headers[1] = $"耐{failureRateStr[1]} | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}"; break;
                                case 106 or 605:
                                    headers[4] = $"智{failureRateStr[4]} | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}"; break;
                            }
                            break;
                        case 3:
                            footers[0] = $"出行 | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}";
                            break;
                        case 4:
                            footers[2] = $"比赛 | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}";
                            break;
                        case 7:
                            footers[1] = $"休息 | {GameGlobal.GrandMastersSpiritNames[i.spirit_id]}{(i.is_boost == 1 ? "x2" : string.Empty)}";
                            break;
                    }
                }
            }
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc) && @event.data.arc_data_set.selection_info != null)
            {
                var selectedRivalCount = @event.data.arc_data_set.selection_info.selection_rival_info_array.Length;
                turnStat.larc_SSPersonCount = selectedRivalCount;
                turnStat.larc_isSSS = @event.data.arc_data_set.selection_info.is_special_match == 1;
                for (var i = 0; i < selectedRivalCount; i++)
                {
                    var rival = @event.data.arc_data_set.selection_info.selection_rival_info_array[i];
                    var rivalName = Database.Names.DisplayNickname(rival.chara_id);
                    if (selectedRivalCount == 5)
                    {
                        var sc = supportCards.Values.FirstOrDefault(sc => rival.chara_id == Database.Names.GetRequiredSupportCard(sc).CharaId); // SS Match中的S卡，值为defau时即为NPC
                        if (@event.data.arc_data_set.selection_info.selection_rival_info_array[i].mark != 1)
                            rivalName = $"{rivalName}(可能失败)";
                        else if (sc != default && @event.data.chara_info.evaluation_info_array[supportCards.First(x => x.Value == sc).Key - 1].evaluation < 80)
                            rivalName = $"{rivalName}(羁绊不足80)"; // 羁绊不满80的S卡
                        else if (@event.data.arc_data_set.selection_info.is_special_match == 1)
                            rivalName = $"{rivalName}(SSS)"; // SSS Match
                    }

                    var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == rival.chara_id);
                    var effectId = arc_data.selection_peff_array.First(x => x.effect_num == arc_data.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                    rivalName += $"({GameGlobal.LArcSSEffectNames[effectId]})";
                    tableRows[i][5] = rivalName;
                }
                // 把攒满但没进ss的人头也显示在下面
                if (selectedRivalCount == 5)
                {
                    tableRows[5][5] = separatorLineSSMatch;

                    var otherChargedRivals = @event.data.arc_data_set.arc_rival_array
                        .Where(rival =>
                               !(rival.selection_peff_array == null // 马娘自身
                            || rival.rival_boost != 3 // 没攒满
                            || @event.data.arc_data_set.selection_info.selection_rival_info_array.Any(x => x.chara_id == rival.chara_id)) // 已经在ss训练中了
                            );
                    if (otherChargedRivals.Any())
                    {
                        tableRows[6][5] = "其他满格人头:";
                        var chargedRivalCount = 0;
                        foreach (var rival in otherChargedRivals)
                        {
                            chargedRivalCount++;
                            if (chargedRivalCount > 5) break;
                            var rivalName = Database.Names.DisplayNickname(rival.chara_id);
                            var effectId = rival.selection_peff_array.First(x => x.effect_num == rival.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                            rivalName += $"({GameGlobal.LArcSSEffectNames[effectId]})";
                            tableRows[chargedRivalCount + 6][5] = rivalName;
                        }

                        if (otherChargedRivals.Count() > 5)//有没显示的
                        {
                            tableRows[12][5] = $"... + {otherChargedRivals.Count() - 5} 人";
                        }
                    }
                }
                // 增加当前SS训练属性和PT的显示
                if (selectedRivalCount > 0)
                {
                    var totalStats = 0;
                    var totalPt = 0;
                    var totalVital = 0;
                    if (@event.data.arc_data_set.selection_info.params_inc_dec_info_array != null)
                    {
                        totalStats += @event.data.arc_data_set.selection_info.params_inc_dec_info_array
                            .Where(x => x.target_type >= 1 && x.target_type <= 5)
                            .Sum(x => x.value);
                        totalPt += @event.data.arc_data_set.selection_info.params_inc_dec_info_array
                            .Where(x => x.target_type == 30)
                            .Sum(x => x.value);
                        totalVital += @event.data.arc_data_set.selection_info.params_inc_dec_info_array
                            .Where(x => x.target_type == 10)
                            .Sum(x => x.value);
                    }
                    if (@event.data.arc_data_set.selection_info.bonus_params_inc_dec_info_array != null)
                    {
                        totalStats += @event.data.arc_data_set.selection_info.bonus_params_inc_dec_info_array
                            .Where(x => x.target_type >= 1 && x.target_type <= 5)
                            .Sum(x => x.value);
                        totalPt += @event.data.arc_data_set.selection_info.bonus_params_inc_dec_info_array
                            .Where(x => x.target_type == 30)
                            .Sum(x => x.value);
                        totalVital += @event.data.arc_data_set.selection_info.bonus_params_inc_dec_info_array
                            .Where(x => x.target_type == 10)
                            .Sum(x => x.value);
                    }
                    tableRows[13][5] = $"属性:{totalStats}|Pt:{totalPt}";
                }
            }
            rows.Add(RenderTable(headers, tableRows, footers));

            //远征/没买友情+20%或者pt+10警告
            if (ScenarioExtensions.IsScenario(@event, ScenarioType.LArc))
            {
                //两次远征分别是37,60回合
                if (turnNum >= 34 && turnNum < 37)
                    rows.Add($"还有{37 - turnNum}回合第二年远征！");
                else if (turnNum >= 55 && turnNum < 60)
                    rows.Add($"还有{60 - turnNum}回合第三年远征！");
                if (turnNum == 59)
                {
                    rows.Add("下回合第三年远征！");
                    rows.Add("下回合第三年远征！");
                    rows.Add("下回合第三年远征！（重要的事情说三遍）");
                }

                if (turnNum > 42)
                {
                    //十个升级的id分别是
                    //  2 5
                    // 1 4 6
                    // 3 7 8
                    //  9 10
                    //检查是否买了友情+20
                    var friendLevel = @event.data.arc_data_set.arc_info.potential_array.First(x => x.potential_id == 8).level;
                    var ptLevel = @event.data.arc_data_set.arc_info.potential_array.First(x => x.potential_id == 3).level;
                    if (friendLevel < 3)//没买友情
                    {
                        var cost = friendLevel == 2 ? 300 : 500;
                        if (@event.data.arc_data_set.arc_info.global_exp >= cost)
                        {
                            rows.Add("没买友情+20%！");
                            rows.Add("没买友情+20%！");
                            rows.Add("没买友情+20%！（重要的事情说三遍）");
                        }
                    }
                    else if (ptLevel < 3)//买了友情但没买pt+10
                    {
                        var cost = ptLevel == 2 ? 200 : 400;
                        if (@event.data.arc_data_set.arc_info.global_exp >= cost)
                        {
                            rows.Add("没买pt+10！");
                            rows.Add("没买pt+10！");
                            rows.Add("没买pt+10！（重要的事情说三遍）");
                        }
                    }

                    // 大逃日本杯提示
                    if (turnNum == 46)
                        rows.Add("日本杯！拿大逃别忘了打！");
                }
            }
            if (!isRepeat)
                EventLogger.CommitScenarioTurn(
                    @event.data.chara_info.scenario_id,
                    turnNum,
                    turnStat,
                    ssRivalsSpecialBuffs);
            return string.Join(Environment.NewLine, rows);
        }

        static string RenderTable(string[] headers, string[][] rows, string[] footers)
        {
            var builder = new StringBuilder()
                .AppendLine(string.Join(" | ", headers));
            foreach (var row in rows.Where(row => row.Any(cell => !string.IsNullOrEmpty(cell))))
                builder.AppendLine(string.Join(" | ", row));
            if (footers.Any(footer => !string.IsNullOrEmpty(footer)))
                builder.AppendLine(string.Join(" | ", footers));
            return builder.ToString().TrimEnd();
        }
    }
}
