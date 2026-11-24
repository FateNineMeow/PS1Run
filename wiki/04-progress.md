# 04 项目进度与差距

> 状态横幅：本页是项目进度的唯一真源。编制于 2026-11-24，同日根据代码实现更新。当前 **45 个功能点中 42 个代码完成、3 个部分实现**，M0–M5 主体收口；双目标构建通过、7/7 组自动化测试通过。剩余为需要真实桌面交互的系统级验收，以及设计稿中描述但尚未落地的 6 项增强（D16–D21）。功能级状态见 [02 功能实现清单](02-feature-matrix.md)。

---

## 1. 总体完成度

| 维度 | 数值 |
|---|---|
| 需求条目 | R7/R8 评估闭环；R1–R6、R9、R10 代码主体完成，系统级验收待人工执行 |
| 功能点 | 45 个：**🔵 42**（代码完成）、**🟨 3**（部分实现）、**⬜ 0**、**✅ 0**（完整验收） |
| 代码文件 | 93 个工程/资源文件（含 wiki 图片与脚本），其中 C#/XAML 57 个 |
| 代码行数 | 约 7094 行（不含 `obj/`、`bin/`、`Build/`、`.workbuddy/`） |
| 可运行产物 | 2：`Build\net48\Ps1Run.exe`、`Build\net11.0-windows\Ps1Run.dll` |
| 构建结果 | `dotnet build Ps1Run.slnx`：**0 警告 0 错误** |
| 自动化测试 | `Ps1Run.Core.Tests`：**7/7 组通过**（编码判型、参数解析、解释器探测、设置持久化、日志写入与保留、程序配置、多语言资源） |
| 资源 | `icon.ico`（多尺寸软件 logo）已内嵌为 exe 图标与窗口图标，DefaultIcon 指向 exe；旧 `ps1.ico` 已移除 |
| 日志 | `<软件根目录>\log\yyyyMMdd.log`：分级、会话 ID、进程 ID、退出码；保留天数取 `config.json` 的 `LogDays`（默认 7），每天首次运行清理一次；主界面可打开目录/查看/复制 |
| 程序配置 | `<软件根目录>\config.json`：`LogDays` / `Theme` / `Language`，首次运行生成 |
| 多语言 | `<软件根目录>\lang\*.json`：缺省生成 `zh-cn.json`，随程序附带 `en-us.json`，下拉即时切换 |
| 文档 | 13 页（本 wiki），已交付并同步至当前代码状态 |
| **代码完成度** | **93%（42/45）**，另有 3 项部分实现 |
| **完整验收完成度** | **0/45**（系统级验收未自动执行） |
| **设计已描述但代码未落地项** | **6 项（D16–D21）**，不计入 45 个功能点 |

> 说明："代码完成"指设计中的功能点均已实现且通过构建/自动化测试；"完整验收"指 wiki [01](01-requirements.md) 的全部逐条标准，包括注册表实机变更与双击交互，这部分保留给人工确认。

---

## 2. 里程碑

| 里程碑 | 内容 | 状态 | 产出物 / 验收 |
|---|---|---|---|
| **M0 现状调研与设计** | 环境核实、技术约束验证、双版本探针、wiki 编写 | ✅ 已完成（2026-11-24） | 本 wiki 13 页；探针工程在 `work\probe\` |
| **M1 内核骨架** | 建解决方案与三个工程；`AppSettings`/`SettingsStore`；`CommandLineOptions`；分级日志 | 🔵 代码完成 | 双目标构建通过；设置 JSON 往返与损坏恢复、日志写入/保留已单测 |
| **M2 探测能力** | `PowerShellLocator`、`ScriptEncodingDetector`、`EditorLocator` | 🔵 代码完成 | 编码判型 6 类、PS5/PS7 探测已单测；编辑器实机命中待确认 |
| **M3 分发链路** | `LaunchDispatcher` + 无窗口直通模式 + 选择窗 | 🔵 代码完成 | 参数解析已单测；命令行 `--run`/`--edit`/`--ask` 手工验证未做 |
| **M4 关联注册** | `AssociationService` 注册/注销/状态；UserChoice 提示 | 🔵 代码完成 | 含 HKLM 提升与系统"打开方式"引导；注册表实机验证待人工执行 |
| **M5 UI 与收尾** | 主界面、选择窗、诊断导出、图标、全局异常处理、日志查看入口 | 🔵 代码完成 | 主界面/选择窗/诊断与日志复制打开/icon.ico logo/异常兜底均已落地 |

依赖关系：M1 → M2 → M3 → M4 → M5 的顺序仍然成立。代码是"倒序补齐"的（先写内核再写 UI），但**实机验收必须按依赖顺序做**：先 M3 命令行分发，再 M4 注册表。

---

## 3. 差距清单（需求 vs 现状）

| 编号 | 需求 / 项 | 现状 | 差距 | 优先级 | 对应功能 |
|---|---|---|---|---|---|
| D1 | R1 关联注册/注销 | 注册/注销/状态、HKLM 提升、系统"打开方式"引导均已实现 | 仅剩实机验证：注册后查三动词、注销后查无残留 | 高（验收） | F1.1–F1.9 |
| D2 | R2 运行方式三态 | 参数、决策、直通、选择窗、Topmost、取消日志均已实现 | 仅剩 6 种组合的实机验收 | 高（验收） | F2.1–F2.6 |
| D3 | R3 执行程序策略 | 三态策略、编码判型、自动选型均已实现并单测 | 仅剩实机验证 | 中（验收） | F3.3–F3.5 |
| D4 | R4 探测 + 缺失提示 | PS5/PS7 探测链、人工指定并记住均已实现并单测 | 仅剩实机验证；Store 版 PS7 探测未覆盖 | 中（验收） | F3.1、F3.2、F3.6 |
| D5 | R5 编辑器探测 | 四级回退链已实现 | 仅剩实机确认本机 VS Code 是否由 App Paths 命中 | 中（验收） | F4.1–F4.6 |
| D6 | R6 双击分发 | 命令行入口与分发链路已实现并单测参数部分 | 仅剩双击 `.ps1` 实机验收 | 高（验收） | F2.1–F2.3 |
| D7 | R7 双版本评估 | ✅ 已给出结论，正式工程双目标构建通过 | 无 | — | [09](09-eval-dual-target-framework.md) |
| D8 | R8 常驻评估 | ✅ 已给出结论 | 无 | — | [10](10-eval-background-residency.md) |
| D9 | 工程脚手架 | ✅ `Ps1Run.slnx` + 三工程 + `Directory.Build.props`，构建通过 | 无 | — | [03](03-project-structure.md) |
| D10 | 单元测试 | ✅ `Ps1Run.Core.Tests` 7/7 组通过（编码判型、参数解析、解释器探测、设置持久化、日志写入与保留、程序配置、多语言资源） | 无（UI 与注册表交互不在自动化范围） | — | [03](03-project-structure.md#3-规划目录树) |
| D11 | 图标资源 | ✅ `icon.ico`（16~256 多尺寸）已设为 exe 图标与窗口图标，DefaultIcon 指向 exe | 无 | — | [03](03-project-structure.md#3-规划目录树) |
| D12 | 安装/卸载体验 | 主界面已提供用户级/机器级注册注销与系统默认程序入口 | 仍无独立安装包或一键安装脚本 | 低 | F1.7 |
| D13 | 全局异常处理 | ✅ `DispatcherUnhandledException` 已接线 | 无 | — | F6.3 |
| D14 | 设置格式与即时生效 | ✅ JSON 持久化 + 变更自动保存 | 无 | — | F5.2、R2 |
| D15 | 机器级注册与系统对话框 | ✅ ElevationHelper + HKLM 参数 + SHOpenWithDialog 均已接线 | 无 | — | F1.7、F1.9 |
| D16 | 关联回滚备份 | 注销只删自有 ProgId 与 `OpenWithProgids` 声明；未写 `.ps1\(默认)` 原值，也无 `association-backup.json` | R1 中"完整回滚到注册前状态"的严格口径未达成；需写前快照、注销时按快照恢复 | 高（验收） | F1.8 |
| D17 | UserChoice 高级移除 | C 方案（删除当前选择）与"高级：移除当前选择"按钮仅为设计 | 06 页的 C 方案、备份 Hash 恢复均未实现；当前只做合规引导 | 中（设计） | F1.6、F1.7 |
| D18 | 注册临界区互斥量 | 无 `Global\Ps1Run.Association` 互斥量 | 05 §9 / 10 §4.3 中描述的保护未实现；并发注册可能交叉 | 中（设计） | F1.1–F1.9 |
| D19 | PS7 Store 版探测 | 探测链止于 PATH；WindowsApps 分支未实现 | Store 版 PowerShell 7 无法被自动发现 | 中 | F3.2 |
| D20 | 编辑器自动弹窗指定 | 探测失败时主界面可手选，但编辑分支不会自动弹 `OpenFileDialog` | 08 §4 第 6 级未实现；"文件不存在时提示用户选择"需手工打开主界面 | 中 | F4.6 |
| D21 | 降级"改用另一版本重跑"入口 | 降级说明写入 `PowerShellResolution.Note` 与日志；界面入口未做 | 07 §5 要求界面的快捷重跑入口缺失 | 低 | F3.5 |

**统计**：D7–D11、D13–D15 已闭环；D1–D6 为**实机验收项**；D12 为低优先级分发体验项；D16–D21 为**设计稿已描述但代码未落地**的 6 项增强，其中 D16 优先级最高。

---

## 4. 建议实施顺序（剩余工作）

1. **命令行验收 M3**：`--run` / `--edit` / `--ask` / 中文路径 / 取消退出码，确认无窗口闪烁与工作目录正确。
2. **实机验收 M4**：注册 → 查看 `HKCU\Software\Classes\Ps1Run.ps1` 三动词 → 注销 → 确认无残留、未误伤 `VSCode.ps1`。因当前 `UserChoice=VSCode.ps1`，还需用"系统'打开方式'"引导手动确认。
3. **UI 交互验收 M5**：多屏定位、`Topmost`、快捷键、勾选"以后都这样处理"后的取消副作用。
4. **未落地增强**：按 D16（关联回滚备份）→ D20（编辑器自动指定）→ D17/D18/D19/D21 的顺序处理；这些不属于当前 45 个功能点的代码完成口径。
5. **分发收尾**：当前 `src\PS1Run` 尚未纳入 git（`?? ./`），验收通过后提交；net48 作为主发布目标；如需安装包再处理 D12。

> 风险提示不变：D1 是全项目唯一会改变系统行为的操作。实机注册前建议先做 dry-run 打印注册表改动，人工核对后再落盘。

---

## 5. 已知风险与阻塞项

| 风险 | 等级 | 说明 | 应对 |
|---|---|---|---|
| 系统级验收未执行 | **高** | 核心逻辑已通过 7/7 组单测，但注册表变更、双击分发、多屏弹窗尚未在真实桌面执行 | 按 §4 顺序人工验收 |
| 设计稿与代码仍有落差 | 中 | D16–D21 描述的行为尚未落地，其中 D16 影响 R1 的"完整回滚"严格口径 | 按 §4 第 4 项处理，处理前不得在文档写成已实现 |
| UserChoice 哈希墙 | **高** | Windows 不允许程序直接改默认程序；写错 Hash 会被 Explorer 重置。本机 `ProgId=VSCode.ps1`、`Hash=aBi9Z4VDA20=` | 只做 ProgId + OpenWithProgids 注册，配合 `SHOpenWithDialog` / `ms-settings:defaultapps` 引导用户确认，见 [06](06-tech-file-association.md#3-userchoice-这道墙) |
| HKLM 写入权限 | 中 | 实测 `HKLM\Software\Classes` 打开可写句柄抛 "Requested registry access is not allowed." | 默认 HKCU；HKLM 作为可选并显式提示需管理员 |
| net48 现代语法限制 | 中 | `record`/`init` 在 net48 报 CS0518 | 加 `IsExternalInit` 垫片（两个工程均已加） |
| net11 运行时分发 | 中 | 终端用户默认没有 .NET 11 Desktop Runtime | 主发布 net48；net11 单独说明或自包含 |
| GBK 脚本读取 | 中 | net11 下 `Encoding.GetEncoding(936)` 抛 `NotSupportedException` | `CoreBootstrap.Initialize()` 注册 `CodePagesEncodingProvider`（已实现） |
| 编码判型误判 | 中 | 无 BOM 且内容恰好是合法 UTF-8 的 GBK 文件会被误判 | 自动模式降级时已通过 `PowerShellResolution.Note` 与日志提示；界面"改用另一版本重跑"入口未做 |
| 仓库构建脚本兼容 | 低 | `src\运行.ps1` 会遍历子目录执行 `dotnet build` | `Ps1Run.slnx` 已在 `src\PS1Run\` 根，构建通过 |
| 杀软误报 | 低 | 写注册表 + 启动进程的组合可能触发启发式告警 | 代码签名（可选）、不常驻（见 [10](10-eval-background-residency.md)） |

**当前无外部阻塞**：原构建阻塞（MSB4025 / CS0006）已解除；剩余均为内部待办。

---

## 6. 已完成的验证工作

| 验证项 | 方法 | 结论 |
|---|---|---|
| 仓库现状（更新） | 递归枚举 `src\PS1Run`（排除 obj/bin/Build/.workbuddy） | 93 个工程/资源文件（含 wiki 图片与脚本），57 个 C#/XAML，约 7094 行 |
| 正式工程双目标构建 | `dotnet build Ps1Run.slnx` | 成功，0 警告 0 错误；产出 net48 与 net11.0-windows 两份产物 |
| 自动化测试 | `dotnet run --project Ps1Run.Core.Tests` | 7/7 组通过：编码判型、参数解析、解释器探测、设置持久化、日志写入与保留、程序配置、多语言资源 |
| 构建配置修复 | 修正 `Ps1Run.App.csproj` 与 `Directory.Build.props` | MSB4025（`<tfm>` 非法 XML）与 CS0006（双目标共用中间目录）均已消除 |
| 双目标可行性（M0） | `work\probe\dual` 探针工程 | 构建成功，0 警告 0 错误 |
| record/init 兼容 | 探针 + 正式工程 | net48 报 CS0518，加垫片后通过 |
| 注册表 API 兼容 | 探针使用 `Microsoft.Win32.Registry` 双目标编译 | 两版本均可用 |
| 编码 API 差异 | 控制台探针分别运行 net48/net11 | net48 支持 936；net11 抛 `NotSupportedException` |
| 关联现状 | 读取 HKCR/HKCU 相关键 | `UserChoice=VSCode.ps1`；`HKCR\.ps1=ps1_auto_file` |
| 权限边界 | 尝试打开 HKLM\Software\Classes 可写句柄 | 拒绝访问 |
| 编辑器安装位置 | 探测 6 个常见路径 | 全部不存在；实际在 `D:\Soft\Code\VS Code`，仅 `App Paths\Code.exe` 可命中 |

验证用的探针工程不属于交付代码，已从当前 `src\PS1Run` 交付树移除；本页仅保留其历史验证结论。

---

## 7. 变更记录

| 日期 | 变更 | 说明 |
|---|---|---|
| 2026-11-24 | 初始版本 | 完成现状核实、页面设计、两项技术评估、全部 35 个功能点拆解 |
| 2026-11-24 | 补入"选择"运行方式 | 运行方式由两态扩为三态；新增选择窗定位、键盘交互、"以后都这样处理"、取消语义与退出码约定；`open` 动词改为 `--open`，新增 `run` 动词 |
| 2026-11-24 | 代码实现同步 | M1–M5 主体代码落地；引入 🔵「代码完成待验收」状态，28/35 代码写完，仍无功能通过验收（历史节点，当前口径为 35/38 🔵） |
| 2026-11-24 | 构建修复 | 删除 `Ps1Run.App.csproj` 中非法的 `<tfm>` 占位符；恢复 SDK 默认 TFM 目录追加，双目标构建 0 警告 0 错误 |
| 2026-11-24 | 剩余功能补齐 | 实现 F1.7 / F1.9 / F2.4 / F2.6 / F3.6 / F6.3；设置改 JSON + 自动保存；新增 `ps1.ico` 与 `Ps1Run.Core.Tests`，35/35 功能点代码完成 |
| 2026-11-24 | 主界面布局与一键探测 | 底部按钮改为 `WrapPanel` 自动换行并按功能分组，消除叠加；新增独立「自动探测」行，一键按 wiki 07 §3 / 08 §4 探测链识别 PS5、PS7、编辑器并回填设置；双目标构建与 4/4 组单测通过（历史节点，当前 7/7） |
| 2026-11-24 | 增加 scripts 目录 | 新增 `run.ps1` 与三个构建入口 `build-all.ps1` / `build-net48.ps1` / `build-net11.ps1`，共用 `build-common.ps1` 统一退出码与编码；三个入口均实测退出码 0，`-Test` 联动 4/4 组单测（历史节点，当前 7/7） |
| 2026-11-24 | icon.ico 设为软件 logo | 用多尺寸 `icon.ico` 取代单尺寸 `ps1.ico`（旧文件已删除）：经 `<ApplicationIcon>` 内嵌为 exe 图标、经 `<Resource>` 作为主界面与选择窗图标，文件关联 `DefaultIcon` 改为指向 exe（`",0"`）；字节校验确认两个 exe 均含新图标数据，双目标构建 0 警告 0 错误、单测 4/4（历史节点，当前 7/7） |
| 2026-11-24 | 日志功能增强 | `DiagnosticsLog` 增加 DEBUG/INFO/WARN/ERROR 分级、会话 ID、进程 ID、异常摘要与 `ReadRecent()`；`App.OnStartup/OnExit` 接线开始/结束日志与 7 天清理；ProcessLauncher、SettingsStore、MainViewModel 补日志；主界面新增"打开日志目录/查看当日日志/复制最近日志"；新增第 5 组单测，5/5 通过 |
| 2026-11-24 | wiki 状态审计与补缺 | 以代码为准重写 02/04 统计（38 项：35 🔵 / 3 🟨），登记 D16–D21 六项未落地设计；修正 01/03/05~12 的过期路径、错误命令、按钮清单、弹窗尺寸与状态横幅 |
| 2026-11-24 | 修复启动时 XAML 图标解析崩溃 | 两个窗口原写 `Icon="icon.ico"`，WPF 按 XAML 目录解析成 `views/icon.ico` 导致 `TypeConverterMarkupExtension` 异常；改为 `pack://application:,,,/icon.ico`；net48/net11 × 主界面/Ask 弹窗 4 种组合启动验证通过 |
| 2026-11-24 | 选择窗标识执行解释器 | 新增 `LaunchDispatcher.PreviewPowerShell()` 只读预判与 `PowerShellPreview`，经 `PickRequest` 传入弹窗；`PickerWindow` 增加 PS5/PS7 标签、版本标题、exe 与判定依据（固定策略/编码）、降级或缺失的橙色提醒；窗口高度 300→360；实测 UTF-8 BOM 脚本显示 PS7、GBK 脚本显示 PS5，构建 0 警告 0 错误、单测 5/5（历史节点，当前 7/7） |
| 2026-11-24 | 配置 / 主题 / 多语言 | ① 日志改到 `<软件根目录>\log\yyyyMMdd.log`，保留天数取 `config.json` 的 `LogDays`，当天首次运行才清理；② 新增 `AppPaths`/`AppConfig`/`ConfigStore`（首次生成、原子写入、越界收敛、损坏备份），根目录不可写时三者一起退化到 `%APPDATA%\Ps1Run`；③ 新增 Light/Dark 主题字典与 `ThemeManager`（Auto 读 `AppsUseLightTheme`），主界面加"主题/语言/日志天数/配置文件"分组；④ 新增 `lang\*.json` 语言包、`LocalizationManager`、`{loc:T}` 绑定、`d:DesignText.Text` 设计期文本，语言下拉即时切换；⑤ 新增第 6/7 组单测，7/7 通过；浅色/深色 + 中英文截图验证，进程内探针确认切换后已绑定文案立即更新且无自激 |

---

← [03 项目结构](03-project-structure.md) ｜ [首页](index.md) ｜ 下一篇：[05 架构与关键流程 →](05-architecture-flows.md)
