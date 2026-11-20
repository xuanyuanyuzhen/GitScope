# 2026-11-20 桌面交互修订验证

TASK-UI-02：修复用户截图中的夜间禁用文字和导出位置，增加首尾/指定页跳转、日期含义说明与日历。Core后端未变。

## 交付行为

- ScopeButton继承Button，保留禁用/键盘/可访问性语义，绘制深浅主题下的文字、边框、悬停、按下与焦点；未就绪导出仍不可点击。
- 输出操作靠右，顺序为取消、打开目录、检查计划、导出；导出位于最右下，结果仍显示在状态区。
- 首页/上一页/页码输入/跳转/下一页/末页。输入从1起，Enter可跳转；拒绝越界、负数、小数、非数字和溢出。翻页不改变所选提交/冻结计划，空结果禁止跳转。
- 起止日期仍筛选提交者自然日、包含首尾、留空不限；固定说明明确导出仍为完整提交。日历按钮/双击/Alt+Down可打开主题日历，支持前后月、yyyy-MM跳月、今天、清空和方向键/Enter/Esc；选日只改对应字段，点击查询应用筛选。

## 构建与控件

[发布日志](../../artifacts/ui-20261120/build-publish-delivery.log)：Release构建0警告0错误。[最终结果](../../artifacts/ui-20261120/verify-delivery/results.json)为**41项通过**，[退出码0](../../artifacts/ui-20261120/verify-delivery-exit.json)。涵盖原27项、分页边界/选择与计划保持、真实禁用文字像素与Unavailable语义、闰日/单侧清空/关闭不改值/月份跳转与校验、最小窗口操作顺序。

首轮最小恢复状态预览过窄，已改紧凑详情布局；真实操作发现首页提示仍残留原页码，已同步修正。中间verify-01/verify-final证据保留，最终入口为verify-delivery。

深浅、禁用、最小/恢复及日历控件图已经批量查看并确认修正。预览：[夜间](../../artifacts/ui-20261120/verify-delivery/dark-disabled.png)、[最小恢复](../../artifacts/ui-20261120/verify-delivery/small-recovery.png)、[日历](../../artifacts/ui-20261120/verify-delivery/calendar-dark.png)。控件图不代替DPI全矩阵或系统主题通知验证。

## 真实桌面

使用原隔离query夹具main（212条、5页）及本轮新state/output，未操作用户业务仓库。

1. 根启动脚本打开正式产物，切换夜间，禁用首页/导出/取消文字可读。
2. 末页到5/5，输入3跳到3/5，再首页回1/5，选中完整OID保持`a9326a0a199ba0e3f3c65b4c92d5c8a2b71f4c48`。
3. 日历真实打开且深色布局正确。自动化工具操作子窗口时激活主窗，日历按失焦行为关闭；真实指针选日未完成，不能记通过。选择/清空/闰日/跳月由真实WinForms控件自检验证。
4. 新desktop-output绑定后，从右下检查计划和导出成功。独立git ls-tree/cat-file核对7个文件与完整树字节一致，源274个文件指纹不变，见[导出验证](../../artifacts/ui-20261120/desktop-export-verification.json)。
5. 验证窗口正常关闭。观察文本在[desktop-observations.json](../../artifacts/ui-20261120/desktop-observations.json)。

首次测试设置误填不存在的refs/heads/paged，程序明确报告引用失效；读取真实refs后通过UI选择main。没有改源引用或用户正常配置。

## 修改与边界

修改MainForm.cs、MainForm.Actions.cs、MainForm.Layout.cs、Theme.cs、Verification.cs、design.json，新增[DateCalendar.cs](../../src/GitScope.App/DateCalendar.cs)。Core DLL SHA256仍为`6768F7D079CB1380F9DC569648728E832D1B9D13123DED2FCC72B7748D1BD4B6`，与原251项验证版本一致，未重复完整事务矩阵。

真实指针选日、全部DPI/键盘矩阵/系统主题通知未完全覆盖。原生组合框箭头、滚动条和日历系统标题栏仍受OS外观影响。TASK-07/08和轻量预算仍待后续；本轮不增加fetch或包命令。

入口不变：[Start-GitScope.cmd](../../Start-GitScope.cmd)。重新启动即可使用更新。
