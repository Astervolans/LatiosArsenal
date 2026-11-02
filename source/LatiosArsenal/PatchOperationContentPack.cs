using System.Xml;
using UnityEngine;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 内容包开关的补丁操作。挂在 1.7/Patches/LatiosArsenal_ContentPacks.xml，
    /// 在 Def 实例化之前（LoadedModManager.ApplyPatches）把被关闭内容包对应的 XML 节点删掉，
    /// 因此关闭的内容包不会被加载进游戏，也不占用 DefDatabase。
    ///
    /// 约定：一条 Operation 只处理一个内容包 + 一条 xpath。
    /// 任何异常/设置读不到时都按"启用"处理（fail-open），避免设置损坏导致内容凭空消失。
    /// </summary>
    public class PatchOperationContentPack : PatchOperation
    {
        public string pack;
        public string xpath;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (pack.NullOrEmpty() || xpath.NullOrEmpty())
            {
                Log.Error("[LatiosArsenal] PatchOperationContentPack 缺少 pack 或 xpath");
                return false;
            }

            bool enabled;
            try
            {
                enabled = LatiosArsenalMod.IsPackEnabled(pack);
            }
            catch (System.Exception e)
            {
                Log.Warning("[LatiosArsenal] 读取内容包设置失败，按启用处理（" + pack + "）: " + e.Message);
                enabled = true;
            }

            if (enabled)
            {
                return true;
            }

            LatiosArsenalMod.DisabledAtLoad.Add(pack);

            XmlNodeList nodes = xml.SelectNodes(xpath);
            if (nodes == null || nodes.Count == 0)
            {
                // 节点可能被其他模组先行移除；内容包本身关闭是既成事实，不当作补丁失败
                return true;
            }

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                XmlNode node = nodes[i];
                if (node is XmlElement element)
                {
                    // 记录被摘掉的 defName：翻译注入阶段会跳过这些键，避免"找不到 Def"的翻译错误
                    string defName = element.SelectSingleNode("defName")?.InnerText;
                    ContentPackTranslationCleanup.RegisterRemovedDef(defName);
                }
                node.ParentNode?.RemoveChild(node);
            }

            Log.Message("[LatiosArsenal] 内容包已关闭: " + pack + "（移除 " + nodes.Count + " 个 Def 节点）");
            return true;
        }
    }
}
