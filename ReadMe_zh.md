## 可视化对话编辑器

[English Version](ReadMe.md)

这是一个基于 [xNode](https://github.com/Siccity/xNode) 的简单可视化对话编辑器，支持对话、选项、事件、条件分支和多语言。如果你使用过 Visual Scripting 或 Amplify Shader Editor，应该会比较熟悉这类节点编辑操作。

### 创建

在 Project 窗口右键选择 **Create > Dialogue Graph**，创建对话图，双击资产打开编辑窗口。

编辑之前，可以通过顶部的 **Settings** 打开 **Dialogue Settings**，先在上方添加语言，再在下方选择对话图和语言，编辑角色名称。语言由项目共享，角色属于各自的对话图；未选择对话图时，仍可编辑语言。

右上角的 **Language** 切换当前编辑语言，可以分别填写对话、选项和角色名称的译文。索引 0 为默认语言，译文留空时播放也显示空文本，不回退。默认语言可重命名，但不能移动或删除；其他语言的删除、排序会同步所有已保存对话图中的译文。新建对话和选项条目时，编辑器会按语言数量初始化译文列表。角色排序会更新对话中的角色引用，删除角色后，使用它的对话改为无角色。

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (1).png" style="width: 33%; object-fit: contain;" />
  <img src="img (2).png" style="width: 33%; object-fit: contain;" />
  <img src="img (3).png" style="width: 33%; object-fit: contain;" />
</div>

### 编辑

在画布空白处右键创建节点，默认带一个空条目，再连接端口组织对话流程。点击列表添加按钮时，有选中条目就复制，否则新建空条目。编辑器中有四类节点：

| 节点 | 用途 |
|------|------|
| Dialogue | 对话节点，每行设置 Sprite、角色和正文。 |
| Option | 选项节点，展示选项，沿玩家选中项的连线跳转。 |
| Event | 事件节点，调用场景组件的公开实例方法，支持无参数或一个 `int`、`float`、`bool`、`string`、枚举参数。 |
| Check | 条件节点，读取场景组件的公开实例字段，支持上述类型，沿首个满足条件的分支跳转。 |

对话和事件的每个条目都可以单独设置端口：`None` 无端口、`Input` 仅输入、`Output` 仅输出、`All` 同时有输入和输出。连到某个条目的输入，就从该条目开始。选项和条件节点从节点的 `Before` 输入进入，各项分别提供输出；条件支持等于、不等于、大于、小于。

首个创建的节点自动成为入口，也可以右键节点选择 **Set as Entry** 更换。**条目有输出端口但没有连接时，对话结束；没有输出端口时，顺序执行下一项，列表结束则结束对话。** 条件节点没有匹配条件时也会结束。事件和条件会连续同步执行，不需要逐次点击，但应避免无法退出的自动循环。

Graph 会在绑定目标时记住该场景，打开 Graph 资产时自动打开它，已加载则仅设为活动场景；替换场景前会询问未保存修改，取消后保留当前场景。Play 中不自动打开场景；该记录只存在于编辑器，运行时不会加载它。

如果节点太长，可以点击 **Fold** 折叠，只显示摘要，原有连线仍然保留；点击 **Expand** 展开后继续编辑和连接。节点绿色表示入口，黄色表示正在播放，红色表示有问题，问题状态优先显示；条目也会通过黄色或红色边线标示播放位置或问题。悬停顶部问题数量可查看明细。没有相关场景或 System 时，已有绑定显示为 **unverified（未验证）**，不会把节点全部标红，也不会当作校验通过；未配置目标、重复 System 和相关场景中的实际绑定错误仍会报错。

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (4).png" style="width: 49%; object-fit: contain;" />
  <img src="img (5).png" style="width: 49%; object-fit: contain;" />
</div>

### 使用

将制作好的 `DialogueGraph` 资产配置到 `DialogueSystemBase` 或派生 System，由 System 创建播放实例，不要直接推进资产本身。通过 `system.Next()` 推进，再读取 `system.graph.dataType`、`dialogueInfo` 和 `optionInfo` 交给自己的 UI；`Next()` 没有返回值。

#### 跨图跳转

要通过事件跨图，在 Event 节点选择挂载 `DemoDialogueSystem` 或自定义派生 System 的物体，选择继承的 **Switch(int)**，参数填目标图在 `graphs` 中的序号。Event 内只调用 `Switch()`，不要再调用 `Next()`：外层已有的 `Next()` 循环会启动新图，结束后返回父图，并继续到对话、选项或整个流程结束。支持嵌套图和只有事件的子图，不再递归推进。把事件放在流程末尾，返回后父图即结束。切到同一张资产会开始新实例。自定义切图方法应修改 `graph`，不要在内部调用 `Next()`。
