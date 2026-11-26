# 12 附录：注册表键位地图

> 采集时间：2026-11-24，采集机器：Windows 11 10.0.26300。
> 用途：为 [06 文件关联](06-tech-file-association.md)、[07 版本探测](07-tech-powershell-encoding.md)、[08 编辑器探测](08-tech-editor-discovery.md) 提供"这台机器上到底是什么值"的事实底稿。
> 状态横幅：§1–§3 是历史实测快照；**§4 是设计稿**——当前代码实际只写 ProgId 子树（元数据 / DefaultIcon / 三个动词）与 `OpenWithProgids\Ps1Run.ps1`，不写 `.ps1\(默认)`，也不生成备份文件，详见 §4 标注与 [04 D16/D17](04-progress.md#3-差距清单需求-vs-现状)。功能状态真源为 [02](02-feature-matrix.md)。

---

## 1. `.ps1` 关联相关（本机实测快照）

| 键 / 值 | 类型 | 实测值 | 含义 |
|---|---|---|---|
| `HKEY_CLASSES_ROOT\.ps1` `(默认)` | REG_SZ | `ps1_auto_file` | Explorer 自动生成的兜底 ProgId，说明当前**没有**正规关联 |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ps1\UserChoice\ProgId` | REG_SZ | `VSCode.ps1` | 用户当前真实默认程序（优先级最高） |
| `HKCU\...\FileExts\.ps1\UserChoice\Hash` | REG_SZ | `aBi9Z4VDA20=` | 系统校验签名，**不可伪造** |
| `HKCU\Software\Classes\.ps1\OpenWithProgids\VSCode.ps1` | REG_NONE | （空） | VS Code 注册的"打开方式"候选 |
| `HKCU\Software\Classes\.ps1\OpenWithProgids\AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g` | REG_NONE | （空） | 某个 UWP/Store 应用的候选 |
| `HKCU\Software\Classes\.ps1` 可写性 | — | **可写**（无需管理员） | 用户级注册可行 |
| `HKLM\Software\Classes` 可写性 | — | **不可写**，抛 `Requested registry access is not allowed.` | 机器级注册必须 UAC 提升 |

**结论**：本机 `.ps1` 的默认程序由 `UserChoice` 决定（VS Code），任何 ProgId 注册都会被它覆盖。这直接决定了 [06 §3](06-tech-file-association.md#3-userchoice-这道墙) 的方案必须包含"引导用户确认"。

---

## 2. PowerShell 安装位置相关

| 键 / 值 | 实测值 | 用途 |
|---|---|---|
| `HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\{GUID}\SemanticVersion` | `7.6.10` | 判断 PS7 版本 |
| `HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\{GUID}\InstallLocation` | `C:\Program Files\PowerShell\7\` | **PS7 首选探测锚点** |
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\pwsh.exe` `(默认)` | `C:\Program Files\PowerShell\7\pwsh.exe` | PS7 次选锚点 |
| `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` | 存在 | **PS5 首选路径** |
| `HKLM\SOFTWARE\Classes\Microsoft.PowerShellScript.1\Shell\PowerShell7x64\command` | `"C:\Program Files\PowerShell\7\pwsh.exe" -Command "$host.UI.RawUI.WindowTitle = 'PowerShell 7 (x64)'; & '%1'"` | 系统自带的 PS7 关联命令（本项目**不复用**该 ProgId，仅作参考） |

**说明**：`Microsoft.PowerShellScript.1\Shell` 下本机只有 `PowerShell7x64` 一个子键，没有 `open` 动词、没有默认动词。这就是 [06 §2.2](06-tech-file-association.md#22-设计取舍) 选择"新建 ProgId 而不是复用系统 ProgId"的实测理由。

---

## 3. 编辑器相关

| 键 / 值 | 实测值 | 用途 |
|---|---|---|
| `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Code.exe` `(默认)` | `D:\Soft\Code\VS Code\Code.exe` | **本机唯一能命中 VS Code 的探测源** |
| `Uninstall` 项 `Microsoft Visual Studio Code (User)` → `InstallLocation` | `D:\Soft\Code\VS Code\` | VS Code 的次级锚点（注意在 **HKCU** 下） |
| `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe` | ❌ 不存在 | 硬编码路径在本机失效 |
| `%ProgramFiles%\Microsoft VS Code\Code.exe` | ❌ 不存在 | 同上 |
| `%ProgramFiles(x86)%\Microsoft VS Code\Code.exe` | ❌ 不存在 | 同上 |
| `%ProgramFiles%\Notepad++\notepad++.exe` | ❌ 不存在 | 未安装 |
| `%ProgramFiles%\Sublime Text\sublime_text.exe` | ❌ 不存在 | 未安装 |
| `%SystemRoot%\System32\notepad.exe` | ✅ 存在 | 兜底编辑器 |

---

## 4. 本程序将写入/删除的键（设计 vs 已实现）

### 4.1 注册时写入

```
HKCU\Software\Classes\Ps1Run.ps1
├─ (默认)                = "PowerShell 脚本"
├─ FriendlyTypeName      = "PowerShell 脚本"
├─ DefaultIcon\(默认)     = "<安装目录>\Ps1Run.exe,0"
└─ shell
   ├─ (默认)             = "open"
   ├─ open\command\(默认) = "\"<exe>\" --open \"%1\""
   ├─ run\command\(默认)  = "\"<exe>\" --run \"%1\""
   └─ edit\command\(默认) = "\"<exe>\" --edit \"%1\""

HKCU\Software\Classes\.ps1
├─ (默认)                        = "Ps1Run.ps1"        ← 设计项，当前代码不写（D16）
└─ OpenWithProgids\Ps1Run.ps1    = (REG_NONE，空数据)  ← 已实现
```

### 4.2 注销时删除

| 目标 | 操作 | 保护措施 |
|---|---|---|
| `HKCU\Software\Classes\Ps1Run.ps1` | `DeleteSubKeyTree` | 只删自有 ProgId |
| `HKCU\Software\Classes\.ps1\OpenWithProgids\Ps1Run.ps1` | `DeleteValue(name, false)` | 保留 `VSCode.ps1` 等第三方值 |
| `HKCU\Software\Classes\.ps1` `(默认)` | **未实现**（设计项）：本应仅当值等于 `Ps1Run.ps1` 时删除或还原备份值；当前代码不写也不读该值（D16） | 防止误清他人设置 |
| `UserChoice` | **不动** | 见 [06 §5](06-tech-file-association.md#5-注销策略) |

### 4.3 备份文件（设计，未实现）

设计位置 `%APPDATA%\Ps1Run\association-backup.lang（预留）`，拟记录注册前的以下内容；**当前代码不生成该文件**（见 [04 D16](04-progress.md#3-差距清单需求-vs-现状)）：

```json
{
  "capturedAt": "2026-11-24T10:00:00+08:00",
  "extensionDefault": "ps1_auto_file",
  "openWithProgids": ["VSCode.ps1", "AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g"],
  "userChoice": { "progId": "VSCode.ps1", "hash": "aBi9Z4VDA20=" }
}
```

---

## 5. 常用查询命令

```powershell
# 扩展名当前指向
reg query "HKCU\Software\Classes\.ps1" /ve
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ps1\UserChoice'

# 本程序 ProgId 全树
reg query "HKCU\Software\Classes\Ps1Run.ps1" /s

# 打开方式候选
reg query "HKCU\Software\Classes\.ps1\OpenWithProgids"

# PowerShell 7 安装信息
reg query "HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions" /s

# VS Code 路径
reg query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Code.exe"

# 执行策略（仅查看，本程序不修改）
Get-ExecutionPolicy -List
```

---

## 6. 术语速查

| 术语 | 一句话解释 |
|---|---|
| **ProgId** | 程序的注册标识，如 `Ps1Run.ps1`；承载 verbs 与图标 |
| **verb（动词）** | ProgId 下的动作定义，如 `open` / `edit` / `run` |
| **UserChoice** | Win8+ 用户显式选择的默认程序，带不可伪造 Hash，优先级最高 |
| **OpenWithProgids** | "打开方式"列表的候选清单，只列不设默认 |
| **HKCR** | `HKLM\Software\Classes` + `HKCU\Software\Classes` 的合并只读视图 |
| **SHOpenWithDialog** | shell32 提供的"打开方式"系统对话框，可由 Explorer 生成合法 UserChoice |
| **SHChangeNotify** | 通知 shell 关联已变更，用于刷新图标与缓存 |
| **IsExternalInit** | `record`/`init` 依赖的编译器标记类型，net48 缺失需垫片 |
| **CodePagesEncodingProvider** | .NET Core/5+ 上提供 GBK(936) 等传统代码页的提供程序 |
| **App Paths** | 注册表里登记 exe 绝对路径的位置，按 exe 名查询 |

---

## 7. 相关 API 速查

| API | 所在 | 用途 | 本项目用例 |
|---|---|---|---|
| `SHOpenWithDialog` | shell32.dll | 弹"打开方式" | 引导用户确认默认程序（F1.7） |
| `SHChangeNotify` | shell32.dll | 刷新 shell 缓存 | 注册/注销后刷新（F1.1–F1.8） |
| `GetCursorPos` | user32.dll | 取鼠标位置 | Ask 弹窗多屏定位（F2.4） |
| `MonitorFromPoint` | user32.dll | 取所在显示器 | 同上 |
| `Microsoft.Win32.Registry` | BCL | 注册表读写 | 全部关联操作 |
| `Process.Start` | BCL | 启动目标进程 | 执行/编辑分发 |
| `Encoding.RegisterProvider` | BCL | 注册代码页 | net11 读 GBK |
| `DiagnosticsLog` | 本项目 | 分级日志、会话 ID、保留清理 | 启动/分发/关联/异常的日志写入与最近日志读取（F6.1） |

---

## 8. 采集方法说明

本页数据由以下语句采集（可复现）：

```powershell
[Microsoft.Win32.Registry]::GetValue('HKEY_CLASSES_ROOT\.ps1','',$null)
[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ps1\UserChoice')
[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\.ps1\OpenWithProgids')
[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\Microsoft\PowerShellCore\InstalledVersions')
[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Code.exe')
```

> 换机器部署时，`UserChoice`、编辑器路径、PS7 版本都会不同，**不要**把本页的值硬编码进代码，只作为探测链的设计依据。

---

← [11 开发与构建指南](11-build-and-dev-guide.md) ｜ [首页](index.md) ｜ 回到 [01 需求与验收口径](01-requirements.md)
