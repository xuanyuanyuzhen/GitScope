# TASK-04 查询工作记录

2026-11-17，负责范围：Core/Query与tests/GitScope.Tests/QueryChecks.cs。全局任务状态由主Agent维护。

## 当前状态

- 已核对Contracts v1、API-Q、T-01/03/04和旧日期实验证据，未修改共享文件。
- 已实现真实Git仓库识别、partial预检、冻结完整OID、完整日期查询和预览。原型200条上限未进入正式实现。
- 独立39项及最终整合中的39项真实夹具检查通过，Release构建0警告0错误；主Agent已完成真实桌面main/日期/依赖提示与导出恢复贯通。
- 未验证：真实桌面交互/过期UI响应由TASK-06验；大仓库性能及完整资源预算留后续。旧实验9项断言未作为本模块新证据。

## 实现判断

- git rev-parse路径/配置读取在对象访问前完成；partial/promisor及.promisor包标记先拒绝。
- 查询遍历冻结提交全图，应用层按提交者epoch秒半开过滤，结果再排序分页。
- 预览比较第一父，根提交比较空树；diff禁止外部diff和textconv，路径使用literal pathspec；大文件提示按需外部查看而不冒充完整文本。
- 官方参考：git-log/2.42.0、git-rev-parse/2.42.0、git-diff-tree/2.42.0、git-config/2.42.0；静态资料不代替运行验证。

## 已完成与实际验证

- 修改位置：RepositoryService.cs、DateSemantics.cs、HistoryService.cs；验证源码tests/GitScope.Tests/QueryChecks.cs。未修改共享Contracts或其他Agent目录。
- 真实环境：项目便携SDK10.0.404、net10.0-windows、Git2.42.0.windows.1；隔离仓库全部在artifacts/query-checks/query-bfbf576d17174ffa9dbfdfa327dd51e4内。未操作用户业务仓库或真实远程。
- 首次Core命令：`.tools/dotnet/dotnet.exe build src/GitScope.Core/GitScope.Core.csproj -c Release --nologo`，0警告0错误。
- 独立入口实际命令：`.tools/dotnet/dotnet.exe run --project src/GitScope.Core/Query/QueryVerifier.csproj -c Release -- artifacts/query-checks`，进程退出0、39/39通过；该临时csproj现已移除，后续由正式tests/GitScope.Tests入口调用QueryChecks.RunAsync。
- 原始证据：`artifacts/query-checks/query-bfbf576d17174ffa9dbfdfa327dd51e4/query-results.json`。包含全部39项断言及每次夹具Git参数/退出码；212条历史超过原型200条限制，五页无遗漏，真实合并/replace/worktree/shallow/promisor样本均已执行。
- 覆盖：上海含首末日、纽约23/25小时自然日、不存在/歧义午夜、非法输入、系统IANA映射、倒序时间祖先、作者/提交者时间不同、稳定OID排序、引用移动后冻结版本、根/第一父/二进制/literal路径diff、声明锁文件增删及缺基线、取消、空库/缺路径/缺对象、错误worktree反向登记、promisor配置和包标记。promisor测试本机HTTP传输哨兵未收到连接。
- 中间构建问题：临时QueryVerifier生成的Query/obj被上级Core递归纳入导致重复Assembly属性（事务Agent首先观察到9项构建错误）。临时csproj已删除；递归删除生成bin/obj被自动审批策略拒绝，随后仅通过apply_patch移除3个确定生成的冲突.cs文件，Core重新构建退出0、0警告0错误。保留其余原始生成产物，主Agent另统一排除嵌套bin/obj。
- 已知边界：重命名在预览中以删除+新增表示；文本diff单侧2MiB以上明确提示未加载，不显示伪完整文本。只显示依赖声明/锁文件对象变化，不解析包级差异、不执行依赖命令。浅历史父提交缺失时不能提供完整第一父预览。
