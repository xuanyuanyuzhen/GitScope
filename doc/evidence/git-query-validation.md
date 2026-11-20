# Git 查询语义验证

- 状态：已执行的窄范围技术验证；不是 GitScope 功能验收。
- 版本：v0.1，2026-11-10（Asia/Shanghai）。
- 依据：用户已确认的“时间段筛选提交，再选择一个提交导出完整代码”。最新交付方向为轻量 Windows 桌面程序，读取本地仓库并提供手动 fetch，选定提交后完整替换指定固定输出目录；本报告不替用户决定日期字段或历史范围默认值。
- 执行环境：Windows，`git version 2.42.0.windows.1`；Python `subprocess` 调用本机 Git。
- 结果：9 项断言通过，覆盖范围见下文；未覆盖项不能据此判为通过。

## 1. 操作边界与证据位置

仅在本次新建的临时目录中创建了 3 个测试仓库：

```text
C:\Users\xuanyuan\AppData\Local\Temp\gitscope-query-validation-6vmyha1e
  boundaries/
  skewed-history/
  merge-history/
  results.json
```

没有执行旧脚本，没有对用户仓库写入，没有联网。临时目录保留供复核；系统临时目录后续可能被清理，本文件保留关键输入、命令和结果。原始 `results.json` 已复制到本项目的 [git-query-results.json](git-query-results.json)，保留 46 次仓库 Git 调用的参数、工作目录、临时日期环境、退出码及输出，另保存单独查询得到的 Git 版本。主 Agent 已核对命令数46、非零退出0、断言9、失败断言0。

每个 Git 子进程单独设置 `GIT_CONFIG_GLOBAL=NUL`、`GIT_CONFIG_NOSYSTEM=1`、`GIT_TERMINAL_PROMPT=0`，并使用 `git -c user.name="GitScope Fixture" -c user.email="fixture@example.invalid" -c core.autocrlf=false -C <测试仓库> <参数>`。日期只通过 Python 的子进程 `env` 传入 `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE`，没有修改父进程环境或全局配置，因此没有遗留需要恢复的日期环境变量。

以下查询代码块省略上述统一的 `-c` 参数。建库实际使用 `git init --initial-branch=main`；各提交写入夹具文件，再执行 `git add -- <文件>`、`git commit -m <名称>`。没有在产品目录创建实现代码。

## 2. 日期字段与精确边界

`boundaries` 为如下顺序的线性历史。时间全部显式带偏移量，表中采用 UTC。

| 提交名称 | 作者时间 `%aI` | 提交者时间 `%cI` |
| --- | --- | --- |
| BEFORE | 2026-10-31 23:59:59 +00:00 | 同左 |
| START_AUTHOR_OLD | 2020-01-01 00:00:00 +00:00 | 2026-11-01 00:00:00 +00:00 |
| INSIDE_AUTHOR_FUTURE | 2026-12-01 00:00:00 +00:00 | 2026-11-01 12:00:00 +00:00 |
| END | 2026-11-02 00:00:00 +00:00 | 同左 |
| AFTER | 2026-11-02 00:00:01 +00:00 | 同左 |

实际命令：

```powershell
git -C <boundaries> log --reverse --format="%s|%aI|%cI|%at|%ct" HEAD
git -C <boundaries> log --reverse --since-as-filter=2026-11-01T00:00:00+00:00 --until=2026-11-02T00:00:00+00:00 --format="%s|%aI|%cI|%at|%ct" HEAD
```

第二条查询实际输出：

```text
START_AUTHOR_OLD|2020-01-01T00:00:00+00:00|2026-11-01T00:00:00+00:00|1577836800|1793491200
INSIDE_AUTHOR_FUTURE|2026-12-01T00:00:00+00:00|2026-11-01T12:00:00+00:00|1796083200|1793534400
END|2026-11-02T00:00:00+00:00|2026-11-02T00:00:00+00:00|1793577600|1793577600
```

| 检查 | 预期与实际 | 结果 |
| --- | --- | --- |
| 日期过滤字段 | 两条作者日期在区间外、提交者日期在区间内的提交均入选，符合按提交者时间过滤的预期 | 通过 |
| 原生结束边界 | `END` 恰好位于 `--until` 指定时刻，仍被返回；不能直接将该结果当成 `[start,end)` | 通过 |
| 应用层半开区间 | 对候选行 `%ct` 执行 `1793491200 <= ct < 1793577600`，结果仅为 `START_AUTHOR_OLD`、`INSIDE_AUTHOR_FUTURE` | 通过 |
| 作者时间对照 | 对全部历史 `%at` 执行同一半开区间条件，结果为空，与提交者时间结果不同 | 通过 |
| 显式时区偏移 | 将查询边界改为 `2026-11-01T08:00:00+08:00`、`2026-11-02T08:00:00+08:00`，结果与对应 UTC 时刻逐字相同 | 通过 |

半开区间过滤是本次 Python 实验操作，尚未实现为 GitScope 产品能力。如果未来支持按作者时间查询，不能先用提交者时间过滤掉候选，再按作者时间二次过滤，否则会漏掉提交。

## 3. 提交时间倒序的遍历

`skewed-history` 的父子关系与提交者时间为：

```text
ANCESTOR_IN_RANGE (2026-11-01 00:00 UTC)
  → CHILD_BACKDATED (2026-10-01 00:00 UTC)
    → TIP_IN_RANGE (2026-11-02 00:00 UTC，HEAD)
```

作者时间与对应提交者时间一致。实际命令：

```powershell
git -C <skewed-history> log --reverse --since=2026-11-01T00:00:00+00:00 --format=%s HEAD
git -C <skewed-history> log --reverse --since-as-filter=2026-11-01T00:00:00+00:00 --format=%s HEAD
```

| 检查 | 预期与实际 | 结果 |
| --- | --- | --- |
| 普通 `--since` | 仅返回 `TIP_IN_RANGE`，漏掉经过倒序时间提交后仍处于范围内的祖先 | 通过，确认该反例 |
| `--since-as-filter` | 返回 `ANCESTOR_IN_RANGE`、`TIP_IN_RANGE` | 通过 |

设计含义：本机 Git 支持 `--since-as-filter`，它在该夹具上避免了普通 `--since` 的提前停止问题。该结果不代表所有旧 Git 版本都兼容；最低支持版本和不支持时的处理仍需在实现前确定。

## 4. 第一父历史与完整历史

`merge-history` 先创建 `BASE`，再从它建立 `feature` 分支提交 `FEATURE`；回到 `main` 提交 `MAIN`，最后执行 `git merge --no-ff feature -m MERGE`。四个提交使用独立文件，提交者时间分别为 2026-11-01 UTC 的 00:00、01:00、02:00、03:00。

实际命令：

```powershell
git -C <merge-history> log --reverse --first-parent --format=%s HEAD
git -C <merge-history> log --reverse --format=%s HEAD
```

| 检查 | 预期与实际 | 结果 |
| --- | --- | --- |
| 第一父历史 | `BASE`、`MAIN`、`MERGE`，不包含 `FEATURE` 提交项 | 通过 |
| 完整可达历史 | `BASE`、`FEATURE`、`MAIN`、`MERGE` | 通过 |

设计含义：旧工具的第一父规则会改变可供选择的提交集合。“时间段内全部提交”和“主线历史”需要在界面及文档中明确区分。本实验没有验证合并提交 diff 的展示规则，也没有验证导出合并树的内容。

## 5. 限制与后续验证

- 以上仅覆盖本机 Git 2.42.0.windows.1 的小型合成仓库，没有完成目标软件的自动化测试或用户验收。
- 未验证大仓库响应速度、内存、取消、分块读取和增量缓存。
- 未验证浅克隆缺失历史、损坏仓库、Git LFS、子模块、特殊文件名与编码。
- 未验证夏令时、日期选择器自然日边界、无偏移输入的时区解释；产品应先明确时间规则再测试。
- 未验证复杂合并图中时间倒序、路径过滤、不同父级 diff 及完整快照物化。
- 未运行 clone、fetch、任何用户 worktree 操作，也未验证目录冲突、固定导出目录完整替换及失败恢复。
- 下一步：主 Agent 将此证据关联到日期语义、历史范围、查询兼容性与验收设计；未决定的默认行为仍保持草案。
