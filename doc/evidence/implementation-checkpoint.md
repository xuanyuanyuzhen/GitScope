# 正式工程首个贯通检查点

日期：2026-11-17（Asia/Shanghai）；需求基线PLAN-2026-11-17-C；正式契约v1。TASK-03至06的本阶段交付已完成，第一版全部验收仍由TASK-07/08继续。过程见[WORKLOG](../../WORKLOG.md)，任务状态只在[tasks](../tasks.md)维护。

## 可运行产物与版本

- 推荐启动：[Start-GitScope.cmd](../../Start-GitScope.cmd)。正式程序位于[artifacts/app](../../artifacts/app)，源码位于[src](../../src)。
- 本机采用.NET SDK 10.0.404、运行时10.0.15、net10.0-windows及Git 2.42.0.windows.1。SDK放在项目.tools，不是全局安装；启动脚本设置项目内运行时路径。
- 本轮重新构建/发布退出0，0警告/0错误。发布Core DLL与251项整合测试使用的DLL SHA256一致：`6768F7D079CB1380F9DC569648728E832D1B9D13123DED2FCC72B7748D1BD4B6`。见[版本核对](../../artifacts/desktop-checkpoint/continuation/verified-build.json)。
- 初次重新发布遇到旧验证进程46620占用DLL，失败日志保留在[build-publish.log](../../artifacts/desktop-checkpoint/continuation/build-publish.log)；正常关闭该夹具窗口后[重试成功](../../artifacts/desktop-checkpoint/continuation/build-publish-retry.log)。未修改生产源码，完整故障矩阵未无故重跑。

## 已执行验证

| 范围 | 实际结果 | 原始证据与边界 |
| --- | --- | --- |
| runner | 5项通过 | 二进制stdout/stderr/退出码、无shell参数边界、真实进程取消、脱敏；见整合结果 |
| 正式查询 | 39项通过 | 完整212条历史、五页、自然日/DST边界、引用冻结、merge/replace/worktree/shallow、promisor无连接哨兵、预览及依赖声明变化 |
| 正式事务 | 207项通过 | 包含最后两项叶子reparse保护；43个真实进程退出点，每点两次新进程恢复；不是进程内异常模拟重启 |
| 整合运行 | **251项PASS，退出0** | [results.json](../../artifacts/verification/20261117-132931-ec9553/results.json)，13:29:31开始、13:34:43完成；退出码与输出由原会话实际命令复核 |
| 最终发布UI自检 | **27项通过，退出0** | [results.json](../../artifacts/desktop-checkpoint/continuation/ui-verification/results.json)、[退出记录](../../artifacts/desktop-checkpoint/continuation/ui-verification-exit.json)；真实WinForms控件、注入服务替身，独立于真实事务测试 |
| 桌面A→B | UI导出与内容核验通过 | [B验证](../../artifacts/desktop-checkpoint/b-verification.json)、[退出记录](../../artifacts/desktop-checkpoint/continuation/verify-b-exit.json) |
| 故障后UI恢复 | 新进程恢复与内容核验通过 | [故障退出83](../../artifacts/desktop-checkpoint/continuation/fault-exit.json)、[恢复验证](../../artifacts/desktop-checkpoint/recovery-verification.json)、[退出记录](../../artifacts/desktop-checkpoint/continuation/verify-recovery-exit.json) |
| 根启动脚本 | 可见窗口及再次重启通过 | `Start-GitScope.cmd --state <隔离state>`启动PID3612；仓库/引用/日期/时区/保留设置恢复，没有再次出现恢复横幅 |

事务完整清单见[transaction-results.json](../../artifacts/verification/20261117-132931-ec9553/transactions/transaction-results.json)，43点的PID/退出码/两次恢复输出见[process-recovery-results.json](../../artifacts/verification/20261117-132931-ec9553/transactions/process-recovery-results.json)。独立内容清单和中间失败现场均保留。

## 真实桌面检查点

使用专用[夹具](../../artifacts/desktop-checkpoint/fixture.json)，从原会话中断处接续；A=`34966d20502c56d8d018eb06ddb09d32342e6037`，B=`4f9fd22efb0c5b0a5fac88ea39d190f434665d08`。未操作旧业务目录或真实远程。

1. 原会话已从桌面选择main引用、2026-11-15至16日及Asia/Shanghai，导出A；独立命令退出0后添加A本地修改、未选缓存、模拟.env与node_modules。
2. 本轮更新到已测DLL并启动PID47544。观察到当前选择B、上次导出A、声明/锁文件变化提示，两项保留已勾选。真实点击检查计划，显示9个跟踪文件及2项保留，随后点击导出。
3. 界面成功后独立验证B的9个blob字节（含package/lock、中文、二进制及archive属性文件）；旧独有源码与未选缓存消失，.env及依赖指纹不变，源仓库完整文件指纹不变。验证退出0。
4. 正常关闭UI，在B输出加入独有用户修改和user-note，记录故障前文件指纹；使用同一Core DLL的测试worker尝试导出A，在`after-promote`执行`Environment.Exit(83)`。PID38896真实退出83，日志停于`PROMOTE_INTENT`，尚未COMMITTED。
5. 新正式UI进程38120启动，显示待恢复横幅并禁用新计划/导出。真实点击“恢复旧目录”后横幅消失、计划按钮恢复可用、上次导出仍B。
6. 独立命令验证目标全部文件路径和字节综合指纹等于故障前，包含用户修改、note和保留项；源仓库未变、无待恢复事务、登记LastCommit=B。验证退出0。再通过根启动脚本启动，正常恢复配置且无重复恢复任务。

观察文本见[desktop-observations.json](../../artifacts/desktop-checkpoint/continuation/desktop-observations.json)。真实窗口截图已在会话中查看；控件位图、UI观察、退出码和内容指纹各自证明不同层面，不能相互替代。故障注入由隔离worker完成，正式UI没有故障注入开关。

## 界面检查

最终发布产物生成的[浅色](../../artifacts/desktop-checkpoint/continuation/ui-verification/light.png)、[夜间](../../artifacts/desktop-checkpoint/continuation/ui-verification/dark.png)、[最小浅色](../../artifacts/desktop-checkpoint/continuation/ui-verification/small-light.png)、[最小夜间](../../artifacts/desktop-checkpoint/continuation/ui-verification/small-dark.png)、[空态](../../artifacts/desktop-checkpoint/continuation/ui-verification/empty.png)、[错误](../../artifacts/desktop-checkpoint/continuation/ui-verification/failed.png)、[恢复](../../artifacts/desktop-checkpoint/continuation/ui-verification/recovery.png)共7张控件图均已查看。输出/保留/操作区域完整，错误原因和状态可读。原生箭头、复选框、滚动条及禁用文字仍受系统绘制影响；这些位图不等于真实系统主题通知或DPI全矩阵通过。

## 复现入口

从项目根运行；重新发布前正常关闭该产物的窗口，以免DLL占用：

```powershell
.\scripts\build.ps1 -Test -Publish
.\.tools\dotnet\dotnet.exe .\artifacts\app\GitScope.dll --verify .\artifacts\ui-verification-new
.\Start-GitScope.cmd
```

测试入口会创建全新隔离目录。已存在的desktop-checkpoint是验证证据，不能直接重做setup或故障步骤覆盖它；需要复现实验时先用`--checkpoint-setup <全新目录>`生成夹具，具体流程由[Checkpoint.cs](../../tests/GitScope.Tests/Checkpoint.cs)定义。

## 剩余范围与限制

- TASK-07：手动fetch、完整最近使用/失效设置和错误整合。当前程序只操作本地对象，没有远程更新按钮；首版fetch需求仍保留。
- TASK-08：AC-001至009逐项完整验收、真实使用体验、键盘/DPI/系统主题通知、资源测量及P-06预算。当前没有“轻量达标”结论。
- 后端尚未证明断电持久性、真实填满磁盘、全部ACL/超长路径/外部并发写入、大型依赖目录性能和其他Windows/文件系统。首版保守限制本机固定NTFS及无链接祖先；替换前需停止输出内写进程。
- stage创建后身份尚未落盘的极小中断间隙会保留未知stage并返回RecoveryRequired，不按路径名猜测删除。完整边界见[事务报告](../../src/GitScope.Core/Transactions/VALIDATION.md)。
- TASK-09/10依赖命令属于后续能力；本阶段只提示声明变化，不安装/删除依赖。
