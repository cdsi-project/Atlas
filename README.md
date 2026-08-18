# CDSI Atlas

CDSI Atlas 是 CDSI 的本地资产发现与索引应用。它在创作者自己的 Windows 设备上扫描所选目录，建立独立于文件路径的资产与位置记录，不移动、不重命名，也不删除源文件。

当前仓库实现 Milestone 0.3：带精确重复检测和基础媒体理解的本地资产索引闭环。

## 当前能力

- 手动选择本地目录并在后台递归扫描
- 默认忽略 <code>.git</code>、<code>.vs</code>、<code>node_modules</code>、<code>vendor</code>、<code>bin</code>、<code>obj</code> 等目录
- 默认不跟随符号链接和 junction
- 识别常见文件扩展名与 MIME 类型，未知格式仍可索引
- 以异步流式读取计算 SHA-256，不把大文件载入内存
- 扫描与哈希分为两个阶段，资产索引完成后立即显示
- 默认仅对同尺寸的重复候选计算 SHA-256，避免无条件读取每个大视频
- 勾选“完整校验”时补齐所有未哈希文件，精确重复仍以完整 SHA-256 为准
- 基于文件大小和修改时间复用缓存，避免重复哈希
- 按 SHA-256 生成精确重复组并在独立标签页展示
- 通过可扩展的提取器注册表处理资产元数据
- 在本机只读提取常见图片、音频和视频的基础属性
- 提取图片/视频尺寸、媒体时长、编码、比特率、采样率和声道数
- 提取标题、艺术家和专辑等常用媒体标签
- 以文件大小、修改时间和管线版本缓存元数据结果
- 在资产列表中展示分辨率、时长和编码摘要
- 在资产页汇总可用本地文件总数、实际占用空间、视频数量和视频总时长
- 将资产、位置、扫描根和扫描任务持久化到 SQLite
- 同一设备和路径重复扫描时保持幂等
- 文件消失时仅将本地位置标记为 <code>Missing</code>，保留逻辑资产
- 在 WinForms 中显示扫描进度、错误计数和资产列表
- 在 Windows 标题栏和应用页眉显示当前构建版本
- 哈希阶段显示文件数、读取字节数与吞吐率
- 支持取消扫描或哈希；已完成哈希会保留，下次从未完成文件继续
- 支持取消元数据提取；不支持或损坏的单个文件不会阻断任务
- 单文件错误不会中断整个任务

## 架构

~~~text
CDSI.Agent.WinForms
        |
        v
CDSI.Agent.Application
        |
        v
CDSI.Agent.Core
        ^
        |
CDSI.Agent.Infrastructure
~~~

- <code>CDSI.Agent.Core</code>：领域模型与抽象，不依赖 WinForms、SQLite 或云 SDK。
- <code>CDSI.Agent.Application</code>：扫描、元数据和哈希工作流编排。
- <code>CDSI.Agent.Infrastructure</code>：文件系统扫描、媒体元数据提取和 SQLite 实现。
- <code>CDSI.Agent.WinForms</code>：桌面界面与依赖组合根。
- <code>tests</code>：领域、基础设施和端到端临时目录测试。

## 开发环境

要求：

- Windows 10 或更高版本
- Visual Studio Community 2026 18.9 或兼容版本
- .NET SDK 10.0.400 或兼容的 .NET 10 Feature Band

构建和测试：

~~~powershell
dotnet build CDSI.Agent.slnx
dotnet test CDSI.Agent.slnx
~~~

运行桌面应用：

~~~powershell
dotnet run --project CDSI.Agent.WinForms/CDSI.Agent.WinForms.csproj
~~~

如果系统同时安装了 x86 和 x64 <code>dotnet</code>，请确保 <code>C:\Program Files\dotnet</code> 在 PATH 中优先于 <code>C:\Program Files (x86)\dotnet</code>。

## 版本管理

仓库根目录的 <code>VERSION</code> 是唯一版本来源。构建时，所有程序集和桌面界面都会读取该文件。

每次提交将版本递增 <code>0.001</code>，并创建同名 Git 标签，例如 <code>VERSION=0.104</code> 对应 <code>v0.104</code>。

## 本地数据

应用状态写入：

~~~text
%LOCALAPPDATA%\CDSI\cdsi.db
~~~

扫描目标只进行读取。测试只使用 <code>%TEMP%\cdsi-agent-tests\&lt;随机目录&gt;</code>，不会扫描或清理真实用户目录。

## 下一阶段

下一阶段将扩展文本资产理解与复核能力：

- TXT 与 Markdown 文本提取
- PDF 与 Office 文本提取
- Inbox 与批量复核入口
- 更完整的位置核验

AI 分类、云存储、CDSI Server API 和自动文件整理不在当前阶段。
