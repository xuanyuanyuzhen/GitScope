# .NET 桌面有限验证

日期：2026-11-17。依据：PLAN-2026-11-17-B，TASK-01、DEC-06/07、P-05/P-06。状态：已真实构建和测量一个有限原型；可见UI交互未完成验证；不是技术选型已定或AC-008已通过。

## 1. 方案与边界

选择.NET 8 WinForms，是因为这次样本只需要原生窗口、系统文件夹选择和一个20行列表，用内置控件即可覆盖；这个选择不代表WinForms已经优于Tauri，也不是正式UI方案。未引入第三方UI库或NuGet包，未安装全局工具。

原型源码、夹具、运行脚本和发布目录位于 [validation/desktop/dotnet](../../validation/desktop/dotnet/README.md)。它没有导出、目录替换、保留迁移、fetch或安装依赖功能。界面文件夹选择仅演示系统对话框；提交查询固定读取带本次验证标识的合成仓库，不读取用户业务仓库。

## 2. 环境、构建与样本

| 项目 | 本次实际环境/结果 |
| --- | --- |
| 系统 | Windows 11 专业版，10.0.22631，64位 |
| CPU / 物理内存 | AMD Ryzen 7 5800H；17,024,741,376字节 |
| SDK / PowerShell | .NET SDK 8.0.421；PowerShell 7.6.9 |
| 目标与实际运行时 | net8.0-windows，Microsoft.NETCore.App / Microsoft.WindowsDesktop.App 8.0.27；进程加载模块路径已核对 |
| Git | 2.42.0.windows.1 |
| 构建 | 本地restore与Release framework-dependent publish均退出0；NuGet源清空，使用本地8.0.27引用包 |
| 样本 | `validation/desktop/dotnet/fixture-repo`，20次线性提交，固定UTC+08:00日期；只变更一个sample.txt |
| 网络/源仓库 | 没有fetch，没有读取用户业务仓库；无包下载或开发环境安装；公开Microsoft文档查询与构建分离 |

实际构建命令（工作目录为原型目录）：

```powershell
dotnet restore --configfile .\NuGet.Config -p:NuGetAudit=false
dotnet publish -c Release --no-restore -o .\publish --self-contained false
.\run-benchmarks.ps1 -Runs 5
```

每次查询先运行`git --version`，再执行：

```text
git --no-replace-objects --no-pager -C <fixture-repo> log -20 --format=%H%x09%cI%x09%s HEAD
```

参数以数组传入，不拼shell；子进程局部设置`GIT_CONFIG_GLOBAL=NUL`、`GIT_CONFIG_NOSYSTEM=1`、`GIT_TERMINAL_PROMPT=0`、`GIT_OPTIONAL_LOCKS=0`。夹具创建日志保留在`fixture-setup.json`，未改变全局Git配置。

## 3. 测量方式

- 五次独立进程，间隔600ms；首次是本次测试第一次启动，后四次是重复启动。没有重启系统或清理文件缓存，均不能称“真正冷启动”。
- 后台测量通过`Start-Process -WindowStyle Hidden`启动。程序仍创建原生Form/控件句柄和消息循环，同时设置Opacity=0、ShowWithoutActivation、不显示任务栏按钮，避免抢焦点。
- “进程→就绪”由进程开始时间至`Form.Shown`后一次排队的BeginInvoke回调；“外部调用→就绪”从父进程发起启动前计时。两者都是UI就绪代理，不是实际屏幕首帧或人工可操作时刻。
- 就绪后等待1、2、3秒各采样一次；查询后再次等待1、2、3秒。未强制GC。表中空闲取第3秒；原始数据保留三个点。
- Git耗时从创建进程至退出，包含进程启动；“查询并填表”还包含版本查询、解析和ListView填充，不包含真正显示器呈现。
- 内存记录WorkingSet64与PrivateMemorySize64。Git执行期间约5ms抽样本进程加当前直接Git子进程；不是完整所有后代采样，也不保证捕获瞬时峰值。不能据此宣称已满足P-06完整进程组峰值验收。

## 4. 五次实际数据

所有运行退出0，Git命令退出0，均得到20行；单位ms和MiB（1MiB=1,048,576字节）。

| 次数 | 缓存说明 | 进程→就绪 | 外部调用→就绪 | Git log | 查询并填表 | 初始空闲WS / Private | 查询后空闲WS / Private | 抽样组最大WS |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 首次测试启动，已有系统缓存 | 752.5 | 916.9 | 63.8 | 146.7 | 45.94 / 12.24 | 52.36 / 16.07 | 52.65 |
| 2 | 重复启动 | 208.3 | 224.2 | 55.0 | 123.6 | 45.47 / 12.23 | 51.99 / 16.07 | 52.21 |
| 3 | 重复启动 | 202.0 | 219.2 | 88.6 | 167.8 | 45.48 / 12.24 | 52.61 / 16.18 | 52.61 |
| 4 | 重复启动 | 201.0 | 217.3 | 63.6 | 126.2 | 45.50 / 12.35 | 52.00 / 16.12 | 52.19 |
| 5 | 重复启动 | 208.1 | 223.8 | 56.1 | 128.8 | 45.42 / 12.13 | 51.98 / 15.96 | 52.08 |

表中抽样组包括当前直接Git子进程的可观察样本，但全生命周期最大值可能发生在查询后；不是“Git峰值”的同义词。原始结果与所有采样点见 [JSON证据](dotnet-desktop-validation.json) 和 [测量汇总](../../validation/desktop/dotnet/measurements/20261117-111324/summary.json)。每次ready信号文件和完整Git元数据输出均保留于同目录。

## 5. 发布内容与UI核验

发布目录5个文件合计207,886字节，去除PDB后191,410字节；压缩包96,375字节。它们**不包含预装的共享.NET桌面运行时，也不包含Git或夹具**，不是独立安装包大小。runtimeconfig固定8.0.27并禁用roll-forward以保持本次测量一致；这不是正式产品的更新兼容策略。没有验证缺运行时的干净机器部署。

可见手动模式确实启动了进程32256，读取到标题“GitScope · .NET 桌面验证（非正式产品）”，进程Responding为true；模块路径确认使用8.0.27。没有用进程存在推断视觉布局或交互通过。

目录选择按钮/系统FolderBrowserDialog已编译在原型中，但桌面工具`node_repl`初始化报`failed to write kernel assets: 系统找不到指定的路径。 (os error 3)`；重置重试仍失败，统一CUA入口同样初始化失败。因此没有完成截图、文件夹选择器点击或键盘操作验证，明确标为未验证。没有改用PowerShell UI自动化绕过工具。

## 6. 结论与下一步

本次证明：现有本机.NET环境能离线构建并运行一个小型WinForms窗口，异步读取20条Git提交，产生可重复的启动/内存数据。仍需同条件Tauri候选数据、可见交互验证，以及完整进程组内存与实际可操作时间测量才能支持最终P-05选择；不能凭此报告断言.NET更轻。

未覆盖：真实冷重启、完整产品UI、导出/事务/保留功能、大仓库、长期运行、缺运行时机器、其他Windows设备和P-06数值预算。现有数字只用于方案取舍，不是产品性能承诺。

## 7. 官方API依据

2026-11-17读取Microsoft官方页面，HTTP 200；API文档静态审阅与上述本机真实运行分开记录：

- Form.Shown：`https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.shown?view=windowsdesktop-8.0`
- FolderBrowserDialog：`https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.folderbrowserdialog?view=windowsdesktop-8.0`
- WorkingSet64：`https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.workingset64?view=net-8.0`
- PrivateMemorySize64：`https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.privatememorysize64?view=net-8.0`
