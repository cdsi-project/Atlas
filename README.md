# CDSI Atlas

CDSI Atlas 是 CDSI 的本地资产发现与索引应用。它在创作者自己的 Windows 设备上扫描所选目录，建立独立于文件路径的资产与位置记录。扫描和分析不会修改源文件；复制、移动或 OSS 备份只会在用户明确选择文件并确认后执行。

当前仓库实现 Milestone 0.9：带受管工作目录、多扫描目录、逻辑资产清单、显式受管资产操作、可验证 OSS 备份、精确重复检测、基础媒体理解和本地文本理解的资产索引闭环。

## 当前能力

- 首次启动配置一个受管工作目录，并自动创建 <code>Inbox</code>、<code>Assets</code>、<code>Exports</code>、<code>Cache</code>、<code>Temp</code> 和 <code>System</code>
- 工作目录可后续修改；切换时不搬移、不删除旧目录中的任何文件
- 将受管工作目录的 <code>Inbox</code> 作为受管扫描入口
- 添加、停用、启用和软移除多个外部扫描目录
- 外部扫描目录固定为只读策略；扫描、索引、哈希和提取不会修改源文件
- 资产列表支持单选、Ctrl/Shift 多选；右键会保留或扩展批量选择
- 创建视频、音频、图片、文字或综合类型的资产清单/项目
- 从资产列表将单个或多个资产加入清单；同一资产可属于多个清单
- 从清单移除成员只删除逻辑关系，不移动或删除本地文件
- 将清单内全部可用资产作为一个批次同步到 OSS，并沿用逐项文件名确认、上传进度和完整性校验
- 复制到工作目录时使用 <code>Assets/&lt;AssetId&gt;/&lt;原文件名&gt;</code>，保持同一个逻辑资产身份
- 本地复制使用临时文件和流式 SHA-256 校验，不覆盖内容不同的已有文件
- 移动必须再次确认完整源文件列表；只有副本校验并登记成功后才删除源文件
- 复制、移动和每个文件的成功/失败状态持久化到 SQLite 操作审计
- 一次扫描全部已启用目录；离线磁盘或 NAS 标记为不可用后继续处理其他目录
- 检测嵌套或重叠扫描目录并提示，位置身份仍按设备和规范化路径保持幂等
- 在设置页添加、编辑和删除多个阿里云 OSS 配置
- 按阿里云规则校验 Bucket，并规范化 Endpoint、地域和 HTTPS 设置
- SQLite 只保存非敏感存储配置；AccessKey Secret 保存到 Windows 凭据管理器
- 在资产列表中显式单选或多选文件，在确认窗口逐项设置 OSS 文件名；默认与当前本地文件名一致
- 远端对象使用 <code>storage_profile_id + assets/&lt;AssetId&gt;/&lt;OSS文件名&gt;</code> 标识，不把文件名或永久 URL 当作资产身份
- 上传以只读流处理本地文件；大文件使用分片上传，并将 UploadId 和已完成分片保存到 SQLite 以支持重试续传
- 上传前拒绝覆盖同一对象键下内容不同的对象；相同大小和 SHA-256 的对象可幂等复用
- 上传完成后通过 HEAD 校验对象存在性、大小和 <code>cdsi-sha256</code> 元数据，再登记为健康远端位置
- 资产列表用绿色显示已通过校验的 OSS 备份状态；上传进度显示本次实际网络传输速度，任务、文件结果和失败原因写入本地审计
- 编辑配置时不读取或回显已有 Secret，留空会保留原凭据
- 删除 OSS 配置只删除本机记录和凭据，不删除 Bucket 或云端对象
- 配置 OSS 不会触发连接、上传或同步
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
- 通过独立提取器注册表只读处理 TXT 与 Markdown 文本
- 识别 UTF-8、带 BOM 的 UTF-16/UTF-32、GB18030 和 Windows-1252 文本编码
- 从 Markdown 提取标题、各级标题和规范化纯文本
- 单文件最多读取 4 MiB、最多缓存 200,000 个字符，超限内容明确标记为节选
- 以文件大小、修改时间和文本管线版本缓存结果，重复扫描不重复提取
- 在资产详情中显示文本状态、编码、标题数量和只读预览
- 在资产列表中展示分辨率、时长和编码摘要
- 在资产页汇总可用本地文件总数、实际占用空间、视频数量和视频总时长
- 将资产、位置、扫描根和扫描任务持久化到 SQLite
- 同一设备和路径重复扫描时保持幂等
- 文件消失时仅将本地位置标记为 <code>Missing</code>，保留逻辑资产
- 只有完整且无遍历错误的扫描才更新缺失状态，避免权限或临时 IO 故障造成误报
- 在 WinForms 中显示扫描进度、错误计数和资产列表
- 在 Windows 标题栏和应用页眉显示当前构建版本
- 哈希阶段显示文件数、读取字节数与吞吐率
- 支持取消扫描或哈希；已完成哈希会保留，下次从未完成文件继续
- 支持取消元数据和文本提取；已完成结果会保留，下次从未完成文件继续
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
- <code>CDSI.Agent.Application</code>：扫描、元数据、文本、哈希、受管文件操作和对象存储备份工作流编排。
- <code>CDSI.Agent.Infrastructure</code>：文件系统扫描、媒体/文本提取、SQLite、Windows 凭据管理器和阿里云 OSS 适配器。
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

每次提交将版本递增 <code>0.001</code>，并创建同名 Git 标签，例如 <code>VERSION=0.108</code> 对应 <code>v0.108</code>。

## 本地数据

应用状态写入：

~~~text
%LOCALAPPDATA%\CDSI\cdsi.db
~~~

工作目录由用户首次启动时选择，推荐路径为 <code>D:\cdsi_workspace</code>；如果 D 盘不可用，则使用用户目录下的 <code>cdsi_workspace</code>。切换工作目录不会搬移或删除旧内容。

扫描、索引、哈希和提取只读取扫描目标。资产清单及其成员关系保存在本机 SQLite 中，不改变文件的物理位置。用户可在资产列表中显式复制或移动选中的文件到 CDSI 工作目录，或在确认目标 Bucket 和源文件清单后备份单个资产、多个资产或整个清单到 OSS；这些操作失败时保留源文件。提取文本、文件操作审计、上传断点和非敏感 OSS 配置保存在本机 SQLite 中；AccessKey Secret 保存在当前 Windows 用户的凭据管理器中。仅配置 OSS、创建清单或执行扫描不会自动上传。测试只使用 <code>%TEMP%\cdsi-agent-tests\&lt;随机目录&gt;</code>，不会扫描或清理真实用户目录，也不会连接真实 OSS。

## 下一阶段

下一阶段将增强对象存储的身份与运维能力：

- OSS 连接测试和最小权限检查
- 优先接入 CDSI Server 下发的 STS 临时凭证
- 上传队列、并发数、带宽限制和后台优先级
- 定期重新验证、缺失提醒和显式修复任务
- S3 兼容存储适配器，并继续保持外部扫描资产不自动上传

后续再扩展 PDF/Office 文本提取和 Inbox 复核。AI 分类、自动文件整理和任何静默上传仍不在当前阶段。
