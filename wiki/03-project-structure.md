# 03 项目结构

> 状态横幅：本页描述的结构**主体已落地**。截至 2026-11-25，项目已迁至独立仓库（含 `.git`），仓库根已有 `Ps1Run.slnx`、三个工程（含 `Ps1Run.Core.Tests`）、57 个 C#/XAML 源文件及 `icon.ico`，双目标构建与 7/7 组单测通过。状态真源见 [02](02-feature-matrix.md) / [04](04-progress.md)。

---

## 1. 现状（实测）

```
.\（仓库根）
├─ Ps1Run.slnx                          ✅ 已存在
├─ Directory.Build.props                ✅ 已存在（双目标 + 输出路径）
├─ Ps1Run.Core\                         ✅ 已存在（Associations/Dispatch/PowerShell/Editors/Settings/Infrastructure）
├─ Ps1Run.App\                          ✅ 已存在（App/Views/ViewModels/Services/Resources/icon.ico/app.manifest）
├─ Ps1Run.Core.Tests\                   ✅ 已存在（7/7 组自动化测试通过）
├─ scripts\                             ✅ 已存在（运行脚本 + 三个构建入口）
├─ Build\net48\Ps1Run.exe               ✅ 构建产物（内嵌 icon.ico）
├─ Build\net11.0-windows\Ps1Run.dll     ✅ 构建产物（内嵌 icon.ico）
└─ wiki\                                ✅ 本设计文档
```

两个工程各自已有 `Compat\IsExternalInit.cs` 垫片；`icon.ico`（多尺寸 16~256，32bpp）通过 `<ApplicationIcon>` 内嵌为 exe 图标、通过 `<Resource>` 作为窗口图标，文件关联 `DefaultIcon` 直接指向 exe（`"...Ps1Run.exe",0`）。**坑**：XAML 中必须用绝对 pack URI `Icon="pack://application:,,,/icon.ico"`；写成相对路径 `Icon="icon.ico"` 会被解析为 `views/icon.ico`，导致 `XamlParseException`（TypeConverterMarkupExtension），两个窗口都曾踩过，已修复。旧的单尺寸 `Resources\ps1.ico` 已删除。代码风格按同仓库兄弟项目 `打开方式清理` 的既有做法对齐：手写 MVVM、不用额外框架、中文注释。

---

## 2. 对齐仓库既有约定

调研同级项目 `src\打开方式清理\`（同样是 WPF + 注册表操作的工具），得到以下可复用约定：

| 约定 | 兄弟项目实际值 | 本项目采用 |
|---|---|---|
| SDK 风格工程 | `Microsoft.NET.Sdk` | 采用 |
| 输出类型 | `<OutputType>WinExe</OutputType>` | 采用 |
| WPF 开关 | `<UseWPF>true</UseWPF>` | 采用 |
| 程序集名可中文 | `<AssemblyName>打开方式</AssemblyName>` | 采用英文 `Ps1Run`（便于命令行与关联命令书写） |
| 输出目录集中 | `<OutputPath>..\..\Build\打开方式\</OutputPath>` | 采用工程根下 `Build\<TFM>\`（由 `Directory.Build.props` 注入） |
| MVVM 轻量实现 | `DataContext = new MainViewModel()`，手写 `MainViewModel` | 采用，不引入 MVVM 框架 |
| 语言版本 | `<LangVersion>preview</LangVersion>` | 改为 `latest`（避免 preview 特性带来不确定性） |

项目已是独立仓库（自带 `.git`），`Ps1Run.slnx` 位于仓库根，`dotnet build Ps1Run.slnx` 直接可用。原先"父仓库 `src\运行.ps1` 遍历 `src\` 各子目录执行构建"的约束随之失效，本页不再按 `tool-box` 的子目录约定组织。

---

## 3. 当前目录树（已落地）

```
.\（仓库根）
├─ Ps1Run.slnx                          # 解决方案根，保证 `dotnet build` 在根目录可用
├─ Directory.Build.props                # 公共属性：LangVersion/Nullable/双目标列表
├─ scripts\                             # 运维脚本（UTF-8 BOM，兼容 Windows PowerShell 5.1）
│  ├─ run.ps1                           #   运行产物：-Framework / -Build / -Intent / -ScriptPath
│  ├─ build-common.ps1                  #   共用构建实现（由三个入口调用，不直接运行）
│  ├─ build-all.ps1                     #   全部生成：net48 + net11.0-windows
│  ├─ build-net48.ps1                   #   只生成 net48
│  └─ build-net11.ps1                   #   只生成 net11.0-windows
├─ .editorconfig                        # 代码风格（可选）
│
├─ Ps1Run.App\                          # WPF 宿主：只负责 UI 与进程启动
│  ├─ Ps1Run.App.csproj                 #   TargetFrameworks=net48;net11.0-windows, UseWPF
│  ├─ app.manifest                      #   DPI 感知、requestedExecutionLevel=asInvoker
│  ├─ App.xaml / App.xaml.cs            #   入口：参数分流（直通 / 显示主窗）
│  ├─ Views\
│  │  ├─ MainWindow.xaml / .xaml.cs     #   主界面：关联状态、设置、诊断
│  │  └─ PickerWindow.xaml / .xaml.cs   #   "选择"模式弹窗（执行/编辑/取消）+ 文件选择
│  ├─ ViewModels\
│  │  ├─ MainViewModel.cs               #   参照兄弟项目的 MainViewModel 写法
│  │  └─ PickerViewModel.cs
│  └─ icon.ico                          #   软件 logo（ApplicationIcon + 窗口图标，XAML 用绝对 pack URI 引用）
│
├─ Ps1Run.Core\                         # 无 UI 内核：可单元测试、可双目标
│  ├─ Ps1Run.Core.csproj                #   TargetFrameworks=net48;net11.0-windows
│  ├─ Compat\
│  │  └─ IsExternalInit.cs              #   #if NETFRAMEWORK 垫片（record/init 必需）
│  ├─ Associations\
│  │  ├─ AssociationService.cs          #   注册/注销/状态查询 门面
│  │  ├─ AssociationRegistration.cs     #   ProgId 与动词布局
│  │  ├─ UserChoiceGuard.cs             #   UserChoice 检测与提示
│  │  ├─ ElevationHelper.cs             #   可选：HKLM 注册时的 UAC 提升
│  │  └─ ShellNotify.cs                 #   SHChangeNotify 刷新图标缓存
│  ├─ Dispatch\
│  │  ├─ CommandLineOptions.cs          #   参数协议解析
│  │  └─ LaunchDispatcher.cs            #   决策与分发
│  ├─ PowerShell\
│  │  ├─ PowerShellEdition.cs           #   枚举：Ps5 / Ps7
│  │  ├─ PowerShellLocator.cs           #   探测链
│  │  └─ ScriptEncodingDetector.cs      #   编码判型
│  ├─ Editors\
│  │  ├─ EditorDefinition.cs            #   编辑器名/路径/参数模板
│  │  ├─ EditorLocator.cs               #   四级回退链
│  │  └─ RegistryAppPathReader.cs       #   App Paths / Uninstall 读取
│  ├─ Configuration\
│  │  ├─ AppConfig.cs                   #   程序配置模型（日志天数 / 主题 / 语言）
│  │  ├─ AppState.cs                    #   合并后 config.json 的完整内容（Version + 两者）
│  │  ├─ ConfigDocument.cs              #   单文件读改写：原子写入 / 损坏备份 / 旧 settings.json 合并
│  │  ├─ ConfigStore.cs                 #   程序配置读写门面
│  │  └─ AppPaths.cs                    #   根目录 / config.json / lang / log 约定
│  ├─ Settings\
│  │  ├─ AppSettings.cs                 #   设置模型
│  │  └─ SettingsStore.cs               #   运行设置读写门面（与 ConfigStore 同一文件）
│  └─ Infrastructure\
│     ├─ AppInfo.cs                     #   程序集版本号（界面显示 + 缺失提示）
│     ├─ NativeMethods.cs               #   SHOpenWithDialog / SHChangeNotify P/Invoke
│     ├─ DiagnosticsLog.cs              #   分级日志（会话 ID / 保留 7 天 / ReadRecent）
│     ├─ IFileSystem.cs                 #   文件系统抽象（便于测试注入）
│     ├─ PhysicalFileSystem.cs          #   IFileSystem 的磁盘实现
│     ├─ PathSearch.cs                  #   PATH 环境变量查找
│     └─ ProcessLauncher.cs             #   启动目标进程（记录 PID 与 Win32 错误码）
│
├─ Ps1Run.Core.Tests\                   # 零依赖控制台单测（7/7 组通过，仅 net11 目标）
│  ├─ Ps1Run.Core.Tests.csproj
│  ├─ Program.cs                        #   测试入口与断言辅助
│  ├─ FakeFileSystem.cs                 #   内存文件系统（探测链测试用）
│  ├─ ScriptEncodingDetectorTests.cs    #   编码判型 6 类输入
│  ├─ CommandLineOptionsTests.cs        #   参数优先级与冲突
│  ├─ PowerShellLocatorTests.cs         #   PS5/PS7 探测回退
│  ├─ SettingsStoreTests.cs             #   JSON 往返 / 与 config.json 同文件 / 损坏恢复
│  └─ DiagnosticsLogTests.cs            #   日志写入 / 读取 / 保留策略
│
└─ wiki\                                # 本文档（13 页，位于工程根）
```

---

## 4. 为什么拆 App 与 Core

| 理由 | 说明 |
|---|---|
| 可测试性 | 编码判型、探测链、参数解析这些逻辑有大量边界条件，必须能脱离 UI 单测。Core 不引用 `PresentationFramework`，测试工程可直接引用。 |
| 双目标成本更低 | Core 不依赖 WPF 的 XAML 编译，`net48` 与 `net11.0-windows` 的差异集中在少数几处（见 [09](09-eval-dual-target-framework.md)），隔离后变更面更小。 |
| 故障隔离 | 关联注册是全项目最有破坏性的操作，把它关在 Core 里，Ui 层只能通过 `AssociationService` 门面调用，便于加审计日志与二次确认。 |
| 与兄弟项目一致 | `打开方式清理` 已经把手写 `MainViewModel` 与注册表逻辑分文件，拆分只是顺势而为，不引入新范式。 |

> 注意：这里**不**引入 MVVM 框架（CommunityToolkit/Prism），保持与仓库其他项目一致的"手写 VM + DataContext"风格，避免额外依赖在 net48 上的兼容负担。

---

## 5. 关键工程属性

### 5.1 `Directory.Build.props`（公共部分）

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net48;net11.0-windows</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>   <!-- 兼容仓库既有 using 风格 -->
    <OutputPath>$(MSBuildThisFileDirectory)Build\</OutputPath>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

> 注意：`AppendTargetFrameworkToOutputPath` 必须保留 SDK 默认值（true），由 SDK 在 `OutputPath` 和 `IntermediateOutputPath` 后追加 TFM。若设为 false，双目标会共用 `obj\Debug\ref\`，导致 net11 编译时找不到正确的 `Ps1Run.Core.dll`（已实际踩坑并修复）。

### 5.2 `Ps1Run.App.csproj`（双目标）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWPF>true</UseWPF>
    <AssemblyName>Ps1Run</AssemblyName>
    <RootNamespace>Ps1Run.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <SatelliteResourceLanguages>zh-Hans</SatelliteResourceLanguages>
  </PropertyGroup>

  <PropertyGroup Condition="'$(TargetFramework)' == 'net48'">
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Ps1Run.Core\Ps1Run.Core.csproj" />
  </ItemGroup>
</Project>
```

> 双目标列表与输出根目录由 `Directory.Build.props` 统一注入；早期版本中此处曾误写 `<OutputPath>...\<tfm>\</OutputPath>`，导致 XML 解析失败（MSB4025），已删除。

> 该写法先在 [09 评估页](09-eval-dual-target-framework.md#3-实测证据) 的探针工程中验证，现已在正式工程 `dotnet build Ps1Run.slnx` 中实测通过：一次构建同时产出 `Build\net48\Ps1Run.exe` 与 `Build\net11.0-windows\Ps1Run.dll`，0 警告 0 错误。

### 5.3 `Ps1Run.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Ps1Run.Core</AssemblyName>
    <RootNamespace>Ps1Run.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

> 双目标与输出根目录同样由 `Directory.Build.props` 注入，工程文件无需重复声明。GBK 支持未引入 NuGet 包：`CoreBootstrap.Initialize()` 在 net11 下调用 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`，net48 内置代码页；条件编译放在 Core 内部，避免额外依赖。

---

## 6. 输出产物约定

| 目标框架 | 产物 | 运行前提 | 建议分发形态 |
|---|---|---|---|
| `net48` | `Build\net48\Ps1Run.exe`（约几 MB） | Win10 1903+/Win11 **内置** .NET Framework 4.8 | **主发布目标**，单文件直发 |
| `net11.0-windows` | `Build\net11.0-windows\Ps1Run.dll` + 启动器 + `runtimeconfig.json` | 用户需安装 .NET 11 Desktop Runtime | 可选产物；或改成自包含单文件（体积显著增大） |

两者可并存于同一安装目录的子文件夹中，互不干扰。关联命令只需指向其中一个。

---

## 7. 命名与编码规范

| 项 | 约定 |
|---|---|
| 命名空间根 | `Ps1Run.App.*` / `Ps1Run.Core.*` |
| 文件名 | PascalCase，与类型同名 |
| 源码编码 | **UTF-8 with BOM**（重要：与 R3 讨论的 `.ps1` 编码不同，这是 C# 源码；带 BOM 可避免中文注释在 GBK 环境下的误读） |
| 注释语言 | 中文（与仓库其他项目一致） |
| 空值处理 | 启用 `Nullable`，边界（注册表/文件/进程）必须显式判空 |

---

## 8. 新增文件清单（与功能清单对应）

截至 2026-11-24 已有 **57 个 C#/XAML 源文件 + 3 个工程文件 + 1 个解决方案文件 + `Directory.Build.props` + `icon.ico`**，约 7094 行。`Ps1Run.Core.Tests` 已建立并通过 7/7 组测试。文件系统交互通过 `IFileSystem` 抽象注入（`ScriptEncodingDetector` / `PowerShellLocator` / `EditorLocator` 均可替换）；注册表读取是直接调用 `Microsoft.Win32.Registry`（无接口抽象），交互型注册表用例保留给人工验收。运行期产物（`config.json`、`lang\`、`log\`）落在 exe 所在目录，不在仓库内。

---

← [02 功能实现清单](02-feature-matrix.md) ｜ [首页](index.md) ｜ 下一篇：[04 项目进度与差距 →](04-progress.md)
