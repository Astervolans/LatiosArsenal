using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 模组入口：内容包开关的设置界面。
    /// 说明：
    ///  - 内容包是否加载由 1.7/Patches/LatiosArsenal_ContentPacks.xml 在"Def 载入前"决定，
    ///    所以开关改动只能在下一次启动游戏时生效；
    ///  - 运行期只保留一份很小的状态集合（DisabledAtLoad），不参与逐帧逻辑。
    /// </summary>
    public class LatiosArsenalMod : Mod
    {
        public static LatiosArsenalMod Instance;

        /// <summary>本次运行（Def 载入时）实际被关闭的内容包 id。由 PatchOperationContentPack 填充，恒为只读语义。</summary>
        public static readonly HashSet<string> DisabledAtLoad = new HashSet<string>();

        private static LatiosSettings settings;

        private Vector2 scrollPosition;

        public LatiosArsenalMod(ModContentPack content) : base(content)
        {
            Instance = this;
        }

        public static LatiosSettings Settings
        {
            get
            {
                if (settings == null && Instance != null)
                {
                    settings = Instance.GetSettings<LatiosSettings>();
                }
                return settings;
            }
        }

        /// <summary>读取失败时按"启用"处理（fail-open），绝不因为设置文件损坏而吞掉内容。</summary>
        public static bool IsPackEnabled(string packId)
        {
            LatiosSettings s = Settings;
            return s == null || s.IsEnabled(packId);
        }

        /// <summary>设置里的状态与本次实际加载状态是否一致（不一致就需要重启）。</summary>
        public static bool IsRestartPending(string packId)
        {
            bool configured = IsPackEnabled(packId);
            bool loaded = !DisabledAtLoad.Contains(packId);
            return configured != loaded;
        }

        public override string SettingsCategory()
        {
            return "LatiosArsenal_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            LatiosSettings s = Settings;
            if (s == null)
            {
                return;
            }

            Rect viewRect = new Rect(0f, 0f, inRect.width - 24f, 320f + s.disabledPacks.Count * 2f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("LatiosArsenal_Settings_Header".Translate());
            listing.Gap(6f);

            for (int i = 0; i < ContentPacks.All.Length; i++)
            {
                ContentPackInfo pack = ContentPacks.All[i];
                bool enabled = s.IsEnabled(pack.Id);
                bool before = enabled;
                listing.CheckboxLabeled(pack.Label, ref enabled, pack.Description);
                if (enabled != before)
                {
                    s.SetEnabled(pack.Id, enabled);
                }

                string state = IsRestartPending(pack.Id)
                    ? "LatiosArsenal_Settings_RestartPending".Translate()
                    : (DisabledAtLoad.Contains(pack.Id)
                        ? "LatiosArsenal_Settings_NotLoaded".Translate()
                        : "LatiosArsenal_Settings_Loaded".Translate());
                listing.Label("  " + state);
                listing.Gap(6f);
            }

            listing.Gap(12f);
            listing.Label("LatiosArsenal_Settings_RestartHint".Translate());
            listing.GapLine(6f);
            listing.Label("LatiosArsenal_Settings_SaveHint".Translate());
            listing.End();

            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
