# TASK-05 正式后端验证交接

日期：2026-11-17。当前结果是正式Core后端的隔离测试证据，不是全部AC-005/006/007/009或整机资源验收。

## 实现入口

- `TargetService`实现`ITargetService`，命名空间`GitScope.Core`；构造`(string stateDirectory, GitProcessRunner runner, Action<string>? faultInjector = null)`。
- `RegisterAsync`只接受新建/空本地固定NTFS目录；已有同仓库登记按真实身份返回，非空不再当首次接管。拒绝源/worktree/common/程序/状态/协调目录重叠及reparse祖先。
- `PlanAsync`冻结完整OID、树、保留根/身份/内容指纹/设置版本；保留归一化后按target记忆，旧/新跟踪冲突先拒绝。`ExecuteAsync`检查持久计划未变、未执行、目标版本未变。
- `SnapshotExporter`流式写原始blob，独立按Git blob头+字节计算SHA-1/SHA-256并核对路径集合。LFS、gitlink、symlink、路径别名/大小写/Windows保留名整次拒绝。
- `TransactionStore`的v1 JSON以独立临时文件、Flush(true)、原子File.Move更新；损坏/更新schema拒绝覆盖。此写法未证明断电持久性。
- `COMMITTED`日志是提交边界；之后登记/清理失败只报告success+CleanupPending，恢复只补登记/清理；旧备份清理不会撤销较新导出。
- `Rollback`按真实身份定位stage/backup/target，逐根归还后验证旧目录完整指纹，再恢复旧目录；保留项来源/目的都存在等歧义不猜测、不删除。

## 实际运行与证据

构建：`.tools/dotnet/dotnet.exe build tests/GitScope.Tests/GitScope.Tests.csproj -c Release --nologo`；主Agent已核对最终整合构建0警告0错误、测试退出0，包含最后3处guard变更。发布Core与已测DLL SHA256一致，见[正式检查点报告](../../../doc/evidence/implementation-checkpoint.md)。

事务单独运行：`.tools/dotnet/dotnet.exe tests/GitScope.Tests/bin/Release/net10.0-windows/GitScope.Tests.dll --transaction-worker suite <全新夹具目录> - -`。完整测试Program也会调用`TransactionChecks.RunAsync`。所有fixture路径由此根生成，绝不接收用户业务仓库作覆盖测试。

| 运行 | 实际结果 | 版本/边界 |
| --- | --- | --- |
| `artifacts/verification/transaction-suite-01` | 184项、退出0 | 早期DLL；全局锁/owner加固前，仅中间证据 |
| `transaction-suite-02` | 失败，已保留进度与夹具 | Git默认NTFS保护拒绝构造NUL.txt；未掩盖成产品通过 |
| `transaction-suite-03` | 204项、退出0 | 修正夹具单次命令配置，增加Windows链接/占用/特殊条目等 |
| `transaction-suite-04` | **205项、退出0** | 最新完成的独立suite；包含缺blob、全局跨state锁、文件句柄加固、磁盘预检 |
| `20261117-132931-ec9553/transactions` | **207项、整合进程退出0** | 最终3处入口guard及新增2项reparse检查已覆盖；总入口251项PASS |

每个通过suite的`transaction-results.json`保存全部检查；`process-recovery-results.json`保存43个强制退出点的子进程PID/退出码/恢复输出，`checks-progress.json`保存过程，`old-manifest.json`是独立逐文件SHA256/目录/链接判据。43个点均真实`Environment.Exit(83)`，每点两个新进程调用恢复，不是进程内抛异常代替重启。

205项包括A→B原始字节/属性/中文/二进制、选择保留和取消保留、未保留源码删除、声明/锁文件等于B、未知/同名替换目录拒绝、旧/新跟踪冲突、无效路径、缺失保留项出现导致计划失效、真实Windows不共享Delete句柄、真实junction外部哨兵、内部链接迁移/清理、临界前/后取消、注入IO/日志写失败、暂存篡改哈希、缺blob、清理待办后继续导出、另一进程和不同state锁、未知版本配置保留、特殊Git条目、保留位置歧义阻断。

最后安全复核新增：`ValidateParent`对stage/backup/target叶子也拒绝reparse，`Move`验证source根，`Fingerprint`验证入口根；并加入“保留已迁移后stage被换成junction”的两项检查。最终整合运行已覆盖此版本，事务207项通过，43个退出点各两次新进程恢复。另由真实桌面完成B更新和after-promote中断后的新UI恢复，独立内容指纹通过；详见正式报告。

## 协调目录与故障入口

跨state锁必须独立于stateDirectory，实际位于`%LOCALAPPDATA%\GitScope\target-coordination`。`<target路径SHA256>.lock`由FileShare.None锁住；进程结束由OS释放。对应`.owner.json`绑定状态目录，阻止另一配置接管已登记空目标。测试只新增其独占夹具目标的协调记录，不覆盖用户设置；这些小记录保留供复核。

生产App不传faultInjector。测试可在`before/after-journal:<state>`、`before/after-old-move`、`before/after-preserve:<index>`、`before/after-promote`、`before/after-isolate-new`、`before/after-return:<index>`、`before/after-restore-old`、`before/after-backup-cleanup`等明确步骤抛异常或真实退出。测试worker入口由Program转发：`--transaction-worker <execute|recover|hold-lock|suite> <state/root> <planFile/targetId> <point/->`。

## 明确边界

- stage目录已创建、其身份尚未落盘的极小进程间隙，恢复保留原目录和未知stage，返回RecoveryRequired，需要核对现场；不根据路径名认领并删除。
- 断电/磁盘损坏、真实填满磁盘、各种权限ACL、超长路径极限、大型node_modules、所有并发外部写入模式尚未测量。可用空间预检和磁盘满异常分类不是已做真实满盘实验。
- 目录/文件句柄降低换链竞态，应用不提供任意外部并发写入的文件系统快照隔离。用户仍应停止输出内写进程。
- 仅本机固定NTFS经过本轮验证；网络共享、其他文件系统、链接源入口保守拒绝。其他Windows设备/DPI/运行性能不属此报告。
