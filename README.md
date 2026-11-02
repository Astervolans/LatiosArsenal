# Latios' Arsenal

RimWorld 1.7 的 **Combat Extended** 武器/医疗内容包式模组：用回收的机械族材料拼装发射器，配套一个腰带式生物泡沫注射器。
所有内容按「内容包」组织，可以在模组设置里单独开关；关闭的内容包在 Def 载入前就被整段摘掉，不进入游戏。

- packageId：`UNSCLatios.LatiosArsenal`
- 游戏版本：RimWorld **1.7**
- 依赖：**Harmony**、**Combat Extended（1.7 版）**、**Biotech**（武器研究线使用机械科技项目）
- 作者：UNSCLatios

## 内容

### 武器 - 机械族回收发射器（`weapons_mechanoid_launchers`）

| 武器 | 弹药（CE 现有物品） | 弹容 | 射程 | 散布 | 重量 | 研究 |
|---|---|---|---|---|---|---|
| 紧凑机械族热能弹发射器（`Latios_BL66_Thermal`） | `Ammo_66mmThermalBolt_Incendiary` | 6（BL 逐发） | 42 | 0.16 | 7.5 | 基础机械科技 + 精密装配 |
| 紧凑机械族燃料弹发射器（`Latios_BL80_Fuel`） | `Ammo_80x256mmFuel_Incendiary` | 3（BL 逐发） | 38 | 0.20 | 9.0 | 基础机械科技 + 精密装配 |
| 肩扛式机械族热能弹发射器（`Latios_HL66_Thermal`） | `Ammo_66mmThermalBolt_Incendiary` | 6（HL 整弹匣） | 56 | 0.05 | 11.5 | 高级机械科技 + 精密装配 |
| 肩扛式机械族燃料弹发射器（`Latios_HL80_Fuel`） | `Ammo_80x256mmFuel_Incendiary` | 3（HL 整弹匣） | 53 | 0.07 | 12.5 | 高级机械科技 + 精密装配 |
| 肩扛式机械族爆破弹发射器（`Latios_HL164_Demo`） | `Ammo_164x284mmDemo` | 2（HL 整弹匣） | 50 | 0.10 | 13.5 | 高级机械科技 + 精密装配 |

- 装填：BL 系列逐发压弹（1.2 s / 发、1.6 s / 发）；HL 系列直接更换转轮弹舱，整个弹匣一次完成（3.0 s / 4.5 s / 2.4 s）。
- 制作：精密装配台；BL 50 钢铁 + 2 机械族残块 + 5 零部件，HL 60 钢铁 + 3 机械族残块 + 6 零部件。
- 弹药直接复用 Combat Extended 的机械族重型弹药，战场上缴获的炮弹可直接装填。

### 高抛发射模式

五把武器各带一个「高抛」模式（武器 gizmo 切换）：

- 弹头换成 `Latios_Bullet_*_Lob`：`flyOverhead=true`（CE 原生越障弹道）、速度 ×0.6、`spreadMult=1.5`、`gravityFactor=10`；
- 瞄准时间 ×1.5、冷却时间 ×1.5；
- 实现方式：`CombatExtended.Verb_ShootCE.get_Projectile` 后置替换弹头、`Verb.WarmupTime` 后置乘倍数、`ThingComp.GetStatFactor` 处理冷却。
- 射程安全：`CombatExtended.Verb_LaunchProjectileCE.EffectiveRange` 后置把有效射程夹到当前弹头的弹道上限
  （BL-66 ≈ 70.8 格、HL-164 ≈ 91 格，其余更高），避免基因/植入物给的射程系数把高抛打成“打不到还刷 CE 警告”。

### 医疗 - 生物泡沫（`medical_biofoam`）

`Latios_BiofoamInjector`（腰带槽位）：

- 穿戴者流血速度超过 100% 时，消耗 1 次充能，以 **75%** 包扎质量立刻封住最严重的一处出血伤口；
- 4 次充能；点「补充生物泡沫」会走一个装填作业（走到医药处或取用随身医药，耗时 4 秒），消耗 1 份医药恢复 2 次充能；
- 检测由「受伤」事件驱动（Harmony postfix `Pawn.PostApplyDamage`），另有 60 tick 低频兜底，不做逐 tick 健康扫描；
- 制作：机械加工台，研究「医药生产」。

## 目录结构

```text
About\            模组元数据（About.xml、Preview.png）
1.7\Defs\         按内容包分目录的 Defs（Packs\...）
1.7\Patches\      内容包开关补丁（关闭时删除对应 Def 节点）
1.7\Textures\     武器、图标、占位装备贴图
1.7\Assemblies\   构建输出（DLL 不入库，git 忽略）
Languages\        English / ChineseSimplified / ChineseTraditional
LoadFolders.xml   1.7 加载规则
source\           C# 工程（net10.0）：设置、内容包补丁、生物泡沫组件、高抛模式、Harmony 补丁
策划案\           参数表与机制说明
mod_config.example.jsonc   本地开发用配置模板（真实配置含本机路径，不入库）
```

## 构建

需要 .NET 10 SDK 与 RimWorld 1.7 安装目录（含 `Assembly-CSharp.dll`、`UnityStubs.dll`、`Waylith.dll`），
以及 Harmony 的 `0Harmony.dll`：

```powershell
dotnet build source\LatiosArsenal\LatiosArsenal.csproj -c Release `
  -p:RimWorldDir="<RimWorld 安装目录>" `
  -p:RimWorldAssemblyDir="<RimWorld 安装目录>" `
  -p:RimWorldManagedDir="<RimWorld 安装目录>" `
  -p:HarmonyDllPath="<Harmony 的 0Harmony.dll 路径>"
```

产物：`1.7\Assemblies\LatiosArsenal.dll`。

## 安装

1. 把整个仓库目录复制到 `RimWorld\Mods\`，目录名为 `LatiosArsenal`；
2. 启用 Harmony、Combat Extended、Biotech 与本模组（本模组需排在 CE 之后）；
3. 首次进入游戏后可在 `选项 → 模组设置 → Latios' Arsenal（内容包）` 里开关两个内容包（改动重启后生效）。

## 说明

- 模组不修改原版与 Combat Extended 的任何内容，只新增自己的 Def、贴图、文本与一个程序集。
- 关闭内容包属于「下次启动生效」；关闭后载入含其内容的旧存档时，这些内容会被移除，消息栏会提示差异。
- 与 CE 的接口：`CombatExtended.AmmoSetDef`、`CompProperties_AmmoUser`、`VerbPropertiesCE`/`Verb_ShootCE`、
  `ProjectilePropertiesCE`（`flyOverhead` / `spreadMult` / `gravityFactor`）。

## 致谢

- Combat Extended 团队：弹药、弹头行为与机械族武器基准。
- Harmony（Andreas Pardeike）：Harmony 框架。
- Ludeon Studios：RimWorld。
