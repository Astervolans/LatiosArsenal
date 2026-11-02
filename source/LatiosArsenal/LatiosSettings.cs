using System.Collections.Generic;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 全局设置。只保存"关闭了哪些内容包"（默认全开），
    /// 这样以后新增内容包默认就是开启的，不会因为老配置缺键而被误关。
    /// </summary>
    public class LatiosSettings : ModSettings
    {
        public List<string> disabledPacks = new List<string>();

        public bool IsEnabled(string packId)
        {
            return disabledPacks == null || !disabledPacks.Contains(packId);
        }

        public void SetEnabled(string packId, bool enabled)
        {
            if (disabledPacks == null)
            {
                disabledPacks = new List<string>();
            }
            bool currentlyEnabled = !disabledPacks.Contains(packId);
            if (enabled == currentlyEnabled)
            {
                return;
            }
            if (enabled)
            {
                disabledPacks.Remove(packId);
            }
            else
            {
                disabledPacks.Add(packId);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref disabledPacks, "disabledPacks", LookMode.Value);
            if (disabledPacks == null)
            {
                disabledPacks = new List<string>();
            }
            // 只保留仍然存在的内容包 id，避免历史配置无限增长
            disabledPacks.RemoveAll(id => id == null || ContentPacks.Get(id) == null);
        }
    }
}
