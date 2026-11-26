# 08 技术专题：编辑器探测与回退

> 状态横幅：截至 2026-11-24，本页 F4.1–F4.5 **主体已落地为代码**（`EditorLocator` 实现了用户配置 → 硬编码 → App Paths → Uninstall → PATH → 记事本兜底的回退链，主界面可一键探测并回填设置）。F4.6 中"探测全部失败时自动弹 `OpenFileDialog` 指定"仍是**设计未落地项**（见 [04 D20](04-progress.md#3-差距清单需求-vs-现状)）；目前只能在主界面手工指定路径。本页所有"实测"结论均来自本机（Windows 11 26300），功能状态真源为 [02](02-feature-matrix.md#f4-编辑程序对应-r5)。

---

## 1. 问题定义

需求原文是"尝试常见编辑器的默认安装位置，文件不存在时提示用户选择"。本页要说清的是：**只靠"常见默认位置"这一层一定会失败**，必须扩展探测源，否则在本机上该功能直接不可用。

---

## 2. 候选与探测源

| 编辑器 | 常见安装路径（硬编码候选） | 注册表锚点 | 命令行模板 |
|---|---|---|---|
| **VS Code** | `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`<br>`%ProgramFiles%\Microsoft VS Code\Code.exe`<br>`%ProgramFiles(x86)%\Microsoft VS Code\Code.exe` | `App Paths\Code.exe`<br>`Uninstall\*` 中 `DisplayName` 含 `Visual Studio Code` | `"Code.exe" "%1"` |
| **Notepad++** | `%ProgramFiles%\Notepad++\notepad++.exe`<br>`%ProgramFiles(x86)%\Notepad++\notepad++.exe` | `App Paths\notepad++.exe`<br>`Uninstall\*` | `"notepad++.exe" "%1"` |
| **Sublime Text** | `%ProgramFiles%\Sublime Text\sublime_text.exe` | `App Paths\sublime_text.exe`<br>`Uninstall\*` | `"sublime_text.exe" "%1"` |
| **Notepad（兜底）** | `%SystemRoot%\System32\notepad.exe`<br>`%SystemRoot%\notepad.exe` | — | `"notepad.exe" "%1"` |
| **PowerShell ISE** | `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell_ise.exe` | — | `"powershell_ise.exe" "%1"` |

**探测源优先级**（与 `EditorLocator.Resolve()` 代码顺序一致）：用户配置 → 硬编码路径 → `App Paths` → `Uninstall\InstallLocation` → `PATH` → 记事本兜底。`App Paths` 查询同时覆盖 HKCU/HKLM 与 Registry64/Registry32；`Uninstall` 查询同时覆盖 HKCU 与 HKLM。

---

## 3. 为什么硬编码不够用

以下是本机实证。

在本机依次检查 VS Code 的三个"常见默认路径"，结果：

| 路径 | 是否存在 |
|---|---|
| `C:\Users\XinTai\AppData\Local\Programs\Microsoft VS Code\Code.exe` | ❌ False |
| `C:\Program Files\Microsoft VS Code\Code.exe` | ❌ False |
| `C:\Program Files (x86)\Microsoft VS Code\Code.exe` | ❌ False |

而实际安装位置是：

| 探测源 | 结果 |
|---|---|
| `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Code.exe` | ✅ `D:\Soft\Code\VS Code\Code.exe` |
| `Uninstall` 项 `Microsoft Visual Studio Code (User)` | ✅ `InstallLocation = D:\Soft\Code\VS Code\` |
| `PATH` 中的 `code.cmd` | ✅ `D:\Soft\Code\VS Code\bin\code.cmd` |

**结论**：用户把程序装到非系统盘（D 盘）是极常见的做法。如果只实现需求字面上的"常见默认位置"，这台机器上编辑器功能直接失效。因此必须把注册表作为**主要**探测手段而非补充。

同理，Notepad++ / Sublime 的三个候选路径在本机均不存在，说明"常见路径"只能覆盖一部分机器。

---

## 4. 四级回退链

本节的解析逻辑**只在"编辑"分支被调用**，入口有两个，行为完全一致：

| 入口 | 说明 |
|---|---|
| 运行方式 = `Edit` | 双击后直接进入编辑分支，不弹窗 |
| 运行方式 = `Ask` 且用户在弹窗中选"编辑" | 与上一条走同一段代码，不重复实现 |
| 右键 `edit` 动词（`--edit`） | 显式意图，跳过 `Ask`，直接进入 |

```
1. 用户显式配置（AppSettings.CustomEditor）
      ↓ 未配置或路径失效
2. 内置候选 + 常见硬编码路径
      ↓ 全部未命中
3. 注册表探测（App Paths → Uninstall InstallLocation）
      ↓ 全部未命中
4. PATH 中查找已知 exe 名（code.exe / notepad++.exe / sublime_text.exe）
      ↓ 全部未命中
5. 兜底：%SystemRoot%\System32\notepad.exe（本机实测存在）
      ↓ 极端情况仍不存在
6. 弹窗要求用户手工指定（OpenFileDialog）—— **未实现**（见 [04 D20](04-progress.md#3-差距清单需求-vs-现状)）
```

> 第 1–5 级已落地：第 5 级记事本不存在时 `Resolve()` 返回 `null`，分发链路给出可读错误提示并记日志；用户需到主界面手工指定编辑器路径。这与原始需求"文件不存在时提示用户选择"仍有差距，D20 登记为待补项。

**保存策略（当前实现）**：`Resolve()` 返回的 `EditorDefinition` 带 `Source`（`Settings` / `KnownPath` / `AppPaths` / `Uninstall` / `PATH` / `Fallback`）；主界面的"一键自动探测"会把探测结果写回 `config.cfg` 的 `CustomEditorPath`，下次启动优先复用。分发过程本身不自动改写设置，但每次使用配置路径前都会做 `File.Exists` 校验——程序可能已被卸载。

**界面呈现**：主界面显示"当前编辑器：VS Code（来自注册表 App Paths）"，让用户知道为什么选了这个，也方便他改。

---

## 5. 参数模板

| 编辑器 | 参数模板 | 说明 |
|---|---|---|
| VS Code | `"%1"` | 直接给文件路径即可，无需 `-g` |
| Notepad++ | `-n%2` 或 `"%1"` | 需要行号时用 `-n<line>`，本项目只给路径 |
| Sublime Text | `"%1"` | — |
| Notepad | `"%1"` | — |
| 自定义 | **设计**：用户填写模板并校验 `%1`；**当前实现**：`AppSettings` 只保存 `CustomEditorPath`（exe 路径），参数模板固定 `%1`（见 [04 D20](04-progress.md#3-差距清单需求-vs-现状)） | 手选编辑器优先级最高，路径按 `%1` 追加 |

**实现注意**

- 模板展开后不能直接拼成一个大字符串交给 shell 解析，否则含空格的路径会断。推荐先拆成参数列表再传 `ProcessStartInfo.ArgumentList`（net11）或按模板解析为 `Arguments`（net48 手动转义）。
- `%1` 一律替换为**加引号的绝对路径**。
- 设计上"用户模板不含 `%1` 时追加路径"尚未实现；当前模板固定 `%1`，由 `EditorDefinition.BuildArguments(scriptPath)` 统一生成参数。

---

## 6. 关键实现代码

> 以下片段为设计示意；正式代码在 `Ps1Run.Core\Editors\RegistryAppPathReader.cs` 与 `EditorLocator.cs`。与片段相比，正式实现额外遍历 HKCU/HKLM 两个配置单元及 Registry64/Registry32 两种视图，以覆盖用户级安装与 32 位程序。

### 6.1 读取 App Paths

```csharp
internal static string? ResolveFromAppPaths(string exeName)
{
    var subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName;

    foreach (var root in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(root, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            if (key?.GetValue(null) is string path && path.Length > 0)
            {
                path = path.Trim('"');
                if (File.Exists(path)) return path;
            }
        }
        catch (System.Security.SecurityException) { /* 跳过无权限的分支 */ }
        catch (UnauthorizedAccessException) { }
    }
    return null;
}
```

### 6.2 读取 Uninstall 的 InstallLocation

```csharp
internal static string? ResolveFromUninstall(string displayNameKeyword, string relativeExe)
{
    string[] roots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    foreach (var root in roots)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
        using var uninstall = baseKey.OpenSubKey(root);
        if (uninstall == null) continue;

        foreach (var name in uninstall.GetSubKeyNames())
        {
            using var item = uninstall.OpenSubKey(name);
            if (item?.GetValue("DisplayName") is not string dn) continue;
            if (dn.IndexOf(displayNameKeyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (item.GetValue("InstallLocation") is not string loc || loc.Length == 0) continue;

            var candidate = Path.Combine(loc.Trim('"'), relativeExe);
            if (File.Exists(candidate)) return candidate;
        }
    }
    return null;
}
```

> 注意 VS Code 的**用户级安装**只在 `HKCU\...\Uninstall` 下有记录，因此 `Uninstall` 探测必须同时遍历 HKCU 与 HKLM 两个配置单元，不能只查 HKLM。**正式代码已按此实现**（`ResolveInstallLocation()` 先查 HKCU 再查 HKLM）。

**本机实测**：`HKCU` 下 `Microsoft Visual Studio Code (User)` 的 `InstallLocation = D:\Soft\Code\VS Code\`，按 `Code.exe` 拼接后正好命中。

---

## 7. 与需求的对应与差距

| 需求原文 | 覆盖情况 |
|---|---|
| R2 编辑分支的来源（`Edit` / `Ask→编辑` / `--edit`） | 三个入口共用本节逻辑，见 §4 开头 |
| "尝试常见编辑器的默认安装位置" | 已覆盖（第 2 节的硬编码候选），但**实测不足**；注册表探测已补上 |
| "文件不存在时提示用户选择" | 🟨 部分覆盖：第 5 级记事本兜底已实现；第 6 级自动弹窗未实现（D20），目前需在主界面手工指定 |
| （需求未提及）注册表探测 | **超出原始需求但必要**，否则本机不可用；已落地为代码并单测 |

---

← [07 PowerShell 版本探测与编码判型](07-tech-powershell-encoding.md) ｜ [首页](index.md) ｜ 下一篇：[09 评估：.NET 4.8 + .NET 11 双版本 →](09-eval-dual-target-framework.md)
