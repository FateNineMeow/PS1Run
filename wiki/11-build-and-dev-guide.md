# 11 开发与构建指南

> 状态横幅：项目已是独立仓库，工程搭建完成，在仓库根执行 `dotnet build Ps1Run.slnx` 实测 **0 警告 0 错误**。本页保留"从零搭建"步骤供重建参考，日常直接使用 §3 的构建命令（全部命令默认**当前目录 = 仓库根**）。所有命令均在本机（Windows 11 26300 + SDK 11.0.100）验证过。

---

## 1. 环境基线

| 组件 | 要求 | 本机实测 |
|---|---|---|
| 操作系统 | Windows 10 1903+ / Windows 11 | Windows 11 10.0.26300 ✅ |
| .NET SDK | 能编译 `net11.0-windows` 的 SDK | 11.0.100 ✅ |
| net48 参考程序集 | .NET Framework 4.8 Developer Pack，或 `Microsoft.NETFramework.ReferenceAssemblies` 包 | 参考程序集目录存在 ✅ |
| .NET Framework 运行时 | 4.8（运行 net48 产物） | 4.8.10008 ✅ |
| .NET 桌面运行时 | 11.0（运行 net11 产物） | 11.0.0 ✅ |
| PowerShell | 5.1 与/或 7+（功能验证用） | 5.1 + 7.6.10 ✅ |

**环境自检**

```powershell
dotnet --list-sdks
dotnet --list-runtimes | Select-String 'WindowsDesktop'
Test-Path 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release   # >= 528040 即 4.8
```

---

## 2. 从零搭建

> 当前工程（含 `Ps1Run.Core.Tests`）已经存在，本节仅作重建/迁移参考；不要对现有目录重复执行 `dotnet new`。正式测试工程是零外部依赖的控制台项目，而非 xunit 模板。

```powershell
$root = (Resolve-Path '.').Path   # 当前目录 = 仓库根

# 1) 解决方案
dotnet new sln -n Ps1Run -o $root
Rename-Item "$root\Ps1Run.sln" 'Ps1Run.slnx' -ErrorAction SilentlyContinue   # 也可直接新建 slnx

# 2) WPF 宿主（net48 + net11 由 Directory.Build.props 统一注入）
dotnet new wpf -n Ps1Run.App -o "$root\Ps1Run.App"
dotnet new classlib -n Ps1Run.Core -o "$root\Ps1Run.Core"
dotnet new console -n Ps1Run.Core.Tests -o "$root\Ps1Run.Core.Tests"   # 正式工程是零依赖控制台项目，不用 xunit 模板

# 3) 加入解决方案
dotnet sln "$root\Ps1Run.slnx" add "$root\Ps1Run.App\Ps1Run.App.csproj" `
                                    "$root\Ps1Run.Core\Ps1Run.Core.csproj" `
                                    "$root\Ps1Run.Core.Tests\Ps1Run.Core.Tests.csproj"
```

> 模板生成后需要按 [03 项目结构](03-project-structure.md#5-关键工程属性) 与 [09 §4](09-eval-dual-target-framework.md#4-双版本生成方案可执行) 改写 csproj（改 `TargetFrameworks`、`OutputType`、`UseWPF`、`OutputPath` 等）。

**与仓库构建脚本的兼容**：项目已是独立仓库，直接在仓库根执行 `dotnet build Ps1Run.slnx` 即可；原先"父仓库 `src\运行.ps1` 遍历 `src\` 各子目录执行构建"的约束已不再适用。仓库自带的构建入口在 `scripts\` 下（见 §3.1）。

---

## 3. 日常构建

```powershell
# 全量（两个目标）
dotnet build Ps1Run.slnx -c Debug

# 只构建一个目标（迭代更快）
dotnet build Ps1Run.App -c Debug -f net48
dotnet build Ps1Run.App -c Debug -f net11.0-windows

# 单元测试（零依赖控制台测试工程，7/7 组通过）
dotnet run --project Ps1Run.Core.Tests -f net11.0-windows
```

**运行日志**：`<软件根目录>\log\yyyyMMdd.log`（按天滚动；保留天数取 `config.json` 的 `LogDays`，默认 7 天，每天首次运行清理一次；带级别/会话 ID/进程 ID）。主界面"打开日志目录"可直接定位，"复制最近日志"可复制当日最后 150 行用于反馈；启动失败、分发结果、关联操作与全局异常均写日志。详见 [02 F6](02-feature-matrix.md#f6-日志与诊断)。

**配置文件与语言包**：`<软件根目录>\config.json`（首次运行生成）与 `<软件根目录>\lang\*.json`（默认生成 `zh-cn.json`，随程序附带 `en-us.json`）。若根目录不可写（例如装在 `Program Files` 且未提权），三者会一起退化到 `%APPDATA%\Ps1Run`，日志里会有一条 WARN。详见 [02 F7](02-feature-matrix.md#f7-配置--主题--多语言)。

### 3.1 脚本入口（推荐）

`scripts\` 下提供 PowerShell 入口，均为 UTF-8 BOM 编码，兼容 Windows PowerShell 5.1：

| 脚本 | 作用 | 常用参数 |
|---|---|---|
| `scripts\build-all.ps1` | 构建整个解决方案（net48 + net11.0-windows + 测试工程） | `-Configuration Release`、`-Test`、`-NoRestore` |
| `scripts\build-net48.ps1` | 只构建 net48（主发布目标） | 同上 |
| `scripts\build-net11.ps1` | 只构建 net11.0-windows | 同上 |
| `scripts\run.ps1` | 启动已构建产物 | `-Framework Net11`、`-Build`、`-Intent Run`、`-ScriptPath`、`-Arguments` |

```powershell
.\scripts\build-all.ps1 -Test                                  # 全量构建 + 7/7 组单测
.\scripts\build-net48.ps1                                      # 只生成 net48
.\scripts\build-net11.ps1 -Configuration Release               # 只生成 net11 / Release
.\scripts\run.ps1                                              # 打开主界面（net48）
.\scripts\run.ps1 -Build -Intent Run -ScriptPath "D:\a\test.ps1"
```

三个构建脚本共用 `scripts\build-common.ps1`：统一项目路径、失败返回退出码 1、重置控制台 UTF-8 编码避免中文日志乱码；`-Test` 在构建成功后运行 `Ps1Run.Core.Tests`。若首次运行被执行策略拦截，用 `powershell -ExecutionPolicy Bypass -File .\scripts\build-all.ps1`，或先执行 `Set-ExecutionPolicy -Scope Process Bypass`。

**期望输出**

```
  Ps1Run.Core -> ...\Build\net48\Ps1Run.Core.dll
  Ps1Run.Core -> ...\Build\net11.0-windows\Ps1Run.Core.dll
  Ps1Run.App  -> ...\Build\net48\Ps1Run.exe
  Ps1Run.App  -> ...\Build\net11.0-windows\Ps1Run.dll
    0 个警告
    0 个错误
```

> 2026-11-24 实测：修复 `Ps1Run.App.csproj` 中的非法 `<tfm>` 占位符，并恢复 `AppendTargetFrameworkToOutputPath` 默认值后，上述输出已实际达成。

---

## 4. 发布

```powershell
# net48：框架依赖，产物小，用户零安装（推荐主发布）
dotnet publish Ps1Run.App -c Release -f net48 -o Build\net48

# net11：框架依赖（目标机需 .NET 11 Desktop Runtime）
dotnet publish Ps1Run.App -c Release -f net11.0-windows -o Build\net11

# net11：自包含单文件（体积大，但无运行时依赖）
dotnet publish Ps1Run.App -c Release -f net11.0-windows `
    --self-contained true -r win-x64 `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o Build\net11-selfcontained
```

注意：**不要**对 WPF 项目加 `-p:PublishTrimmed=true`，会生成运行期崩溃的产物（见 [09 §4.3](09-eval-dual-target-framework.md#43-按目标框架差异化)）。

---

## 5. 双版本验证（已做过的实测，可复现）

历史探针工程曾位于 `2026-11-24\ps1run-wiki\work\probe\`，**已从当前交付树移除**；以下命令仅作方法记录，结论见 [09 §3](09-eval-dual-target-framework.md#3-实测证据)：

```powershell
# 1) 双目标 WPF 构建
cd 2026-11-24\ps1run-wiki\work\probe\dual
dotnet build .\Probe.csproj -v:m        # 期望 0 警告 0 错误，产出 net48\Probe.exe 与 net11.0-windows\Probe.dll

# 2) 运行期 API 差异（GBK 编码）
cd ..\console
dotnet run -f net48                      # 期望：GBK936=OK
dotnet run -f net11.0-windows            # 期望：GBK936=FAIL NotSupportedException
```

> 这两条的结果就是 [09 §3](09-eval-dual-target-framework.md#3-实测证据) 的数据来源。`work\probe\` 已按计划在正式开发启动后删除。

---

## 6. 功能验证清单

按里程碑顺序逐项手工验证（对应 [04 里程碑](04-progress.md#2-里程碑)）。**截至 2026-11-24 核心逻辑已通过 7/7 组自动化测试与双目标构建，但下列系统级交互项仍待人工执行。**

### 6.1 探测能力（M2）

| # | 操作 | 期望 |
|---|---|---|
| 1 | 调用 `PowerShellLocator.LocatePs5()` | 返回 `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` |
| 2 | 调用 `LocatePs7()` | 返回 `C:\Program Files\PowerShell\7\pwsh.exe`（7.6.10） |
| 3 | `EditorLocator.Resolve()` | 返回 `D:\Soft\Code\VS Code\Code.exe`，来源标记为 `AppPaths` |
| 4 | 对 UTF-8 无 BOM 脚本调用 `Detect` | `Utf8NoBom` → 选 PS7 |
| 5 | 对 GBK 脚本调用 `Detect` | `Ansi` → 选 PS5 |
| 6 | 对带 BOM 的 UTF-8 脚本调用 `Detect` | `Utf8Bom` → 选 PS7 |

### 6.2 分发链路（M3）

| # | 操作 | 期望 |
|---|---|---|
| 1 | `Ps1Run.exe --run "C:\t\中文 脚本.ps1"` | 控制台窗口出现并执行，无本程序窗口闪烁 |
| 2 | `Ps1Run.exe --edit "C:\t\中文 脚本.ps1"` | 编辑器打开该文件 |
| 3 | 运行方式设为 `Ask`，双击脚本 | 弹出居中小窗，默认焦点在"执行" |
| 4 | 在小窗按 `E` | 进入编辑器 |
| 5 | 在小窗按 `Esc` | 不启动任何程序，进程退出码 0 |
| 6 | 勾选"以后都这样处理"后点执行 | 下次双击不再弹窗，直接执行 |
| 7 | 勾选"以后都这样处理"后点取消 | **设置不变**，下次仍然弹窗 |

### 6.3 关联注册（M4）

| # | 操作 | 期望 |
|---|---|---|
| 1 | 点"注册" | `reg query "HKCU\Software\Classes\Ps1Run.ps1" /s` 能看到 `open`/`run`/`edit` 三个动词 |
| 2 | `reg query "HKCU\Software\Classes\.ps1" /ve` | **当前代码不写扩展名默认值**（D16），看到 `ps1_auto_file` 属正常；实际默认程序仍由 `UserChoice` 决定 |
| 3 | 右键 `.ps1` → 打开方式 | 列表中出现 Ps1Run |
| 4 | 因 UserChoice 存在而提示 | 界面明确显示"当前默认程序为 VSCode.ps1" |
| 5 | 点"注销" | ProgId 子树消失；`OpenWithProgids` 中 `VSCode.ps1` 仍在 |

---

## 7. 常见问题排查

| 现象 | 原因 | 处理 |
|---|---|---|
| `error MSB4025: 项目文件未能加载 / <tfm> start tag ...` | `OutputPath` 里写了字面量 `<tfm>`，XML 非法 | 删除该行，输出目录交给 `Directory.Build.props`（已修复） |
| `error CS0006: 未能找到 ...\obj\Debug\ref\Ps1Run.Core.dll`（仅 net11） | `AppendTargetFrameworkToOutputPath=false` 让双目标共用中间目录，引用程序集互相覆盖 | 恢复 SDK 默认值，仅设置 `OutputPath` 为 `Build\` 根目录（已修复） |
| `error CS0518: IsExternalInit 未定义` | net48 使用 `record`/`init` 缺垫片 | 添加 `Compat\IsExternalInit.cs`（`#if NETFRAMEWORK`） |
| `error MSB3644: 找不到 .NETFramework v4.8 的参考程序集` | 未装 Developer Pack | 装 4.8 Developer Pack，或加 `Microsoft.NETFramework.ReferenceAssemblies` 包 |
| net11 下读 GBK 脚本抛 `NotSupportedException` | 未注册代码页提供程序 | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` |
| 注册时 `Requested registry access is not allowed` | 试图写 HKLM | 改用 HKCU；确需 HKLM 时走 `runas` 提升 |
| 注册成功了但双击仍是 VS Code | UserChoice 覆盖 | 属预期，用 `SHOpenWithDialog` 引导用户确认，见 [06 §3.3](06-tech-file-association.md#33-三条可行路径按推荐度排序) |
| 双击后图标没变 | 未刷新关联缓存 | 调 `SHChangeNotify(SHCNE_ASSOCCHANGED, ...)` |
| net11 单文件发布后启动崩溃 | 误开了 `PublishTrimmed` | 关闭裁剪 |
| 启动时报"在 `System.Windows.Baml2006.TypeConverterMarkupExtension` 上提供时引发了异常"（第 4 行） | 窗口 XAML 曾用相对路径 `Icon="icon.ico"`，WPF 解析成 `views/icon.ico` 找不到资源 | **已修复**为绝对 pack URI `pack://application:,,,/icon.ico`，并确认 `.csproj` 用 `<Resource Include="icon.ico" />` 嵌入；重建时勿改回相对路径 |
| 想查分发/注册失败原因 | 启动、分发、关联操作与异常均按会话写日志 | 看 `<软件根目录>\log\yyyyMMdd.log`，或在主界面点"复制最近日志" |
| 界面语言/主题不生效 | `config.json` 的值非法或语言包缺失 | 检查 `config.json`（`Theme` 取 Auto/Light/Dark，`Language` 对应 `lang\<code>.json`），日志里有"已应用语言/主题"记录 |
| 双击脚本中文路径执行失败 | 命令拼接未加引号 | 检查 `"%1"` 与 `ArgumentList`/转义 |
| `dotnet build` 在仓库根找不到项目 | 缺 slnx，或当前目录不是仓库根 | 在仓库根执行 `dotnet build Ps1Run.slnx` |

---

## 8. 代码约定

| 项 | 约定 |
|---|---|
| 语言版本 | `latest`；但**不得使用** net48 不可用的 BCL API（Core 工程以 net48 为最小编译约束） |
| 可空性 | `Nullable=enable`，边界判空 |
| 注释 | 中文；只注释"为什么"，不注释"是什么" |
| 源文件编码 | UTF-8 with BOM |
| 异常 | 边界（注册表/文件/进程）必须捕获并转成可读中文提示；不吞异常 |
| 测试 | 探测链与编码判型必须有单测；注册表操作通过接口抽象注入假实现 |

---

## 9. 清理与回滚（开发期）

```powershell
# 删除本程序写入的关联（等价于"注销"）
reg delete "HKCU\Software\Classes\Ps1Run.ps1" /f
reg delete "HKCU\Software\Classes\.ps1\OpenWithProgids" /v "Ps1Run.ps1" /f
reg delete "HKCU\Software\Classes\.ps1" /ve /f     # 谨慎：仅在确认该值为 Ps1Run.ps1 时执行

# 刷新缓存
# 或在资源管理器地址栏执行后重启 explorer：taskkill /f /im explorer.exe & start explorer
```

> 开发期反复注册/注销时，`UserChoice` 不会自动清理；当前代码也不会删它。上面的第三条命令（`reg delete ... .ps1 /ve`）对应设计中的"完整回滚"，但**当前注册流程并不写 `.ps1` 默认值**，`association-backup.json` 与"高级：移除当前选择"也**均未实现**（见 [04 D16/D17](04-progress.md#3-差距清单需求-vs-现状)）——仅在确认该值确为本程序写入时才执行删除。

---

← [10 评估：是否需要后台常驻](10-eval-background-residency.md) ｜ [首页](index.md) ｜ 下一篇：[12 附录：注册表键位地图 →](12-appendix-registry-map.md)
