# 06 技术专题：文件关联注册与注销

> 状态横幅：截至 2026-11-24，本专题 F1.1–F1.9 **主体已落地为代码**（F1.8 为 🟨 部分实现）。本页保留了经本机实测校验的设计方案；凡标注"设计/规划/可选"的条目（runas、Content Type、C 方案删 UserChoice、备份文件、互斥量）**尚未实现**，详情见 [04 D16–D18](04-progress.md#3-差距清单需求-vs-现状)。状态真源为 [02](02-feature-matrix.md#f1-文件关联对应-r1)。

---

## 1. 关联的三级解析模型

Windows 把一个扩展名映射到一个程序，走的是三级跳：

```
.ps1  ──①──▶  ProgId（如 Ps1Run.ps1）  ──②──▶  shell\<verb>\command  ──③──▶  可执行文件 + 参数
```

| 层级 | 存储位置 | 本项目要写的内容 |
|---|---|---|
| ① 扩展名 → ProgId | `HKCU\Software\Classes\.ps1\OpenWithProgids` | `Ps1Run.ps1`（REG_NONE）；**不写** `.ps1` 默认值 |
| ② ProgId → 动词 | `HKCU\Software\Classes\Ps1Run.ps1\shell\*` | `open` / `run` / `edit` |
| ③ 动词 → 命令行 | `...\shell\<verb>\command` 的默认值 | `open` 用 `--open "%1"`；`run` 用 `--run "%1"`；`edit` 用 `--edit "%1"` |

**关键前提**：这三级的查找顺序是 **UserChoice → HKCU\Software\Classes → HKLM\Software\Classes**。任何一级存在，后面的都不再被考虑。这正是本项目最大技术障碍的来源——见第 3 节。

**另一个必须知道的点**：`HKEY_CLASSES_ROOT`（HKCR）只是 `HKLM\Software\Classes` 与 `HKCU\Software\Classes` 的**合并视图**。对 HKCR 的写入最终会落到 HKLM 分支，因此**需要管理员权限**。本项目的所有写操作必须走 `HKCU\Software\Classes\...` 或 `HKLM\Software\Classes\...` 的显式路径，不要对 HKCR 直接写。

---

## 2. ProgId 与动词布局

### 2.1 注册后应有的完整树形

```
HKEY_CURRENT_USER\Software\Classes
├─ .ps1
│   ├─ (默认) = "Ps1Run.ps1"                    ← ① 设计项：代码实际未写入默认值
│   └─ OpenWithProgids
│       └─ Ps1Run.ps1 = (REG_NONE 空值)          ← 出现在"打开方式"列表
│
└─ Ps1Run.ps1                                   ← ② ProgId 定义
    ├─ (默认) = "PowerShell 脚本"
    ├─ FriendlyTypeName = "PowerShell 脚本"
    ├─ DefaultIcon
    │   └─ (默认) = "C:\...\Ps1Run.exe,0"
    └─ shell
        ├─ (默认) = "open"                       ← 双击时使用的默认动词
        ├─ open                                  ← 按设置中的运行方式处理（可能弹窗）
        │   ├─ (默认) = "打开"
        │   └─ command
        │       └─ (默认) = "\"C:\...\Ps1Run.exe\" --open \"%1\""     ← ③
        ├─ run                                   ← 右键"运行"：强制执行，跳过 Ask
        │   ├─ (默认) = "运行"
        │   └─ command
        │       └─ (默认) = "\"C:\...\Ps1Run.exe\" --run \"%1\""
        ├─ edit                                  ← 右键"编辑"：强制编辑，跳过 Ask
        │   ├─ (默认) = "编辑"
        │   └─ command
        │       └─ (默认) = "\"C:\...\Ps1Run.exe\" --edit \"%1\""
        └─ runas                                  ← 设计备选，未实现
```

> 上树中 `.ps1\(默认)` 与 `runas` 均为设计稿内容；**当前代码实际写入**：ProgId 元数据/DefaultIcon/三个动词 + `OpenWithProgids` 声明。详见 [12 附录](12-appendix-registry-map.md#4-本程序将写入删除的键已实现与未实现对照)。

### 2.2 设计取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 新建 ProgId 还是复用 `Microsoft.PowerShellScript.1` | **新建 `Ps1Run.ps1`** | 复用系统 ProgId 会污染系统默认行为，注销时难以界定"哪些是原本就有的"，且实测系统 ProgId 的 `shell` 下只有 `PowerShell7x64` 一个子键，结构不利 |
| 双击默认动词 | `open` 指向 `--open`（按设置） | 运行方式（Run/Edit/Ask）是用户可改的策略，若把 `open` 硬绑成 `--run`，"选择"模式就被绕过了（见 [05 §4.2](05-architecture-flows.md#42-双击编辑)） |
| 是否额外注册 `run` 动词 | 注册 | 右键"运行"需要"跳过 Ask 的强制执行"这一显式入口；否则用户在 `Ask` 模式下无法一键直跑 |
| 是否注册 `runas` | 不注册（设计备选未实现） | 满足"以管理员身份运行脚本"的常见诉求；不注册也不影响主流程 |
| 是否写 `Content Type` | 不写（设计备选未实现） | 让部分工具能识别为文本文件，无副作用 |

### 2.3 命令行书写规范（易错）

```
正确： "C:\Program Files\Ps1Run\Ps1Run.exe" --run "%1"
错误： C:\Program Files\Ps1Run\Ps1Run.exe --run %1
错误： "C:\Program Files\Ps1Run\Ps1Run.exe --run "%1""
```

- exe 路径**必须**用 `"` 包裹（安装目录可能含空格）。
- `%1` **必须**用 `"` 包裹（脚本路径可能含空格、中文）。
- 写入注册表时，在 C# 字符串里要写成 `"\"C:\\...\\Ps1Run.exe\" --open \"%1\""`，即**反斜杠需双写**、引号需转义。

---

## 3. UserChoice 这道墙

### 3.1 本机实测现状（2026-11-24）

| 读取项 | 实测值 |
|---|---|
| `HKCU\...\Explorer\FileExts\.ps1\UserChoice\ProgId` | `VSCode.ps1` |
| `HKCU\...\Explorer\FileExts\.ps1\UserChoice\Hash` | `aBi9Z4VDA20=` |
| `HKEY_CLASSES_ROOT\.ps1` 默认值 | `ps1_auto_file`（Explorer 自动生成的兜底 ProgId） |
| `HKCU\Software\Classes\.ps1\OpenWithProgids` | 含 `VSCode.ps1`、一个 AppX GUID |
| `HKCU\Software\Classes\.ps1` 可写性 | **可写**（无需管理员） |
| `HKLM\Software\Classes` 可写性 | **不可写**，抛 `Requested registry access is not allowed.` |

**结论**：这台机器上 `.ps1` 的真实默认程序是 VS Code，而且**不是通过普通 ProgId 建立的，而是通过 UserChoice**。这意味着：单纯注册 ProgId 在这台机器上**不会**让双击 `.ps1` 打开本程序。

### 3.2 为什么不能直接改

UserChoice 的 `Hash` 是对 `扩展名 + 用户 SID + ProgId + 时间戳` 等信息做的一个**未公开算法**的签名。规则是：

- Hash 必须与系统计算值一致，否则 Explorer 视为无效并**重置该关联**。
- 算法依赖 Windows 版本与内部实现，可能随更新改变。
- 网上流传的逆向实现属于不可靠、随时失效、且行为不透明的方案。

因此本项目**明确不做**：不写 UserChoice、不伪造 Hash。这条已写入 [01 范围外](01-requirements.md#4-明确不做范围外)。

### 3.3 三条可行路径（按推荐度排序）

| 方案 | 做法 | 能否让双击生效 | 合规性 | 需要用户操作 | 推荐度 |
|---|---|---|---|---|---|
| **A. 仅注册 ProgId** | 按第 2 节写 HKCU 键 | 仅当**不存在** UserChoice 时生效 | 完全合规 | 否 | 必做（基础） |
| **B. 引导用户确认** | 调 `SHOpenWithDialog` 弹"打开方式"窗，用户勾选"始终使用此应用" | **能**（由 Explorer 生成合法 Hash） | 完全合规 | 是（勾一次） | **首选** |
| **C. 删除 UserChoice 项** | `Registry.CurrentUser.DeleteSubKeyTree("...\FileExts\.ps1\UserChoice")` 后再注册 ProgId | **能**（回落到 ProgId 解析） | 灰色（等同移除用户此前的显式选择） | 否（但需显式确认） | **未实现**（设计备选，见 D17） |

### 3.4 推荐组合方案

```
注册流程：
 1) 先做 A（写 ProgId + 扩展名 + OpenWithProgids）        ← 无风险，必做
 2) 读 UserChoice：
    ├─ 不存在  → 完成，提示"关联已生效"
    └─ 存在    → 显示："系统当前默认程序为 VSCode.ps1，本程序暂不会生效"
                提供两个按钮：
                  [引导设置默认程序]  → B（SHOpenWithDialog）—— 已实现
                  [高级：移除当前选择] → C（设计备选，未实现，见 D17）
 3) 调用 SHChangeNotify(SHCNE_ASSOCCHANGED) 刷新图标与关联缓存
```

### 3.5 B 方案的 P/Invoke 细节

```csharp
[Flags]
internal enum OpenAsFlags : uint
{
    OAIF_ALLOW_REGISTRATION = 0x00000001,   // 显示"始终使用此应用打开"复选框
    OAIF_REGISTER_EXT       = 0x00000002,
    OAIF_EXEC              = 0x00000004,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OPENASINFO
{
    public string pcszFile;
    public string pcszClass;
    public OpenAsFlags oaifInFlags;
}

[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
internal static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO poainfo);

// 用法
var info = new OPENASINFO
{
    pcszFile = @"C:\scripts\demo.ps1",
    pcszClass = null,
    oaifInFlags = OpenAsFlags.OAIF_ALLOW_REGISTRATION,
};
int hr = SHOpenWithDialog(IntPtr.Zero, ref info);
```

要点：
- `pcszFile` 必须是**真实存在的文件路径**，否则对话框可能不显示或行为异常。
- 返回 `0` 表示用户完成选择；`0x800704C7`（`ERROR_CANCELLED`）表示用户取消，属正常分支，不要当错误弹窗。
- 调用后**必须**再发 `SHChangeNotify`，否则资源管理器可能仍显示旧图标。

### 3.6 刷新关联缓存

```csharp
const int SHCNE_ASSOCCHANGED = 0x08000000;
const int SHCNF_IDLIST      = 0x0000;

[DllImport("shell32.dll")]
internal static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
```

---

## 4. 实现步骤（对应 F1.1–F1.5）

| 步骤 | 动作 | 落点 |
|---|---|---|
| 1 | 校验 exe 自身路径（`Environment.ProcessPath` / `Assembly.Location`），确保写入的是绝对路径 | `AssociationService.Register()` |
| 2 | 创建 `HKCU\Software\Classes\Ps1Run.ps1` 及其子键 | `AssociationRegistration.BuildProgId()` |
| 3 | 写 `(默认)`、`FriendlyTypeName`、`DefaultIcon` | 同上 |
| 4 | 写 `shell\(默认)=open`、`shell\open\command`、`shell\run\command`、`shell\edit\command` | `RegisterOpenVerb()` / `RegisterRunVerb()` / `RegisterEditVerb()` |
| 5 | 写 `HKCU\Software\Classes\.ps1\(默认)=Ps1Run.ps1` | **未实现**（设计项，见 D16） |
| 6 | 写 `...\.ps1\OpenWithProgids\Ps1Run.ps1`（值类型 `REG_NONE`，空数据） | 同上 |
| 7 | 检测 UserChoice，按 §3.4 分支处理 | `UserChoiceGuard.Inspect()` |
| 8 | 发 `SHChangeNotify` | `ShellNotify.RaiseAssocChanged()` |
| 9 | 回读校验并返回结构化状态 | `AssociationService.GetStatus()` |

**保护机制（设计，未实现）**：写之前记录"原有值快照"（`.ps1` 原默认值、OpenWithProgids 原有值列表）到 `%APPDATA%\Ps1Run\association-backup.json`，供注销时精确回滚；当前代码未写备份文件，见 [04 D16](04-progress.md#3-差距清单需求-vs-现状)。

---

## 5. 注销策略

原则：**只删自己写的，不动别人的**。

| 动作 | 具体操作 | 风险控制 |
|---|---|---|
| 删除 ProgId 子树 | `DeleteSubKeyTree("Ps1Run.ps1")` | 仅删自有 ProgId，不影响 `Microsoft.PowerShellScript.1` |
| 移除扩展名声明 | 当前只删 `OpenWithProgids\Ps1Run.ps1`；`.ps1\(默认)` 原值恢复与备份回滚**未实现**（见 D16） | 防止把别人设的值清掉 |
| 移除 OpenWithProgids 项 | 只删值名 `Ps1Run.ps1`，用 `DeleteValue(name, throwOnMissingValue: false)` | 保留 `VSCode.ps1` 与 AppX 项（本机实测存在这两项） |
| **UserChoice 不处理** | 保持原样 | 若强行删除，系统可能把 `.ps1` 变成"未知类型"，反而更糟 |
| 刷新缓存 | `SHChangeNotify` | — |
| 回读确认 | 检查 ProgId 不存在、OpenWithProgids 无 `Ps1Run.ps1` | — |

**注销后用户会看到什么**：由于 UserChoice 仍是 `VSCode.ps1`（本机情况），双击 `.ps1` 会继续用 VS Code 打开——这正是"恢复到注册前状态"的正确结果。

**若此前用了 C 方案（删掉了 UserChoice）**：注销无法恢复用户原本的选择。因此 C 方案执行前必须把原 `ProgId` **与** `Hash` 一起写入备份文件，注销时尽力恢复（写入原 ProgId + 原 Hash；Hash 由原系统生成，理论上仍有效）。这是 C 方案的风险与补偿。

---

## 6. 权限与作用域

| 作用域 | 键路径 | 权限 | 适用场景 |
|---|---|---|---|
| 用户级（默认） | `HKCU\Software\Classes\...` | 无需管理员（实测可写） | 单用户安装、绿色版 |
| 机器级（可选） | `HKLM\Software\Classes\...` | **需要 UAC 提升**（实测拒绝访问） | 多用户共用、安装到 Program Files |

**提权实现**：`ElevationHelper` 用 `ProcessStartInfo { Verb = "runas", UseShellExecute = true }` 以同一 exe 带 `--register-machine` 参数重启自身。用户拒绝 UAC 时（`Win32Exception.NativeErrorCode == 1223`）界面明确提示"未能获取管理员权限"，**不自动**降级为仅用户级（与 [01 R1 边界](01-requirements.md#r1-关联注册注销) 一致）。

`app.manifest` 中 `requestedExecutionLevel` 保持 `asInvoker`，不要用 `requireAdministrator`——否则每次双击脚本都会弹 UAC，彻底破坏体验。

---

## 7. 验证方法（人工）

| 检查项 | 命令 / 操作 | 期望 |
|---|---|---|
| ProgId 已建 | `reg query "HKCU\Software\Classes\Ps1Run.ps1" /s` | 看到 `shell\open\command` 与 `shell\edit\command` |
| 扩展名已指向 | `reg query "HKCU\Software\Classes\.ps1" /ve` | 值为 `Ps1Run.ps1` |
| 出现在打开方式 | 右键 `.ps1` → 打开方式 → 更多应用 | 列表中有 `Ps1Run` |
| 双击生效 | 双击任一 `.ps1` | 按设置的动作执行/编辑；若为"选择"则先弹小窗 |
| 注销干净 | 注销后 `reg query` 上述两处 | ProgId 不存在；`.ps1` 默认值不被强行置空为他人值 |
| 未误伤 | `reg query "HKCU\Software\Classes\.ps1\OpenWithProgids"` | 仍能看到 `VSCode.ps1` |

---

## 8. 本页结论

1. **技术上完全可做**，用户级注册无需管理员，且已实测对应键可写。
2. **唯一的硬约束是 UserChoice**：它会让"注册了 ProgId 但双击没变化"成为常态，必须在 UI 中如实告知，并用 `SHOpenWithDialog` 引导用户完成最后一步。
3. **注销必须做精确回滚**，只删自有键值，保护第三方项。
4. 绝不做 Hash 伪造，这是可靠性与合规的底线。

---

← [05 架构与关键流程](05-architecture-flows.md) ｜ [首页](index.md) ｜ 下一篇：[07 PowerShell 版本探测与编码判型 →](07-tech-powershell-encoding.md)
