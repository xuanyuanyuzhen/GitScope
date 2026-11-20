# GitScope 工作记录

## 当前状态

- 更新时间：2026-11-20（Asia/Shanghai）。
- 项目阶段：首个贯通检查点与界面修订已完成；按最新要求暂时将协作工作文档纳入仓库。
- 工作区：`E:\AI_Code\GitScope`。旧工具参考：`D:\jiutianWork\store-learning`。
- 当前有效决定与待决事项只在[proposal](doc/proposal.md)的DEC/P表维护；本轮范围变化和用户原话见[background](doc/background.md)。
- 本轮状态：产品源码及README基线8b19816保持；23份工作文档已通过69e5f0a普通提交推送并核对远程一致。仓库现54文件，测试、AI工具配置、design.json、原始JSON/历史原型继续本地；本地UiChecks分离后41项通过。
- 下一步：产品继续TASK-07/08并维护当前文档；之后按用户指令清理临时工作文档，尚未指定清理时间或阶段，不自动删除。
- 等待事项：当前无用户等待项；P-06数值预算仍未定，只阻塞最终轻量验收。

## 有效文档

- [README](README.md)：项目和阅读入口。
- [背景与沟通](doc/background.md)：原话、澄清和修订顺序。
- [调研与证据](doc/research.md)：官方/本地/推断来源与验证方式分列。
- [需求与验收](doc/proposal.md)：DEC/P权威记录，REQ/AC-001至009为首版，010为后续版本。
- [现状调查](doc/current-state.md)：旧工具能力与缺陷证据。
- [设计草案](doc/design.md)：桌面技术建议、查询、导出和目录事务。
- [测试计划](doc/test-plan.md)：已执行与未执行分开列示。
- [任务状态](doc/tasks.md)：唯一任务状态入口；正式业务实现与验证进度以该页为准。
- [后续实施主提示词](doc/prompt.md)：可直接发送的启动句与下一阶段执行要求。
- [接续交接](doc/handoff.md)：产物、证据边界、环境线索和恢复顺序。
- [日期语义实验](doc/evidence/git-query-validation.md)及[原始结果](doc/evidence/git-query-results.json)。
- [UI原型](validation/desktop/ui/README.md)与[真实验证](doc/evidence/ui-prototype-validation.md)：程序入口、三态主题、21项自检及桌面操作。
- [事务实验](doc/evidence/transaction-validation.md)、[.NET基准](doc/evidence/dotnet-desktop-validation.md)及[工具链盘点](doc/evidence/desktop-toolchain-inventory.md)。

当前有效需求基线为PLAN-2026-11-17-C，实施契约v1；需求/设计/测试/任务文档已同步v0.4，包含DEC-13界面与DEC-14正式技术栈。历史技术实验输入保留PLAN-2026-11-17-B/v0.2；现状调查及日期实验保留2026-11-10/v0.1。状态以tasks为准。

## 2026-11-10：接手与现状核查

### 完成内容与关键决定

- 已阅读 project-workflow 技能及需求设计、任务规划参考。
- 已检查旧工具根目录与当前工作区，未发现工具自身的规划或工作记录。旧 `code/.claude/learning-journal.md` 和 `code/.trae/documents/` 属于业务代码学习记录，不作为 GitScope 项目规划；已只读参考。
- 旧目录根包含 `learn-store.ps1`、`learn-store.bat`、`code/`、`code-junk/`，根目录自身不是 Git 仓库。
- 用户本轮明确确认文档和后续项目写入当前 GitScope 工作区，旧目录仅作参考。
- 旧工具是基于 Git CLI 和关联 worktree 的本地历史浏览脚本；没有日期筛选、fetch 或 clone。确认是静态代码结论，不是运行旧工具的验收。
- 已并行委托旧脚本只读审查和 Git 官方语义核查；主 Agent 统一维护本记录与文档。

### 依据与实际验证

- PowerShell AST 解析 `learn-store.ps1`：0 个语法错误；没有执行交互脚本。
- `learn-store.ps1` SHA256：`3D497D842A3357AB5F06185D23EFC12061613CD01E13450B3E65A7C32168A921`。
- `learn-store.bat` SHA256：`7EDF7D07CD26E819AC92EE7AC991D3E739103917537D0424F5F978F267FBE260`。
- 本机 Git：`2.42.0.windows.1`；本轮终端 PowerShell：`7.6.9`。旧 BAT 调用的是 `powershell`，两者运行时不能混同。
- 主仓库 HEAD：`bb44dd24db48931d1d631884b3d6fd5411a3bee0`；从该 HEAD 可达提交 2075 个，第一父历史 673 个；非浅克隆。
- 主仓库已有 15 项已跟踪文件变更（`git --no-optional-locks status --porcelain=v1 -uno`）；未操作这些变更。
- `code` 为 detached worktree，HEAD：`56f002c5bc30f7d0a4780faa104aa10cc51ece29`；其未跟踪内容包含 `.claude/`、`.trae/`。
- `code` 和 `code-junk` 的 `.git` 文件指向同一个 `.git/worktrees/code`；反向 `gitdir` 记录指向 `code/.git`。`worktree list --porcelain` 未单独登记 `code-junk`。仅凭能读取 HEAD 不能认定后者是独立有效 worktree。
- 本轮只运行读取、状态、计数、哈希及语法解析命令，没有运行 checkout、switch、fetch、reset、clean 或 worktree 创建/修复。

### 已发现的问题与待办

- 日期筛选缺失；连续分页没有独立页码；切换失败会使界面位置与实际 HEAD 不一致；已有学习区缺少归属验证；空结果缺少处理；路径硬编码。
- 原脚本“主项目绝不修改”的表述需修正：关联 worktree 会写入共享 Git 元数据；主工作目录文件、当前分支与已有修改应受到保护。
- 下一步将问题落实到稳定需求/验收编号、设计责任和验证场景，未确认的产品规则保留建议或待决状态。

## 2026-11-10：使用方式收敛与现状文档

- 已写入 `doc/current-state.md`，包含能力矩阵、10 项问题、证据行号、实际环境和未验证边界。
- 用户补充明确：Windows 桌面应用，最好轻量；本地仓库加手动远程更新；输出固定目录，每次选定新提交都移除旧内容并替换。
- 据此替换早期“反复切换学习 worktree”的候选方向。新的核心是“查询提交 → 选定 OID → 完整导出到临时位置 → 成功后替换指定目录”。旧 worktree 的迁移/修复不属于本轮范围。
- 目录完全替换包含旧版本已删除文件的清除，不等于复制时覆盖同名文件；失败时必须保留可恢复的旧版本。
- 独立临时仓库的日期语义实验已启动，尚未记录为通过；只写系统 TEMP 中的测试数据，证据由审查 Agent 写入 `doc/evidence/`。
- 只读环境探测：已发现 WebView2 安装目录（156/157 版本），.NET SDK 8.0.421 与多个桌面运行时；PATH 未找到 rustc/cargo。安装目录存在不等于桌面框架构建验证成功。
- 验证：旧 PS1/BAT 再次计算哈希，与首次记录一致；本轮目前只新增规划文档。

## 2026-11-10：规划草案、技术证据与交叉审阅

- 已形成 README、现状、需求/约束/验收、设计、测试、任务文档，以及工作记录和实验说明，共8个Markdown文件；具体文件见上方有效文档。没有生成应用源码或执行产品构建。
- 核心方案调整为 Git 对象快照导出，固定目录完整替换；不再以切换关联 worktree 为主。对 archive 属性、字节保真、特殊条目、文件占用、失败恢复和目标身份定义了验收。
- Windows轻量桌面暂建议 Tauri 2 + Rust + TypeScript原生界面，复用系统WebView2与已安装Git；.NET原生桌面保留为备选。没有把框架特点换算成未经测量的包体/内存承诺，也没有安装Rust等工具。
- 日期语义窄验证完成：系统TEMP下3个合成仓库；原始结果46次仓库Git调用全部退出0，9项断言全部为true。主Agent已读取报告和原始JSON核对，并保存原始结果到项目证据目录。
- 实验证明本机Git的日期参数按提交者时间，`--until`包含精确结束边界，普通`--since`在时间倒序夹具中漏祖先提交，`--since-as-filter`不漏该夹具；第一父历史不包含侧分支提交。自然日/DST、UI、导出和恢复均未据此宣称通过。
- 独立交叉审阅后补充：fetch显式限制引用映射并禁用prune/tags自动跟随/子模块递归；所有读取禁用replacement objects；备份清理失败后的终态与锁释放职责闭合。这些是设计修订，尚未实现或做技术实验。
- 最终核对已完成：8个Markdown文件，31个相对链接均有效，代码块成对；需求定义9项、验收定义9项，测试计划全部有映射，无未定义REQ/AC引用。任务引用T-05等与实际测试编号已对齐。
- 最终只读复查：旧PS1/BAT哈希与首次一致；源HEAD、学习区HEAD和源工作区已跟踪变更计数15均与首次观察一致。没有对旧目录或源仓库执行任何写操作。
- PLAN-01已标记DONE，表示本轮文档交付完成；CHECK-01为窄验证完成。TASK-01/02仅READY，后续实现任务均未开始；日期默认规则、技术栈和轻量预算仍有明确的建议/待验证标记。
- 下一步：用户后续请求实施时，从TASK-01桌面轻量原型和TASK-02导出/目录替换恢复验证开始；当前无必须等待用户回答的事项。

## 2026-11-17：重新复核规划与范围修订

- 已读取 project-workflow v0.3.2 及需求设计、启动调研、任务规划、执行交接参考，沿用既有路径与REQ/AC/DEC/P编号。
- 实际目录仍只有规划与历史实验资料，没有正式源码；当前不是Git仓库。本轮不自行初始化或提交；文档头部及工作记录追踪版本。
- 用户新要求为完善规划；没有实施授权。独立Agent分别复核事务/日期、任务依赖和官方技术/现成工具依据，均只读。
- 已发现需要修订：原有方案把全部输出清理写成默认；用户先回答全部移除，随后明确修订为环境/已安装依赖可选保留。最终有效语义待写入DEC-08至DEC-11及P-07，前一回答保留为被修订记录，不静默覆盖。
- 用户明确轻量优先级为运行占用少、打开快，允许系统共享运行时；首版先在自己的Windows电脑使用。平台测试不再擅自扩展成强制Windows10/11双平台交付。
- 用户明确已安装依赖指node_modules等目录；package.json和锁文件跟随所选历史版本。依赖变化只提示，安装由用户自行执行；环境文件可选保留。
- 审阅发现：旧输出可能有用户修改，回滚不能拿旧提交OID当其内容证明；取消终态需区分已提交/已回滚；内部链接清理不得越界；shallow/partial的历史及离线边界需明确；资源预算与工程开工门槛需分开。
- 下一步：在同一修订中更新受影响的需求、设计、测试与任务；补背景原话及独立来源/验证方式记录，避免仅增加文档数量。

## 2026-11-17：同步用户修订与补齐流程

- 已新增background.md，回溯记录可见的2026-11-10原话，并逐轮记录本日清理规则从全移除到可选保留的修订。决定只引用proposal，不建立第二套DEC表。
- proposal更新v0.2：保留原DEC-01至05，补DEC-06至12；DEC-08明确被DEC-09取代。新增P-07至10，注明责任/验证/阻塞范围；首版REQ-001至009同步保留项和本机资源目标，后续新增REQ-010/AC-010。
- 用户后续提出依赖变化查询和按钮自动匹配执行增删命令，已记录DEC-12；首版仅提示变化，后续09/10单独收敛与实现，未运行任何安装命令。
- design更新v0.2：候选技术按实际运行成本比较；补API-P、保留根冲突、依赖提示基线；TX-01增加逐项迁移、唯一用户数据保护、提升后逆向恢复、COMMITTED后清理终态、取消实际结果。REQ-010以API-D草案留到后续。
- test-plan更新v0.2：T-01至09同步可选保留、用户修改回滚、浅/部分克隆、时区和本机测量；新增后续T-10但明确未就绪。全部产品测试均未执行。
- tasks更新v0.2：增加本轮PLAN-02及后续TASK-09/10；缩小TASK-02为三个有限假设实验；恢复入口归05/06；建立贯通检查点与10项追溯；解决P-06预算与工程开工门槛矛盾。
- research新增v0.2：8项针对性结论，区分来源/验证方式；官方手册作为静态证据，不称现成工具或框架已实测。原实验JSON复核46次退出0、9项true，没有重跑、刷新环境或操作真实源仓库。
- README已同步首版可选保留和后续依赖能力。约束继续合并proposal，概要/详细设计继续合并design，沿用tasks为唯一进度入口；无需机械拆成更多空文件。
- 过程中一次apply_patch因同文件同时delete/add被校验拒绝，没有产生文件修改；随后以单次UTF-8写入更新tasks。它是文档写入工具限制，不是审批拒绝。
- 下一步：核对链接、REQ/AC/DEC/P/TASK引用及状态，独立复查新规则是否仍有旧“全部移除”残留，然后完成PLAN-02。

## 2026-11-17：本轮交付核对

- 静态检查：10个Markdown文件、51个相对链接均有效，代码围栏成对；10项REQ和10项AC都有测试及任务映射；12项DEC、10项P引用均存在，没有未定义编号。REQ-010明确只在后续验收。
- 独立复核发现AC-006的“规则重叠拒绝”与父子选择归一化矛盾，已修正为重复/父子选择规范化展示，Git跟踪冲突和危险路径拒绝；需求、设计与测试现一致。
- 保留2026-11-10实验报告和JSON原内容；本轮只复核46次记录退出0、9项断言true，没有新增产品测试，没有安装候选框架、运行旧脚本、访问真实远程或执行依赖命令。
- PLAN-02已标DONE，含义是规划文档复核交付完成。技术栈P-05、资源预算P-06、保留事务P-07可行性和后续依赖P-08仍按其阻塞范围待验证/收敛；未写成实现完成。
- 下一步：后续授权实施时执行TASK-01/02有限验证，分别解决桌面资源/启动选型和可选保留事务可行性；当前无需用户再回答问题。

## 2026-11-17：启动有限技术验证

- 用户最新要求：“请开始新一步的推进吧”。结合上一轮明确下一步，执行范围为TASK-01/02的有限验证；旧记录中“本轮仅文档”不限制本次新授权。
- 已复读工作日志、任务和project-workflow技能及执行交接参考；未发现正式工程或额外AGENTS。产物将放在validation/各任务隔离目录和doc/evidence/，不覆盖用户旧业务目录。
- 实际本机：Windows11专业版10.0.22631 x64，AMD Ryzen7 5800H，15.9GiB RAM，PowerShell7.6.9；Git2.42.0.windows.1；.NET SDK8.0.421，桌面运行时包含8.0.27和10.0.1；Node/npm/Python可用，PATH没有rustc/cargo/cmake。
- 派发TASK-02窄实验到transaction目录，.NET最小窗口样本到desktop/dotnet目录，另一个Agent只读核查Tauri构建条件及正式.NET版本支持。所有Agent文件所有权互不重叠。
- 不安装全局工具、不修改Git全局配置、不对真实用户仓库fetch或执行旧脚本；只在合成仓库和独立输出目录中写入/注入故障。这些是本轮实验边界，不替代后续完整产品测试。

## 2026-11-17：UI追加及有限验证复核

- 用户追加使用guizang-design优化UI、添加夜间模式并要求继续，已同步background、proposal的DEC-13/REQ-008/AC-008、design主题设计、test-plan及TASK-UI-01；当前需求基线升为PLAN-2026-11-17-C/v0.3，技术实验仍记录其原B/v0.2输入。
- 采用归藏瑞士风ikb思路：左对齐网格、无衬线、直角、单蓝色强调；浅色/夜间/系统三态即时应用与记忆。实现独占validation/desktop/ui，dotnet原始基准不覆盖。design.json记录本机字体与来源；导出事务未接入原型。
- 主Agent读取.NET报告和源码；现有基准5次退出0，显示20条合成提交；隐藏窗口的Shown回调计时不等于可见首帧。工具链诊断记录Tauri缺Rust/MSVC，.NET8仅用于现有环境有限样本；正式选型仍未定。
- TASK-02报告及JSON经主Agent和独立Agent交叉核对：主断言36/36，Windows探针11/11；48次主调用中的52/91/92退出为预期故障，其余45次为0。三份脚本/首次失败文件的SHA256与报告一致；有限任务标DONE，AC-005/006/007仍未完整验收。
- 首次目录句柄夹具反例被保留：desiredAccess=0未阻止rename；换成明确FILE_LIST_DIRECTORY且不共享delete后真实WinError32，释放后成功。不能推广为所有占用均阻止移动。提交后恢复只验证身份且不回滚，未验证待清理重试。
- UI验证工具此前由子Agent初始化失败；主Agent本次成功导入@oai/sky并列出桌面窗口，待UI构建后进行真实交互。尚未把该成功当成UI验收。
- 下一步：构建原型、查看浅/深与小窗实际渲染，验证选择/日期/主题/重启行为，更新真实证据与交付入口。

## 2026-11-17：首轮可见UI验收与修正

- UI原型首次Release构建成功，启动PID54676；主Agent使用@oai/sky真实观察窗口，20条夹具提交显示正常。实际点击夜间，输入/列表/详情换色；点击第二条后详情显示Sample revision19及对应完整OID。
- 真实系统FolderBrowserDialog已打开并选回专属fixture-repo，返回后仍显示20条。一次自动化元素索引失效，重新观察并按当前截图坐标完成选择；没有猜测为应用失败。原生系统对话框保持系统浅色外观，未修改OS设置。
- 夜间选择已写入LocalAppData/GitScope.UiPrototype/settings.json（Theme=1）；已通过Alt+F4关闭该验证进程，待修正构建后复测真正新进程恢复。
- 实见副标题被标题控件遮挡，已交实现者调整边界。独立审阅另发现读取B时筛选可短暂重显A、保存失败提示被筛选覆盖；正在修复并补有针对性的检查。主按钮/选中主题悬停对比度、非法日期重载状态已修正。
- 首轮程序自检18项通过，六张位图已生成；这一轮仍包含待修正视觉/状态项，不能作为最终验收。后续以修正后的自检和桌面结果为准。原生滚动条夜间偏亮仍在处理/界定。
- 下一步：修正构建→重新运行相关自检→检查新进程夜间恢复与键盘/日期操作→汇总UI证据及入口。

## 2026-11-17：UI原型交付

- 最终Release发布成功；validation/desktop/ui/verification/results.json为21项全部通过，进程退出0。中间一次重复Dispose导致自检异常退出已修正并复跑；最终结果不只看JSON。
- 主Agent对修正版执行真实新进程启动（PID55220，无参数），确认夜间与仓库恢复。日期2099-01-01筛选为空、重置返回20条；点击第二条后Down到第三条；滚轮及拖动自绘滚动条可到末尾；跟随系统读取当前浅色，切回夜间供用户查看。
- 实际浅/深标准窗口及控件渲染已查看，副标题遮挡已消除；选中/错误/空态可读。小窗的详情使用原生滚动条，可滚动但夜间偏浅，系统文件夹对话框仍用OS外观。实际改变OS主题通知、完整DPI/Tab矩阵和剪贴板内容未测，报告已明确。
- 新增doc/evidence/ui-prototype-validation.md，更新README启动入口、proposal/design/test-plan/tasks/background及本记录。TASK-UI-01标DONE；TASK-01维持IN_PROGRESS（正式栈未冻结），TASK-02仅有限验证DONE；完整REQ-001至009仍未验收。
- 原型只读当前HEAD最多200条、在已加载集合按本机日期筛选；完整引用/历史查询、导出、可选保留、恢复和fetch尚未集成，后续依赖执行仍在TASK-09/10。
- 可运行入口validation/desktop/ui/publish/GitScope.UiPrototype.exe；设计约定design.json、6张控件预览及构建说明均在ui目录。没有安装全局开发工具或修改业务仓库。
- 下一步：收敛P-05正式工程版本，开始TASK-03，再接入04/05/06；原始基准保留，未对UI优化后做完整性能复测，不宣称轻量达标。
- 交付末次静态检查：16份相关Markdown、101个本地相对链接全部有效，代码围栏成对；主Agent已补看最终六张控件图（浅/深/小窗/空/失败）。未额外重跑已通过且未变更的事务实验。

## 2026-11-17：补充后续推进与提示词入口

- 用户询问是否有后续工作推进和提示词文档。核对已有tasks/WORKLOG，确认原先没有独立prompt/handoff；按project-workflow补doc/prompt.md、doc/handoff.md，不在本轮启动正式工程。
- prompt提供可直接发送的启动句、读取顺序、P-05先收敛、TASK-03至06首个贯通检查点和留痕/验证要求；07/08保留为第一版后续，09/10依赖执行保留后续范围。handoff提供实际产物、证据、环境/进程历史和接续索引。
- 同步README、tasks接续入口、background原话和本记录；不新增第二套任务状态或DEC/P表，产品需求基线仍C/v0.3。
- 独立只读复核确认6项关键边界：有限完成范围、正式栈未定、实施依赖顺序、保留规则修订、200条原型限制及历史证据日期。已体现在新增文件中。
- 静态核对16份根入口/doc Markdown，相关相对链接均有效、代码围栏成对；没有执行应用、安装环境、重跑产品测试或操作业务仓库。
- 下一步：用户可直接发送doc/prompt.md中的启动句接续实施；工程推进路线保持P-05→TASK-03→TASK-04/05/06。

## 2026-11-17：按主提示词启动正式实施

- 用户明确要求读取doc/prompt.md并按流程实施，范围为TASK-01收敛、TASK-03至06及首个贯通检查点，持续留痕。随后明确选择“.NET 10 + WinForms（推荐）”。
- 已读取project-workflow及执行交接参考、guizang-design和现有design.json。Context7技能已读取，但当前没有其可调用连接器，API资料使用Microsoft/Git官方来源。
- 实际核查：项目根非Git仓库；无新增AGENTS；SDK仍只有8.0.421、桌面10.0.1已安装；Rust命令和MSVC实例仍缺失。
- 官方10.0发行元数据真实读取：截至2026-11-17，2026-11-10发布SDK10.0.404/runtime10.0.15，LTS结束2028-11-14。按用户选择采用项目内便携SDK，无全局安装；选择依据复用/工程成本，不宣称双候选性能胜负。
- 尚未完成：正式构建、完整查询、事务和桌面集成均未验收；旧21/36+11断言不升级为正式应用证据。
- 下一步：锁定工具链、契约与schema，建立正式src/tests/scripts目录，完成最小构建后按互不重叠范围实施。

## 2026-11-17：正式契约、工具链与首个构建

- 新建global.json、Directory.Build.props、NuGet.Config、src/Core+App、tests及doc/contracts.md；无第三方NuGet包，SDK固定10.0.404。Contracts.cs定义API-Q/E/P、四类终态、schema1与设置类型。
- GitProcessRunner使用参数数组、分离二进制stdout与stderr、取消杀进程树、禁replace/transport/prompt及脱敏；GitObjectReader提供NUL树解析和cat-file batch流式读取。后续需集成验证，不把代码存在当验收。
- 项目内SDK下载通过官方SHA512，运行时10.0.15；Core Release真实构建退出0，0警告/0错误。
- 首次SDK运行默认创建未信任ASP.NET开发证书（SDK输出）；后续脚本将禁用此默认行为及遥测，不用于本项目。没有安装全局SDK。
- 按prompt允许的并行文件划分派发Query、Transactions、Desktop工作包；各自独占目录维护子工作记录，主Agent负责共享契约、脚本、集成与全局文档。已同步proposal DEC-14/P-05、tasks、background及design。
- 下一步：正式服务相关验证、桌面最小启动和完整贯通测试；P-06仍未冻结，TASK-07/08不算完成。

## 2026-11-17：project-workflow 技能经验回写（核查完成，修订中）

- 用户要求将本次提示词与交接文档晚补的经验用于完善技能；本轮范围为 C:\Users\xuanyuan\.codex\skills\project-workflow，按 skill-creator 指引修订。
- 已核查：原 v0.3.2 包含执行入口与交接模板，但任务规划仅在显式请求提示词时继续读取执行参考；完整规划收尾与阶段交付缺少主动检查触发条件。GitScope 已有任务与工作记录，却在用户询问后才新建 prompt/handoff。
- 改进决定：按阶段检查内容是否足以执行和接续，优先复用已有文件；明确何时新增、更新或暂不适用。不把每次小修复扩为全套文档，不把生成入口当成实施授权。
- 当前状态：技能修订与验证尚未完成；下一步修改规则，再执行结构检查与隔离场景验证。
- 本轮不改变 GitScope 正式工程任务状态；项目产品进度继续以 doc/tasks.md 和实施记录为准。

### 技能修订完成，验证进行中

- project-workflow 升为 0.3.3，修改 SKILL.md 和 references 下 task-planning、requirements-and-design、execution-and-handoff、progress-visibility，共 5 个文件；保留名称、描述、调用策略和 agents/openai.yaml。
- 增加规划/阶段收尾的入口检查，明确完整入口与启动短句的区别；执行入口和恢复摘要可以放在已有文件的明确章节，按内容复用，任务与决定继续引用唯一权威位置。
- 增加引用、版本、首批动作及证据边界的交付核对；仍区分文档生成与执行授权，仅讨论或单项修改不强制整套文档。
- 验证准备：已在系统临时目录建立三个隔离场景（完整规划、阶段交接、局部需求修改），尚未完成行为验证或结构验证。下一步执行检查并按结果调整。

### 技能结构验证通过，行为验证待完成

- skill-creator/scripts/quick_validate.py 真实运行退出 0，输出 Skill is valid!；校验范围为技能结构与元数据，不代表行为已验证。
- 自定义静态核对：6 份 Markdown、21 个本地相对链接全部有效，代码围栏成对；名称、描述及 agents/openai.yaml 与修订前一致。差异核对仅涉及上述 5 份技能文件。
- 3 个独立子 Agent 正在隔离目录实际执行完整规划、阶段接续和局部修改场景；已提供用户请求、修订后的技能与最少资料，未提供预期答案或修复动机。行为结果尚待主 Agent 复核。

### 隔离行为验证：局部修订与阶段接续已复核

- 局部修订：实际产物只包含原有需求与 WORKLOG 两份文件；需求文本仅 MB→MiB，其余原文保持，未新增提示词或交接文件，待澄清问题未被擅自补定。
- 阶段接续：只更新已有 WORKLOG 的执行入口、恢复索引和阶段摘要，未新增重复文件；过时 P-01 下一步修正为权威任务表中的 P-02，明确历史实验并非正式 CLI、回归或产品验收。
- 主 Agent 已读取两个场景的实际产物并核对文件清单、相对链接和代码围栏；检查通过。第三个完整规划场景尚未结束，暂不报告全部行为验证完成。

### 技能经验回写收尾完成

- 完整规划场景已完成并经主 Agent 阅读实际四份产物：独立执行者在用户未单独要求提示词的情况下，主动将 tasks 中一句启动说明扩为完整执行入口，包含有效基线、实际读取顺序、首批任务/依赖、范围、验证、留痕和启动句；恢复索引复用 WORKLOG。未创建产品代码，未把未来测试记为通过。
- 三个隔离场景均达到本次检查目标：完整规划主动补足入口、阶段接续更新并复用、局部修订保持范围。主 Agent 核对实际文件与证据后确认；没有据此宣称覆盖所有项目情形。隔离产物位于 C:\Users\xuanyuan\AppData\Local\Temp\project-workflow-033-xe90q8kr 的 planning/handoff/small_edit 子目录。
- 最终技能版本：0.3.3。实际修改位置：C:\Users\xuanyuan\.codex\skills\project-workflow\SKILL.md，以及 references/task-planning.md、requirements-and-design.md、execution-and-handoff.md、progress-visibility.md。未修改其他技能或调用配置。
- 本次经验：模板存在不等于阶段交付完整；应明确何时主动检查、怎样判定内容足够、何时复用更新，以及状态/证据与执行授权的边界。文件数量不作为完成标准。
- 当前状态：本轮技能修订与验证已完成，无等待事项。接续入口复用本节及技能的“规划收尾与接续”，未新建技能变更日志或竞争状态文件。
- 下一步：后续使用 project-workflow 时按阶段执行入口检查，依据实际使用发现的问题继续小范围调整。本轮未推进 GitScope 产品实现，产品任务仍以 doc/tasks.md 为准。

## 2026-11-17：正式服务与桌面集成验证进行中

- Core/Query已实现且39项真实夹具检查通过；App已Release构建、自检27项通过，7张控件图检查后修正组合框与小窗布局。正式产物位于artifacts/app。
- 事务工作包逐步扩充为205项和43个真实进程退出点，最后叶子reparse保护新增2项尚待主Agent最终测试；suite-02夹具构造失败及中间结果保留，不将其当最终结果。
- 主Agent审阅推动跨state同目标全局锁/owner登记、源路径别名拒绝、逐层清理句柄保护；已修复主测试Checkpoint中的Span跨await编译错误。一次构建与旧测试进程争用DLL，后改独立输出避免覆盖运行中的产物。
- 正式测试总入口已真实构建0警告/0错误，目前正在运行最终整合检查。程序已实际显示（进程46620），从桌面选择refs/heads/main、2026-11-15至16日及A提交，准备绑定隔离输出。
- 先前桌面启动未形成可观察窗口、随后进程已退出；未记为UI通过。现以新可见进程的真实操作为证据；原业务仓库未操作。
- 期间发现同一工作文档新增了另一项project-workflow技能维护记录；保留该记录，本轮仍继续原GitScope目标，不把其“未推进产品”表述当成撤销本轮授权。
- 下一步：真实A导出→添加环境/依赖→B保留更新→字节核验→强制中断→新进程界面恢复，最后同步tasks/报告/交接。

## 2026-11-17：跨会话接手与证据核对

- 用户要求查看并接手会话01a1e7c1-3599-7100-badf-4b764d1fe1a6。已读取其全部2轮记录；两轮分别因并发限制和429限流中断，原任务未取消，.NET10/WinForms选择已有用户确认。
- 已复读prompt/handoff/tasks、需求/设计/测试、契约及模块记录，沿用project-workflow；不重走选型或启动访谈。README/handoff/WORKLOG头部存在原型阶段旧快照，正在逐项同步。
- artifacts/verification/20261117-132931-ec9553/results.json记录2026-11-17 13:34:43结束、251项PASS，当前没有GitScope/dotnet活动进程；退出码与发布DLL一致性仍待核对，暂不重复完整故障矩阵。
- desktop-checkpoint夹具已完成A导出及本地修改，配置保存了.env/node_modules勾选；未发现B/恢复验证JSON。下一步从该夹具续做真实桌面更新及新进程恢复，并留下独立内容核验。

### 整合结果与发布版本已对齐

- 已从原会话命令结果确认整合构建0警告/0错误、测试进程退出0；251项由5项runner、39项query、207项事务组成，43个真实退出点每次均有两个新进程恢复证据，包含最终叶子reparse加固。
- 发现artifacts/app仍携带旧Core DLL（99E49A…）；测试DLL为6768F7…。首次重新发布受仍运行的夹具窗口PID46620占用而失败，日志已保留。前述“无活动进程”是一次命令输出未正确显示的误判，现已更正，未据此强制终止任何进程。
- 通过桌面工具确认窗口为desktop-checkpoint夹具，正常点击关闭后重发成功，0警告/0错误、退出0。发布Core与已测Core SHA256完全相同：6768F7D079CB1380F9DC569648728E832D1B9D13123DED2FCC72B7748D1BD4B6。证据：artifacts/desktop-checkpoint/continuation/verified-build.json及两次发布日志。
- 未更改生产源码，未重复完整故障矩阵。下一步启动更新后的产物，继续B导出与恢复检查；checkpoint命令改用tests/bin中的已测版本，不使用旧checkpoint-tool DLL。

### B保留更新通过，恢复现场已建立

- 更新后的可见窗口PID47544自动恢复main引用、2026-11-15至16自然日、Asia/Shanghai及两项勾选；真实点击检查计划，显示9个跟踪文件、保留.env/node_modules、其余旧内容移除，再点击导出成功，上次导出变为B。
- --checkpoint-verify-b真实退出0；逐个读取B的9个blob比较输出字节，环境内容和依赖目录指纹保持，old-only及未选缓存移除，源完整指纹不变。b-verification.json和continuation/verify-b-exit.json已落盘。
- 正常关闭UI后，测试worker在B输出写入独有用户修改/新增note，记录完整文件指纹，尝试导出A；after-promote处Environment.Exit(83)，PID38896，实际退出83。日志停于PROMOTE_INTENT，新目录已提升但尚未提交。
- 下一步：新桌面进程应出现待恢复横幅、阻止新计划；通过恢复旧目录按钮操作，再独立校验整个旧目录、保留内容及LastCommit仍B。

### 新进程桌面恢复与内容验证通过

- 新进程PID38120启动时出现“存在待恢复的目录更新”，同目标检查计划/导出按钮禁用，保存的仓库/日期/保留选择仍在。真实点击“恢复旧目录”后横幅消失，计划按钮恢复可用，上次导出仍B。
- --checkpoint-verify-recovery退出0；目标所有文件路径/字节综合指纹与故障前相同（包含app.txt用户修改、user-note及.env/node_modules），源仓库指纹未变，PendingAsync=0，登记LastCommit=B。证据：recovery-verification.json、continuation/verify-recovery-exit.json、desktop-observations.json。
- 首个贯通检查点现有真实UI动作、进程退出、独立文件内容三类证据。故障由引用同一Core DLL的隔离worker触发，恢复由新正式UI进程执行；不冒称桌面程序自身包含故障注入开关。
- 下一步：最终发布控件自检、根启动脚本与再次启动核对；同步任务/验收边界/交付和接续入口。手动fetch、完整最近使用、P-06资源预算及第一版完整验收仍属TASK-07/08。

### 最终入口与文档同步

- 根Start-GitScope.cmd携隔离state启动PID3612，真实可见窗口再次恢复仓库/main/日期/Asia/Shanghai、保留勾选和上次B，未再出现恢复横幅。
- 对最终发布产物运行--verify，27项通过、退出0；7张浅/深/最小/空/失败/恢复图全部复核。新路径使图像哈希与旧自检不同，不以旧截图代替新图检查。
- 已新增doc/evidence/implementation-checkpoint.md归集版本/退出码/桌面/内容证据与限制；README切换到正式启动入口；tasks将03至06按本阶段交付范围记DONE、07记READY；proposal的P-01/02/03/04/07/09/10同步到契约与实测状态，P-06/P-08仍待决。
- 已更新design/test-plan及模块工作记录，重写handoff/prompt的恢复起点和下一阶段入口，避免继续要求重新收敛已确认技术栈。历史失败/原型/窄实验原文保留。
- 一次文档写入因JS模板字符串中的Markdown反引号触发语法错误，未执行任何写入；改用PowerShell字面here-string后完成。当前正在核对链接/编号/状态，尚未将静态核对标为通过。

### 本轮交付核对完成

- 文档检查退出0：23份根/doc/src Markdown、168个本地链接全部有效，代码围栏配对，58个REQ/AC/DEC/P/TASK等定义及引用无缺项。原始结果：artifacts/desktop-checkpoint/continuation/document-checks.json。
- 复核43个进程退出点：全部退出83，首次/第二次恢复均退出0且为不同进程，异常行0；源码/工程/启动脚本SHA256清单已保存，未修改本轮生产代码。
- 推荐入口Start-GitScope.cmd；根脚本测试窗口PID3612已正常关闭，夹具与证据保留。一次关闭动作因桌面几何不可用未执行，重新观察/激活后用Alt+F4关闭，退出已核实。
- 正式产物、构建、使用、验证边界和下一阶段提示词已同步；doc/handoff.md与doc/prompt.md复用更新，没有新增竞争进度入口。
- 当前无用户等待事项。本轮完成范围为原会话首个贯通检查点；下一阶段TASK-07已READY，TASK-08及P-06轻量验收仍待完成，TASK-09/10继续后续。

## 2026-11-20：截图反馈与桌面交互修订

- 用户确认基本导入导出正常，要求修复红框夜间按钮、导出操作移到右下、解释起止日期、增加首页/末页/指定页及日历选择。
- 已读当前工作记录/任务和App源码，截图显示禁用导出/取消/上一页文字过暗；现有Button由系统绘制禁用文字，单设ForeColor未覆盖该路径。
- 本轮采用有语义的Button子类自绘状态；输出右下以导出为最终主操作，取消放在操作组内；日期保留手工输入/留空与IANA自然日语义，增加弹出日历和固定说明，不改变导出完整提交的规则。
- 分页增加首尾按钮、页码输入与跳转，越界输入给明确提示；翻页不改当前选择/导出基线。修改范围App与相关文档/验证，不推进fetch或后端目录事务。
- 已使用impeccable上下文工具，当前是既有WinForms局部修订，沿用src/GitScope.App/design.json；没有为缺PRODUCT.md重走产品访谈。UI/UX检索匹配可读对比度与禁用语义，忽略无关移动端返回建议。Context7工具未提供，WinForms绘制/弹出文档改从Microsoft Learn直接核对。
- 下一步实现四项改动，构建后批量检查深浅/最小尺寸/禁用/日历，补有边界意义的分页与日期检查，真实桌面只操作隔离夹具。

### 四项交互已实现，首轮检查通过

- 修改MainForm及Actions/Layout，新增DateCalendar.cs，Theme.cs新增ScopeButton。禁用态仍为真实Enabled=false/无操作；文字按Muted绘制，保留焦点/键盘及原生按钮语义。输出按钮顺序为取消、打开目录、检查计划、最右侧导出。
- 分页新增首页/末页及页码输入（Enter/跳转），拒绝0/负数/越界/小数/超大值，不改变已选提交或冻结计划；空结果禁止跳转。
- 日期支持日历按钮、双击、Alt+Down；主题日历可选择日期、前后月、yyyy-MM跳月、今天、清空、Esc；默认今天按当前IANA时区，只改变对应输入，保留手工输入和后端自然日校验。固定说明已解释筛选与完整导出。
- Release构建0警告0错误；首次相关UI检查39项全部通过、退出0（artifacts/ui-20261120/verify-01），包括真实绘制禁用文字像素、分页边界/选择保持、闰日与单侧清空。
- 第一轮批量查看深浅、禁用、最小、恢复及日历图片，发现最小恢复状态的预览仅露表头；已集中修正为紧凑详情布局，待最终确认。Core DLL哈希仍为原251项已测版本，未重跑目录事务矩阵。
- 外部Microsoft Learn工具请求未返回可读内容，本轮技术依据以现有源码和本机真实构建/行为结果为准；此前“直接核对”是预定动作，不能作为已取得官方文档证据。
- 下一步确认修正后的控件图，发布并使用隔离多页仓库做真实日历/分页/导出按钮验证。

### 最终构建与真实分页验证

- 发布构建0警告0错误、最终39项检查退出0，最小恢复预览修正已确认。正式Core哈希未变。
- 真实窗口在隔离main夹具查询212条、5页；实际点击末页到5/5、输入3并跳转到3/5、首页回1/5，所选完整OID不变；深色禁用文字与右下操作已观察。
- 首次隔离配置误填不存在的refs/heads/paged，产品明确拒绝且保留失效引用；核对真实refs后从UI改为main。此为测试设置错误，未改源仓库。
- 日历可真实打开、主题正确。桌面工具将动作送到主窗时日历因失焦关闭，元素缓存随之失效；这是当前真实点击验证的工具交互限制，不能把失败动作记成选日成功。控件自检中的闰日/清空/关闭行为已通过，正核对独立窗口选择方式。

### 真实导出与交付版本确认

- 新右下按钮实际导出隔离main所选OID a9326a0a199ba0e3f3c65b4c92d5c8a2b71f4c48成功。独立git ls-tree/cat-file逐文件SHA256验证7个文件与整个树完全一致，源274个文件指纹未变；desktop-export-verification.json为PASS。
- 真实桌面确认日历打开/主题/布局；选日UI动作被工具激活主窗口导致弹窗关闭，切换另一原生控制接口结果相同，未将未完成动作记成功。日历选择/闰日/清空/关闭/跳月以真实WinForms控件执行自检留证；完整真人输入矩阵仍不声称已测。
- 真实分页时发现首页后底部提示仍残留“第3页”，已修正所有翻页同步状态；日期placeholder缩为完整yyyy-MM-dd避免窄框截断，留空语义由固定帮助文字承担。
- 最终交付构建/发布0警告0错误，新增非法月份与跳月不提前改筛选的检查，总41项通过、退出0；artifacts/ui-20261120/verify-delivery为本轮最终控件证据。
- 隔离验证窗口已正常关闭；用户实际仓库/输出/正常配置未修改。接下来同步使用说明、验收边界与工作状态并静态核对。

### TASK-UI-02交付完成

- 已更新根/模块使用说明、设计/需求/测试、tasks/handoff/prompt及设计token说明，新增doc/evidence/ui-interaction-20261120.md集中记录本次证据与未测边界。原启动脚本不变，artifacts/app为新发布版本。
- 静态核对24份Markdown、185个本地链接全部有效，代码围栏匹配；发布App与当前构建DLL逐字节一致，Core SHA256与原已验证版本一致。结果保存artifacts/ui-20261120/delivery-checks.json。
- 文档一次批量patch因末段上下文不匹配整体未应用，确认无部分写入后重做成功；未影响生产代码或验证产物。
- 本轮无等待事项。用户可重新打开Start-GitScope.cmd查看；下一任务仍为TASK-07/08。日历真实指针自动化限制已明确写入报告，未扩大为声称全键盘/DPI矩阵通过。

## 2026-11-20：建立版本控制与首次上传

- 用户提供https://github.com/xuanyuanyuzhen/GitScope.git并明确要求上传，同时询问是否尚无版本控制。
- 本地git rev-parse确认根目录不是Git仓库；远程git ls-remote退出0且无引用，为空仓库。本机Git2.42.0.windows.1和Git Credential Manager可用，未安装gh，沿用已有Git作者配置。
- 已补.gitignore排除便携SDK、所有bin/obj/publish、生成的Git夹具/UI验证目录、缓存与本地.env/私钥；保留源码、测试、规划和小型历史证据。.gitattributes规定文本识别和Windows启动脚本CRLF。
- README补克隆/准备工具链/构建/启动命令及生成产物/本机证据边界。当前尚未初始化、提交或推送；下一步核对候选文件和凭据模式后执行。

### Git已初始化，提交范围核对完成

- git init -b main成功，origin指向用户给定仓库；远程空仓库未发生覆盖。当前候选98个文件、约1.28MB，最大文件为历史事务JSON；没有exe/dll/zip、嵌套.git或gitlink候选。
- .gitignore实际命中SDK、正式产物、原型发布、测试Git仓库、UI验证目录及.env。凭据模式扫描仅命中tests/Program.cs中明确的sentinel脱敏测试URL，已阅读确认是无效fixture；未发现真实密钥模式，不输出凭据。
- 已修正当前WORKLOG/handoff/prompt/proposal中的无Git快照，历史记录保留。下一步暂存、从暂存内容构建干净副本、首次提交和push，再比对远程OID。

### 首次提交前验证通过

- git暂存98个文件，git diff --cached --check退出0，无子模块/gitlink、生成产物或SDK条目。
- 使用git checkout-index从暂存文件导出到artifacts/repository-upload/clean-checkout，再用项目锁定SDK构建其中App；退出0、0警告0错误，证明纳管文件不依赖被忽略的本机生成源码。
- 构建日志/候选扫描保留在artifacts/repository-upload，本机生成证据不重复纳入提交。下一步创建初始提交并普通push到origin/main，禁止force；推送后核对远程OID。

### 首次推送已完成并核对

- 初始提交e0b7a5c（feat: initial GitScope desktop app with recoverable exports）包含98个文件；git push -u origin main退出0，创建远程main并设置上游。
- git ls-remote origin refs/heads/main与本地HEAD完全一致，初次推送后工作区干净。原始核验保存在artifacts/repository-upload/first-push-verification.json。
- 推送过程中显示旧credential-manager-core命令不存在的提示，但Git通过现有认证链成功推送且已独立核对远程；未修改全局凭据设置，不把提示误记为上传失败。
- 本次交付记录更新将单独作为文档提交继续普通push；不改写初始提交或远程历史。用户项目现有真实版本控制，不再是仅文件夹/工作日志留痕。

## 2026-11-20：文档与AI资料退出版本控制

- 用户要求所有文档/AI相关文件不提交，包含此前已上传内容；本轮仅从Git移除，本地资料继续保留以满足工作留痕和后续恢复。
- 已核对远程只有main，两次提交均为本会话刚上传。移出61个文件：全部Markdown（含根README/WORKLOG）、doc目录、小型历史原型/实验validation目录和design.json；保留37个正式源码/测试/构建配置文件。
- .gitignore增加文档、设计说明、历史验证及常见AI助手状态目录规则；已git rm --cached，61个文件全部仍在本机。原Git全部引用备份在artifacts/repository-cleanup/before-cleanup.bundle，仅本地忽略，不上传。
- 下一步核对精简后的文件可构建，并确认用户对“此前已上传”的范围是否包含改写两次远程提交历史。仅当前树删除与历史清理不同，不擅自声称旧提交已经消失。

### 历史清理已获明确确认，精简源码构建通过

- 用户在本轮明确选择“连提交历史一并清除（推荐）”；已告知改写main和提交ID变化，本地文件及备份保留。
- 从暂存的37个文件导出干净副本，分别构建正式App与Tests：均退出0，0警告0错误。未重跑功能矩阵，生产源码未修改；结果与日志在artifacts/repository-cleanup。
- 远程旧main为db6872b6f21a19395da5d6ee8c32428955b92c3a；准备以过滤后的树建立无父提交的新根，再用精确force-with-lease校验该旧OID后替换远程main。旧提交只保留在本地bundle/reflog，不创建或推送备份分支。

### 历史清理与远程复核完成

- 新无父提交169f744eff7620bb01d6fbee76e5886e723ffb3d已建立；push使用精确force-with-lease绑定原db6872b，成功替换origin/main，未向远程推送任何备份引用。
- 从GitHub新建独立mirror克隆检查全部广告引用：只有main、1个提交、37个文件，无Markdown/doc/validation/design.json。两个旧提交对象在新克隆中均不存在；本地HEAD与远程一致。
- 61个退出跟踪文件均在本机并被gitignore命中，除本WORKLOG追加记录外其余60个文件哈希与清理前完全一致；正式源码未变，精简源码App/Tests均构建0警告0错误，工作区干净。
- 最终证据artifacts/repository-cleanup/final-verification.json；本地原历史备份before-cleanup.bundle保持忽略。验证范围是远程可达历史及新克隆接收对象，未验证GitHub服务器缓存或第三方已有克隆。
- 本工作文档及后续本地记录不再纳入提交。用户目标完成，无等待项；产品后续仍TASK-07/08。

## 2026-11-20：恢复用户README并缩小仓库为产品源码

- 用户询问tests是否必须、要求排查类似非运行内容，并指出README不应移除。已承认上一轮把全部文档排除范围理解过宽。
- 用户明确选择“只留产品源码、构建脚本和README；测试留本地”。将tests目录及App/Verification.cs留本地，恢复面向用户的根README；保留SDK/还原/编译相关配置与锁文件。
- 界面自检当前直接编进App并由--verify调用；准备拆成本地独立UiChecks工程，正式入口移除自检。公开build脚本只构建/发布，原测试入口迁入本地tests脚本，测试不会丢失。
- 沿用本轮先前明确“连提交历史一并清除”的仓库清理要求，以当前169f744为租约基线整理main；本地bundle/README/构建入口备份在artifacts/repository-product-only，不推送备份引用。
- 下一步：修改并核对精简仓库、正常构建和本地自检；确认README无AI记录/个人路径/本地文档链接，再推送及新克隆核验。

### 产品与本地测试已分离

- 根README已恢复为纯用户说明：实际功能、克隆/工具链/构建/使用、日期含义、导出规则与当前限制；无AI工作记录、私人路径或被忽略文档链接。.gitignore仅为根README加例外，其余文档继续本地。
- 已取消跟踪7个测试文件。后端tests内容保留原位置；UI Verification.cs完整移至tests/GitScope.UiChecks，新增独立本地运行工程，链接真实App源码进行控件检查。
- 正式Program移除--verify入口；公开build.ps1只构建/发布，不再引用本地tests。新本地tests/run-checks.ps1承接Backend/Ui验证入口；全部在忽略范围，不上传。
- 当前暂存31个文件（原37减7加README），下一步对仅含暂存内容的干净副本构建，同时执行分离后的本地UI检查；尚未记为构建通过或推送完成。

### 产品构建与本地验证均通过

- 仅31个暂存文件的干净副本发布成功，退出0；不存在tests/或Verification.cs仍可构建。正式程序集不含FixtureServices/界面自检内容。
- 新本地tests/run-checks.ps1 -Ui真实构建0警告0错误，41项控件检查全部通过、失败0，结果在artifacts/verification/local-ui-20261120-123420。7个原测试文件内容哈希保持（Verification只移位置）。
- 已运行简化后的正式scripts/build.ps1 -Publish，产品正常构建发布；没有恢复公开测试入口或把本地测试编入正式App。
- 静态核对31个跟踪文件，唯一Markdown是README；无tests/doc/validation/design.json、无README内AI过程词/个人路径/失效资料链接；git diff --cached --check通过。
- 下一步以169f744旧HEAD为精确租约基线同步干净main及README，重新克隆核对远程内容。所有本地工作记录和测试继续被忽略。

### 仅产品源码与README的仓库已同步

- 8b1981660944bd081de9cb0647d636cf5a0d00cf已按精确租约推送，fresh mirror核对1次提交/31个文件，唯一Markdown为README，无tests/或Verification.cs。7个测试文件仍在本机，hash不变；工作区当时干净。
- 结果保存在artifacts/repository-product-only/final-verification.json。此后用户追加临时提交工作文档，作为新的范围修订继续处理，不撤销README保留或测试本地化。

## 2026-11-20：工作文档暂时纳入提交

- 用户原话：“工作文档部分内容是否能够暂时一并提交，等到之后再将其从仓库里去掉”。按行动请求执行，临时恢复协作所需Markdown，不恢复先前含非产品内容的旧Git历史。
- 已更新.gitignore为明确临时允许列表：WORKLOG、doc/*.md、doc/evidence/*.md及App/Core模块工作记录/说明。测试、AI工具状态、design.json、原始JSON与validation继续本地，根README始终纳管。
- 已在WORKLOG当前状态、handoff、prompt、proposal同步最新规则和测试新入口，避免历史“不提交文档”或旧-Test/--verify命令误导接手者。之后清理的具体时间/阶段未指定，不创建自动删除或定时任务。
- 下一步检查允许列表/资料中的凭据模式和文档引用边界，普通commit/push后核对远程；不改写这次产品基线历史。

### 临时工作文档提交前核对

- 本批23份工作Markdown加.gitignore允许列表，共24个变更文件；仓库总54文件，根README保留。暂存中无测试、AI工具目录、design.json、历史原型或原始JSON。
- 凭据模式扫描无发现、Markdown围栏配对、本机相对链接均存在；59处链接指向只在本机保留的附件/历史实验，已在handoff明确新克隆边界，不恢复这些附件。
- 本轮只改文档和忽略规则，不重复产品构建/测试；代码基线仍为已通过正常构建与41项本地UI检查的8b19816。
- 将以普通提交暂时保留这些文档，远程推送后核对；之后清理按用户新指令执行，不设自动删除时间。

### 临时工作文档已同步

- 普通提交69e5f0a已推送到origin/main，本地与远程OID核对一致；没有改写产品基线8b19816或恢复已清理的旧提交。
- 目前临时纳管23份工作Markdown；测试/AI工具配置等忽略范围保持。未来要求移除时再处理；普通删除提交仍保留历史，若届时要求历史一并清理再按明确范围执行。
- 本轮用户目标完成、无等待事项；本次核验记录随文档提交保持可接续。
