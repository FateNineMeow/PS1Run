# 07 技术专题：PowerShell 版本探测与编码判型

> 状态横幅：截至 2026-11-24，本页 F3.1–F3.6 **主体已落地为代码**（三态策略、编码判型、探测链、缺失人工指定、分发规则均有实现与单测；核心逻辑 7/7 组自动化测试通过）。注册表路径、可执行文件位置、API 行为均在本机（Windows 11 26300、PS 7.6.10）实测过。仍属**设计未落地**的有：PS7 的 WindowsApps/Store 版探测（F3.2，见 [04 D19](04-progress.md#3-差距清单需求-vs-现状)）与降级时"改用另一版本重跑"的界面入口（见 [04 D21](04-progress.md#3-差距清单需求-vs-现状)）。功能状态真源为 [02](02-feature-matrix.md#f3-执行程序对应-r3r4)。

---

## 1. 三种策略

| 策略 | 行为 | 适用人群 |
|---|---|---|
| `PS5` | 固定使用 Windows PowerShell 5.1（`powershell.exe`） | 脚本依赖 5.1 特有模块/行为 |
| `PS7` | 固定使用 PowerShell 7+（`pwsh.exe`） | 已全面迁移到 7 的用户 |
| `Auto`（默认） | 读脚本编码，自动在 PS5/PS7 间选择 | 大多数用户 |

**Auto 的技术依据**：PS5.1 与 PS7 对"无 BOM 脚本文件"的默认解码方式不同——PS5.1 用系统 ANSI 代码页（简体中文系统为 GBK/936），PS7 用 UTF-8。这导致同一个文件在两个版本里可能一个正常、一个乱码。反过来看，"文件是什么编码"就成了"该用哪个版本"的可靠判据。

| 脚本实际编码 | PS5.1 行为 | PS7 行为 | Auto 应选 |
|---|---|---|---|
| UTF-8 **带 BOM** | 正确 | 正确 | PS7 |
| UTF-16（带 BOM） | 正确 | 正确 | PS7 |
| UTF-8 **无 BOM** | 中文乱码 | 正确 | **PS7** |
| ANSI / GBK（无 BOM） | 正确 | 中文乱码 | **PS5** |
| 纯 ASCII | 正确 | 正确 | PS7（等价） |

> 这张表是整个"自动"功能的立论基础。注意最后一行：纯 ASCII 脚本两版本行为完全一致，所以判成 PS7 不会带来风险。

---

## 2. PS5 与 PS7 是什么

| 项 | PS5.1 | PS7+ |
|---|---|---|
| 可执行 | `powershell.exe` | `pwsh.exe` |
| 本质 | .NET Framework 上的 Windows PowerShell，随系统内置 | 独立安装的跨平台 PowerShell |
| 本机实测 | `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` ✅ | 7.6.10，`C:\Program Files\PowerShell\7\pwsh.exe` ✅ |
| 是否存在"找不到"的情况 | 极少（Win7 SP1 起内置） | **常见**（用户没装） |

---

## 3. 探测顺序

### 3.1 PS5

| 序 | 探测源 | 本机结果 |
|---|---|---|
| 1 | `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` | ✅ 命中 |
| 2 | `%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`（32 位进程场景） | 存在 |
| 3 | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\powershell.exe` | 未验证（第 1 步已命中） |
| 4 | `PATH` 中查找 `powershell.exe` | 命中（`Get-Command powershell` 解析到 System32） |

### 3.2 PS7

| 序 | 探测源 | 本机结果 |
|---|---|---|
| 1 | `HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\{GUID}\InstallLocation` + `pwsh.exe` | ✅ 命中，`SemanticVersion=7.6.10`，`InstallLocation=C:\Program Files\PowerShell\7\` |
| 2 | `HKLM\...\App Paths\pwsh.exe` 默认值 | ✅ 命中，`C:\Program Files\PowerShell\7\pwsh.exe` |
| 3 | `%ProgramFiles%\PowerShell\7\pwsh.exe`、`%ProgramFiles%\PowerShell\7-preview\pwsh.exe` | 存在（第 1 步已命中） |
| 4 | `PATH` 中查找 `pwsh.exe` | ✅ 命中（`D:\...` 之外，实际解析到 Program Files） |
| 5 | Microsoft Store 版：`%ProgramFiles%\WindowsApps\Microsoft.PowerShell_*` | 本机无；**代码未实现该分支**（见 [04 D19](04-progress.md#3-差距清单需求-vs-现状)） |

**实现要点（当前代码 vs 设计）**

- 第 1 步已实现：`RegistryAppPathReader.ResolvePowerShellCoreInstallLocation()` **枚举** GUID 子键而非硬编码，读 `InstallLocation` 后由 `PowerShellLocator` 拼 `pwsh.exe` 并校验存在性，不只信注册表。
- 第 5 步**未实现**：`WindowsApps` 目录默认 ACL 受限，`File.Exists` 可能返回 `false` 或抛异常；若后续实现需 `try/catch` 包裹并跳过（D19）。
- 探测链未做委托注入，但 `PowerShellLocator.Trace(edition)` 会返回 `ProbeAttempt`（候选路径 + 来源 + 是否存在）列表，供主界面"一键探测"和诊断展示使用；单测通过注入 `IFileSystem` 伪造文件存在性实现（F3.5）。

---

## 4. 编码判型决策表

```
读文件头 4 字节
 ├─ EF BB BF        → Utf8Bom
 ├─ FF FE (00 00)   → Utf16Le / Utf32Le
 ├─ FE FF           → Utf16Be
 ├─ 文件为空        → Empty
 └─ 其它            → 全文严格 UTF-8 解码尝试
                       ├─ 成功 → Utf8NoBom
                       └─ 抛 DecoderFallbackException → Ansi
```

| 判定结果 | 含义 | 选用 |
|---|---|---|
| `Utf8Bom` | UTF-8 带 BOM | PS7 |
| `Utf16Le` / `Utf16Be` | UTF-16 带 BOM | PS7 |
| `Utf8NoBom` | 无 BOM 且字节序列是合法 UTF-8 | PS7 |
| `Ansi` | 不是合法 UTF-8，按系统 ANSI（GBK）处理 | PS5 |
| `Empty` / `Unknown` | 无内容或读失败 | 用设置中的兜底策略（默认 PS7），并记录日志 |

### 4.1 实现片段（双目标安全写法）

> 以下为与正式实现等价的示意片段；正式代码在 `Ps1Run.Core\PowerShell\ScriptEncodingDetector.cs`，通过 `IFileSystem` 注入便于单测。

```csharp
public enum ScriptEncodingKind { Empty, Utf8Bom, Utf16Le, Utf16Be, Utf8NoBom, Ansi, Unknown }

public static ScriptEncodingKind Detect(string path)
{
    try
    {
        using var fs = File.OpenRead(path);

        // 注意：net48 无内置 Span<T>，不要用 stackalloc Span<byte>，改用数组
        var head = new byte[4];
        int n = fs.Read(head, 0, head.Length);

        if (n == 0) return ScriptEncodingKind.Empty;
        if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            return ScriptEncodingKind.Utf8Bom;
        if (n >= 2 && head[0] == 0xFF && head[1] == 0xFE)
            return head[2] == 0x00 && head[3] == 0x00
                ? ScriptEncodingKind.Unknown        // UTF-32，极少见，交给兜底
                : ScriptEncodingKind.Utf16Le;
        if (n >= 2 && head[0] == 0xFE && head[1] == 0xFF)
            return ScriptEncodingKind.Utf16Be;

        // 无 BOM：整文件严格 UTF-8 校验（throwOnInvalidBytes: true）
        fs.Position = 0;
        using var reader = new StreamReader(
            fs,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false);
        reader.ReadToEnd();
        return ScriptEncodingKind.Utf8NoBom;
    }
    catch (DecoderFallbackException)
    {
        return ScriptEncodingKind.Ansi;
    }
    catch (IOException)
    {
        return ScriptEncodingKind.Unknown;
    }
}
```

**三个易错点**

1. **必须整文件校验**，不能只采样前 N 字节——多字节字符可能跨在采样边界上，导致误判 `Ansi`。`.ps1` 通常几十 KB，整读成本可忽略；正式代码已加 32 MB 上限（`MaxFullScanBytes`）保护，超过才退化为头部采样。
2. **不要用 `stackalloc Span<byte>`**：`Span<T>` 在 net48 依赖 `System.Memory` 包，为省依赖直接 `new byte[4]`。
3. **检测阶段无需 `Encoding.GetEncoding(936)`**：判定"是不是合法 UTF-8"不依赖 GBK 解码器，因此不必引入代码页。只有当界面要预览 GBK 内容时才需要注册 `CodePagesEncodingProvider`（net11 必需，见 [09 §5](09-eval-dual-target-framework.md#5-双版本差异清单开发时逐条对照)）。

---

## 5. 自动选型规则

```
ResolvePowerShell(scriptPath, policy):
  availablePs5 = LocatePs5()
  availablePs7 = LocatePs7()

  switch policy:
    PS5 → 用 availablePs5，缺失则走"人工指定"流程
    PS7 → 用 availablePs7，缺失则走"人工指定"流程
    Auto:
      kind = Detect(scriptPath)
      wantPs5 = (kind == Ansi)
      if wantPs5:
          return availablePs5 ?? 降级(availablePs7, 提示"该脚本疑似 GBK，用 PS7 可能乱码")
      else:
          return availablePs7 ?? 降级(availablePs5, 提示"该脚本疑似 UTF-8，用 PS5 可能乱码")
```

**降级原则**：宁可给出"能用但可能乱码"的结果并明确提示，也不要直接失败——因为用户可能就是要看那段乱码来定位问题。**当前代码**：降级说明写入 `PowerShellResolution.Note` 并记入日志；但"改用另一版本重跑"的**界面快捷入口尚未实现**（见 [04 D21](04-progress.md#3-差距清单需求-vs-现状)），用户需到主界面改策略后重跑。

### 5.1 与"运行方式"的关系（不要混淆两层）

运行方式（`Run` / `Edit` / `Ask`）回答的是"**编辑还是执行**"；本节的 PS5/PS7/Auto 回答的是"**执行时用哪个解释器**"。

| 运行方式 | 是否会走到本节逻辑 |
|---|---|
| `Run` | 会，直接进入 |
| `Edit` | **不会**（不启动任何 PowerShell） |
| `Ask` → 用户选"执行" | 会，与 `Run` 完全相同 |
| `Ask` → 用户选"编辑" | **不会**，转到 [08 编辑器探测](08-tech-editor-discovery.md) |
| `Ask` → 取消 | **不会**，不启动任何进程 |

因此"选择"模式不会增加本节的判定分支，也不会让编码判型执行两次——它只在分发前插入一次用户决策。

---

## 6. 缺失回退

| 场景 | 行为 |
|---|---|
| 策略为 PS7，未装 PS7 | 弹窗："未找到 PowerShell 7。请安装，或指定 pwsh.exe 路径。" 按钮：`浏览…` / `改用 PS5` / `取消` |
| 用户浏览选择 | `OpenFileDialog`（过滤 `pwsh.exe;powershell.exe`），选中后校验 `File.Exists` 并写入设置 |
| 已记住的路径后来失效 | 启动时校验，失效则清除并重新提示，不静默失败 |
| 两版本都缺 | 提示"未找到任何 PowerShell，无法执行脚本"，不尝试猜测 |

**持久化字段**（`AppSettings`）：`PowerShellPolicy`、`CustomPs5Path`、`CustomPs7Path`。

---

## 7. 进程启动参数

| 版本 | 命令行 |
|---|---|
| PS5 | `powershell.exe -NoProfile -File "C:\a\b.ps1"` |
| PS7 | `pwsh.exe -NoProfile -File "C:\a\b.ps1"` |

要点：
- `-File` 必须是**最后一个**参数，后面跟脚本路径。
- 使用 `-File` 而不是 `-Command`，避免路径中的空格/引号被二次解析。
- 工作目录设为脚本所在目录（`ProcessStartInfo.WorkingDirectory`）。
- 不追加 `-ExecutionPolicy`，遵循系统策略（本机 `LocalMachine=RemoteSigned`）。
- `UseShellExecute = false` 才能精确控制参数与工作目录；同时需要 `CreateNoWindow = false` 以显示控制台。

---

## 8. 与需求条目的对应

| 需求 | 覆盖点 | 本页章节 |
|---|---|---|
| R2 运行方式为执行或"选择→执行" | 本页逻辑只在执行分支生效 | §5.1 |
| R3 三态选择 | 策略模型 | §1 |
| R3 按编码自动选择 | 决策表 + 选型规则 | §4、§5 |
| R4 自动搜索执行程序 | 探测顺序 | §3 |
| R4 不存在时提示指定 | 缺失回退 | §6 |

---

← [06 文件关联注册与注销](06-tech-file-association.md) ｜ [首页](index.md) ｜ 下一篇：[08 编辑器探测与回退 →](08-tech-editor-discovery.md)
