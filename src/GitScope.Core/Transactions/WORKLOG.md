# TASK-05 工作记录

## 当前状态

- 2026-11-17：已核对Contracts v1、API-E/P/TX-01、测试故障矩阵和历史窄验证。
- 正式后端已实现；主Agent已复核最终整合运行的207项事务检查和43点进程恢复，包含最后reparse入口加固；整合进程退出0，发布Core版本已对齐。
- 文件范围：Transactions目录、TransactionChecks.cs；共享类型由主Agent维护。
- 下一步：本阶段已通过桌面保留更新/新进程恢复；后续未测边界随TASK-08核对，详见VALIDATION.md，全局状态见doc/tasks.md。

## 关键实现决策

- COMMITTED日志成功落盘为唯一提交边界；登记配置之后补写，失败不逆转已提交版本。
- 每个事务日志保存旧目录完整指纹、保留根身份/指纹、同父stage/backup绝对位置；恢复按实际身份定位，不靠状态名猜目录。
- 首版仅本地NTFS，目录路径无reparse祖先；内部链接迁移/清理不跟随，外部写入不承诺快照隔离。
- schema=1，未知/损坏状态拒绝覆盖；跨实例目标锁使用规范路径哈希锁文件与FileShare.None，进程退出由OS释放。

## 实现与首次验证

- 已实现FileSafety、TransactionStore、SnapshotExporter、TargetService以及TransactionChecks。真实Core/tests Release构建0警告0错误。
- 初次事务套件transaction-suite-01在早期DLL运行184项通过，覆盖43个进程退出点、每次新进程恢复两遍；属于修复全局目标锁之前的中间证据，不能当最终结果。
- 主Agent交叉审阅指出跨配置目录锁隔离及源链接别名风险，已修复：锁与owner登记位于LocalAppData/GitScope/target-coordination；所有保护目录拒绝reparse祖先并取真实路径。清理/指纹/验证遍历时持有不共享Delete的目录句柄，链接只处理本身。
- 新suite-02正在验证新增的不同配置双进程锁、重复空目录归属、真实句柄占用、junction哨兵、特殊Git条目、损坏schema和歧义保留位置。
- 已明确创建stage后身份日志尚未落盘的进程间隙：未知stage不猜测删除，原目录保持，返回RecoveryRequired。这是保守的恢复边界，不伪装为自动恢复通过。
- 当前未验证：断电持久性、真实磁盘耗尽、其它文件系统/Windows版本及大型依赖性能。下一步完成新增套件并按结果修正。

## 扩充验证过程

- suite-02在夹具创建NUL.txt时被Git for Windows的core.protectNTFS默认保护拒绝，未进入产品验收断言；保留现场/进度。修正夹具使用单次命令-c core.protectNTFS=false写索引，并为大小写冲突显式-c core.ignorecase=false，不修改任何全局配置。
- suite-03重新独立生成夹具后正在执行新增用例及43点重启恢复。真实junction、目录句柄、跨state锁、保留歧义及特殊模式均已有通过进度。
- 进一步代码审阅加固普通文件读取/清理：持有不共享Delete的文件句柄后再确认非reparse，避免attrs检查与打开之间换链接。此修改在suite-03启动后发生，最终需要新构建验证；不把运行中的旧程序集当最终结果。

## 安全审阅补充

- 在suite-04运行期间核对出恢复入口需要重查stage/backup本身的reparse属性，已在ValidateParent、Move和Fingerprint入口补全；内部链接仍由递归分支只记录链接，不跟随。
- 此小修正之后正式集成构建/测试需覆盖；suite-04使用修正前已编译程序集，报告保留此版本边界。

## 正式后端交付主Agent

- suite-03实际204项通过；suite-04实际205项通过、退出0，43个真实退出点每个重启恢复两次。MissingObject新增用例通过，源Git/index/worktree最终快照一致。
- 产物5个正式实现文件、TransactionChecks及VALIDATION.md。所有源写入只在Transactions与指定测试文件，无公共契约和全局文档改动。
- 最后stage叶子reparse保护及2条测试尚待主Agent集成运行（预计207项）。已通知主Agent，不重跑旧实验或继续重复完整suite。
- 新目标协调目录在LocalAppData/GitScope/target-coordination，锁和owner记录独立于配置根，保证不同state实例不能绕过；测试留下的均为独占fixture路径身份记录。
