## 可视化对话编辑器

[English Version](ReadMe.md)

基于 [xNode](https://github.com/Siccity/xNode) 的 Unity 可视化对话编辑器，支持对话、选项、事件、条件分支和多语言。在 Unity 内直接编辑对话，运行时以 Graph 资产作为播放源。

### 创建与编辑

在 Project 窗口右键选择 **Create > Dialogue Graph**，双击资产打开。语言统一在 **Project Settings > Dialogue Editor** 管理，图窗口 **Edit > Edit Languages** 打开同一设置。角色仍属于各自 Graph，在 Graph Inspector 或 **Edit > Edit Speakers** 中管理。右上角语言下拉框切换项目当前语言，对全部 Graph 生效；缺失的翻译回退到默认语言。

顶部工具栏不随画布缩放或平移。工具栏绘制及播放期编辑保护均位于插件自身的 `Editor` 目录；随附的 `xNode` 目录恢复为 `9402be4` 基线，不包含后加的只读补丁。

非默认语言的对话文本、选项或角色名称留空时，统一回退到默认语言。播放期间 Graph 内容和语言结构只读；仍可查看播放高亮、平移、缩放和切换当前语言。切换语言立即刷新已显示的对话或选项，不推进剧情。

项目语言和 Graph 角色均使用可排序列表。默认语言单独固定在语言列表上方，不能移动或删除。新增、删除、排序语言时，同步所有已保存 Graph 的文本槽位，包括未打开的资产；删除需确认。遇到只读资产会拒绝整个结构操作，Undo 同时恢复设置和受影响的图。角色的增删、排序在默认语言下进行，排序会同步更新对话中的角色引用。

| 节点 | 用途 |
|------|------|
| Dialogue | 展示 Sprite、角色名称和文本；条目可配置输入、输出端口。 |
| Option | 展示选项，沿选中项的连线跳转。 |
| Event | 调用组件的公开方法，支持无参数或一个受支持类型的参数。 |
| Check | 比较组件的公开字段，沿首个满足条件的分支跳转。 |

- **入口**：首个创建的节点自动成为入口，也可右键其他节点选择 **Set as Entry**。
- **跳转**：每个输出端口最多连接一个目标。输出端口未连接时结束；对话/事件条目没有输出端口时，按列表顺序执行下一项。条件节点没有匹配分支时结束。
- **循环合法**：事件/条件节点同步执行，不设步数限制、不跨帧续跑。需自行保证自动循环最终能退出；纯自动死循环会阻塞主线程。
- **折叠**：点击 **Show less** 只展示摘要，各项连线改由节点的 `before`/`after` 端口显示，不改写连线；编辑连接需先展开。展开状态不显示摘要字段。
- **边框状态**：入口绿色、当前播放节点/条目黄色、错误红色，不改变节点背景色；节点边框优先显示错误状态。
- **引用保留**：Sprite 直接使用 Unity 资产引用；场景对象、组件或成员失效时保留配置并在原位置标错。保留场景对象 ID 和路径用于引用恢复，图内内容不再维护额外的字符串 ID。
- **未加载场景**：目标场景未加载时只显示暂缓检查的提示，不将绑定标红；保留路径和成员配置，打开原场景后重新检查。场景已加载但目标失效时仍报错，不自动加载场景。
- **图片选择**：直接点击 Sprite 预览选图，也可拖入 Sprite 或右键清空。图片预览和状态边框跟随 Graph 缩放。

`Demo` 中的两张演示图已更新为新的入口与折叠逻辑；不提供旧折叠表示的自动迁移。

### 运行时接入

**Graph 负责图内推进和提供结果，DialogueSystem 只负责选图与 UI。** Graph 的 `Next()` 初始化入口，再通过 `MoveOn()`、`GetNodeAndIndex()` 和 `GetTypeInfo()` 完成节点推进、事件调用、条件跳转和数据读取。Event 的 `InvokeMethod()` 与 Check 的 `GetNextStr()` 保留在各自 Node 中。

与原版一样，播放前先实例化资产：`Instantiate(graphs[graphIndex])`。当前节点、条目索引和读取结果保存在这个播放实例中，当前语言由项目共享。Node 定义只读，不写入共享节点的活动标记；多个 System 可播放同一份资产而互不覆盖进度。

将 `Demo/DialogueSystem.cs` 挂到场景物体，配置 `cha`、`bg`、`options` 和 `graphs`。`Start()` 调用 `Init()`，切换到首张图并显示第一项；后续点击对话调用 `Next()`，选项按钮调用 `Next(choiceIndex)`，序号从 0 开始。等待选项时 `Next(-1)` 不推进。不需要 `Update()` 或跨帧续跑。

```csharp
dialogueSystem.Init(0); // 切换/重启指定图，并显示第一项
dialogueSystem.Next();  // 点击对话时推进
dialogueSystem.Next(1); // 选择第二个选项时调用
```

System 与原版一样，用一个 `bool option` 等待选择，根据 Graph 返回的 `DataType.Dialogue / Option / End` 更新 UI，不遍历或判断具体节点。`graphs` 保存资产列表，非序列化的 `graph` 指向当前播放实例；播放位置在 `graph.node` 和 `graph.index` 中。编辑器通过入口节点所属的资产关联播放高亮，不把播放实例与资产直接比较。配置问题仍由编辑器校验并标红，执行异常直接在 Unity Console 报错，演示脚本不另设错误处理流程。

自行接入 UI 时，调用播放实例的 `Next(num)`，根据返回类型读取 `dialogueInfo.sprite / name / context` 或 `optionInfo`。设置 `DialogueSettings.Language` 切换当前语言，序号对应 `DialogueSettings.Instance.languages`。订阅 `DialogueSettings.LanguageChanged`，在回调中调用实例的 `GetTypeInfo()` 并重绘当前结果，不调用 `Next()`；禁用时取消订阅。Demo System 已接好此流程。切图前调用实例的 `Stop()` 结束旧流程，再销毁实例；不要直接推进 Graph 资产。

对话正文、选项和角色名称统一使用 `LocalizedText`，一个字符串列表按项目语言序号存储全部文本，索引 0 为默认语言。`Get(language)` 带回退读取，`GetRaw(language)` 读取待编辑的原始槽位，`Set(language, value)` 写入文本；播放结果仍是普通字符串。读取不会初始化或修改文本数据。`GetSpeaker(index, language)`、`SetSpeakerName(index, language, value)` 显式接收语言序号；`AddSpeaker(name)` 接收角色名。`EnsureLocalizationData()` 在打开/编辑图或添加内容时对齐文本位置，不在读取时调用。语言目录只通过 Project Settings 修改，确保全部 Graph 对齐。只支持当前数据格式，不再迁移旧译文或旧程序集名称。

唯一的 `Resources/DialogueSettings.asset` 保存项目语言列表和编辑时的选择，后者也是播放初始语言。运行中的选择不序列化，每次进入 Play 都重置，关闭 Domain Reload 时也一样；切换不会改写配置资产。请勿复制出第二份配置。两张示例图均包含 English（索引 0）和 Chinese（索引 1）文本。

### 通过 Event 跨 Graph 跳转

`DialogueSystem` 中提供 `DialogueGraph[] graphs`、`Init(int graphIndex = 0)` 和 `Next(int num = -1)`。将首张图放在 `graphs[0]`；`Init(index)` 结束并释放旧播放实例，实例化目标图并显示首项，无需再额外调用 `Next()`。切换 Graph 时自动沿用项目当前语言。

Graph 和 System 不再各自序列化语言选择。共享语言下拉位于图窗口和 Project Settings；System 使用 Unity 默认 Inspector。

Graph 画布状态、固定工具栏、Graph Inspector / Speaker 列表分别由 `DialogueGraphEditor`、`DialogueGraphToolbar`、`DialogueGraphInspector` 负责。节点画布编辑器共用 `DialogueNodeEditorBase`；`DialogueNodeAssetEditor` 仅负责节点资源的 Edit graph 按钮，以及播放时 Inspector 字段只读。

跨图时，在 Event 节点选择挂载自身 `DialogueSystem` 的物体，选择 **Init(int)**，整数参数填写目标图在 `graphs` 中从 0 开始的序号。`Init` 立即切图并显示首项；事件返回后不再执行旧图剩余条目与连线。跳回当前图则重新开始，不需要新增跳转节点。

### 文件结构

- `Scripts`：Graph 定义、四类节点、多语言数据与结构/绑定校验。
- `Resources`：项目共享的唯一对话配置资产。
- `Editor`：Graph/节点界面、Inspector、语言/角色管理及引用选择。
- `Demo`：演示场景、两张 Graph、读取结构并执行对话的 DialogueSystem、UI 和图片。
- `xNode`：随附图编辑框架；第三方声明见 `ThirdPartyNotice.txt`。
