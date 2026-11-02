using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 存档中途调整内容包的提示通道。
    /// 内容包是在启动时决定加不加载的，所以存档里的开关状态可能与"当前运行"不同：
    ///  - 存档里有、现在关闭 -> 该存档内相关物品/装备已被移除，需要明确告知玩家；
    ///  - 存档里没有、现在开启 -> 只是新内容可用，提示一下即可。
    /// ExposeData 只写一个小字符串列表，读档开销可以忽略。
    /// </summary>
    public class GameComponent_LatiosArsenalPacks : GameComponent
    {
        private List<string> packsEnabledAtSave;

        // 1.7 的 GameComponent 基类没有 (Game) 构造函数，但 Game.FillComponents 用
        // Activator.CreateInstance(type, game) 实例化，所以派生类必须保留这个签名，且不调用 base(game)。
        public GameComponent_LatiosArsenalPacks(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref packsEnabledAtSave, "latiosArsenalPacksEnabledAtSave", LookMode.Value);
        }

        public override void StartedNewGame()
        {
            packsEnabledAtSave = ContentPacks.EnabledIds();
        }

        public override void LoadedGame()
        {
            List<string> current = ContentPacks.EnabledIds();

            if (packsEnabledAtSave == null)
            {
                // 旧存档没有快照：只记录，不打扰玩家
                packsEnabledAtSave = current;
                return;
            }

            List<string> disabledSinceSave = packsEnabledAtSave.Where(id => !current.Contains(id)).ToList();
            List<string> enabledSinceSave = current.Where(id => !packsEnabledAtSave.Contains(id)).ToList();
            if (disabledSinceSave.Count == 0 && enabledSinceSave.Count == 0)
            {
                return;
            }

            packsEnabledAtSave = current;

            if (disabledSinceSave.Count > 0)
            {
                string names = string.Join("、", disabledSinceSave.Select(DisplayName).ToArray());
                string text = "LatiosArsenal_PackChange_Disabled".Translate(names);
                Log.Warning("[LatiosArsenal] " + text);
                Messages.Message(text, MessageTypeDefOf.CautionInput, historical: true);
            }
            if (enabledSinceSave.Count > 0)
            {
                string names = string.Join("、", enabledSinceSave.Select(DisplayName).ToArray());
                string text = "LatiosArsenal_PackChange_Enabled".Translate(names);
                Log.Message("[LatiosArsenal] " + text);
                Messages.Message(text, MessageTypeDefOf.NeutralEvent, historical: true);
            }
        }

        private static string DisplayName(string packId)
        {
            ContentPackInfo pack = ContentPacks.Get(packId);
            return pack != null ? pack.Label : packId;
        }
    }
}
