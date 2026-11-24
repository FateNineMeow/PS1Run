# PS1Run 项目 Wiki

> **项目定位**：一个 C# + WPF 编写的 Windows 桌面小工具，把 `.ps1` 文件的双击行为接管为"执行 / 编辑 / 每次弹窗让我选"，并能在 PowerShell 5.1 与 PowerShell 7 之间按脚本编码自动挑选解释器。

---

## ⚠️ 文档状态声明（必读）

| 项 | 值 |
|---|---|
| 文档基线日期 | 2026-11-24（代码状态同日更新） |
| 代码仓库实际状态 | `f:\Code\tool-box\src\PS1Run` 已有 `Ps1Run.slnx` 与 `Ps1Run.Core` / `Ps1Run.App` / `Ps1Run.Core.Tests` 三个工程；`dotnet build Ps1Run.slnx` 双目标 **0 警告 0 错误** |
| 代码规模 | 93 个工程/资源文件（含 wiki 图片与脚本），其中 C#/XAML 57 个、约 7094 行（不含 `obj/`、`bin/`、`Build/`、`.workbuddy/`） |
| 功能状态 | 45 个功能点中：🔵 42（代码完成）、🟨 3（部分实现）、⬜ 0、✅ 0（完整验收）；另有 6 项设计稿描述但未落地项 D16–D21 |
| 关键路径 | `config.json` / `lang\*.json` / `log\yyyyMMdd.log` 均落在**软件根目录**（不可写时整体退化到 `%APPDATA%\Ps1Run`） |
| 本文档性质 | **设计 + 当前实现状态**；状态真源为 [02 功能实现清单](02-feature-matrix.md)，进度真源为 [04 项目进度与差距](04-progress.md) |

**阅读时请务必记住**：本 wiki 的 45 个功能点均已有代码（42 个按设计完成、3 个部分实现），核心逻辑通过 7/7 组自动化测试。但"代码完成"不等于"通过完整验收"：注册表实机变更、双击分发、多屏弹窗等会改变系统状态或需要真实桌面交互，保留给人工确认。技术专题中标注"设计/规划"的行为不一定已实现，未落地项统一登记为 D16–D21。⬜ 表示未开始；🟨 表示部分实现；🔵 表示代码完成；✅ 才表示通过 wiki [01](01-requirements.md) 的全部逐条验收标准。差异与待办统一记录在 [项目进度与差距](04-progress.md)。

---

## 快速导航

### 一、产品与实现

| 页面 | 内容 | 适合谁读 |
|---|---|---|
| [01 需求与验收口径](01-requirements.md) | 把原始需求拆成可验收的 R1–R9，明确边界与不做什么 | 所有人（先读这页） |
| [02 功能实现清单](02-feature-matrix.md) | 45 个功能点 × 实现落点 × 状态 × 验收标准的矩阵 | 开发者、评审者 |
| [03 项目结构](03-project-structure.md) | 目录规划、工程划分、与仓库既有约定的对齐 | 开发者 |
| [04 项目进度与差距](04-progress.md) | 里程碑、完成度、需求与现状的全部差距清单 | 项目负责人 |

### 二、架构与技术专题

| 页面 | 内容 |
|---|---|
| [05 架构与关键流程](05-architecture-flows.md) | 双击 `.ps1` 之后的完整调用链、进程生命周期、参数协议 |
| [06 文件关联注册与注销](06-tech-file-association.md) | ProgId 设计、UserChoice 哈希墙、HKCU/HKLM 权限、注销策略 |
| [07 PowerShell 版本探测与编码判型](07-tech-powershell-encoding.md) | PS5/PS7 探测顺序、编码判定决策表、自动选型规则 |
| [08 编辑器探测与回退](08-tech-editor-discovery.md) | 硬编码路径为何不够用、注册表锚点、四级回退链 |

### 三、技术评估与工程

| 页面 | 结论一句话 |
|---|---|
| [09 评估：.NET 4.8 + .NET 11 双版本](09-eval-dual-target-framework.md) | **可行**，单工程多目标已实测通过（0 警告 0 错误） |
| [10 评估：是否需要后台常驻](10-eval-background-residency.md) | **不需要**，按需启动、分发后立即退出 |
| [11 开发与构建指南](11-build-and-dev-guide.md) | 构建/发布命令、本机环境实测基线、验证清单 |
| [12 附录：注册表键位地图](12-appendix-registry-map.md) | 本机实测的关联相关注册表全量快照与字段含义 |

---

## 现状速览（2026-11-24 实测数据）

### 运行环境

| 项 | 实测值 |
|---|---|
| 操作系统 | Windows 11 专业版 10.0.26300 |
| .NET SDK | 10.0.115、**11.0.100**（另有 10.0.100-rc.1） |
| .NET 桌面运行时 | WindowsDesktop 6.0 / 8.0 / 9.0 / 10.0 / **11.0.0** |
| .NET Framework | 4.8.10008（Release 534296），参考程序集 `...\v4.8` 存在 |
| Visual Studio | 2026 Insiders（18），MSBuild 位于 `...\18\Insiders\MSBuild\Current\Bin\MSBuild.exe` |
| PowerShell 5.1 | `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` ✅ |
| PowerShell 7 | 7.6.10，`C:\Program Files\PowerShell\7\pwsh.exe` ✅ |

### 代码与构建状态（2026-11-24 更新）

| 项 | 实测值 |
|---|---|
| 解决方案 | `Ps1Run.slnx`，包含 `Ps1Run.Core`、`Ps1Run.App` 与 `Ps1Run.Core.Tests` 三个工程 |
| 构建命令 | `dotnet build Ps1Run.slnx` |
| 构建结果 | 成功，0 警告 0 错误 |
| 产物 | `Build\net48\Ps1Run.exe`、`Build\net11.0-windows\Ps1Run.dll`（均已内嵌 `icon.ico` 作为软件 logo） |
| 自动化测试 | `dotnet run --project Ps1Run.Core.Tests`：7/7 组通过（含日志、config.json、语言包） |
| 功能验收 | 代码完成 42/45（93%）、部分实现 3/45；系统级验收（注册表、双击、多屏）待人工执行；D16–D21 为未落地设计项 |

### 与需求强相关的现场事实

| 事实 | 影响 |
|---|---|
| `.ps1` 的 `UserChoice` 当前为 `VSCode.ps1` | 直接改默认程序会被 Windows 的哈希校验挡住，见 [06](06-tech-file-association.md) |
| `HKCR\.ps1` 默认值退化为 `ps1_auto_file` | 说明当前关联是 Explorer 自动生成的兜底项，无正规 ProgId |
| `HKCU\Software\Classes\.ps1` **可写**、`HKLM\Software\Classes` **不可写**（"Requested registry access is not allowed"） | 用户级注册无需管理员；机器级注册必须 UAC 提升 |
| VS Code 实际装在 `D:\Soft\Code\VS Code\Code.exe` | 三个"常见默认路径"全部不存在，硬编码探测必然失败，见 [08](08-tech-editor-discovery.md) |
| `Encoding.GetEncoding(936)` 在 net11 抛 `NotSupportedException` | 双版本下读取 GBK 脚本必须注册 `CodePagesEncodingProvider`，见 [07](07-tech-powershell-encoding.md) |

### 两项技术评估的落地结论

**3.1 双版本（.NET 4.8 + .NET 11）**：可行。单个 csproj 用 `TargetFrameworks=net48;net11.0-windows`、`UseWPF=true`，用 SDK 11.0.100 执行一次 `dotnet build` 即同时产出两份产物，实测 0 警告 0 错误。需处理三处差异（IsExternalInit 垫片、CodePages 注册、发布形态）。建议以 **net48 为主发布目标**（Win10 1903+/Win11 内置零依赖），net11 作为可选产物。详见 [09](09-eval-dual-target-framework.md)。

**3.2 后台常驻**：不需要。文件关联由 Explorer 在双击时按注册表拉起进程，程序没有常驻职责；常驻只会带来内存占用、开机自启、单实例与 IPC 复杂度、杀软误报等成本，唯一收益（冷启动更快）不足以构成理由。做法是"按需启动 → 分发 → 立即退出"，并用无窗口直通模式消除窗口闪烁。详见 [10](10-eval-background-residency.md)。

---

## 推荐阅读路径

- **第一次接触本项目**：本页 → [01 需求与验收口径](01-requirements.md) → [02 功能实现清单](02-feature-matrix.md) → [04 项目进度与差距](04-progress.md)
- **要动手写代码**：[03 项目结构](03-project-structure.md) → [05 架构与关键流程](05-architecture-flows.md) → 对应技术专题（[06](06-tech-file-association.md) / [07](07-tech-powershell-encoding.md) / [08](08-tech-editor-discovery.md)）→ [11 构建指南](11-build-and-dev-guide.md)
- **要做技术决策**：[09 双版本评估](09-eval-dual-target-framework.md) → [10 常驻评估](10-eval-background-residency.md) → [04 项目进度与差距](04-progress.md)

---

## 文档维护约定

1. 每个功能点都有唯一编号（`F1.1`、`F3.3` 等），[02 功能实现清单](02-feature-matrix.md) 是唯一的状态真源，其他页面只引用编号，不重复维护状态；各页状态横幅只引用 02/04，不得自行判定完成度。
2. 进度变化只改 [04 项目进度与差距](04-progress.md) 与 [02 功能实现清单](02-feature-matrix.md) 两处，避免状态漂移。
3. 任何"需求 vs 现状"的落差，一律在 [04](04-progress.md) 登记，不允许在正文中把未实现写成已实现；技术专题中的"设计/规划/建议"段落必须显式标注是否已落地。
4. 涉及环境数据的结论必须标注实测日期，环境不同需重新验证。

---

← 当前为首页 ｜ 下一篇：[01 需求与验收口径 →](01-requirements.md)
