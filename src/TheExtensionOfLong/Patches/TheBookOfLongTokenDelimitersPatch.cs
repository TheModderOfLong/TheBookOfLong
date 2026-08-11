
using HarmonyLib;
using System.Collections.Generic;
using TheBookOfLong;

namespace TheExtensionOfLong
{
    // 目标：修改OtherMod中TargetClass类的PublicProperty属性的getter返回值
    [HarmonyPatch(typeof(SymbolicIdTokenDelimiters), "TokenDelimiters", MethodType.Getter)]
    public class TheBookOfLongTokenDelimitersPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref List<char> __result)
        {
            // 修改getter的返回值
            __result = new List<char> {
                // 本体指令分隔符
                ';',
                '|',
                '-',
                '/',
                '~',
                ':',
                // 标准文本分隔符
                ',',
                // 路径和URL相关
                //'\\',
                '.',
                '?',
                '#',
                '&',
                '=',
                '@',
                // 编程语言常见分隔符
                // '_',  // 允许使用下划线
                '+',
                '*',
                '%',
                '$',
                '!',
                '^',
                '(',
                ')',
                '[',
                ']',
                '{',
                '}',
                '<',
                '>',
                '"',
                //'\'',
            };
            return false; // 返回false表示不执行原getter方法
        }
    }
}
