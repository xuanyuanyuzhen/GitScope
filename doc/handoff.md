# GitScope 接续交接

整理日期：2026-11-17；基线PLAN-2026-11-17-C、正式契约v1。本页是恢复索引，任务状态只在[tasks](tasks.md)，过程在[WORKLOG](../WORKLOG.md)，DEC/P只在[proposal](proposal.md)。

## 当前恢复点

**2026-11-20最新仓库规则：** 用户要求工作文档暂时一并提交。根WORKLOG、doc下Markdown、模块工作记录和说明现临时纳管；测试、design.json、原始JSON、历史原型与AI工具配置仍只在本机。README始终作为产品说明保留。之后用户要求清理时再移除这批临时文档；下方先前“所有文档不得提交”等记录仅代表当时规则。

正式产品入口不包含自检代码。构建使用`scripts/build.ps1 -Publish`；本地完整测试使用`tests/run-checks.ps1 -Backend`或`-Ui`，UI验证源已移至tests/GitScope.UiChecks。测试不在仓库，其他机器新克隆不可直接执行这些本地测试入口。原始证据/截图/部分设计链接同样指向本机资料，不能将远程缺附件理解为本轮重新验证。

2026-11-20已完成用户截图反馈TASK-UI-02：夜间禁用文字、右下导出、首末/指定页、主题日历与日期说明已发布；最终41项控件检查和真实分页/导出验证见[新报告](evidence/ui-interaction-20261120.md)。启动入口不变，原检查点和Core契约保留。

正式.NET10 + WinForms已完成TASK-03至06首个贯通检查点。原会话01a1e7c1-3599-7100-badf-4b764d1fe1a6因服务限流中断，本会话完成B保留更新、真实进程中断、新UI进程恢复及发布版本核对。不要从浏览原型或技术栈选择重新开始。

工作区`E:\AI_Code\GitScope`，2026-11-20按用户授权建立Git/main，origin为`https://github.com/xuanyuanyuzhen/GitScope.git`；上传结果见WORKLOG最新记录。先读WORKLOG当前状态及末尾 → 本页/tasks → background/proposal → contracts与当前任务相关design/test-plan → 正式证据与源码。下一阶段执行入口见[prompt](prompt.md)。

## 产物与证据

| 入口 | 内容与边界 |
| --- | --- |
| [Start-GitScope.cmd](../Start-GitScope.cmd) | 推荐正式入口；使用项目内.NET运行时；程序在[artifacts/app](../artifacts/app) |
| [桌面说明](../src/GitScope.App/README.md) | 使用、配置、隔离state、自检与已知界面限制 |
| [正式报告](evidence/implementation-checkpoint.md) | 251项后端、27项控件、真实桌面及独立内容恢复证据，包含未测范围 |
| [整合结果](../artifacts/verification/20261117-132931-ec9553/results.json) | 5项runner+39项query+207项事务，退出0；43点真实退出/两次新进程恢复 |
| [最终UI结果](../artifacts/desktop-checkpoint/continuation/ui-verification/results.json) | 发布程序27项替身服务检查，退出0，7图已复核；不替代真实事务 |
| [B验证](../artifacts/desktop-checkpoint/b-verification.json)、[恢复验证](../artifacts/desktop-checkpoint/recovery-verification.json) | 9个blob、保留项、旧内容移除、整个旧目录文件指纹及源不变 |
| [版本核对](../artifacts/desktop-checkpoint/continuation/verified-build.json) | 发布Core SHA256与已测DLL一致 |
| [公共契约](contracts.md) | 日期/错误/schema/取消/恢复；代码类型在Core/Contracts.cs |

源码位于src/GitScope.Core/Query、src/GitScope.Core/Transactions、src/GitScope.App；测试在tests/GitScope.Tests，脚本在scripts。模块WORKLOG保留实施过程，全局状态以tasks为准。历史原型和窄实验保留原始日期与边界，原型200条上限不是正式程序限制。

## 下一阶段

1. TASK-07已READY：按REQ-001/002/009、API-Q、F-REMOTE补手动fetch、完整最近使用及错误整合。使用本地bare远程；仅更新允许的远程跟踪命名空间，禁止prune/tags自动跟随/子模块递归，不依赖危险默认refspec。保持当前选择OID和源工作区。
2. TASK-08：逐项核对AC-001至009；测正式启动/查询/导出进程组资源，补键盘/DPI/系统主题通知。P-06预算待测量与用户收敛，不能沿用旧隐藏窗口样本宣称轻量达标。
3. TASK-09/10仍是后续依赖命令能力；当前仅提示声明/锁文件变化，不运行包命令。

## 本地现场与复现

工具链在.tools/dotnet，SDK10.0.404、runtime10.0.15。从项目根运行：

```powershell
.\scripts\build.ps1 -Publish
.\Start-GitScope.cmd
```

发布前正常关闭该产物窗口。首次接手发布曾因旧夹具进程46620锁DLL失败，正常关闭后重试成功；失败日志保留。

- 正常配置：`%LOCALAPPDATA%\GitScope`；桌面夹具state：`artifacts/desktop-checkpoint/state`；跨state目标锁/归属：`%LOCALAPPDATA%\GitScope\target-coordination`。
- 中断现场已恢复，输出包含故障前B的用户修改和note；不要重做setup覆盖证据，复现用全新fixture根。
- 根启动脚本复核曾启动PID3612。PID只是历史线索，接手时核对实际路径/命令行，不按旧PID直接终止进程。
- `D:\jiutianWork\store-learning`及code/code-junk只读参考，未用于覆盖/清理/fetch实验。历史源变更计数不是当前事实。
- 项目已建立Git版本控制；接手时先检查git status与git log，不把历史“未初始化”当当前状态。历史子Agent记录不代表它们仍在运行。

## 边界

首个检查点不等于第一版验收。真实系统主题通知、完整DPI/键盘矩阵、真实填满磁盘/全部ACL/超长路径/断电持久性、大型依赖性能、任意并发外部写入及其他Windows设备未覆盖。stage创建后身份未落盘的极小间隙保守保留未知stage，见[事务限制](../src/GitScope.Core/Transactions/VALIDATION.md)。

沿用DEC-14及已有接续授权；普通隔离实现不重复审批。真实远程、外部写入或包安装不从产品需求推导授权。每个小部分更新WORKLOG及受影响文档；实现未变且没有新缺口时不重复整套测试。

末次交付核对：PID3612验证窗口已正常关闭；23份Markdown/168个本地链接/58个定义引用静态检查通过。当前无等待项，接续从TASK-07开始。

2026-11-20仓库范围更新：用户确认文档/AI资料连历史一起清除，main已重建为169f744，仅37个源码/测试/构建文件。本页、WORKLOG、所有Markdown、doc/validation/design.json只留本机且被忽略；后续不得强行加入提交。原两次提交仅在本地artifacts/repository-cleanup/before-cleanup.bundle备份，新克隆不含旧历史。

2026-11-20仓库范围再次收敛：用户确认只留产品源码、构建脚本与根README。tests/及原App/Verification.cs仅留本地，UI自检迁至tests/GitScope.UiChecks。本地验证入口为tests/run-checks.ps1 -Ui或-Backend；正式build.ps1仅支持构建/Publish，正式App不再提供--verify。旧文档中的-Test/--verify命令是历史入口，接手优先采用本节。
