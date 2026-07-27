using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OldScenarioAnalyzer.LocalText;

namespace OldScenarioAnalyzer
{
    public static partial class GameGlobal
    {
        #region LARC
        public static readonly int[] LArcTrainBonusEvery5Percent = [0, 5, 8, 10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 30, 31, 31, 32, 32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 38, 39, 39, 40];
        public static readonly int[] LArcLessonMappingInv = [2, 5, 1, 4, 6, 3, 7, 8, 9, 10];
        public static readonly FrozenDictionary<int, string> LArcSSEffectNamesFull = new Dictionary<int, string>
        {
            { 1, "技能hint" },
            { 3, "体力" },
            { 4, "体力与上限" },
            { 5, "心情体力" },
            { 6, "充电" },
            { 7, "适性pt" },
            { 8, "爱娇" },
            { 9, "上手" },
            { 11, "属性" },
            { 12, "技能点" }
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, string> LArcSSEffectNamesShort = new Dictionary<int, string>
        {
            { 1, "技" },
            { 3, "体" },
            { 4, "体" },
            { 5, "心" },
            { 6, "充" },
            { 7, "适" },
            { 8, "娇" },
            { 9, "练" },
            { 11, "属" },
            { 12, "pt" },
        }.ToFrozenDictionary();
        public static readonly FrozenDictionary<int, string> LArcSSEffectNames = new Dictionary<int, string>
        {
            { 1, "技能" } ,
            { 3, "体力" },
            { 4, "体力" },
            { 5, "心情" },
            { 6, "充电" },
            { 7, "适pt" },
            { 8, "爱娇" },
            { 9, "上手" },
            { 11, "属性" },
            { 12, "技pt" }
        }.ToFrozenDictionary();
        #endregion

        #region GM
        public static readonly FrozenDictionary<int, string> GrandMastersSpiritNames = new Dictionary<int, string>
        {
            { 1, I18N_SpeedSimple },
            { 2, I18N_StaminaSimple },
            { 3, I18N_PowerSimple },
            { 4, I18N_NutsSimple },
            { 5, I18N_WizSimple },
            { 6, "星" },
            { 9, I18N_SpeedSimple },
            { 10, I18N_StaminaSimple },
            { 11, I18N_PowerSimple },
            { 12, I18N_NutsSimple },
            { 13, I18N_WizSimple },
            { 14, "星" },
            { 17, I18N_SpeedSimple },
            { 18, I18N_StaminaSimple },
            { 19, I18N_PowerSimple },
            { 20, I18N_NutsSimple },
            { 21, I18N_WizSimple },
            { 22, "星" }
        }.ToFrozenDictionary();
        #endregion
    }
}
