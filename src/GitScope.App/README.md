# GitScope 桌面入口

正式.NET 10 WinForms界面，基于`GitScope.Core`公共接口。设计token的design.json仅在本机保留；过程见[WORKLOG.md](WORKLOG.md)。本说明按用户要求暂时纳入仓库，根README仍为面向使用者的入口。

本机直接双击项目根[Start-GitScope.cmd](../../Start-GitScope.cmd)。2026-11-20交互修订后41项控件自检退出0，真实分页/右下导出通过，见[本次报告](../../doc/evidence/ui-interaction-20261120.md)。原保留更新/新进程恢复证据见[检查点报告](../../doc/evidence/implementation-checkpoint.md)。手动fetch与完整第一版验收仍待后续。

从项目根构建和启动（便携SDK由项目脚本准备）：

```powershell
.\.tools\dotnet\dotnet.exe build .\src\GitScope.App\GitScope.App.csproj -c Release
.\.tools\dotnet\dotnet.exe .\src\GitScope.App\bin\Release\net10.0-windows\GitScope.dll
```

正常设置和目标登记在`%LOCALAPPDATA%\GitScope`，不写入导出目录。可使用`--state <绝对目录>`隔离整个状态区，`--repo <仓库路径>`指定初始仓库；两者都不自动导出代码。未知版本或损坏的settings.json保留原文件，界面提示暂停保存。

操作顺序：选择仓库／读取引用 → 选择引用及起止日期与IANA时区 → 查询历史 → 选定提交并预览文件（双击或回车加载文本diff） → 绑定空或新输出目录 → 勾选/添加精确相对保留路径 → 检查更新计划 → 核对保留清单和移除说明 → 导出到此目录。打开目录只在已有成功导出记录时可用。

日期允许留空，起止日均包含；每页50条，分页不会改变已选提交。浅仓库标注仅本地已知历史。所选提交与上次导出分别显示，后者不表示目录没有用户修改。环境/依赖只保留用户选择，声明及锁文件来自选定提交，不执行依赖安装。

点击日期旁“日历”选择日期，可跳转月份、选今天或清空；也支持手工输入和Alt+↓。日期只筛选提交列表，导出仍是所选版本完整代码。分页提供首页/末页，输入页码按Enter或“跳转”直达。输出操作位于右下：先检查计划，再点最右侧导出；未就绪按钮显示可读的禁用态。

未决事务在启动时显示恢复横幅；需要恢复的目标阻止新计划，已提交后纯清理待办允许后续更新。关闭窗口遇到活动导出时先请求取消并等待后端安全收尾，UI按后端实际终态显示结果。

## 隔离控件自检

测试源码只在开发机保留，不随仓库克隆。正式程序已移除--verify入口；本机可使用分离的UI检查工程：

```powershell
.\tests\run-checks.ps1 -Ui
```

此命令构建tests/GitScope.UiChecks，使用真实App源码和独立测试入口，注入后端替身；每次生成独立设置和证据目录，包含results.json及深浅/最小/空/失败/恢复/日历等控件图。当前41项检查通过；命令退出0且结果PASS才说明该轮替身检查通过，不能代替真实桌面、事务或系统主题通知测试。后端检查入口为本地tests/run-checks.ps1 -Backend。

已知界面边界：原生文件夹对话框、ComboBox箭头、复选框和文本/文件列表滚动条仍部分采用系统外观；列表/输入/正文/选择有应用浅深token。真实OS主题通知、完整显示缩放/键盘矩阵和本机轻量预算由集成验收记录。手动fetch和完整最近使用属于TASK-07。
