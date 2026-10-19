## 可视化对话编辑器

[English Version](ReadMe.md)

基于 [xNode](https://github.com/Siccity/xNode) 的 Unity 可视化对话编辑器，支持对话、选项、事件、条件分支、多语言及编辑器内 JSON/YAML 导入导出。**运行时仍以 Graph 资产作为播放源。**

### 安装与依赖

**YamlDotNet.Uni 为可选依赖**：未安装时可正常编辑 Graph 和播放对话，仅禁用 JSON/YAML 导入导出。可在 Graph Inspector 或图窗口 **Edit > Install YamlDotNet** 点击安装，确认后通过 Unity Package Manager 从 GitHub 下载；需要 Git 和网络，安装进度及失败信息会显示在编辑器中，编译完成后自动启用导入导出。

也可通过 **Add package from git URL** 手动安装：`https://github.com/epishev-m/YamlDotNet.UniEx.git`。导入本插件的 `.unitypackage` 不会自动联网安装依赖。

### 创建与编辑

在 Project 窗口右键选择 **Create > Dialogue Graph**，双击资产打开。可在 Graph Inspector 或图窗口 **Edit > Edit Languages / Edit Speakers** 中管理语言和角色。右上角语言下拉框切换正在编辑的文本；缺失的翻译回退到默认语言。

非默认语言文本旁的勾选框表示启用该翻译，允许勾选后留空以显示空文本；取消勾选则回退。输入文本会自动勾选。旧数据中的空翻译保留原来的回退行为。播放期间 Graph 只读，包括节点、条目、连线和语言/角色配置；仍可查看播放高亮、平移和缩放。

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
- **引用保留**：Sprite、场景对象、组件或成员失效时保留配置，在原位置标错，修复引用即可，不自动清空整项。
- **未加载场景**：目标场景未加载时只显示暂缓检查的提示，不将绑定标红；保留路径和成员配置，打开原场景后重新检查。场景已加载但目标失效时仍报错，不自动加载场景。
- **图片选择**：直接点击 Sprite 预览选图，也可拖入 Sprite 或右键清空。图片预览和状态边框跟随 Graph 缩放。

`Demo` 中的两张演示图已更新为新的入口与折叠逻辑；不提供旧折叠表示的自动迁移。

### 运行时接入

**Graph 负责图内推进和提供结果，DialogueSystem 只负责选图与 UI。** Graph 的 `Next()` 通过 `Init()`、`MoveOn()`、`GetNodeAndIndex()` 和 `GetInfo()` 完成入口初始化、节点推进、事件调用、条件跳转和数据读取。Event 的 `InvokeMethod()` 与 Check 的 `GetNextStr()` 保留在各自 Node 中。

与原版一样，播放前先实例化资产：`Instantiate(graphs[graphIndex])`。当前节点、条目索引、播放语言和读取结果保存在这个播放实例中，Node 定义只读，不写入共享节点的活动标记；多个 System 可播放同一份资产而互不覆盖进度。

将 `Demo/DialogueSystem.cs` 挂到场景物体，配置 `cha`、`bg`、`options` 和 `graphs`。`Start()` 调用 `Init()`，切换到首张图并显示第一项；后续点击对话调用 `Next()`，选项按钮调用 `Next(choiceIndex)`，序号从 0 开始。等待选项时 `Next(-1)` 不推进。不需要 `Update()` 或跨帧续跑。

```csharp
dialogueSystem.Init(0); // 切换/重启指定图，并显示第一项
dialogueSystem.Next();  // 点击对话时推进
dialogueSystem.Next(1); // 选择第二个选项时调用
```

System 与原版一样，用一个 `bool option` 等待选择，根据 Graph 返回的 `DataType.Dialogue / Option / End` 更新 UI，不遍历或判断具体节点。`graphs` 保存资产列表，非序列化的 `graph` 指向当前播放实例；播放位置在 `graph.node` 和 `graph.index` 中。编辑器通过入口节点所属的资产关联播放高亮，不把播放实例与资产直接比较。配置问题仍由编辑器校验并标红，执行异常直接在 Unity Console 报错，演示脚本不另设错误处理流程。

自行接入 UI 时，调用播放实例的 `Next(num)`，根据返回类型读取 `dialogueInfo.sprite / name / context` 或 `optionInfo`；播放语言通过实例的 `languageIndex` 设置，序号对应 `Languages`。切换前调用实例的 `Stop()` 结束旧流程，再销毁实例；不要直接推进 Graph 资产。

### 通过 Event 跨 Graph 跳转

`DialogueSystem` 中提供 `DialogueGraph[] graphs`、`Init(int graphIndex = 0)` 和 `Next(int num = -1)`。将首张图放在 `graphs[0]`；`Init(index)` 结束并释放旧播放实例，实例化目标图并显示第一项，无需再额外调用 `Next()`。Language 位于 Inspector 的 Bg 与 Options 之间，直接将所选索引用于目标图，因此各图的语言顺序需保持一致。

Language 是从 `graphs[0].Languages` 动态绘制的下拉，底层保存整数 `languageIndex`，并非 C# enum。`DialogueSystemEditor` 负责生成这个下拉，不只是调整字段顺序；删除它会使语言选择 UI 消失。

跨图时，在 Event 节点选择挂载自身 `DialogueSystem` 的物体，选择 **Init(int)**，整数参数填写目标图在 `graphs` 中从 0 开始的序号。`Init` 立即切图并显示首项；事件返回后不再执行旧图剩余条目与连线。跳回当前图则重新开始，不需要新增跳转节点。

### JSON / YAML 导入导出

非播放状态下，在 Graph Inspector 或图窗口 **Edit** 菜单选择 **Import** 或 **Export**。导入支持 `.json`、`.yaml`；Windows 下在导出保存窗口的**保存类型**下拉框选择 JSON 或 YAML，扩展名和实际编码跟随所选类型。其他编辑器平台先选择格式，再选择保存位置。导入需要已保存的 Graph，会确认后替换其内容、保留资产自身身份，并支持 Undo。先解析并校验结构，格式错误不会覆盖原图；引用失效会保留配置并报错，供手动修复。

两种格式仅用于**编辑器内交换配置**，以方便使用AI等工具导入对话，不作为运行时加载源。共用下述字段结构，导入一种格式后可导出另一种，实现互转。

#### 最小示例

```json
{
  "schemaVersion": 1,
  "entryNodeId": "start",
  "defaultLanguage": "zh",
  "languages": ["zh", "en"],
  "speakers": [],
  "nodes": [
    {
      "id": "start",
      "type": "dialogue",
      "lines": [
        {
          "id": "greeting",
          "portType": "output",
          "text": { "zh": "继续吗？", "en": null },
          "next": { "nodeId": "choice", "itemId": null }
        }
      ]
    },
    {
      "id": "choice",
      "type": "option",
      "options": [
        {
          "id": "again",
          "text": { "zh": "再来一次" },
          "next": { "nodeId": "start", "itemId": null }
        },
        {
          "id": "finish",
          "text": { "zh": "结束" },
          "next": null
        }
      ]
    }
  ]
}
```

```yaml
schemaVersion: 1
entryNodeId: start
defaultLanguage: zh
languages: [zh, en]
speakers: []
nodes:
  - id: start
    type: dialogue
    lines:
      - id: greeting
        portType: output
        text:
          zh: 继续吗？
          en: null
        next: {nodeId: choice, itemId: null}
  - id: choice
    type: option
    options:
      - id: again
        text: {zh: 再来一次}
        next: {nodeId: start, itemId: null}
      - id: finish
        text: {zh: 结束}
        next: null
```

多行文本可用 `|`。看起来像数字、布尔值或空值的字符串需要加引号，例如 `"001"`、`"true"`、`"null"`。数字使用 JSON 十进制/指数写法；布尔值为 `true` / `false`，空值可写 `null`、`~` 或留空。支持注释，但导出不保留注释；YAML 读取器也接受 JSON 语法。本交换格式支持单文档、字符串键映射、序列和标量，不支持 YAML 锚点、别名及显式标签。

#### 共用格式说明

| 范围 | 字段及约束 |
|------|------------|
| 顶层 | 必填 `schemaVersion: 1`、`entryNodeId`、`defaultLanguage`、非空 `languages`、非空 `nodes`。`speakers` 可省略，默认为 `[]`。 |
| 角色 | 必填 `id` 和 `names`；后者是语言到文本的对象，例如 `{"zh":"向导","en":null}`。 |
| 所有节点 | 必填唯一 `id`、`type`（`dialogue`、`option`、`event`、`check`）。`name`、`editor` 可选。 |
| 编辑器信息 | `editor` 内可选 `position: [x, y]`、`collapsed: true/false`、`summary: "..."`，不影响执行。 |
| 对话 | 必填非空 `lines`；每项必填 `id`、`portType`、`text`、`next`，可选 `speakerId`、`sprite`。 |
| 选项 | 必填非空 `options`；每项必填 `id`、`text`、`next`。 |
| 事件 | 必填非空 `events`；每项必填 `id`、`portType`、`target`、`componentType`、`method`、`next`。`parameter` 可选，无参数方法可省略或填 `null`。 |
| 条件 | 必填 `target`、`componentType`、`field`、`valueType`、非空 `conditions`；`enumType` 可选。每个条件必填 `id`、`operator`、`value`、`next`。 |
| 连接 | `next` 必须出现，可为 `null`。对话/事件仅接受条目输入，指定 `itemId` 时必须指向 input/both 条目；选项/条件保留节点输入，`itemId` 为 `null`。兼容旧文档：对话/事件目标省略或填 `null` 的 `itemId` 时，导入到首条 Item 并补上 Input 标记；新导出使用明确条目 ID。`next: null` 对输出分支表示结束；对 normal/input 对话或事件条目则表示不显式跳转，继续按列表顺序执行。每个输出最多连接一个目标。 |
| 端口 | `portType` 为 `normal`、`input`、`output`、`both`；仅 `output` / `both` 允许非空 `next`。 |
| 多语言文本 | `text` / `names` 必须包含默认语言且值非 `null`，`""` 是合法文本；其他翻译可省略或填 `null` 以回退。语言键必须已在 `languages` 声明。 |
| Sprite | 可选 `sprite: {"guid":"32位资产GUID","fileId":21300000}`，GUID 和本地文件 ID 必须成对提供。需使用实际资产及 Sprite 子资产的标识。 |
| 场景目标 | `target: {"globalObjectId":null,"path":"Demo Scene/Canvas/ChatPanel"}`，至少提供一种标识。路径以场景名开头，后接根对象/子对象名。编辑器对象 ID 与项目相关；运行时可回退到路径，跨项目移动场景/资产后需修复引用。引用冲突或路径歧义会报错。 |
| 值与参数 | 支持 `int`、`float`、`double`、`bool`、`string`、`enum`。参数示例：`{"type":"int","enumType":null,"value":1}`。数字、布尔值使用 JSON 原生类型，不加引号。枚举须填写完整 `enumType` 和整数值；其他类型的 `enumType` 应省略或为 `null`。字符串值允许 `null`。 |
| 比较符 | `equal`、`notEqual`、`greater`、`less`，按条件列表顺序匹配。 |

`componentType` 应能唯一定位组件类型，可先导出已有绑定获取类型名。节点 ID 在图内唯一，条目 ID 在所属节点内唯一。两种格式都会拒绝重复属性、未知字段、值类型不匹配及无效节点/条目连接。可选字段可省略，允许为空的字段也可填 `null`；导出会包含可选值，因此可能比最小示例更详细。

#### Event / Check 示例

以下完整文档使用演示场景中 `DialogueSystem.Print(int)` 方法和 `cnt` 字段。打开演示场景即可解析绑定；在其他场景中需替换为实际路径、组件和成员名称。YAML 使用相同字段。

```json
{
  "schemaVersion": 1,
  "entryNodeId": "call",
  "defaultLanguage": "zh",
  "languages": ["zh"],
  "nodes": [
    {
      "id": "call",
      "type": "event",
      "events": [{
        "id": "print",
        "portType": "output",
        "target": { "path": "Demo Scene/Canvas/ChatPanel" },
        "componentType": "DialogueEditor.DialogueSystem",
        "method": "Print",
        "parameter": { "type": "int", "value": 1 },
        "next": { "nodeId": "check", "itemId": null }
      }]
    },
    {
      "id": "check",
      "type": "check",
      "target": { "path": "Demo Scene/Canvas/ChatPanel" },
      "componentType": "DialogueEditor.DialogueSystem",
      "field": "cnt",
      "valueType": "int",
      "conditions": [{
        "id": "zero", "operator": "equal", "value": 0, "next": null
      }]
    }
  ]
}
```

#### 使用 AI 生成配置

将本 README、所需对话与分支，以及涉及的真实场景路径、组件类型、方法/字段和资产标识交给 AI。上方对话/选项示例与绑定示例已覆盖四类节点，可附上以下生成要求：

> 输出一个完整的 schemaVersion 1 文档，仅输出 JSON 或 YAML 原文，不要 Markdown 代码围栏。只使用文档列出的字段，类型/端口/比较符名称严格区分大小写。ID 使用唯一非空字符串，entryNodeId 和所有 next 引用必须存在。每个对话、选项、事件和条件条目都要写 next，即使为 null。对话/事件条目的双向端口用 both，不要写 all；仅 output/both 允许非空 next。选项与条件条目不写 portType。itemId 目标必须有输入端口。语言键与 languages 完全一致，默认语言文本不得为 null，缺失翻译省略或填 null。未使用的 speakerId、sprite 省略或填 null，不要虚构 GUID、fileId、对象 ID、场景路径及组件/成员名称。只有提供真实绑定信息时才生成 Event/Check 绑定。跨 Graph 跳转仍使用普通 Event，调用 DialogueEditor.DialogueSystem.Init，参数为 int，不要新增节点类型或给文档添加 graphs 字段。

YAML 中像数字、布尔值或空值的文本需加引号；空字符串表示显示空文本，不触发翻译回退。JSON 不写注释或尾随逗号。能否导入可通过结构校验确认，但绑定能否运行取决于实际场景和资产；AI 无法仅凭 README 推断项目专属标识。

### 文件结构

- `Scripts`：Graph 定义、四类节点、多语言数据与结构/绑定校验。
- `Editor`：Graph/节点界面、Inspector、语言/角色管理及引用选择。
- `Editor/Serialization`：共用文档模型、JSON/YAML 编解码、Graph 映射与导入导出。
- `Demo`：演示场景、两张 Graph、读取结构并执行对话的 DialogueSystem、UI 和图片。
- `xNode`：随附图编辑框架；第三方声明见 `ThirdPartyNotice.txt`。
