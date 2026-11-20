# TASK-02：导出、保留迁移与 Windows 目录事务有限验证

版本：v0.1；执行日期：2026-11-17（Asia/Shanghai）；依据：PLAN-2026-11-17-B / [需求 v0.2](../proposal.md)、[设计 v0.2](../design.md)、[TASK-02](../tasks.md)。

**结论：三个有限样本在本机通过，支持继续冻结正式契约；不是 AC-005/006/007 的完整验收，也不是完整事务实现。** 首次占用夹具出现反例，已保留原始失败记录；改正明确的句柄访问条件后重新建立全新夹具运行，未在失败现场继续破坏性试错。

## 1. 产物与实际运行

- [可重跑主脚本](../../validation/transaction/run_validation.py)：标准库实现的 A/B 有限实验与进程恢复驱动；不接受用户仓库。
- [Windows 探针](../../validation/transaction/windows_probe.py)：目录身份、句柄占用及 junction 清理三个操作。
- [本次通过的原始 JSON](transaction-results.json)：环境、参数数组、命令输出、预期/实际断言、事务日志和事件。
- [首次失败的原始 JSON](../../validation/transaction/initial-run-results.json)：原样保留，不能因后续通过删除反例。

实际重跑命令：

```powershell
& 'E:\anaconda3\python.exe' 'E:\AI_Code\GitScope\validation\transaction\run_validation.py' --result 'E:\AI_Code\GitScope\doc\evidence\transaction-results.json'
```

每次执行都通过 `tempfile.mkdtemp()` 建立新的唯一临时根，内部标记为 `TASK-02`。本次结果：

| 项目 | 实际记录 |
| --- | --- |
| 运行环境 | Windows 11 `10.0.22631`；64 位 Anaconda Python 3.12.7；Git `2.42.0.windows.1` |
| 通过运行开始 | 2026-11-17 11:20:53（Asia/Shanghai；JSON 保存 UTC `03:20:53Z`） |
| 最终状态 | `PASS_LIMITED_EXPERIMENTS` |
| 主脚本断言 | 36/36 通过，含 Windows 探针退出码检查 |
| Windows 探针自身断言 | 11/11 通过，原值保存在 JSON 的 `windows_probe` 中 |
| 进程记录 | 主脚本记录 48 次子进程调用；其中 34 次 Git、13 次事务执行/恢复、1 次 Windows 探针；探针另记录 1 次创建 junction 的 PowerShell 调用 |
| 预期非零退出 | `52` 是第二项迁移前的合成失败；`91/92` 是两个故障点的真实进程直接退出；不是漏报错误 |
| 通过运行夹具 | `C:\Users\xuanyuan\AppData\Local\Temp\gitscope-tx-validation-e_shnzrm` |
| 首次失败夹具 | `C:\Users\xuanyuan\AppData\Local\Temp\gitscope-tx-validation-07pigq7t` |

两次根目录均保留。正常提交案例按实验要求删除了它自己的旧备份；Windows 清理案例删除了它自己的指定测试备份及 junction。其余目录、恢复后的目标、事件与日志保留供检查，不自动清理系统 TEMP。

## 2. 操作边界

没有读取或写入业务仓库、旧 `code/code-junk`，没有 fetch、安装、包管理器调用或全局 Git 配置修改。Git 子进程移除继承的 `GIT_*` 干扰，单独设置无全局/系统配置、固定夹具用户和日期、不签名、不执行 hooks；变更只发生在新夹具内。

所有事务 rename 与清理先核对绝对路径属于已标记实验根。A/B 的递归删除拒绝 reparse point；C 的清理函数只允许指定测试备份，遇到 junction 只移除该链接条目，不递归进入。没有 shell 删除、跨 shell 拼接删除或通用目录清理入口。

本实验目录位于系统 TEMP 所在本地卷，只证明该卷上的观察；不能直接推广到其他文件系统、网络共享或所有 Windows 版本。源码中的路径检查是夹具边界保护，不能当成已经完成正式产品的全部路径安全设计。

## 3. 样本 A：原始 Git blob 导出

新建本地 Git 仓库产生 A/B 两个提交，B 保留中文与空格路径、隐藏文件、空文件、混合 CRLF/LF 内容、含全部 0–255 字节的二进制，并删除旧文件、修改源码、`package.json` 和 `pnpm-lock.yaml`。

实际读取：

```text
git --no-replace-objects ... ls-tree -r -z --full-tree <B-commit-OID>
git --no-replace-objects ... cat-file --batch
```

`ls-tree` 按 NUL 分割记录；`cat-file` 按头部声明的字节长度读取内容及协议分隔符，stdout 不经过文本解码。输出与构造夹具时保存的独立预期字节摘要逐文件比较，不只拿导出器自身清单自证。

| 检查 | 实际结果 |
| --- | --- |
| 11 个普通文件的路径、长度、SHA256 | 与夹具预期全部一致，含中文路径、隐藏与空文件 |
| 1024 字节二进制 | 字节摘要一致，无文本转换 |
| `export-ignore` | 指定文件仍被导出 |
| `export-subst` | `$Format:%H$` 保持原始文本 |
| Git 元数据 | 导出目录中没有 `.git` |
| 源夹具保护 | 所有导出后 HEAD 和 `status --porcelain` 与基线一致 |

该实现把有限样本的 batch 输出放在内存中，不是大仓库流式导出实现；未据此给出性能或内存结论。

## 4. 样本 B：可选保留与恢复

每个案例重新生成独立旧目录 O（由 A 导出后修改）及纯 B 暂存。O 含用户改过的源码、`.env`、两组模拟依赖根 `node_modules`/`vendor_cache`、空依赖子目录，以及未选择保留的缓存文件。保留列表为两个依赖根加 `.env`，没有执行依赖安装。

纯 B 首先校验；O 整体改名为备份；逐项记录意图后将保留根移动到 B 暂存；核验并集后提升。原始 O 的独立内容/目录结构清单作为回滚判据，不要求 O 匹配旧提交 A。记录目录/保留根身份，并用独立进程运行恢复。

| 案例 | 故障性质及步骤 | 实际结果 |
| --- | --- | --- |
| 正常更新 | 真实目录/文件操作，没有注入错误 | B 与保留内容的并集准确；旧版独有文件、未保留缓存消失；包声明跟随 B；终态 `CLEANED` |
| 第一根成功、第二根失败 | 第一根真实 rename；第二根操作前注入合成错误并退出 52，不冒称真实权限失败 | 新恢复进程把第一根归还、恢复 O；再次恢复无变化；`ROLLED_BACK`，O 的用户修改/新增/全部保留内容逐项一致 |
| 第一根已移、完成日志未写 | 子进程在真实 rename 后、逐项完成状态落盘前调用 `os._exit(91)`，没有在该进程执行 finally/恢复 | 新进程依据意图、目录位置和身份恢复 O；再恢复一次幂等 |
| 提升后、提交前中断 | 全部保留根已移动、stage 真正提升后调用 `os._exit(92)`，journal 仍是提升意图 | 新进程识别保留根位于新目标，先隔离新目标，再逆迁移、恢复 O；第二次恢复幂等 |
| 提交后清理失败 | B 已校验并记录 `COMMITTED` 后注入合成清理错误；没有真的制造磁盘权限错误 | `COMMITTED_CLEANUP_PENDING`；B 与保留内容保持，备份保留；两次恢复均不回滚新目标 |

每次恢复由新的 Python 子进程执行；PID、rename 路径/身份及逐项 journal 均可在 JSON/夹具中检查。这验证了所列进程中断点，不是操作系统断电测试。`flush/fsync + 临时文件 replace` 仅是本实验的日志写法，不能因此宣称跨目录状态在断电后已具备可靠持久性。

本实验没有重新实现所有保留规则、冲突检测和配置迁移；保留列表是受控、互不重叠的夹具输入。完整保留契约仍由 TASK-03 冻结、TASK-05 实现。

## 5. 样本 C：Windows 真实文件系统行为

1. 以 `GetFileInformationByHandle` 取得卷序列和文件索引，进行同父目录 rename 后再次读取：身份相同，文件内容保留。
2. 用 `CreateFileW` 以 `FILE_LIST_DIRECTORY` 访问、共享读写但不共享 delete 打开测试目录；真实 rename 失败，错误为 **WinError 32**。关闭该句柄后相同 rename 成功，目录身份不变。
3. 用 `New-Item -ItemType Junction` 创建真实目录 junction，指向清理备份之外、但仍位于实验根内的哨兵目录；确认 reparse 属性和实际指向。限定清理只移除 junction 本身后，备份消失，哨兵目录及二进制字节摘要未变。

**首次反例：** 第一次探针以 `desiredAccess=0` 打开元数据句柄，虽然共享模式未包含 delete，rename 仍成功，原“应被阻止”断言失败。整轮立即停止并保存 JSON；随后仅将占用夹具改为明确请求 `FILE_LIST_DIRECTORY=1`，重建全新根运行。结论是“具体访问/共享条件决定占用行为”，不能说“有任意打开句柄就必然无法重命名”。

未测试普通文件句柄、外部编辑器/安装器的各种共享方式、链接迁移后的运行可用性或恶意并发换链。安全清理只证明本夹具不跟随这一个真实 junction，不能替代正式产品的句柄级路径与竞态设计。

## 6. 后续门槛与未覆盖项

建议将 TASK-02 的三个可行性样本记为已完成并由主 Agent 核对；P-03/P-07/P-09获得有限本机证据。正式接口、错误结果及受支持边界仍需 TASK-03 冻结，不能把该脚本直接当成生产后端。

仍未覆盖：完整故障矩阵、断电/磁盘损坏、所有日志写失败、回滚再次失败、来源与目的同时存在、用户路径接管与所有保留冲突、两实例互斥、外部并发写入、部分克隆离线保证、LFS/子模块/符号链接全部模式、长路径/大小写碰撞、真实大型 node_modules 性能、前端交互、依赖命令及其他设备。

父 Agent 负责更新任务、WORKLOG 与设计依据；本实验没有自行修改其状态。

## 7. 执行脚本校验值

| 文件 | SHA256 |
| --- | --- |
| `run_validation.py` | `5f4b7989da5b677fb708854c9c37902e1234e5e965562853256304a7530eab1e` |
| `windows_probe.py` | `666578d91a2ee9868bf73b1f424071c1b303551daea16dfb91403a54a773bb10` |
| `initial-run-results.json` | `9b5d1924d6fd7222e4c34b3e3de9f5ef67c771f6e99e9bb4cc04a8190b2a129a` |
