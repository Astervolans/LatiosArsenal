using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 内容包注册表。id 是设置文件与补丁里的稳定标识，不要随意改动；
    /// label/description 走 Keyed 翻译，供设置面板显示。
    /// </summary>
    public sealed class ContentPackInfo
    {
        public readonly string Id;
        public readonly string LabelKey;
        public readonly string DescriptionKey;

        public ContentPackInfo(string id, string labelKey, string descriptionKey)
        {
            Id = id;
            LabelKey = labelKey;
            DescriptionKey = descriptionKey;
        }

        public string Label => LabelKey.Translate();

        public string Description => DescriptionKey.Translate();
    }

    public static class ContentPacks
    {
        public const string WeaponsMechanoidLaunchers = "weapons_mechanoid_launchers";
        public const string MedicalBioFoam = "medical_biofoam";

        /// <summary>
        /// 全部内容包。新增内容包时：在此登记 + 在 1.7/Patches/LatiosArsenal_ContentPacks.xml
        /// 增加一条 PatchOperationContentPack（xpath 列出该包的全部 Def）。
        /// </summary>
        public static readonly ContentPackInfo[] All =
        {
            new ContentPackInfo(
                WeaponsMechanoidLaunchers,
                "LatiosArsenal.Pack.Weapons.Label",
                "LatiosArsenal.Pack.Weapons.Description"),
            new ContentPackInfo(
                MedicalBioFoam,
                "LatiosArsenal.Pack.Medical.Label",
                "LatiosArsenal.Pack.Medical.Description")
        };

        public static ContentPackInfo Get(string id)
        {
            return All.FirstOrDefault(p => p.Id == id);
        }

        public static IEnumerable<string> AllIds => All.Select(p => p.Id);

        /// <summary>当前实际启用的内容包 id（顺序与 All 一致，便于存档快照比较）。</summary>
        public static List<string> EnabledIds()
        {
            var result = new List<string>();
            for (int i = 0; i < All.Length; i++)
            {
                if (LatiosArsenalMod.IsPackEnabled(All[i].Id))
                {
                    result.Add(All[i].Id);
                }
            }
            return result;
        }
    }
}
