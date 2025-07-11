using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UmamusumeResponseAnalyzer.Localization.Game;

namespace OldScenarioAnalyzer
{
    public static partial class GameGlobal
    {
        #region LARC
        public static readonly int[] LArcTrainBonusEvery5Percent = [0, 5, 8, 10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 30, 31, 31, 32, 32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 38, 39, 39, 40];
        public static readonly int[] LArcLessonMappingInv = [2, 5, 1, 4, 6, 3, 7, 8, 9, 10];
        public static readonly FrozenDictionary<int, string> LArcSSEffectNameFullColored = new Dictionary<int, string>
        {
            { 1, "技能hint" },
            { 3, "[#00ff00]体力[/]" },
            { 4, "[#00ffff]体力与上限[/]" },//最好的，用亮色
            { 5, "[#00ff00]心情体力[/]" },
            { 6, "充电" },
            { 7, "适性pt" },
            { 8, "[#00ff00]爱娇[/]" },
            { 9, "上手" },
            { 11, "属性" },
            { 12, "[#0000ff]技能点[/]" } //最烂的，用个深色
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, string> LArcSSEffectNameColoredShort = new Dictionary<int, string>
        {
            { 1, "技" },
            { 3, "[#00ff00]体[/]" },
            { 4, "[#00ffff]体[/]" },
            { 5, "[#00ff00]心[/]" },
            { 6, "充" },
            { 7, "适" },
            { 8, "[#ffff00]娇[/]" },
            { 9, "练" },
            { 11, "属" },
            { 12, "pt" },
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, string> LArcSSEffectNameColored = new Dictionary<int, string>
        {
            { 1, "技能" } ,
            { 3, "[#00ff00]体力[/]" },
            { 4, "[#00ffff]体力[/]" },
            { 5, "[#00ff00]心情[/]" },
            { 6, "[#ff00ff]充电[/]" },
            { 7, "适pt" },
            { 8, "[#ffff00]爱娇[/]" },
            { 9, "上手" },
            { 11, "属性" },
            { 12, "技pt" }
        }.ToFrozenDictionary();
        #endregion

        #region GM
        public static readonly FrozenDictionary<int, string> GrandMastersSpiritNamesColored = new Dictionary<int, string>
        {
            { 1, $"[red]{I18N_SpeedSimple}[/]" },
            { 2, $"[red]{I18N_StaminaSimple}[/]" },
            { 3, $"[red]{I18N_PowerSimple}[/]" },
            { 4, $"[red]{I18N_NutsSimple}[/]" },
            { 5, $"[red]{I18N_WizSimple}[/]" },
            { 6, $"[red]星[/]" },
            { 9, $"[blue]{I18N_SpeedSimple}[/]" },
            { 10, $"[blue]{I18N_StaminaSimple}[/]" },
            { 11, $"[blue]{I18N_PowerSimple}[/]" },
            { 12, $"[blue]{I18N_NutsSimple}[/]" },
            { 13, $"[blue]{I18N_WizSimple}[/]" },
            { 14, $"[blue]星[/]" },
            { 17, $"[yellow]{I18N_SpeedSimple}[/]" },
            { 18, $"[yellow]{I18N_StaminaSimple}[/]" },
            { 19, $"[yellow]{I18N_PowerSimple}[/]" },
            { 20, $"[yellow]{I18N_NutsSimple}[/]" },
            { 21, $"[yellow]{I18N_WizSimple}[/]" },
            { 22, $"[yellow]星[/]" }
        }.ToFrozenDictionary();
        #endregion
    }
}
