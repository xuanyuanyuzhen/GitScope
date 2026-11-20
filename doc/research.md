# GitScope 调研与证据复核

版本：v0.3；基线：PLAN-2026-11-17-C。复核日期：2026-11-17（Asia/Shanghai）。状态：规划依据及有限实验索引；完整产品未实现。R-01至08保留规划阶段证据边界，本轮新增实测见R-09。

本页补充会影响方案的外部事实与证据缺口，不重启项目访谈、不改变用户目标。用户决定与后续修订以 [需求](proposal.md) 为准；设计取舍见 [design.md](design.md)，后续验证入口见 [tasks.md](tasks.md)。

“来源”描述信息来自哪里；“验证方式”描述本轮做了什么，两者不能混用。官方文档和上游源码链接属于资料，不等于在本机运行通过。以下仅调研一个现成工具，已足够建立流程基线。

## R-01：TortoiseGit 可作流程基线，未证明能替代固定目录更新流程

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| 官方在线手册 RS-01～03；未覆盖能力的判断是资料缺口 | 静态审阅；未安装或操作 TortoiseGit | 2026-11-17；在线手册未锁定具体发行版 |

- 结论：手册提供引用与日期范围筛选、提交/改动预览、第一父历史、手动 fetch，以及指定提交的 ZIP 导出；Export 文档指向 `git archive`。
- 影响：用它核对“选仓库—找提交—导出”的操作步骤，避免自研基础交互遗漏；用户仍在规划 GitScope，不将目标改成换用现成工具。
- 缺口：未证实导出能清除固定目录中旧版独有文件、事务回滚或保留自选环境文件；也未证明日期字段/边界与本项目一致。不能把“未查证”写成“必然不支持”。

## R-02：Tauri 2 复用 WebView2，不能据此认定运行更轻

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| 官方文档 RS-04～05；性能比较建议为推断 | 静态审阅；无壳应用、打包或性能测试 | 2026-11-17；Tauri 2、当前 WebView2 部署文档 |

- 结论：Evergreen WebView2 可由应用共享；缺失时仍需运行时检测与安装策略。安装 Edge 浏览器不能替代 WebView2 Runtime 检测。
- 影响：用户允许共享运行时，且低运行占用、打开快优先；Tauri 与 .NET 均作为候选，Tauri 不再因分发包可能较小而默认胜出。
- 缺口：在同机同数据下测冷/热启动、可交互时间、空闲及导出进程组内存；包体、总磁盘占用与首次新增依赖作为补充指标。预算与测量结果尚未确定。

## R-03：.NET 原生桌面也可共享运行时，选型须比较真实使用成本

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| Microsoft 官方文档 RS-06～08 | 静态审阅；未构建 WPF/WinForms 样例 | 2026-11-17；现代 .NET 当前文档，具体版本未选定 |

- 结论：framework-dependent 依赖对应 .NET Desktop Runtime；self-contained 携带运行时。官方当前仍说明 WPF/WinForms 存在 trimming 支持限制。
- 影响：先自用且允许共享运行时，使依赖本机桌面运行时的部署成为可比较方案；不能只比较安装包，亦不能预设裁剪能解决自包含分发体积。
- 缺口：运行时/SDK 版本、目标 Windows 版本与架构、启动/内存表现均待核定。2026-11-10 的环境探测没有刷新，不作为今日安装状态或兼容性证明。

## R-04：部分克隆可能在读取时联网，浅克隆可能使查询历史不完整

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| Git 官方文档 RS-09～12；首版处理选择为设计推断 | 静态审阅；未构造 partial/shallow 夹具 | 2026-11-17；Git 2.42.0/2.48.0 文档及当前 partial-clone 文档 |

- 结论：partial clone 可缺少 tree/blob，读取会尝试按需获取；普通 fetch 可能继续遵循过滤配置，不能承诺补齐旧对象。shallow 截断历史，与缺少内容对象是不同边界，也可同时存在。
- 影响：查询需说明“仅本地已有历史”，零结果不能证明远端无提交；缺失对象应说明原因和已验证的补齐方式，不能只笼统提示点一次更新。
- 缺口：2.48.0 文档列出 `--no-lazy-fetch`，本次未从 2.42.0 文档证明该选项可用。需验证实际版本能力或明确拒绝未验证的 promisor 仓库；不得仅凭只读命令名称承诺离线。

## R-05：禁用 replacement objects 是原始 OID 一致性的一部分

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| Git 官方文档 RS-10、13 | 静态审阅；尚无 replacement 端到端夹具 | 2026-11-17；Git 2.42.0 全局选项及当前 replace 文档 |

- 结论：`refs/replace` 可使读取指定对象时得到替换对象；`--no-replace-objects` 可禁用该机制。
- 影响：沿用设计中查询、预览、commit/tree 解析和 blob 导出统一禁用的规则，避免显示原 OID 却导出替换内容。
- 缺口：需实际验证各调用都经过统一 runner；只把参数写入设计不代表 OID 一致性已通过。

## R-06：原始 blob 快照与 archive/checkout 的语义不同

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| Git 官方文档 RS-14～16；实现边界为设计建议 | 静态审阅；未执行字节导出/目录替换测试 | 2026-11-17；cat-file 2.42.0、archive 2.40.0、当前 attributes 文档 |

- 结论：`cat-file --batch` 可读取对象内容；`--filters` 会启用 checkout 侧转换。archive 遵循 `export-ignore`，`export-subst` 可改写内容，因此不能直接等同项目的原始快照定义。
- 影响：保留对象读取方案；将跟踪文件按 Git 树校验，不以可运行项目为成功标准。特殊模式、LFS 指针及 Windows 路径限制仍按需求明确处理。
- 缺口：二进制协议、对象摘要、文件系统映射、失败恢复均未实测。archive 的 2.42.0 文档请求被官方导向 2.40.0，此处保留实际资料版本。

## R-07：日期实验是历史真实运行证据，本轮仅复核留存结果

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| 本地[实验报告](evidence/git-query-validation.md)与[原始 JSON](evidence/git-query-results.json) | 2026-11-10 真实运行小型合成仓库；2026-11-17 静态复核 JSON，未重跑 | 原实验：Windows、Git 2.42.0.windows.1、3 个临时测试仓库 |

- 结果：本轮解析 JSON 得到 46 次命令、非零退出 0、断言 9、失败断言 0；原记录覆盖提交者时间、结束边界、时间倒序遍历、第一父历史及等价显式时区偏移。
- 影响：可继续作为现有查询设计的窄证据；不能升级为产品验收或今天重跑通过，也不能把合成仓库真实运行称为真实用户仓库验收。
- 缺口：自然日/DST、桌面交互、导出/保留/恢复没有因此通过；旧 Git、WebView2、.NET、Rust 探测及源仓库状态均需在实际实施时重新检查。

## R-08：可选保留改变替换试验；后续依赖操作另有验证边界

| 来源 | 验证方式 | 日期与适用版本 |
| --- | --- | --- |
| 本轮用户需求修订见 [proposal.md](proposal.md)；下述事务影响属于设计推断 | 未验证；本轮不查各包管理器命令、不执行依赖操作 | 2026-11-17；本项目 v0.2，后续功能见 REQ-010/AC-010、P-08 |

- 影响：环境文件、`node_modules` 可选保留，使输出由跟踪快照与明确保留内容组成；保留不得覆盖目标提交中的跟踪文件。已跟踪 manifests/locks 随选定提交，首版不自动安装依赖。
- 缺口：需新增保留路径冲突、复制/迁移中断、空间及占用失败、回滚后内容归属的恢复试验；大依赖目录会影响时间/空间预算。具体策略须验证，不能沿用纯空树替换结果直接验收。
- 后续范围：用户另要求依赖变化查询及按钮匹配执行增删命令。包管理器/锁文件识别、命令对目标 package/lock 的改写、安装脚本与联网边界均未验证；不扩张首版，不执行安装。

## R-09：本轮有限技术验证

| 证据 | 验证方式与实际结果 | 限制 |
| --- | --- | --- |
| [工具链盘点](evidence/desktop-toolchain-inventory.md) | 本机命令/目录/注册表只读核查，结合报告所列官方支持资料 | Tauri缺Rust/MSVC；不据缺工具判断框架性能；.NET8仅样本 |
| [.NET基准](evidence/dotnet-desktop-validation.md) | 真实Release构建、5次20提交查询；记录启动代理与进程内存 | Opacity=0，没有测物理首帧/真正冷启动；不是两框架对比，完整组峰值未覆盖 |
| [事务实验](evidence/transaction-validation.md) | 36个主断言和11个Windows断言；真实目录移动、二进制导出、进程中断恢复及junction清理 | 只覆盖3个有限样本；保留首次占用假设反例；生产路径/锁/完整故障矩阵仍未验证 |

本轮UI按DEC-13扩展独立原型，沿用现有可构建环境，不因此冻结P-05正式技术栈。正式工程下一步优先评估受支持.NET原生方案的准备成本；若继续比较Tauri，先补其实际缺失工具链，再做等价实验。

## 官方资料索引（规划阶段来源）

下列链接均为资料出处；访问日期为 2026-11-17。链接或上游源码可访问不代表对应行为已在本机验证。

| 编号 | 来源 URL |
| --- | --- |
| RS-01 | https://tortoisegit.org/docs/tortoisegit/tgit-dug-showlog.html |
| RS-02 | https://tortoisegit.org/docs/tortoisegit/tgit-dug-export.html |
| RS-03 | https://tortoisegit.org/docs/tortoisegit/tgit-dug-pull.html |
| RS-04 | https://v2.tauri.app/distribute/windows-installer/ |
| RS-05 | https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution |
| RS-06 | https://learn.microsoft.com/en-us/dotnet/core/deploying/ |
| RS-07 | https://learn.microsoft.com/en-us/dotnet/core/install/windows |
| RS-08 | https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities |
| RS-09 | https://git-scm.com/docs/partial-clone |
| RS-10 | https://git-scm.com/docs/git/2.42.0 |
| RS-11 | https://git-scm.com/docs/git/2.48.0 |
| RS-12 | https://git-scm.com/docs/git-rev-parse/2.42.0 |
| RS-13 | https://git-scm.com/docs/git-replace |
| RS-14 | https://git-scm.com/docs/git-cat-file/2.42.0 |
| RS-15 | https://git-scm.com/docs/git-archive/2.40.0 |
| RS-16 | https://git-scm.com/docs/gitattributes |
