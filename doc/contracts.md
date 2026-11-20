# 正式契约 v1

2026-11-17；依据PLAN-2026-11-17-C、P-01/02/03/04/05/07/09/10。代码权威类型为src/GitScope.Core/Contracts.cs；公共变更由主Agent协调。

- 正式栈：用户本轮选择.NET10 LTS + WinForms；SDK10.0.404，目标net10.0-windows，Windows本机x64；无第三方包。可执行产物framework-dependent，便携SDK只放.tools。
- 日期：严格yyyy-MM-dd或空，IANA标识（Windows首次可映射），含起止日，按当地日历计算半开区间。无效/歧义/不存在日界明确报错；提交者epoch秒筛选，完整可达图按时间降序、OID序排序；不使用200条上限，页大小50。
- 全部Git读取禁用replace、可选写锁、终端提示及隐式懒获取（并预检promisor）；参数数组、二进制流、退出码与stderr分离。普通/浅库支持；partial/promisor拒绝，绝不偷偷联网。仅真实已安装Git2.42能力，不依赖新开关。
- 导出普通blob的原始字节；gitlink/symlink/LFS及Windows不可映射名称整次拒绝；不执行checkout/archive过滤，不运行包命令。
- 依赖提示识别任何深度的package.json、package-lock.json、npm-shrinkwrap.json、pnpm-lock.yaml、yarn.lock、pnpm-workspace.yaml；比较上次成功Git版本，不表示已安装依赖兼容。
- 所有schema=1。配置/目标/日志损坏、未知版本均拒绝覆盖；v1首次版本不设猜测迁移。路径登记在输出外；只接管新建或空本机目录；真实目录ID校验，源/common/worktrees/程序/状态及包含关系保护。
- 精确相对保留路径去重、父子归一；旧/新跟踪路径相等或相含均拒绝；根链接拒绝，内部链接不跟随。计划冻结设置版本、目标/保留身份，执行需复核。
- TX-01为持久化写意图→动作→完成，每项保留都可逆。COMMITTED之前失败恢复旧目录的全部用户内容；之后仅清理备份，清理失败是success+cleanupPending。任何歧义留所有内容、RecoveryRequired，阻断该目标的新导出。
- 取消：可取消准备阶段返回Cancelled；临界区收尾后按实际结果Success/Cancelled/RecoveryRequired，错误回滚为Failed。应用关闭等候临界区收尾。
- 首个检查点不是第一版全部验收：fetch/最近记录完整体验留TASK-07，资源预算与最终矩阵留TASK-08；不修改原型证据。

## 文件责任

主Agent负责Contracts、GitProcessRunner/GitObjectReader、工程脚本、集成测试和全局文档。TASK-04范围Core/Query；TASK-05范围Core/Transactions；TASK-06范围App（不改csproj/manifest）。各工作包独立记录WORKLOG，不互改文件。遵循prompt中公共契约冻结后04/05/06可并行的安排。
