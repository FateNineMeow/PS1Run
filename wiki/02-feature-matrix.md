# 02 功能实现清单

> 状态横幅：截至 2026-11-24，45 个功能点中 **🔵 42（代码完成）、🟨 3（部分实现）、⬜ 0、✅ 0（通过完整验收）**。`dotnet build Ps1Run.slnx` 双目标 0 警告 0 错误；`Ps1Run.Core.Tests` 8/8 组自动化测试通过。本页是**状态的唯一真源**；🟨 项的缺口同时登记在 [04 差距清单](04-progress.md#3-差距清单需求-vs-现状)。

---

## 0. 状态图例与统计

| 图例 | 含义 |
|---|---|
| ⬜ 未开始 | 无任何代码 |
| 🟨 部分实现 | 有代码，但未覆盖全部验收点，或存在与设计的偏差 |
| 🔵 代码完成待验收 | 已按设计写完且可编译，但尚未运行/手工验收 |
| ✅ 已完成 | 通过 [01 需求与验收口径](01-requirements.md#3-逐条验收口径) |

| 模块 | 功能点数 | ✅ | 🔵 | 🟨 | ⬜ |
|---|---|---|---|---|---|
| F1 文件关联 | 9 | 0 | 8 | 1 | 0 |
| F2 运行方式分发 | 7 | 0 | 7 | 0 | 0 |
| F3 执行程序（PS5/PS7） | 6 | 0 | 6 | 0 | 0 |
| F4 编辑程序 | 6 | 0 | 5 | 1 | 0 |
| F5 设置与持久化 | 4 | 0 | 4 | 0 | 0 |
| F6 日志与诊断 | 6 | 0 | 6 | 0 | 0 |
| F7 配置 / 主题 / 多语言 | 7 | 0 | 7 | 0 | 0 |
| **合计** | **45** | **0** | **42** | **3** | **0** |

> 完成度：按"代码主体完成"口径 **42/45（93%）**；按"完整验收"口径仍为 **0/45**——系统级验收（注册表实机验证、双击分发、多屏定位）需要在真实桌面环境执行，且部分操作会改变用户默认程序，因此未自动执行。

> 🟨 明细：F1.8（注销未恢复 `.ps1\(默认)` 原值、未实现 `association-backup.lang（预留）`）、F3.2（Store 版 PS7 探测未实现）、F4.6（编辑器"全部失败时自动弹窗指定"未实现，仅主界面手工指定）；三者均已登记在 [04 D16–D21](04-progress.md#3-差距清单需求-vs-现状)。

> 自动化覆盖：`Ps1Run.Core.Tests` 8/8 组通过，覆盖编码判型 6 类输入、参数优先级与冲突、PS5/PS7 探测回退、设置键值文本往返与损坏恢复、日志写入/读取/保留策略与「每天首次运行才清理」、config.cfg 往返/收敛/损坏恢复、语言包生成与解析。

---

## F1 文件关联（对应 R1）

| 编号 | 功能点 | 实现落点 | 关键 API / 注册表键 | 状态 | 验收要点 | 详见 |
|---|---|---|---|---|---|---|
| F1.1 | 定义 ProgId `Ps1Run.ps1` 的元数据 | `Ps1Run.Core\Associations\AssociationRegistration.cs` → `BuildProgId()` | `HKCU\Software\Classes\Ps1Run.ps1`（默认值=`PowerShell 脚本`）、`FriendlyTypeName`、`DefaultIcon` | 🔵 | 注册后资源管理器"文件类型"显示为可读名称与图标 | [06](06-tech-file-association.md) |
| F1.2 | 注册 `open` 动词（按运行方式分发） | `AssociationRegistration.RegisterOpenVerb()` | `...\Ps1Run.ps1\shell\open\command` = `"<exe>" --open "%1"` | 🔵 | 双击 `.ps1` 进入"按设置分发"分支（`Run`/`Edit` 直通，`Ask` 弹窗） | [06](06-tech-file-association.md#2-progid-与动词布局) |
| F1.3 | 注册 `run` 动词（强制执行） | `AssociationRegistration.RegisterRunVerb()` | `...\shell\run\command` = `"<exe>" --run "%1"` | 🔵 | 右键"运行"跳过 `Ask`，直接执行 | [06](06-tech-file-association.md#2-progid-与动词布局) |
| F1.4 | 注册 `edit` 动词（强制编辑） | `AssociationRegistration.RegisterEditVerb()` | `...\shell\edit\command` = `"<exe>" --edit "%1"` | 🔵 | 右键"编辑"直达编辑器，不弹窗、不起 PowerShell | [06](06-tech-file-association.md#2-progid-与动词布局) |
| F1.5 | 在 `.ps1` 上声明可选 ProgId | `AssociationRegistration.RegisterExtension()` | `HKCU\Software\Classes\.ps1\OpenWithProgids` 写入 `Ps1Run.ps1`（REG_NONE 空值） | 🔵 | "打开方式"列表中出现本程序 | [12 附录](12-appendix-registry-map.md) |
| F1.6 | UserChoice 冲突检测与提示 | `Ps1Run.Core\Associations\UserChoiceGuard.cs` → `Inspect()` | 读 `HKCU\...\FileExts\.ps1\UserChoice` 的 `ProgId`/`Hash` | 🔵 | 当前机器会提示"默认程序为 VSCode.ps1"，并给出引导 | [06](06-tech-file-association.md#3-userchoice-这道墙) |
| F1.7 | 引导用户完成默认程序切换 | `Ps1Run.Core\Infrastructure\NativeMethods.cs` → `SHOpenWithDialog()`；`MainViewModel.OpenDefaultAppsSettings()` | `shell32!SHOpenWithDialog`（P/Invoke）、`Process.Start("ms-settings:defaultapps")` | 🔵 | 两条引导均已接线；主界面提供"系统'打开方式'…"与"打开默认应用设置"按钮 | [06](06-tech-file-association.md#3-userchoice-这道墙) |
| F1.8 | 注销并完整回滚 | `AssociationService.Unregister()` | 删 `HKCU\Software\Classes\Ps1Run.ps1` 子树；从 `OpenWithProgids` 移除 `Ps1Run.ps1`；**未**恢复 `.ps1\(默认)` 原值 | 🟨 | 已做到无残留、无误伤第三方项；设计中的 `association-backup.lang（预留）` 与原值恢复未实现，见 [04 D16](04-progress.md#3-差距清单需求-vs-现状) | [06](06-tech-file-association.md#5-注销策略) |

**附加（可选，标记为 P2）**

| 编号 | 功能点 | 实现落点 | 状态 |
|---|---|---|---|
| F1.9 | 机器级（HKLM）注册，需 UAC 提升 | `AssociationService.Register(Scope.Machine)` + `ElevationHelper.RunElevated()` | 🔵 |

> 说明：已接线完整路径——主界面"注册到所有用户（管理员）"调用 `ElevationHelper.TryRestartElevated("--register-machine")`，提升后的进程由 `App.OnStartup` 执行 `AssociationService.Register(Scope.Machine)`；注销同理。本机实测 `HKLM\Software\Classes` 写入抛 "Requested registry access is not allowed."，故默认仍走 HKCU。

---

## F2 运行方式分发（对应 R2、R6）

| 编号 | 功能点 | 实现落点 | 关键 API | 状态 | 验收要点 | 详见 |
|---|---|---|---|---|---|---|
| F2.1 | 命令行参数解析 | `Ps1Run.Core\Dispatch\CommandLineOptions.cs` → `Parse(string[])` | 自定义解析：`--open` / `--run` / `--edit` / `--ask` / `--settings` / `--help`、`-h`、`-?` / `--register-machine` / `--unregister-machine` / `--` 后接字面路径 / 裸参数 | 🔵 | 能正确处理含空格、中文、以 `-` 开头的路径；帮助/机器级参数已在帮助文本列出 | [05](05-architecture-flows.md#2-参数协议) |
| F2.2 | 动作决策：显式参数 > 设置运行方式 > 询问窗 | `Ps1Run.Core\Dispatch\LaunchDispatcher.cs` → `DecideAction()` | `RunMode { Run, Edit, Ask }`；显式 `--run/--edit` 绕过 `Ask` | 🔵 | 三种运行方式 × 显式/隐式入口的 6 种组合均符合预期优先级 | [05](05-architecture-flows.md#3-启动流程) |
| F2.3 | 无窗口直通模式 | `Ps1Run.App\App.xaml.cs` → `OnStartup()` 中分流 | `ShutdownMode.OnExplicitShutdown`；不创建 `MainWindow` | 🔵 | `Run`/`Edit` 全程无窗口闪烁；`Ask` 只出现一个独立小窗 | [10](10-eval-background-residency.md#4-落地手段) |
| F2.4 | "选择"模式弹窗（三按钮 + 定位 + 解释器标识） | `Ps1Run.App\Views\PickerWindow.xaml(.cs)`、`ViewModels\PickerViewModel.cs`；数据来自 `LaunchDispatcher.PreviewPowerShell()` 与 `PickRequest.Settings` | WPF `Window`，`ShowDialog()`；构造时按主屏工作区预居中，`SourceInitialized` 再按 `GetCursorPos` + `MonitorFromPoint` 定位到鼠标所在屏居中；`ShowInTaskbar=false`；卡片展示 PS5/PS7、exe 与判定依据 | 🔵 | 三按钮、键盘与多屏定位已实现；首屏不再从左上角闪现；`Topmost=true`；窗口高度按内容自适应；执行前即可看到将用 PS5 还是 PS7，降级/缺失显示橙色提醒，配置路径失效显示红字 | [05](05-architecture-flows.md#5-选择模式的弹窗设计) |
| F2.5 | 选择窗"以后都这样处理" | `PickerViewModel` + `SettingsStore.Save()` | 勾选后按所点按钮写 `RunMode=Run/Edit` | 🔵 | 勾选并执行后，下次双击不再弹窗 | [05](05-architecture-flows.md#5-选择模式的弹窗设计) |
| F2.6 | 取消语义与退出码 | `LaunchDispatcher.Cancel()` | 不启动进程、不写设置、退出码 `0`；窗口创建失败退出码 `4` | 🔵 | 三路取消语义与退出码已实现；`INFO: 用户在 Ask 弹窗选择取消` 日志已补齐 | [05](05-architecture-flows.md#6-选择模式的取消行为) |
| F2.7 | 主界面（关联状态、设置、诊断） | `Ps1Run.App\Views\MainWindow.xaml(.cs)`、`ViewModels\MainViewModel.cs` | 参照同级项目 `打开方式清理` 的 `MainViewModel` + `DataContext` 模式 | 🔵 | 启动本程序（无参数）时显示；采用**三页签**：常规（运行方式 / 执行程序——PS5、PS7 与编辑器路径同组 / 一键识别）、文件关联（状态卡片 + 注册与引导操作）、日志与外观（日志保留与清理、外观与语言、配置文件与版本）。标题栏常驻关联徽标；语言包缺版本号时「日志与外观」页签挂「！」徽标并在页内显示红字提醒与「重新生成 zh-cn.lang」；底部状态条显示最近一次操作反馈（无反馈时提示"更改即时生效"）与关闭按钮；原「诊断信息」分组已移除（与其它页重复）；一键识别 `AutoDetect()` 按 [07 §3](07-tech-powershell-encoding.md#3-探测顺序) / [08 §4](08-tech-editor-discovery.md#4-四级回退链) 识别 PS5、PS7 与编辑器并自动保存，**已配置且文件存在的程序保持原样、不被覆盖**（只补空缺或替换失效路径） | [03](03-project-structure.md)、[05 §4.5](05-architecture-flows.md#45-无参数启动) |

---

## F3 执行程序（对应 R3、R4）

| 编号 | 功能点 | 实现落点 | 关键 API / 探测源 | 状态 | 验收要点 | 详见 |
|---|---|---|---|---|---|---|
| F3.1 | 探测 PowerShell 5.1 | `Ps1Run.Core\PowerShell\PowerShellLocator.cs` → `LocatePs5()` | `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` → SysWOW64 → `App Paths\powershell.exe` → PATH | 🔵 | 本机应命中 System32 路径 | [07](07-tech-powershell-encoding.md#3-探测顺序) |
| F3.2 | 探测 PowerShell 7+ | `PowerShellLocator.LocatePs7()` | `HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\{GUID}\InstallLocation` → `App Paths\pwsh.exe` → `%ProgramFiles%\PowerShell\7\` / `7-preview\` → PATH（**WindowsApps/Store 版未实现**） | 🟨 | 本机命中 7.6.10 @ `C:\Program Files\PowerShell\7\`；Store 版分支见 [04 D19](04-progress.md#3-差距清单需求-vs-现状) | [07](07-tech-powershell-encoding.md#3-探测顺序) |
| F3.3 | 三态执行策略 | `Ps1Run.Core\Settings\AppSettings.cs` → `PowerShellPolicy { PS5, PS7, Auto }` | — | 🔵 | 设置界面可切换并持久化 | [07](07-tech-powershell-encoding.md#1-三种策略) |
| F3.4 | 编码判型（自动模式核心） | `Ps1Run.Core\PowerShell\ScriptEncodingDetector.cs` → `Detect(string path)` | BOM 检测 + 严格 UTF-8 解码（`new UTF8Encoding(false, true)`） | 🔵 | 决策表 5 种输入 + 空文件共 6 类已由 `Ps1Run.Core.Tests` 覆盖并通过 | [07](07-tech-powershell-encoding.md#4-编码判型决策表) |
| F3.5 | 自动选型决策 | `LaunchDispatcher.ResolvePowerShell()` | 组合 F3.3 + F3.4 + 可用性 | 🔵 | PS 单侧缺失时降级并提示，不硬失败 | [07](07-tech-powershell-encoding.md#5-自动选型规则) |
| F3.6 | 缺失时人工指定并记住 | `Ps1Run.App\Views\PickerWindow.xaml.cs`（文件选择）、`SettingsStore.Save()` | `Microsoft.Win32.OpenFileDialog` | 🔵 | `IExecutablePicker` + `WpfExecutablePicker` 已实现：缺失时弹 OpenFileDialog、校验存在、写入设置并立即生效；主界面也已提供 PS5/PS7 路径输入与浏览按钮 | [07](07-tech-powershell-encoding.md#6-缺失回退) |

**进程启动约定（跨 F3/F4 共用）**

| 项 | 约定 |
|---|---|
| 可执行 | `powershell.exe` 或 `pwsh.exe` |
| 参数 | `-NoProfile -File "<脚本绝对路径>"`（不带 `-ExecutionPolicy`，遵循系统策略） |
| 工作目录 | **脚本所在目录**（保证脚本内相对路径正确） |
| 窗口 | 正常显示控制台窗口（用户需要看到输出） |
| 失败处理 | 捕获 `Win32Exception`（如 ERROR_FILE_NOT_FOUND），提示并引导重新指定 |

---

## F4 编辑程序（对应 R5）

| 编号 | 功能点 | 实现落点 | 关键 API / 探测源 | 状态 | 验收要点 | 详见 |
|---|---|---|---|---|---|---|
| F4.1 | 常见默认路径探测 | `Ps1Run.Core\Editors\EditorLocator.cs` → `ProbeKnownPaths()` | VS Code / Notepad++ / Sublime Text / Notepad / PowerShell ISE 的常见安装路径表 | 🔵 | 装默认盘时可直接命中 | [08](08-tech-editor-discovery.md#2-候选与探测源) |
| F4.2 | 注册表 App Paths 探测 | `Ps1Run.Core\Editors\RegistryAppPathReader.cs` → `Resolve(exeName)` | `HKLM/HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\<exe>` | 🔵 | **本机必须通过此路径命中 VS Code**（`D:\Soft\Code\VS Code\Code.exe`） | [08](08-tech-editor-discovery.md#3-为什么硬编码不够用) |
| F4.3 | 卸载信息 InstallLocation 探测 | `RegistryAppPathReader.ProbeUninstallEntries()` | `...\CurrentVersion\Uninstall\*` 的 `DisplayName` + `InstallLocation` | 🔵 | 能发现 `Microsoft Visual Studio Code (User)` | [08](08-tech-editor-discovery.md#3-为什么硬编码不够用) |
| F4.4 | 编辑器参数模板 | `Ps1Run.Core\Editors\EditorDefinition.cs` | 如 `"Code.exe" "%1"`、`"notepad++.exe" "%1"`、`"notepad.exe" "%1"` | 🔵 | `ArgumentTemplate` 与 `%1` 替换已实现；各编辑器统一使用 `%1`（常见编辑器均符合，非必要差异） | [08](08-tech-editor-discovery.md#5-参数模板) |
| F4.5 | 回退到系统记事本 | `EditorLocator.Resolve()` 末级兜底 | `%SystemRoot%\System32\notepad.exe` | 🔵 | 任何情况下都有可用编辑器 | [08](08-tech-editor-discovery.md#4-四级回退链) |
| F4.6 | 用户自定义编辑器 | `MainViewModel.CustomEditorPath` + `AppSettings.CustomEditorPath` | 路径 + 参数模板 + 显示名 | 🟨 | 手选编辑器优先级最高；主界面已提供"浏览…"选择 exe。设计中的"全部失败自动弹窗指定"未实现，见 [04 D20](04-progress.md#3-差距清单需求-vs-现状) | [08](08-tech-editor-discovery.md#4-四级回退链) |

---

## F5 设置与持久化

| 编号 | 功能点 | 实现落点 | 关键 API | 状态 | 验收要点 |
|---|---|---|---|---|---|
| F5.1 | 设置模型 | `Ps1Run.Core\Settings\AppSettings.cs` | `record`（net48 需 `IsExternalInit` 垫片） | 🔵 | 字段覆盖 F3.3/F3.6/F4.6/F2.2 全部选项 |
| F5.2 | 设置读写 | `Ps1Run.Core\Settings\SettingsStore.cs` + `Ps1Run.Core\Configuration\ConfigDocument.cs` | 与程序配置合并到软件根目录 `config.cfg`，固定 schema 轻量序列化（零依赖） | 🔵 | 设置与 config.cfg 合并为一份文件（`AppState`：Version + 运行设置 + 程序配置），两个 Store 都经 `ConfigDocument` 读改写自己的键、互不覆盖；旧 `%APPDATA%\Ps1Run\settings.json` 首次读取时一次性合并，写入成功后删除原文件（失败则保留重试）；缺失用默认值、损坏备份 `.bad.*` 并重建、原子写入；设置变更自动保存 |
| F5.3 | 原子写入 | `SettingsStore.Save()` | 写临时文件 + `File.Replace` | 🔵 | 断电/崩溃不产生半截文件 |
| F5.4 | 关联状态快照 | `AssociationService.GetStatus()` | 汇总：ProgId 存在性、动词命令、UserChoice 指向、PS/编辑器可用性 | 🔵 | 主界面能一眼看出"是否生效、为什么没生效" |

---

## F6 日志与诊断

| 编号 | 功能点 | 实现落点 | 状态 | 验收要点 |
|---|---|---|---|---|
| F6.1 | 轻量日志（分级 + 会话） | `Ps1Run.Core\Infrastructure\DiagnosticsLog.cs`（`<软件根目录>\log\yyyyMMdd.log`，按天滚动，保留天数取 config.cfg 的 `LogDays`，默认 7 天） | 🔵 | `DEBUG/INFO/WARN/ERROR` 四级；每条带 `[s:会话ID]` 与 `[p:进程ID]`；记录参数、判定编码、选中解释器、进程 PID、退出码；Ask 取消记 `INFO`，异常记类型与消息 |
| F6.2 | ~~诊断信息复制~~（已移除） | — | ⚪ | 与文件关联页、日志页、配置文件页重复，已删除"诊断信息"分组与"刷新状态 / 复制诊断信息"按钮；状态反馈改为窗口底部单行；需要日志内容时用 F6.5 的"复制最近日志" |
| F6.3 | 启动失败的可读提示 | `App.xaml.cs` 全局 `DispatcherUnhandledException` | 🔵 | 记录日志、弹中文提示并标记 `Handled`；参数非法/文件缺失/进程启动失败均有中文提示与退出码 |
| F6.4 | 打开日志目录 | `MainViewModel.OpenLogFolder()` + 主界面"打开日志目录"按钮 | 🔵 | 在资源管理器中打开 `<软件根目录>\log\`；目录不存在时自动创建 |
| F6.5 | 查看/复制当日日志 | `MainViewModel.OpenCurrentLog()` / `BuildRecentLogReport()` + "查看当日日志"/"复制最近日志"按钮 | 🔵 | 用系统关联程序打开当日日志；一键复制最近 150 行（含日志路径与会话 ID） |
| F6.6 | 会话生命周期与保留策略 | `DiagnosticsLog.BeginSession(component, args, keepDays)` / `EndSession()` / `Prune(days)`，由 `App.OnStartup` / `OnExit` 调用 | 🔵 | 启动时写 argv/exe/pid/session；**当天第一次运行**（当日日志文件尚不存在）时清理超过 `LogDays` 的旧日志，之后同一天内不再重复清理；退出时写 exitCode；日志失败绝不影响主流程 |

---

## F7 配置 / 主题 / 多语言

| 编号 | 功能点 | 实现落点 | 状态 | 验收要点 |
|---|---|---|---|---|
| F7.1 | config.cfg 持久化（唯一配置文件） | `Ps1Run.Core\Configuration\ConfigStore.cs`、`AppConfig.cs`、`AppState.cs`、`ConfigDocument.cs`；默认内容含 `Version`、`RunMode`、`PowerShellPolicy`、三条自定义路径、`LogDays`、`Theme`、`Language`、三个规则标准 | 🔵 | 纯文本键值格式（`Key: "value",`，首行 `Version: 2,`），与语言包共用 `KeyValueTextParser`；首次运行自动生成；原子写入（临时文件 + 替换）；损坏时备份为 `config.cfg.bad.时间戳` 并重建默认；旧 JSON 配置 / 设置不再读取、不迁移；`LogDays` 收敛到 1–365，非法值回退 7 |
| F7.2 | 路径约定与可写退化 | `Ps1Run.Core\Configuration\AppPaths.cs` | 🔵 | 根目录取 exe 所在目录；不可写时 config / rule.txt / lang / log 一起退化到 `%APPDATA%\Ps1Run` 并记 WARN |
| F7.3 | 日志天数可配 + 每日清理 | `MainViewModel.LogDaysText` + `DiagnosticsLog.Prune` | 🔵 | 界面可改（1–365，合法即落盘）；"立即清理"按当前天数手动清理；每天首次运行自动清理（见 F6.6） |
| F7.4 | 主题（深色/浅色/自动） | `Ps1Run.App\Themes\Light.xaml`、`Dark.xaml`、`Services\ThemeManager.cs`；主界面"主题"下拉 | 🔵 | 切换立即生效（替换 App 级主题字典，`DynamicResource` 全量重绘）；`Auto` 读 `HKCU\...\Themes\Personalize\AppsUseLightTheme`；窗口、按钮、输入框、下拉框、复选框、分组框均随主题变化 |
| F7.5 | i18n 语言包与版本号 | `Ps1Run.Core\Localization\*`（`DefaultStrings`/`LanguageCatalog`）、`Ps1Run.App\Localization\*`（`LocalizationManager`/`TExtension`） | 🔵 | 文案存放在 `<软件根目录>\lang\<code>.lang`；语言包首行写 `Version`（当前 2）；`zh-cn.lang` 随程序发布，缺失/无版本号/版本较旧时启动按内置文案重建（应用新版），其他语言包只读取不覆盖；缺键回退中文再回退键名；`{loc:T Key}` 绑定 |
| F7.6 | 语言下拉与即时切换 | 主界面"语言"下拉（`LanguageIndex`）+ `LocalizationManager.Load()` | 🔵 | 扫描 `lang\*.lang` 生成选项（zh-cn 固定第一，随程序附带 `en-us.lang`）；选择后立即重新加载并刷新已绑定文案、写入 config.cfg，无需重启；语言包缺版本号时「日志与外观」页签显示「！」徽标 + 页内红字提醒，选择窗同步红字，并可用「重新生成 zh-cn.lang」修复 |
| F7.7 | 设计期文本 | `Ps1Run.App\Localization\DesignText.cs`（`d:DesignText.Text`） | 🔵 | 设计器里显示文本、运行时不参与渲染；取值为 `zh-cn.lang` 对应内容 |
| F8.1 | 脚本规则（仅此脚本） | `Ps1Run.Core\Rules\ScriptRule*`、`Ps1Run.Core\Dispatch\LaunchDispatcher.cs` | 🔵 | `rule.txt` 5 列：路径 / 动作 / 大小 / 修改时间 / HASH；路径必匹配，其余标准由 config.cfg 全局控制；大小 → 修改时间 → HASH 短路，HASH 默认关闭；命中直接分发，`--ask` 仍强制弹窗；原子写入 + 命名互斥体，坏行跳过、失败开放 |
| F8.2 | 规则编辑页与选择窗 | `Ps1Run.App\Views\RuleEditorWindow.xaml`、`Views\MainWindow.xaml`（脚本规则页）、`Views\PickerWindow.xaml` | 🔵 | 主窗体「常规 / 脚本规则 / 文件关联 / 日志与外观」四页签；页面增删改查 + 搜索 + 保存 + 脏数据刷新确认；独立弹窗支持浏览或直接输入 `.ps1` 路径，保存校验扩展名与文件存在；选择窗两个「记住」复选框互斥，取消不写任何数据 |

> 说明：WPF 编译器不支持 `d:Text="…"` 这种省略归属类型的短写法（会报 MC3005/MC3072），因此统一写成 `d:DesignText.Text="…"`，语义与需求一致：**仅设计期可见，运行期文案来自语言包**。

---

## 当前未通过完整验收的主要原因

1. **系统级操作未自动执行**：注册表注册/注销会改变用户默认程序，双击分发与多屏弹窗需要真实桌面交互；这些验收项保留给人工执行，避免误改系统状态。
2. **自动化测试已覆盖核心逻辑**：`Ps1Run.Core.Tests` 8/8 组通过，覆盖编码判型、参数解析、解释器探测、设置持久化、日志写入/读取/保留与每日清理、config.cfg 往返/收敛/损坏恢复、语言包生成与解析；但 UI 交互（主题重绘、语言即时切换的视觉表现）与注册表写入没有自动化用例，已用截图与进程内探针人工验证。
3. **设计稿中描述但代码未落地的项**：关联回滚备份、Store 版 PS7 探测、编辑器自动弹窗指定、UserChoice 高级移除、注册互斥量、降级界面入口，已在 [04 D16–D21](04-progress.md#3-差距清单需求-vs-现状) 登记。

---

## 依赖关系与建议实现顺序

```
F5.1/F5.2 (设置模型)
    └→ F2.1/F2.2 (参数与决策)
         ├→ F3.1/F3.2/F3.3 (PS 探测与策略)
         │      └→ F3.4/F3.5 (编码判型与自动选型)
         │              └→ F3.6 (缺失回退) 已实现
         ├→ F4.1..F4.6 (编辑器探测与回退)
         └→ F2.3/F2.4/F2.7 (直通模式、选择窗与 UI)
                └→ F1.x (关联注册，需 UI 承载状态展示)
                       └→ F6.x (日志与诊断贯穿全程)
                              └→ F7.x (配置 / 主题 / 多语言，启动最早期加载)
```

内核优先的顺序仍然适用。当前 45 个功能点均已有代码（42 个按设计完成、3 个部分实现），8/8 组单测通过；下一步按 [04 §4](04-progress.md#4-建议实施顺序) 做命令行、注册表与双击分发的实机验收，再按优先级处理 D16–D21。

---

## 与需求的覆盖对照

| 需求 | 覆盖功能点 | 覆盖情况 |
|---|---|---|
| R1 关联注册/注销 | F1.1–F1.9 | 代码完整（含 HKLM 提升与系统打开方式引导）；待实机验收 |
| R2 运行方式三态（执行/编辑/选择） | F2.1–F2.6、F1.2、F1.3、F1.4 | 代码完整；待实机验收 |
| R3 执行程序三态 + 自动 | F3.3–F3.5 | 代码完整，编码判型已单测；待实机验收 |
| R4 探测 + 缺失提示 | F3.1、F3.2、F3.6 | 代码完整（含人工指定与记住）；探测链已单测 |
| R5 编辑器探测 + 提示 | F4.1–F4.6 | 代码完整；待实机验收 |
| R6 双击打开本程序 | F2.1–F2.3、F1.2 | 代码完整；待实机验收 |
| R7 双版本评估 | — | 已在 [09](09-eval-dual-target-framework.md) 给出结论，且正式工程双目标构建通过 |
| R8 常驻评估 | — | 已在 [10](10-eval-background-residency.md) 给出结论 |
| R9 日志与诊断（新增需求） | F6.1–F6.6 | 日志改到软件根目录、天数可配、每日首次运行清理；目录/查看/复制已落地；8/8 组单测覆盖 |
| R10 配置 / 主题 / 多语言（新增需求） | F7.1–F7.7 | config.cfg、深浅色主题、lang\*.lang 语言包与下拉即时切换、设计期文本均已落地 |

---

← [01 需求与验收口径](01-requirements.md) ｜ [首页](index.md) ｜ 下一篇：[03 项目结构 →](03-project-structure.md)
