using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 被关闭内容包的 Def 已经在补丁阶段被摘掉，但它们的 DefInjected 翻译仍然存在，
    /// 注入时会产生"找不到对应 Def"的翻译错误日志。这里在注入前把属于被摘除 Def 的条目剔掉。
    /// 只在启动/切换语言时执行一次，开销与内容包数量成正比。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ContentPackTranslationCleanup
    {
        private static readonly HashSet<string> RemovedDefNames = new HashSet<string>();
        private static bool pruned;

        static ContentPackTranslationCleanup()
        {
            new Harmony("unscLatios.LatiosArsenal.translations")
                .PatchAll(typeof(ContentPackTranslationCleanup).Assembly);
        }

        public static void RegisterRemovedDef(string defName)
        {
            if (!defName.NullOrEmpty())
            {
                RemovedDefNames.Add(defName);
            }
        }

        internal static void PruneOnce()
        {
            if (pruned)
            {
                return;
            }
            pruned = true;
            if (RemovedDefNames.Count == 0)
            {
                return;
            }

            LoadedLanguage language = LanguageDatabase.activeLanguage;
            if (language?.defInjections == null)
            {
                return;
            }

            int removedCount = 0;
            for (int i = 0; i < language.defInjections.Count; i++)
            {
                DefInjectionPackage package = language.defInjections[i];
                if (package?.injections == null)
                {
                    continue;
                }

                List<string> toRemove = null;
                foreach (KeyValuePair<string, DefInjectionPackage.DefInjection> pair in package.injections)
                {
                    int dot = pair.Key.IndexOf('.');
                    string defName = dot > 0 ? pair.Key.Substring(0, dot) : pair.Key;
                    if (RemovedDefNames.Contains(defName))
                    {
                        (toRemove ??= new List<string>()).Add(pair.Key);
                    }
                }
                if (toRemove == null)
                {
                    continue;
                }
                for (int j = 0; j < toRemove.Count; j++)
                {
                    package.injections.Remove(toRemove[j]);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Log.Message("[LatiosArsenal] 已跳过 " + removedCount + " 条被关闭内容包的翻译注入");
            }
        }
    }

    [HarmonyPatch(typeof(LoadedLanguage), nameof(LoadedLanguage.InjectIntoData_BeforeImpliedDefs))]
    public static class Patch_LoadedLanguage_InjectIntoData
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            ContentPackTranslationCleanup.PruneOnce();
        }
    }
}
