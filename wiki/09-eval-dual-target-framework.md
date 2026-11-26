# 09 评估：.NET 4.8 与 .NET 11 双版本

> 评估对象：需求 3.1「是否支持 .NET 4.8 与 .NET 11 双版本实现，若可行则给出双版本生成方案」。
> 评估方式：在本机用探针工程**实际构建**，不是纸面推演。所有数据采集于 2026-11-24。
> 状态横幅：结论已落地为正式工程（`Ps1Run.slnx` + 两个多目标工程 + 单目标测试工程），双目标构建 0 警告 0 错误、8/8 组单测通过；探针工程仅保留为历史验证记录，已从交付树移除。正式实现的路径差异见 §4.2、§4.5 与 [03](03-project-structure.md#51-directorybuildprops公共部分)。

---

## 1. 结论

**可行。**

单个 csproj 使用 `TargetFrameworks=net48;net11.0-windows` + `UseWPF=true`，用已安装的 .NET SDK 11.0.100 执行**一次** `dotnet build`，即可同时产出两份产物，实测 **0 警告 0 错误**。

但有 **3 处必须处理的差异**，否则会出现"一个目标能编、另一个编不过"或"运行期行为不一致"：

| # | 差异 | 处理方式 | 是否已实测验证 |
|---|---|---|---|
| 1 | net48 不支持 `record` / `init`（报 CS0518） | 加 `IsExternalInit` 编译垫片 | ✅ 已验证 |
| 2 | net11 下 `Encoding.GetEncoding(936)` 抛 `NotSupportedException` | 注册 `CodePagesEncodingProvider` | ✅ 已验证 |
| 3 | net11 运行时不随系统预装 | 主发布 net48；net11 提供运行前提说明或自包含发布 | 环境事实 |

**推荐决策**：以 **net48 为主发布目标**（Win10 1903+ / Win11 内置，用户零安装依赖），net11 作为可选产物提供给已装 .NET 11 Desktop Runtime 的用户。

---

## 2. 依据：本机环境实测

| 项 | 实测值 | 对双版本的意义 |
|---|---|---|
| 操作系统 | Windows 11 专业版 10.0.26300 | 满足 WPF 与 net48 的运行前提 |
| .NET SDK | **11.0.100**（另有 10.0.115、10.0.100-rc.1） | 支持 `net11.0-windows` 目标 |
| .NET 桌面运行时 | WindowsDesktop **11.0.0**、10.0.15、9.0.23、8.0.34 | net11 产物在本机可运行 |
| .NET Framework | 4.8.10008（Release 534296） | net48 产物在本机可运行 |
| net48 参考程序集 | `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8` **存在** | **关键**：没有它 net48 无法编译 |
| Visual Studio | 2026 Insiders (18)，MSBuild 可用 | 备用构建通道 |

> 最容易踩的坑是"有 .NET Framework 运行时但没装参考程序集（Developer Pack）"，那样 `net48` 目标会报找不到 `mscorlib`。本机已具备，CI 机器需用 `Microsoft.NETFramework.ReferenceAssemblies` 包兜底（见 §4.4）。

---

## 3. 实测证据

### 3.1 探针工程

当时的探针工程位于 `2026-11-24\ps1run-wiki\work\probe\dual\`（**已从当前交付树移除**，仅作历史验证记录，见 [04](04-progress.md#6-已完成的验证工作)），最小 WPF 工程：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFrameworks>net48;net11.0-windows</TargetFrameworks>
    <UseWPF>true</UseWPF>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

### 3.2 构建结果（原样记录）

```
已还原 ...\Probe.csproj (用时 226 毫秒)。
  Probe -> ...\bin\Debug\net48\Probe.exe
  Probe -> ...\bin\Debug\net11.0-windows\Probe.dll

已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:08.48
```

### 3.3 产物对照

| 目标 | 产物 | 体积 | 运行前提 |
|---|---|---|---|
| net48 | `Probe.exe` + `.exe.config` + `.pdb` | exe 约 7 KB | 系统内置 .NET Framework 4.8 |
| net11.0-windows | `Probe.dll` + `Probe.exe`(apphost) + `runtimeconfig.json` + `deps.json` | dll 6.5 KB，apphost 72 KB | 需装 .NET 11 Desktop Runtime |

**解读**：net48 产物是"单文件即成品"（框架在系统里）；net11 产物是"托管 dll + 启动器"，必须依赖运行时目录。这直接决定了分发难度差异。

### 3.4 现代 C# 语法兼容实测

在探针中加入 `record` 与 `init` 属性后：

```
error CS0518: 预定义类型"System.Runtime.CompilerServices.IsExternalInit"未定义或导入
   [Probe.csproj::TargetFramework=net48]
```

加入垫片后：

```csharp
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
```

重新构建 → **0 警告 0 错误**，两目标均通过。

### 3.5 关键 API 兼容实测

用控制台探针在**运行期**分别验证（不只是编译期）：

| 检查项 | net48 结果 | net11 结果 |
|---|---|---|
| `AppContext.TargetFrameworkName` | `.NETFramework,Version=v4.8` | `.NETCoreApp,Version=v11.0` |
| `Encoding.GetEncoding(936)` | ✅ `OK name=gb2312` | ❌ `NotSupportedException` |
| `Microsoft.Win32.Registry.CurrentUser.OpenSubKey(...)` | ✅ OK | ✅ OK |

**含义**：

- 注册表 API 两版本行为一致，本项目的核心逻辑（关联注册）不需要条件编译。
- 编码差异**直接影响**需求 R3：读取 GBK 编码的 `.ps1` 时，net11 必须先注册代码页提供程序。这是双版本方案里唯一"会真正影响业务逻辑"的差异。

---

## 4. 双版本生成方案（可执行）

### 4.1 工程组织

```
.\（仓库根）
├─ Ps1Run.slnx
├─ Directory.Build.props          ← 公共属性 + 双目标
├─ Ps1Run.App\                    ← net48;net11.0-windows，WinExe，UseWPF
│  └─ Ps1Run.App.csproj
├─ Ps1Run.Core\                   ← net48;net11.0-windows，无 UI
│  └─ Ps1Run.Core.csproj
└─ Ps1Run.Core.Tests\             ← 单目标 net11.0-windows，零外部依赖控制台工程
   └─ Ps1Run.Core.Tests.csproj
```

两个产品工程都多目标，**测试工程只跑一个目标**（`net11.0-windows`，通过 `TargetFramework` + 空 `TargetFrameworks` 覆盖公共属性），避免测试矩阵翻倍；当前 8/8 组测试全部通过。

### 4.2 `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net48;net11.0-windows</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <AppendTargetFrameworkToOutputPath>true</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <!-- 产物集中到仓库 Build 目录，与兄弟项目约定一致，并按 TFM 分目录避免覆盖 -->
  <PropertyGroup>
    <OutputPath>$(MSBuildThisFileDirectory)..\..\Build\Ps1Run\$(TargetFramework)\</OutputPath>
  </PropertyGroup>
</Project>
```

> 说明：`打开方式清理` 用的是 `..\..\Build\<名称>\` 的集中输出；本项目多一层 TFM 子目录，否则 net48 与 net11 会互相覆盖 `Ps1Run.exe`。
>
> **正式实现（2026-11-24）**：没有采用上面的仓库级路径，而是把 `OutputPath` 设为工程根下的 `Build\`，保留 SDK 默认的 `AppendTargetFrameworkToOutputPath=true`，由 SDK 追加 TFM，得到 `Build\net48\` 与 `Build\net11.0-windows\`。这样中间目录 `obj\Debug\<TFM>\` 也按目标隔离；若把这个开关设为 false，net11 会报 `CS0006: 找不到 obj\Debug\ref\Ps1Run.Core.dll`。详见 [03 §5.1](03-project-structure.md#51-directorybuildprops公共部分)。

### 4.3 按目标框架差异化

```xml
<!-- Ps1Run.App.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWPF>true</UseWPF>
    <AssemblyName>Ps1Run</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <!-- net48：需要绑定重定向（WPF + 第三方库常见） -->
  <PropertyGroup Condition="'$(TargetFramework)'=='net48'">
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
  </PropertyGroup>

  <!-- net11：可选单文件发布；WPF 不支持 PublishTrimmed，不要开裁剪 -->
  <PropertyGroup Condition="'$(TargetFramework)'=='net11.0-windows'">
    <PublishSingleFile Condition="'$(Configuration)'=='Release'">true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <SelfContained>false</SelfContained>
  </PropertyGroup>
</Project>
```

> **当前落地情况**：正式 `Ps1Run.App.csproj` 只保留了 net48 的绑定重定向设置；net11 的单文件发布属性尚未写入工程，需要时在 `dotnet publish` 命令行补充（见 [11 §4](11-build-and-dev-guide.md#4-发布)）。

**WPF 的特殊限制**（务必注意）：

| 选项 | 是否可用 | 说明 |
|---|---|---|
| `PublishTrimmed` | ❌ **不支持** | WPF 依赖反射与 XAML，裁剪会导致运行期崩溃 |
| `PublishSingleFile` | ✅ 支持 | 需 `IncludeNativeLibrariesForSelfExtract=true`，体积较大 |
| `PublishReadyToRun` | ✅ 支持 | 可改善冷启动，但显著增大体积 |
| `SelfContained` | ✅ 支持 | net11 自包含约 150 MB 级，仅在"必须无运行时依赖"时使用 |

### 4.4 CI / 无 VS 环境下编译 net48

```xml
<!-- Ps1Run.Core.csproj 与 Ps1Run.App.csproj 均可加 -->
<ItemGroup Condition="'$(TargetFramework)'=='net48' AND '$(OS)'=='Windows_NT'">
  <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
</ItemGroup>
```

作用：当构建机没装 .NET Framework 4.8 Developer Pack 时，用 NuGet 包提供参考程序集。本地已有参考程序集时可省略，但加上更稳。

### 4.5 统一注册代码页（消除差异 2）

```csharp
// Ps1Run.Core\CoreBootstrap.cs（正式实现）
public static void Initialize()
{
#if !NETFRAMEWORK
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
}
```

> **正式实现调整**：没有引用 `System.Text.Encoding.CodePages` NuGet 包（net11 运行时已提供 `CodePagesEncodingProvider`），改用 `#if !NETFRAMEWORK` 条件编译，只在 net11 注册；net48 内置 936，无需调用。App 启动时调用 `CoreBootstrap.Initialize()`。

### 4.6 构建与发布命令

```powershell
# 一次构建两个目标
dotnet build Ps1Run.slnx -c Release

# 只构建某一目标
dotnet build Ps1Run.App -c Release -f net48
dotnet build Ps1Run.App -c Release -f net11.0-windows

# 发布（net48：框架依赖，产物小，用户零安装）
dotnet publish Ps1Run.App -c Release -f net48 -o Build\net48

# 发布（net11：需目标机装 Desktop Runtime）
dotnet publish Ps1Run.App -c Release -f net11.0-windows -o Build\net11
```

---

## 5. 双版本差异清单（开发时逐条对照）

| 领域 | net48 | net11.0-windows | 本项目处理 |
|---|---|---|---|
| `record` / `init` | 需 `IsExternalInit` 垫片 | 原生支持 | 加垫片（影响所有目标，`#if NETFRAMEWORK` 保护） |
| 代码页 936 | 内置 | 需 `CodePagesEncodingProvider` | `#if !NETFRAMEWORK` 下由 `CoreBootstrap` 注册，不引 NuGet 包 |
| `Span<T>` / `Memory<T>` | 需 `System.Memory` 包 | 内置 | **避免使用**，用数组代替（见 [07 §4.1](07-tech-powershell-encoding.md#41-实现片段双目标安全写法)） |
| `System.Text.Json` | 需 NuGet 包 | 内置 | 引用包，两目标一致 |
| `ProcessStartInfo.ArgumentList` | ❌ 不存在 | ✅ 存在 | 手写转义；或用 `Arguments` 字符串 |
| 单文件发布 | 不支持（本身就是单 exe） | 支持 | 分别处理 |
| 裁剪（Trim） | 不适用 | WPF 不支持 | 一律不开 |
| 启动速度 | 更快（无运行时加载） | 稍慢（首次 JIT） | 不作为选型依据 |
| 安装依赖 | 系统内置 | 需 Desktop Runtime | **这是主发布选 net48 的核心理由** |
| DPI / 高 DPI 支持 | 需 manifest 声明 | 更好（PerMonitorV2 改进） | manifest 里声明 `PerMonitorV2` |

---

## 6. 风险评估

| 风险 | 等级 | 说明 | 缓解 |
|---|---|---|---|
| 用户没有 .NET 11 运行时 | 高（对 net11 产物） | 双击后弹"需要安装 .NET" | 主推 net48；net11 单独标注 |
| net48 已是"冻结"平台 | 中 | 不再有新语言运行时特性，长期看会落后 | 核心逻辑保持在 `netstandard2.0` 可用的 API 子集内，便于将来加 `net8.0-windows` 目标 |
| 双目标导致构建时间翻倍 | 低 | 两个目标串行编译 | 可接受；CI 可并行 |
| 差异点维护成本 | 中 | 新代码可能不小心用了 net11-only API | `Ps1Run.Core` 的最小编译目标设 net48，**net48 编不过就说明踩线**，天然形成约束 |
| 测试只覆盖一个目标 | 低 | 当前 8/8 组测试仅在 net11 目标运行，net48 路径未被自动化测试覆盖；但两个目标均构建通过且主界面/Ask 弹窗已在 net48 上做过启动验证 | 后续对 `ScriptEncodingDetector`、`RegistryAppPathReader` 在 net48 上补一次冒烟 |

---

## 7. 备选方案与取舍

| 方案 | 优点 | 缺点 | 是否推荐 |
|---|---|---|---|
| **A. net48 + net11 双目标**（本方案） | 兼顾零依赖与现代化；单工程维护 | 需处理三处差异 | ✅ **推荐** |
| B. 只做 net48 | 最简单，零依赖 | 技术栈停留在 2019 年，无新特性 | 备选（若只求能用） |
| C. 只做 net11 | 现代、简洁 | 用户需装运行时，分发门槛高 | 不推荐 |
| D. net48 + net8.0-windows | .NET 8 是 LTS，本机已装 | 不满足需求明确点名的".NET 11" | 可作为第三目标追加 |

> 若后续要加 `net8.0-windows`，只需在 `TargetFrameworks` 里追加一项，方案不变——这也是多目标写法的一个附加收益。

---

## 8. 评估小结

| 问题 | 结论 |
|---|---|
| 是否支持双版本？ | **支持**，已实测构建通过 |
| 生成方案是否成熟？ | 成熟，单工程多目标是官方推荐做法 |
| 需要多少额外代码？ | 约 1 个垫片文件 + 1 行编码提供程序注册 + 若干条件编译属性 |
| 主发布目标选谁？ | **net48**（零安装依赖），net11 可选 |
| 有没有不可逾越的障碍？ | **没有** |

---

← [08 编辑器探测与回退](08-tech-editor-discovery.md) ｜ [首页](index.md) ｜ 下一篇：[10 评估：是否需要后台常驻 →](10-eval-background-residency.md)
