# Windows 桌面工具链只读核查

版本：v0.1；核查日期：2026-11-17（Asia/Shanghai）；关联规划：PLAN-2026-11-17-B，TASK-01。

状态：本地诊断命令已执行；官方前置条件及生命周期已静态审阅。未执行 Tauri/.NET 应用构建、安装、性能测试或全局配置修改。工作记录由主 Agent 统一维护。

## 结论

**当前环境不能直接构建 Tauri 2 Windows 样本。** 当前 PATH 与标准用户工具链位置没有发现 Rust，Visual Studio 安装器存在，但注册实例与 MSVC C++ 组件查询均为空。WebView2 和 Windows SDK 已存在，不能代替缺失的 Rust/MSVC。这个结论是当前环境缺项，不表示 Tauri 永远不可用，也不构成其运行性能较差的证据。

本机已有 .NET 8 SDK 和桌面引用包，可作为有限样本的现成工具基础，是否构建成功由独立样本验证。**.NET 8 已于 2026-11-10 结束支持**；本次核查日为 2026-11-17，不能因为已安装就正式选用 .NET 8。已安装 .NET 10.0.1 运行时不等于已具备 .NET 10 SDK 或可直接构建 `net10.0-windows`。

## 操作边界与证据性质

- 来源：本机 PATH、明确环境变量、标准用户目录、Visual Studio 安装器、Windows SDK/EdgeUpdate 注册表和其指向目录、.NET CLI；不遍历整盘。
- 验证方式：真实运行只读诊断命令与本地文件/注册表检查；远端官方资料是静态审阅，不是实际构建证明。
- 只写本报告；未安装 Rust/MSVC/WebView2/.NET，未执行包还原，未修改环境变量、注册表或 NuGet 全局配置。
- 未找到标准路径或注册实例不能排除未登记、非标准位置的便携工具；本轮没有无边界搜索这些位置。

## 本机清单

| 检查项 | 2026-11-17 实际结果 | 判断与限制 |
| --- | --- | --- |
| 系统/RID | `dotnet --info`：Windows `10.0.22631`、`win-x64` | 当前样本环境；未验证其他 Windows 版本 |
| Rust 命令 | `Get-Command rustc,cargo,rustup` 无结果 | 当前进程 PATH 不可用 |
| Rust 标准目录 | `C:\Users\xuanyuan\.cargo\bin`、`C:\Users\xuanyuan\.rustup` 均不存在 | 未发现标准用户安装 |
| Rust 重定向 | `CARGO_HOME`、`RUSTUP_HOME` 的 Process/User/Machine 值均未设置 | 没有据此定位其他工具链目录 |
| VS 安装器 | `C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe` 存在 | 安装器存在不等于 C++ 工具已安装 |
| VS 实例 | `vswhere -all -prerelease -legacy -products * -format json` 返回 `[]`，退出 0 | 没有登记的 VS/Build Tools 实例 |
| MSVC 组件 | `vswhere -all -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json` 返回 `[]`，退出 0 | 未发现所需 C++ x86/x64 工具组件 |
| C/C++ 命令 | `cl`、`link`、`cmake` 均不在当前 PATH | `cl/link` 在普通终端缺失不能单独证明未安装；此处与 vswhere 交叉判断。CMake 不是本报告认定的通用 Tauri 必需项 |
| Windows SDK | 注册 `KitsRoot10=E:\Windows Kits\10\`；版本 `10.0.22621.0` | 下列定点文件存在：`Include\10.0.22621.0\um\Windows.h`、`Lib\10.0.22621.0\um\x64\kernel32.lib`、`Lib\10.0.22621.0\ucrt\x64\ucrt.lib`；SDK 不等于 MSVC 编译/链接器 |
| WebView2 注册 | `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`：name=`Microsoft Edge WebView2 Runtime`，pv=`157.0.4341.46` | 与普通 Edge 浏览器注册分开核对 |
| WebView2 文件 | `C:\Program Files (x86)\Microsoft\EdgeWebView\Application\157.0.4341.46\msedgewebview2.exe` 存在，文件版本一致 | 运行时安装证据；未启动控件、未证明应用集成正常 |
| Node | `E:\nvm4w\nodejs\node.exe`，`v22.20.0`；npm 命令可解析 | 没有安装/还原前端依赖，也未据此证明具体 Tauri CLI 兼容 |
| Git | `E:\Git\cmd\git.exe`，`2.42.0.windows.1` | 本轮重新执行版本命令；不等于导出行为已验收 |
| .NET SDK | 仅 `8.0.421`；MSBuild `17.11.48+02bf66295`；根目录 `C:\Program Files\dotnet` | 有限 .NET 8 样本可尝试构建；没有 .NET 10 SDK |
| .NET 运行时 | WindowsDesktop：`6.0.29`、`8.0.4`、`8.0.27`、`10.0.1`；NETCore 同版本；AspNetCore `8.0.27` | 安装存在与当前受支持/补丁合规是不同问题 |
| .NET 引用包 | `Microsoft.WindowsDesktop.App.Ref` 与 `Microsoft.NETCore.App.Ref` 均只有 `8.0.27` | 无 .NET 10 targeting pack 的本地证据 |
| NuGet 默认缓存 | `dotnet nuget locals global-packages --list` 返回 `C:\Users\xuanyuan\.nuget\packages\`；`NUGET_PACKAGES` 三作用域未设置 | 只读取位置，没有清缓存或还原包 |
| WebView2 NuGet 包 | 上述缓存下 `microsoft.web.webview2` 目录不存在 | 现有 Runtime 不包含已确认可用的 .NET WebView2 SDK 包；不能直接声称离线 WebView2 对照样本已就绪 |

Visual Studio 两个标准安装根也已检查：`C:\Program Files\Microsoft Visual Studio` 不存在；`C:\Program Files (x86)\Microsoft Visual Studio` 仅发现 `Installer` 子目录。WebView2 的 HKLM、WOW6432Node 与 HKCU 标准注册范围以及 Program Files/用户 LocalAppData 标准目录已核查，只有上表运行时具有正向证据。

## 官方前置条件与本机缺项对应

| 官方事实及来源 | 适用范围/资料日期 | 对本轮的影响 |
| --- | --- | --- |
| Tauri Windows 开发要求 Microsoft C++ Build Tools、WebView2、Rust；完整支持选择 MSVC Rust host triple（S-01） | Tauri 2 当前文档；2026-11-17 静态审阅 | x64 样本需要可用的 MSVC Rust 工具链与 C++ 链接工具；本机缺项尚未补齐 |
| Rust 的 MSVC ABI 使用 Visual Studio 工具提供的 linker 和 libraries（S-02） | rustup Windows 官方文档；2026-11-17 静态审阅 | 仅补 Rust 或已有 Windows SDK 都不能据此保证 Tauri 可构建 |
| WebView2 生产运行需要 Runtime，可通过安装注册与版本核查（S-03） | 当前 Microsoft 文档；2026-11-17 静态审阅 | 本机注册与实际 EXE 一致；仍需创建并运行控件才能验证宿主集成 |
| .NET SDK 需支持项目目标框架；旧 SDK 不因安装了新 Runtime 就自动获得新目标构建能力（S-04） | .NET CLI NETSDK1045 官方说明；2026-11-17 静态审阅 | 当前只有 8 SDK；不能以 10 Runtime 存在宣称 `net10.0-windows` 构建已就绪 |

Tauri 文档还把 VBSCRIPT 列为 MSI 打包条件。本轮只核查窗口样本基础，不生成 MSI，因此未检查或启用该可选系统功能；不能把它计为已经验证的分发能力。

## .NET 支持日期及正式栈影响

| 事项 | 官方证据与日期 | 建议处理 |
| --- | --- | --- |
| .NET 8 生命周期 | Microsoft 支持策略和生命周期页均写明 2026-11-10 结束支持；官方 dotnet/core 8.0 发布记录也确认已结束支持（S-05～07） | 2026-11-17 已在支持期外；现成 8 SDK 可用于明确隔离、可丢弃的本地测量样本，但不直接决定正式产品采用 8 |
| 本机 8 补丁 | 实际 SDK `8.0.421`、桌面 Runtime `8.0.27`；官方 8.0 最后发布记录为 `8.0.34` / SDK `8.0.428`（2026-11-10） | 不声称本机补丁最新；即使升级 8 最后补丁也不会恢复其后续产品支持周期 |
| 受支持候选 | 支持策略与发布索引显示 .NET 10 为 active LTS，结束支持日期 2028-11-14；本次官方索引列出的当前 runtime 为 `10.0.15`（S-05、08） | 正式 .NET 候选可评估受支持 LTS；需准备对应 SDK、桌面引用包和受维护补丁后重测，不自动安装 |
| 已装 10 Runtime | 本机为 `10.0.1`，且无 10 SDK/引用包 | “有 10 Runtime”只能是环境线索，不能代表正式目标已可构建或当前补丁已满足维护要求 |

生命周期页标明日期使用美国太平洋时间；本次核查距 2026-11-10 已有数日，不存在时区导致仍在 .NET 8 支持期内的解释。部分一般介绍页仍以“支持至 2026 年 11 月”表述，本报告优先采用写明具体结束日期的生命周期及支持策略。

## TASK-01 的可继续项与未验证项

1. 可继续做现成 .NET 工具链的有限窗口/进程测量，报告明确目标版本与样本性质；不能把 .NET 8 的样本成本无条件当作未来 .NET 10 正式应用数据。
2. Tauri 当前记为“缺 Rust/MSVC，未构建”，保留候选。若继续补工具链，应作为明确的准备动作处理；本轮没有自动进行较大的 Rust/MSVC 安装。
3. .NET + WebView2 可以作为另一个宿主的参考样本，但本地 SDK 包尚无缓存证据；即使之后成功运行，也不能称为 Tauri 本体的启动/内存实测。
4. 最终选型仍按用户的低运行占用、打开快排序；没有完成等价样本不能宣布哪一框架更轻。当前工具缺项属于准备成本，不是性能胜负。
5. 当前报告不证明任何正式应用已经成功构建，也不证明目录选择、Git 调用、提交展示、包体、内存或启动性能通过。相关实际结果应由独立样本报告补充。

## 可复核诊断命令

以下均为本轮已使用的只读检查入口，未执行安装命令：

```powershell
Get-Command rustc,cargo,rustup,cmake,cl,link,dotnet,node,npm,git -ErrorAction SilentlyContinue
Test-Path -LiteralPath 'C:\Users\xuanyuan\.cargo\bin'
Test-Path -LiteralPath 'C:\Users\xuanyuan\.rustup'
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -all -prerelease -legacy -products '*' -format json
& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -all -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json
dotnet --info
dotnet nuget locals global-packages --list
node --version
git --version
```

注册表检查限定于 `Microsoft\EdgeUpdate\Clients`、`Microsoft\VisualStudio\SxS\VS7`、`Microsoft\Windows Kits\Installed Roots` 的标准 HKLM/HKCU 及适用 WOW6432Node 路径；目录检查只访问上述明确位置及注册表指向的 Windows SDK 根。

## 官方来源

均于 2026-11-17 访问；静态资料与本机诊断分开使用，没有把网页说明标成应用真实运行。

| 编号 | 来源 |
| --- | --- |
| S-01 | https://v2.tauri.app/start/prerequisites/ |
| S-02 | https://rust-lang.github.io/rustup/installation/windows.html |
| S-03 | https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution |
| S-04 | https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1045 |
| S-05 | https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core （页面更新 2026-11-10） |
| S-06 | https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core |
| S-07 | https://github.com/dotnet/core/blob/main/release-notes/8.0/README.md |
| S-08 | https://github.com/dotnet/core/blob/main/release-notes/releases-index.json |
