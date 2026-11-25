# 变更说明 / Changelog

## 0.2.0 — 2026-11-25

相对 0.1.0 的正式更新。发布包提供 `net48` 与 `net11.0-windows` 两个目标，均为绿色免安装形态。

### 主界面

- 编辑器合并进「执行程序与编辑器」分组：PS5.1 / PS7+ / 编辑器三行对齐，使用分隔线区分。
- 「外观与语言」从「常规」移到「日志与外观」页签；原「诊断与日志」页签更名为「日志与外观」，「设置与配置文件」更名为「配置文件与版本」。
- 「一键识别」改为只补空缺：已配置且文件存在的 PS5 / PS7 / 编辑器保持原值并标注「保留原设置，未修改」，只有空值或失效路径才回填探测结果。
- 删除「诊断信息」分组及「刷新状态 / 复制诊断信息」按钮；操作反馈收敛到窗口底部单行状态条，日志内容仍可通过「复制最近日志」获取。
- 「配置文件与版本」只显示一份配置文件路径，并显示程序版本与语言包版本。

### 配置与版本

- `settings.json` 与 `config.json` 合并为软件根目录唯一的 `config.json`，含 `Version`、运行设置与程序配置；两个 Store 只读写各自负责的键，互不覆盖，未知键原样保留。
- 旧 `%APPDATA%\Ps1Run\settings.json` 在首次读取时一次性合并；合并结果成功写入 `config.json` 后自动删除原文件，写入失败则保留并在下次启动重试。
- 缺少 `Version` 的旧 `config.json` 读取时补写版本号并合并旧设置；损坏文件备份为 `config.json.bad.*` 后重建。
- 程序版本统一调整为 `0.2.0`，界面与程序集均从此版本读取。
- 语言包新增保留键 `__version`（当前为 1），加载时剔除、不作为文案；`lang\zh-cn.json` 随程序发布，缺失、无版本号或版本过旧时按内置文案重建。其他语言包只读取、不覆盖，处理逻辑不变。
- 语言包缺版本号时：「日志与外观」页签挂「！」徽标、页内显示红字提醒与「重新生成 zh-cn.json」按钮，选择窗同步显示红字。

### 选择窗

- 窗口构造时先按主屏工作区居中，`SourceInitialized` 再按鼠标所在显示器精调，首屏不再从左上角闪现。
- 高度改为随提醒内容自适应，多条红字提醒时底部按钮仍完整可见。
- `PickRequest` 携带当前设置：执行程序 / 编辑器的配置路径不存在，或完全探测不到编辑器时，窗内显示红字提示；正常自动探测时保持安静。

### 验证

- `dotnet build Ps1Run.slnx -c Release` 双目标 0 警告 0 错误；单元测试 7/7 通过。
- 离屏渲染核对浅色 / 深色、中文 / 英文三页签与选择窗布局，确认徽标、红字与按钮无遮挡截断。

### 0.1.0

初始版本：脚本关联、运行 / 编辑 / 询问三态分发、PS5 / PS7 探测、编辑器回退链、日志与基础设置界面。

---

## English summary

0.2.0 merges the editor into the executor group, moves appearance & language into the renamed Logs & appearance tab, stops one-click detection from overwriting valid paths, and removes the duplicated diagnostics panel. Settings and program configuration now live in one root `config.json`; the legacy `settings.json` is merged and deleted after a successful write. Language packs carry `__version`, with a badge and red notices when it is missing. The picker opens centred without flashing and warns about missing executor/editor configuration. Two framework packages are shipped: `net48` and `net11.0-windows`.
