# 05 架构与关键流程

> 状态横幅：本页描述的架构**主体已落地为代码**。截至 2026-11-24，参数解析、无窗口直通、选择窗（含 Topmost）、探测与分发链路、解释器缺失人工指定、SHOpenWithDialog / HKLM 提升、分级日志、config.json / 主题 / 多语言均已实现；双目标构建与 7/7 组单测通过，系统级实机验收待人工执行。状态真源见 [02](02-feature-matrix.md) / [04](04-progress.md)；本页标注"设计"的条目（如注册互斥量）未落地。

---

## 1. 组件总览

本项目本质是一个**极短生命周期的进程分发器**：被 Explorer 拉起 → 解析参数 → 决定"编辑还是执行、用哪个程序" → 拉起目标进程 → 自己退出。

```
                  ┌──────────────────────────────────────────┐
   双击 .ps1 ───▶ │ Explorer（shell）                         │
                  └───────────────┬──────────────────────────┘
                                  │ 读取 .ps1 的解析结果
                                  │ (UserChoice > HKCU\Software\Classes\.ps1 > HKLM)
                                  ▼
                  ┌──────────────────────────────────────────┐
                  │ Ps1Run.exe --run "C:\a\b.ps1"            │
                  └───────────────┬──────────────────────────┘
                                  │
                  ┌───────────────▼──────────────────────────┐
                  │ Ps1Run.App  App.OnStartup()              │
                  │   CommandLineOptions.Parse(argv)          │
                  └───────────────┬──────────────────────────┘
                                  │
                  ┌───────────────▼──────────────────────────┐
                  │ Ps1Run.Core  LaunchDispatcher            │
                  │   DecideAction()  → Run / Edit / Ask      │
                  └───────┬───────────────────────┬──────────┘
                          │ Run                   │ Edit
                          ▼                       ▼
        ┌─────────────────────────────┐  ┌──────────────────────────┐
        │ PowerShellLocator           │  │ EditorLocator            │
        │  + ScriptEncodingDetector   │  │  四级回退链              │
        │  → powershell.exe / pwsh.exe│  │  → Code.exe / notepad.exe│
        └──────────────┬──────────────┘  └────────────┬─────────────┘
                       │ Process.Start                │ Process.Start
                       ▼                              ▼
              ┌────────────────┐              ┌──────────────────┐
              │ 控制台窗口运行  │              │ 编辑器窗口打开    │
              └────────────────┘              └──────────────────┘
                       │                              │
                       └──────────┬───────────────────┘
                                  ▼
                     Ps1Run 自身进程 Exit(0)
```

**分层职责**

| 层 | 工程 | 职责 | 不做什么 |
|---|---|---|---|
| 宿主 | `Ps1Run.App` | 解析参数、决定是否建窗、调用 Core、展示错误 | 不写注册表、不做探测 |
| 内核 | `Ps1Run.Core` | 探测、判型、决策、注册表、设置、日志 | 不引用任何 WPF 类型 |
| 目标程序 | 外部进程 | 真正编辑或执行脚本 | — |

---

## 2. 参数协议

| 参数 | 语义 | 由谁产生 | 备注 |
|---|---|---|---|
| `--open "<path>"` | **按设置中的运行方式处理**（`Run`/`Edit`/`Ask`） | 关联的 `open` 动词（双击） | 路径始终加引号；`Ask` 模式在此分支弹窗 |
| `--run "<path>"` | 强制执行（**绕过** `Ask`） | 关联的 `run` 动词（右键"运行"） | 显式意图，不弹窗 |
| `--edit "<path>"` | 强制编辑（**绕过** `Ask`） | 关联的 `edit` 动词（右键"编辑"） | 显式意图，不弹窗 |
| `--ask "<path>"` | 强制弹出选择窗 | 用户自建快捷方式 | 可选能力 |
| `--settings` | 直接打开设置页 | 开始菜单/快捷方式 | 无文件参数 |
| `<path>`（裸参数） | 等价于 `--open` | 手工拖拽到 exe 上 | 兼容拖放 |
| 无参数 | 显示主界面 | 直接运行 exe | — |

**优先级**：`--run` / `--edit` > `--ask` > `--open` > 裸参数 > 无参数。显式动词永远不弹 `Ask`，因为"右键选运行/编辑"本身就是用户的明确选择。

**解析要点（易错处）**

1. Explorer 传入的路径**可能含空格与中文**，必须以 `"` 包裹，解析时优先按"引号内整体"切分。
2. 裸参数可能是以 `-` 开头的文件名（例如 `-tmp.ps1`），不能简单地把所有 `-` 开头都当开关；未知开关应报错而非猜。
3. 路径分隔符保持原样，不做 `\` 与 `/` 归一化后再回写。
4. 参数个数为 0 或 1 时要有明确兜底，禁止数组越界。

**建议实现形态**：手写解析（约 60 行），不引入 `System.CommandLine`（net48 上需额外依赖，收益不成比例）。

---

## 3. 启动流程

```
OnStartup(argv)
 │
 ├─ CoreBootstrap.Initialize()（net11 代码页提供程序）
 │
 ├─ ConfigStore.Load()：<软件根目录>\config.json（首次生成 LogDays/Theme/Language）
 │
 ├─ DiagnosticsLog.BeginSession(args, LogDays)
 │    └─ 当天第一次运行 → 清理超过 LogDays 的旧日志
 │
 ├─ LocalizationManager.Load(config.Language)：加载 lang\<code>.json（缺失则生成 zh-cn.json）
 ├─ ThemeManager.Apply(config.Theme)：Auto 读系统 AppsUseLightTheme，替换 Light/Dark 主题字典
 │
 ├─ 解析 CommandLineOptions
 │    └─ 失败 → 弹提示 → Exit(2)
 │
 ├─ 无文件参数？
 │    ├─ 是 → 显示 MainWindow（关联状态 / 设置 / 诊断）→ 保持运行
 │    └─ 否 → 进入直通模式（不建 MainWindow）
 │
 ├─ 文件存在性校验
 │    └─ 不存在 → 提示"文件已被移动或删除" → Exit(3)
 │
 ├─ DecideAction()
 │    ├─ 显式 --run  → Run（跳过 Ask）
 │    ├─ 显式 --edit → Edit（跳过 Ask）
 │    ├─ --ask       → 弹 PickerWindow
 │    └─ 无显式 / --open → 读 AppSettings.RunMode
 │                       ├─ Run   → Run
 │                       ├─ Edit  → Edit
 │                       └─ Ask   → 弹 PickerWindow（默认值）
 │
 ├─ Run 分支
 │    ├─ ResolvePowerShell()       ← F3.3 + F3.4 + F3.5
 │    │    └─ 失败 → 提示 + 文件选择框 → 记住 → 重试
 │    └─ Process.Start(ps, -NoProfile -File "<path>", wd = 脚本目录)
 │
 ├─ Edit 分支
 │    ├─ EditorLocator.Resolve()   ← F4.1..F4.6
 │    └─ Process.Start(editor, 参数模板)
 │
 └─ Shutdown(0)      ← 无论成功失败都必须退出
```

**关键设计点**

| 点 | 决策 | 理由 |
|---|---|---|
| 直通模式不建窗口 | `ShutdownMode = OnExplicitShutdown`，不实例化 `MainWindow` | 避免"双击后闪一下白窗"，见 [10](10-eval-background-residency.md#4-落地手段) |
| 工作目录设为脚本目录 | `ProcessStartInfo.WorkingDirectory = Path.GetDirectoryName(scriptPath)` | 脚本内相对路径（如 `.\data.json`）才能正确解析 |
| 不传 `-ExecutionPolicy` | 沿用系统策略（本机 `LocalMachine=RemoteSigned`） | 主动绕过执行策略属安全越界，见 [01 范围外](01-requirements.md#4-明确不做范围外) |
| 不 `WaitForExit` | 拉起即退出 | 本程序是分发器，不该在脚本运行期间占着进程 |
| 目标进程启动失败 | 捕获 `Win32Exception`，弹中文提示 + 打开选择器 | 典型场景：用户手选的 pwsh 路径被卸载后失效 |

---

## 4. 场景时序

### 4.1 双击执行（最常见路径）

```
用户双击 C:\scripts\部署.ps1
  → Explorer 解析 .ps1 → 用户级 ProgId（无 UserChoice 冲突时）
  → 执行 shell\open\command
  → Ps1Run.exe --run "C:\scripts\部署.ps1"
  → 整文件严格 UTF-8 解码校验（超过 32MB 才截断采样）→ 判定 UTF-8 无 BOM
  → 策略 Auto + 判定 UTF8 → 选 PS7
  → 探测到 C:\Program Files\PowerShell\7\pwsh.exe
  → Process.Start(pwsh, -NoProfile -File "C:\scripts\部署.ps1", WD=C:\scripts)
  → Ps1Run 退出，控制台窗口出现并输出
```

### 4.2 双击编辑

```
用户双击 C:\scripts\部署.ps1（运行方式=编辑）
  → Explorer 走 shell\open\command
  → Ps1Run.exe --open "C:\scripts\部署.ps1"
  → AppSettings.RunMode = Edit
  → 解析编辑器：用户配置 > 常见路径 > App Paths > Uninstall > PATH > notepad
  → 命中 D:\Soft\Code\VS Code\Code.exe（经 App Paths）
  → Process.Start(Code.exe, "\"C:\scripts\部署.ps1\"")
  → Ps1Run 退出
```

> 注意 4.2 中"双击为什么能进入编辑"：`open` 动词只表示"交给本程序按设置处理"，真正的动作由**设置中的运行方式**决定。若希望双击固定编辑，把运行方式设为 `Edit` 即可，不必改注册表的默认动词。

### 4.3 右键"运行"

```
用户右键 .ps1 → 看到三项：
    "打开"（默认动词 shell\open，等同双击）→ --open
    "运行"（shell\run）  → --run，强制执行，跳过 Ask
    "编辑"（shell\edit） → --edit，强制编辑，跳过 Ask
  → 点"编辑" → Ps1Run.exe --edit "C:\scripts\部署.ps1"
  → 显式参数优先，忽略 RunMode，直接进 Edit 分支
```

### 4.4 解释器缺失

```
ResolvePowerShell() → PS7 策略但探测全失败
  → 弹窗："未找到 PowerShell 7，请指定 pwsh.exe"
  → OpenFileDialog（过滤 pwsh.exe）
  → 用户选定 D:\PowerShell\7\pwsh.exe
  → 校验存在 → 写入设置 → 立即本次生效
  → 下次直接复用；若文件后来消失 → 重新提示（不静默失败）
```

### 4.5 无参数启动

```
用户从开始菜单点 Ps1Run.exe
  → 无文件参数 → 显示 MainWindow
  → 展示：关联是否生效、UserChoice 当前指向、PS5/PS7 可达性、编辑器解析结果
  → 提供按钮：保存设置、刷新状态、注册/注销关联、系统"打开方式"、打开默认应用设置、机器级注册/注销、复制诊断信息、打开日志目录、查看当日日志、复制最近日志
```

---

## 5. "选择"模式的弹窗设计

运行方式为 `Ask` 时，每次双击都要弹窗。由于此时**没有主窗口**（直通模式），这个弹窗必须是独立窗口，不能做成主界面里的一个对话框。

### 5.1 为什么是独立窗口而不是主界面内的对话框

| 方案 | 问题 |
|---|---|
| 先显示主窗口，再在其中弹对话框 | 用户双击脚本会先看到一个大界面，再被要求选动作，两步操作、观感差 |
| 只弹一个轻量小窗 | 一步完成，窗口消失后直接进入目标程序 ✅ |

因此 `PickerWindow` 是一个独立 `Window`，以 `ShowDialog()` 模态显示，`ShowInTaskbar = false`，不出现任务栏按钮。

### 5.2 交互位置与多屏定位

| 项 | 规则 |
|---|---|
| 定位基准 | **鼠标指针所在显示器的居中位置**，不是主显示器、不是上次位置 |
| 获取光标位置 | `GetCursorPos`（user32） |
| 获取所在显示器 | `MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST)` |
| 计算居中坐标 | 由 `MONITORINFO.rcWork`（排除任务栏）计算，避免压到任务栏 |
| 多屏 | 若光标在副屏，窗口必须出现在副屏，不得弹回主屏 |
| 置顶 | `Topmost = true`，防止被其他窗口遮住导致用户以为"没反应" |
| 尺寸 | `460×360`，不可调整大小（`ResizeMode=NoResize`） |

### 5.3 内容与键盘

```
┌────────────────────────────────────────────┐
│  部署.ps1                                  │
│  C:\scripts\部署.ps1                       │
│  ┌──────────────────────────────────────┐  │
│  │ [PS7] 执行时使用 PowerShell 7+       │  │
│  │ pwsh.exe · 自动判定：UTF-8 无 BOM    │  │
│  └──────────────────────────────────────┘  │
│                                            │
│          [ 取消 (Esc) ] [ 编辑 (E) ] [ 执行 (Enter) ] │
│                                            │
│  ☐ 以后都这样处理（不再询问）               │
└────────────────────────────────────────────┘
```

| 元素 | 规则 |
|---|---|
| 标题行 | 脚本文件名，加粗，过长时中间省略 |
| 第二行 | 完整路径，`ToolTip` 显示全文 |
| 解释器标识 | 卡片展示 `PS5`/`PS7`、主标题（Windows PowerShell 5.1 / PowerShell 7+）、exe 文件名与判定依据（固定策略或自动判定的编码）；完整路径放 `ToolTip`；数据来自 `PreviewPowerShell()` **只读预判**，不弹人工指定框、不改设置 |
| 降级/缺失提醒 | 自动模式降级、固定策略未找到时，卡片下方显示橙色说明（如"按编码应选 PS5，但未找到；执行时将降级为 PS7，中文可能乱码"） |
| 默认焦点 | "执行"按钮（双击脚本的默认意图是运行） |
| `Enter` | 执行 |
| `E` | 编辑（不区分大小写） |
| `Esc` | 取消 |
| 复选框 | "以后都这样处理" —— 勾选后按最终点击的按钮把 `RunMode` 写成 `Run` / `Edit` |
| 关闭按钮 `X` | 等价于取消 |

> 关于默认焦点：把"执行"设为默认，是因为用户双击 `.ps1` 的主动意图通常是运行，`Ask` 的价值在于"让你看见并可改判"，而非阻止执行。若用户更需要保守策略，可在设置中把默认运行方式直接改为 `Edit`。

### 5.4 与执行器/编辑器的组合

弹窗的主决策仍是"编辑还是执行"，但会**只读展示**执行时将使用的解释器，让用户在点"执行"前就知道会走 PS5 还是 PS7。展示不参与决策：

```
Ask 弹窗（PreviewPowerShell 预判 → 仅展示） 
  → 用户点"执行" → ResolvePowerShell()（PS5/PS7/Auto + 编码判型，必要时人工指定）→ 启动
  → 用户点"编辑" → EditorLocator.Resolve()（五级回退）→ 启动（完全不走 PowerShell 分支）
```

预判与实际解析共用同一套策略与探测顺序，因此正常情况下两者一致；差异只可能出现在"固定策略缺失"时——预判显示"未找到，执行时将提示指定路径"，用户点执行后才弹文件选择框。详见 [07 §5](07-tech-powershell-encoding.md#5-自动选型规则) 与 [08 §4](08-tech-editor-discovery.md#4-四级回退链)。

---

## 6. "选择"模式的取消行为

取消是本页最容易被忽略、也最容易写出副作用的地方，规则必须显式。

### 6.1 三路取消必须完全等价

| 触发方式 | 行为 |
|---|---|
| 按 `Esc` | 取消 |
| 点标题栏 `X` | 取消 |
| 点"取消"按钮 | 取消 |

三者必须走**同一个取消方法**（`LaunchDispatcher.Cancel()`），不允许分别实现，否则容易漏掉清理逻辑。

### 6.2 取消后的确定状态

| 项 | 取消后的状态 |
|---|---|
| 是否启动 PowerShell | **否** |
| 是否启动编辑器 | **否** |
| 是否修改 `RunMode` | **否**（即使用户勾了"以后都这样处理"，取消就一律不写设置） |
| 是否修改其他设置 | **否** |
| 进程退出码 | `0`（用户主动取消不是错误） |
| 日志 | 记一条 `INFO: 用户在 Ask 弹窗选择取消, script=<path>` |
| 残留窗口/进程 | 无 |

> 退出码用 `0` 而不是非零，是因为调用方（Explorer）不关心，而"用户取消"在语义上属于正常结束。若把取消当错误，会让后续脚本或监控误判。

### 6.3 不做自动超时

弹窗**不设置倒计时自动关闭**。理由：超时后自动选"执行"是严重的安全问题，自动选"取消"又会让用户以为程序坏了。必须由人做出选择。

### 6.4 无法交互的极端情况

| 场景 | 行为 |
|---|---|
| 无交互式桌面会话（如通过服务/计划任务以非交互方式拉起） | 不执行任何脚本，退出码 `4`，写错误日志 |
| 窗口创建失败（如资源耗尽） | 同上，退出码 `4`，不得"降级为直接执行" |

**绝对禁止**：任何"弹窗失败就默认执行"的兜底逻辑。

---

## 7. 进程生命周期与窗口策略

| 场景 | 是否建窗 | 退出时机 |
|---|---|---|
| `--open` 且运行方式为 `Run`/`Edit` | 否 | 目标进程 `Start()` 返回后立即退出 |
| `--run` / `--edit` 直通成功 | 否 | 目标进程 `Start()` 返回后立即退出 |
| `--open` 且运行方式为 `Ask` | 是（仅 PickerWindow） | 用户选定并成功分发后退出；取消则直接退出 |
| `--ask` | 是（仅 PickerWindow） | 同上 |
| 分发失败 | 是（仅模态提示框） | 用户关闭提示后退出 |
| 无参数 | 是（MainWindow） | 用户关闭窗口 |
| 关联注册/注销 | 是（MainWindow + 确认框） | 操作完成，用户关闭 |

> 关键区别：`Ask` 模式只增加"一个 460×300 的小窗"，**不**等于"显示主界面"。主界面仅在没有文件参数时出现。

`ShutdownMode` 按场景设置：直通模式在 `App.OnStartup` 里改为 `OnExplicitShutdown`，由代码显式 `Shutdown(code)`；无参数主界面用 `OnMainWindowClose`（`App.xaml` 默认 `OnLastWindowClose`，启动时覆盖）。这样退出码可控，又不会让关窗后进程残留。

---

## 8. 失败路径矩阵

| 失败点 | 用户可见提示 | 程序行为 | 日志 |
|---|---|---|---|
| 参数非法 | "命令行参数无法识别：xxx" | 退出码 2 | 记录原始 argv |
| 文件不存在 | "文件已被移动或删除" | 退出码 3 | 记录路径 |
| 未预期内部错误 | "发生未预期错误：xxx" | 退出码 1 | 记录异常类型与消息（`ExitCodes.UnexpectedError`） |
| PS5/PS7 均不可用 | "未找到任何 PowerShell" | 打开选择器 | 记录探测链每一步结果 |
| 编码判定异常（IO 失败） | "无法读取脚本内容" | 询问是否改用指定解释器直接运行 | 记录异常 |
| 编辑器全部失败 | "未找到可用编辑器，已回退到记事本" | 用 notepad 打开 | 记录回退原因 |
| 目标进程启动失败 | "启动 PowerShell/编辑器失败：原因" | 不自动清除已记住路径（需人工修正） | 记录 exe 路径与 `Win32Exception.NativeErrorCode` |
| 注册表写入被拒 | "需要管理员权限才能完成机器级注册" | 保持用户级结果并回退 | 记录键路径 |

---

## 9. 并发与单实例

| 问题 | 决策 |
|---|---|
| 用户连点两下 `.ps1` 会不会起两个 Ps1Run？ | **允许**。直通模式无共享状态，多开无害，且实现最简单。 |
| 关联注册正在进行时又点注册？ | **设计**：用命名互斥量 `Global\Ps1Run.Association` 保护注册表写入段。**现状**：未实现（见 [04 D18](04-progress.md#3-差距清单需求-vs-现状)）；当前注册/注销无跨进程加锁。 |
| 设置文件并发写？ | `SettingsStore` 内部加进程内 `lock` + 原子替换（写临时文件 → `File.Replace`），跨进程冲突概率极低，可忽略。 |
| 是否需要 IPC？ | **不需要**。没有常驻进程，无跨进程会话。见 [10](10-eval-background-residency.md)。 |

---

## 10. 安全边界

1. **不接管执行策略**：不在命令行追加 `-ExecutionPolicy Bypass`，尊重系统与组策略。
2. **不伪造 UserChoice**：见 [06](06-tech-file-association.md#3-userchoice-这道墙)。
3. **命令拼接防注入**：所有路径一律 `"` 包裹；不使用 `cmd.exe /c` 中转；优先用 `ProcessStartInfo.ArgumentList`（net11）或手动转义（net48）。
4. **最小权限**：`app.manifest` 用 `asInvoker`，只在用户点击"机器级注册"时才触发 UAC 提升。
5. **不自动下载**：缺失解释器/编辑器时只提示与引导，不静默安装任何东西。

---

← [04 项目进度与差距](04-progress.md) ｜ [首页](index.md) ｜ 下一篇：[06 文件关联注册与注销 →](06-tech-file-association.md)
