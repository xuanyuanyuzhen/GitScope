# UI原型与夜间模式验证

日期：2026-11-17；输入：PLAN-2026-11-17-C/v0.3，DEC-13、REQ-008/AC-008主题子项、TASK-UI-01。状态：本轮浏览原型已构建、自检及可见交互验证；完整产品流程与轻量预算尚未验收。

## 交付与设计

- [可运行程序](../../validation/desktop/ui/publish/GitScope.UiPrototype.exe)，双击运行；[使用与构建说明](../../validation/desktop/ui/README.md)。
- [design.json](../../validation/desktop/ui/design.json)：guizang-design瑞士家族/ikb的原生桌面适配；独立实现，无第三方UI库、远程字体或图片。
- 浅色、夜间、跟随系统；实际背景、表面、输入、列表、选择、详情与按钮使用对应token。主题与仓库保存到LocalAppData/GitScope.UiPrototype/settings.json，不改变Windows主题。
- 真实本地仓库读取、当前HEAD最多200条、在已加载范围内按本机自然日筛选、提交详情及复制ID；没有接入导出、fetch或保留迁移。
- 默认客户区1200×780，最小外窗960×640；列表与详情分别可滚动。没有把原型查询简化规则写回正式API-Q契约。

## 构建与程序化检查

本机.NET SDK8.0.421构建，运行时8.0.27；沿用现有有限验证环境，非正式工程版本决定。`dotnet publish`成功，未新增第三方包依赖；详细命令见原型README。

最终`--verify verification`进程退出0，[原始JSON](../../validation/desktop/ui/verification/results.json)的21项断言全部通过。主Agent读取JSON，核对21项、失败0。覆盖：真实Git读取20条与唯一OID、实际列表/选择详情、主题颜色及持久化、重新创建Form恢复、系统主题两分支与显式选择优先、日期空结果/无效值/重载恢复、失败状态、加载中不显示旧仓库、主题切换保持选择及日期、列表滚动。

程序化系统主题测试注入主题读取函数，不宣称操作了Windows设置或验证了真实系统通知送达。配置写到自检专用目录，不影响用户主题记录。

中间一轮出现“JSON断言通过但进程异常退出”：重复Dispose访问已释放CancellationTokenSource。已修正释放后清空字段，再运行验证最终退出0。最终结论同时要求断言和进程结果，不用旧成功JSON遮盖异常。

## 实际桌面操作

主Agent使用统一Windows Computer Use（@oai/sky）操作真正窗口；无自写PowerShell UI自动化。此前基准Agent的初始化错误未持续到此轮，不能继续写成无法验收。

| 操作 | 实际观察 |
| --- | --- |
| 启动并查看浅色 | 20条夹具提交、仓库路径与作者/时间/OID可见；发现副标题被遮挡并修复，修正后全文可见 |
| 点击夜间 | 输入、列表、选中行、详情和按钮立即换色；当前选中记录保留 |
| 选择第二条 | 详情切换到Sample revision19，完整OID为17051ffc960da1e27e039214ceee71f15cc1b7f3 |
| 系统文件夹选择器 | 实际打开并选择相邻dotnet/fixture-repo，返回后20条提交正常显示；对话框采用系统外观 |
| 关闭后无参数重启 | 首次验证PID54676关闭后，修正构建以新PID55220启动，恢复夜间和上次仓库，自动读取20条 |
| 日期无结果与重置 | 输入2099-01-01并筛选，显示0条/空状态、清空所选详情、禁用复制；重置后恢复20条 |
| 键盘选择 | 点击第二条再按Down，详情更新为Sample revision18 |
| 列表滚动 | 鼠标滚轮使列表向下，拖动主题滚动条可到最后一条；详情仍保持所选提交 |
| 跟随系统 | 点击后按当前系统浅色切换，选择仍为Sample revision18；随后切回夜间供用户查看 |

一次选择器自动化索引过期，重新观察并用当前截图坐标完成；记录为工具观察缓存问题，没有将失败调用当成应用完成证据。部分工具紧接点击返回旧文本树，另次观察/实际图像确认最终状态。

修正还包含：主按钮/主题按钮悬停文字对比、非法日期重读后无法恢复、加载新仓库期间旧记录重显、配置保存失败提示被后续筛选覆盖。并行审阅与相关状态检查已覆盖这些修改。

## 渲染与局限

已查看[浅色](../../validation/desktop/ui/verification/light.png)、[夜间](../../validation/desktop/ui/verification/dark.png)、[小窗浅色](../../validation/desktop/ui/verification/small-light.png)、[小窗夜间](../../validation/desktop/ui/verification/small-dark.png)以及空/失败状态的控件输出。它们是Control.DrawToBitmap渲染，**不是桌面截图**；真实桌面证据是上述操作和本轮工具显示。

- 小窗详情采用原生滚动条，夜间轨道仍可能浅色；系统文件夹对话框跟随系统外观。列表滚动条已适配双主题。
- 尚未验证实际Windows主题改变后的通知送达、全部DPI/多屏矩阵、完整Tab遍历、剪贴板内容及真实业务仓库矩阵；不宣称完整无障碍/兼容性验收。
- 只有当前HEAD的前200条；日期基于本机时区，仅筛选已加载集合。正式引用选择、完整历史、IANA时区/DST与diff由TASK-04实现。
- 没有把基准数据套用于优化后界面：dotnet目录5次测量保留，当前UI未做同条件完整性能复测，不据此宣称轻量达标。
- 完整快照导出、可选保留、事务恢复、手动fetch和后续依赖命令均未接入此原型。

## 交付校验

| 文件 | SHA256 |
| --- | --- |
| GitScope.UiPrototype.exe | 43FA75C3AB1331D3C234498F656ACAE42E71B5A5E44D4A0F75AAE0143EB7888A |
| GitScope.UiPrototype.dll | 0A045468FAD3F52547E51CCB05775B9899DD92557441360BAE0E4CDCA320CF30 |

TASK-UI-01按本轮范围完成；AC-008完整产品验收仍未完成。下一阶段依据P-05收敛正式工程版本，在已有查询与事务证据上接入完整流程。
