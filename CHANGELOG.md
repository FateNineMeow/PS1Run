# 变更说明 / Changelog

## Unreleased — 脚本规则与纯文本配置（破坏性更新）

> 这是一次**不兼容旧格式**的更新：程序不再读取、解析、迁移或删除旧 `config.json`、`%APPDATA%\Ps1Run\settings.json`、`lang\*.json`。请在升级前按下面的格式手工准备新文件；残留旧文件只会被忽略（语言包旧文件会写一条 WARN 日志），不影响启动。

### 脚本规则（仅此脚本）

- 选择窗新增「以后都这样处理（仅此脚本）」，与原有「以后都这样处理（不再询问）」互斥；取消选择不写任何规则。
- 规则写入软件根目录的 `rule.txt`，格式为 5 列纯文本：
  ```text
  文件路径|动作|文件大小|修改时间|文件HASH
  F:\1.0重装系统脚本.ps1|Run|12345|2026-11-26T03:00:00.0000000Z|0
  ```
  - 文件路径**始终参与匹配**且不可取消（不区分大小写比较）。
  - 动作取 `Run` / `Edit`；关闭的判断标准在规则中写 `0`。
  - 修改时间为 UTC ISO 8601（`O` 格式），HASH 为小写 SHA-256。
- 匹配顺序：路径 → 大小 → 修改时间 → HASH，短路判断；HASH 仅在前面全部通过且被启用时才计算。
- 分发顺序：`--run` / `--edit` 显式意图优先；`--ask` 强制弹窗、**不跳过规则**；全局 `Run / Edit` 按设置执行；仅全局 `Ask` 模式下先查规则，命中直接分发并记录日志。
- 写盘：UTF-8 无 BOM、临时文件 + 原子替换、命名互斥体防止多个双击进程同时写坏；坏行跳过并写 WARN，规则文件异常时失败开放（照常弹窗）。

### 脚本规则编辑页

- 主窗体在「常规」与「文件关联」之间新增「脚本规则」页签。
- 页签顶部为全局判断标准：`文件大小`（默认开）、`修改时间`（默认开）、`文件 HASH`（默认关，附「可能减缓脚本启动速度」备注）；文件路径显示为必选说明。改动标准会立即保存并按新标准重写所有规则指纹。
- 页面主体为规则列表（路径 / 动作 / 大小 / 修改时间 / HASH），支持路径筛选、刷新、新增、编辑、删除、保存；双击行直接编辑，删除需二次确认，刷新时若存在未保存修改会提示确认。
- 新增 / 编辑使用独立弹窗，既可点「选择文件」浏览 `.ps1`，也可直接输入完整路径；保存时校验扩展名必须为 `.ps1` 且文件存在，否则拒绝保存；路径已存在时提示覆盖。

### 配置与语言包改为纯文本

- `config.json` → `config.lang`，语言包 `lang\*.json` → `lang\*.lang`，两者共用 `Key: "value",` 文本格式（首行 `Version: 2,`），解析器兼容行尾逗号有/无，支持常见转义。
- 删除原 `SimpleJson`；项目不再包含任何自定义 JSON 解析代码或依赖。`.NET` 运行时自动生成的 `*.runtimeconfig.json` / `*.deps.json` 不受影响。
- 配置 schema 与语言包版本均升到 `2`；旧格式文件不再迁移。`config.lang` 新增 `RuleMatchSize=1`、`RuleMatchModifiedTime=1`、`RuleMatchHash=0`。
- `rule.txt` 跟随 `config.lang` 的 `%APPDATA%` 回退规则，四者始终位于同一可写根目录。
- 配置文件损坏仍备份为 `config.lang.bad.*` 后重建；语言包缺版本 / 低版本的徽标、红字与重建按钮保持原逻辑，按钮文案改为「重新生成 zh-cn.lang」。

### 验证

- `net48` 与 `net11.0-windows` 双目标构建：0 警告 0 错误。
- 单元测试由 7 组增加到 8 组（新增「脚本规则」），全部通过；覆盖 5 列读写、增删改、路径大小写、大小 / 时间 / HASH 短路、路径与文件存在校验、`.lang` 解析与转义、旧 `.json` 被忽略。

## 0.2.0 — 2026-11-25

0.2.0 是一次围绕「配置收敛、界面去重、语言包版本治理、选择窗体验」的正式更新。发布包提供 `net48` 与 `net11.0-windows` 两个目标，均为绿色免安装形态，解压后直接运行 `Ps1Run.exe`。

### 下载

| 目标框架 | 文件名 | 适用说明 | SHA-256 |
| --- | --- | --- | --- |
| .NET Framework 4.8 | `Ps1Run-0.2.0-net48.zip` | 适用于已具备 .NET Framework 4.8 的 Windows 环境 | `cc7201269dba509e826d9452ad41a3f0080ce2fdfd01c11bca113c5f3579eb73` |
| .NET 11 for Windows | `Ps1Run-0.2.0-net11.0-windows.zip` | 适用于已安装 .NET 11 Windows Desktop 运行时的环境 | `36b6c3d799c2a59e13486a8d26414289e2946116dc37c49a21e6f2692640e3f8` |

两个包均包含程序本体、依赖、`lang\zh-cn.json`、`lang\en-us.json`、`LICENSE`、`README.md`、`README-en.md` 与 `CHANGELOG.md`，不包含调试符号文件。

### 升级与兼容说明

- 配置文件统一为软件根目录下的唯一 `config.json`。首次启动时，程序会读取旧的 `%APPDATA%\Ps1Run\settings.json` 并合并；只有合并结果成功写入根目录 `config.json` 后，旧文件才会被删除。若写入失败，旧文件保留，下次启动继续重试。
- 旧 `config.json` 若缺少版本号，读取时会自动补写 `Version`，不影响原有设置。
- `lang\zh-cn.json` 随包发布并带有 `__version`。如果本地中文语言包缺失、没有版本号或版本过旧，程序会用内置最新文案重建该文件；英文等其他语言包仍只读取、不覆盖。
- 一键识别不再覆盖已经配置且文件仍然存在的 PS5 / PS7 / 编辑器路径；只有空值或失效路径才会回填探测结果。

### 主界面

- 编辑器合并进「执行程序与编辑器」分组：PS5.1 / PS7+ / 编辑器三行对齐，使用分隔线区分。
- 「外观与语言」从「常规」移动到「日志与外观」页签。
- 原「诊断与日志」页签更名为「日志与外观」；「设置与配置文件」更名为「配置文件与版本」。
- 「一键识别」改为只补空缺：已配置且文件存在的 PS5 / PS7 / 编辑器保持原值，并显示「保留原设置，未修改」；只有空值或失效路径才使用探测结果回填。
- 删除「诊断信息」分组及「刷新状态 / 复制诊断信息」按钮；操作反馈收敛到窗口底部单行状态条。日志内容仍可通过「复制最近日志」获取。
- 「配置文件与版本」只显示一份配置文件路径，并显示程序版本与语言包版本。

### 配置与版本

- `settings.json` 与 `config.json` 合并为软件根目录唯一的 `config.json`，包含 `Version`、运行设置与程序配置。
- 两个 Store 只读写各自负责的键，互不覆盖，未知键原样保留。
- 旧 `%APPDATA%\Ps1Run\settings.json` 在首次读取时一次性合并；成功写入 `config.json` 后自动删除原文件，写入失败则保留并在下次启动重试。
- 缺少 `Version` 的旧 `config.json` 会在读取时补写版本号并合并旧设置；损坏文件备份为 `config.json.bad.*` 后重建。
- 程序版本统一调整为 `0.2.0`，界面与程序集版本均从此版本读取。
- 语言包新增保留键 `__version`（当前为 1），加载时剔除，不作为文案显示。
- `lang\zh-cn.json` 缺失、无版本号或版本过旧时，按内置文案重建；其他语言包处理逻辑不变。
- 语言包缺版本号时：「日志与外观」页签显示「！」徽标，页内显示红字提醒与「重新生成 zh-cn.json」按钮，选择窗同步显示红字提示。

### 选择窗

- 窗口构造时先按主屏工作区居中，`SourceInitialized` 后按鼠标所在显示器精调，解决了从左上角闪现到中间的问题。
- 高度改为随提醒内容自适应，多条红字提醒时底部按钮仍完整可见。
- `PickRequest` 携带当前设置：执行程序 / 编辑器的配置路径不存在，或完全探测不到编辑器时，窗内显示红字提示；正常自动探测时保持安静。

### 验证

- `dotnet build Ps1Run.slnx -c Release` 双目标 0 警告 0 错误。
- 单元测试 7/7 通过。
- 离屏渲染核对浅色 / 深色、中文 / 英文三页签与选择窗布局，确认徽标、红字与按钮无遮挡截断。

## 0.1.0

初始版本：脚本关联、运行 / 编辑 / 询问三态分发、PS5 / PS7 探测、编辑器回退链、日志与基础设置界面。

---

## English summary

0.2.0 merges the editor into the executor group, moves appearance & language into the renamed Logs & appearance tab, stops one-click detection from overwriting valid paths, and removes the duplicated diagnostics panel. Settings and program configuration now live in one root `config.json`; the legacy `settings.json` is merged and deleted after a successful write. Language packs carry `__version`, with a badge and red notices when it is missing. The picker opens centred without flashing and warns about missing executor/editor configuration. Two framework packages are shipped: `net48` and `net11.0-windows`.
