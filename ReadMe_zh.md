## 可视化对话编辑器

[English Version](ReadMe.md)

这是一个基于 [xNode](https://github.com/Siccity/xNode) 的简单可视化对话编辑器，支持对话、选项、事件、条件分支和多语言。如果你使用过 Visual Scripting 或 Amplify Shader Editor，应该会比较熟悉这类节点编辑操作。

### 创建

在 Project 窗口右键选择 **Create > Dialogue Graph**，创建对话图，双击资产打开编辑窗口。

编辑之前，可以先通过顶部的 **Languages** 打开 **Project Settings > Dialogue**，添加需要的语言；通过 **Speakers** 打开当前图的角色窗口，添加角色名称。语言由项目共享，角色属于各自的对话图。

右上角的 **Language** 切换当前编辑语言，可以分别填写对话、选项和角色名称的译文。索引 0 为默认语言，其他语言的文本留空时，播放会回退到默认语言。默认语言可重命名，但不能移动或删除；其他语言的删除、排序会同步所有已保存对话图中的译文。角色排序会更新对话中的角色引用，删除角色后，使用它的对话改为无角色。

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (1).png" style="width: 33%; object-fit: contain;" />
  <img src="img (2).png" style="width: 33%; object-fit: contain;" />
  <img src="img (3).png" style="width: 33%; object-fit: contain;" />
</div>

### 编辑

在画布空白处右键创建节点，通过列表的添加按钮增加条目，再连接端口组织对话流程。编辑器中有四类节点：

| 节点 | 用途 |
|------|------|
| Dialogue | 对话节点，每行设置 Sprite、角色和正文。 |
| Option | 选项节点，展示选项，沿玩家选中项的连线跳转。 |
| Event | 事件节点，调用场景组件的公开实例方法，支持无参数或一个 `int`、`float`、`bool`、`string`、枚举参数。 |
| Check | 条件节点，读取场景组件的公开实例字段，支持上述类型，沿首个满足条件的分支跳转。 |

对话和事件的每个条目都可以单独设置端口：`None` 无端口、`Input` 仅输入、`Output` 仅输出、`All` 同时有输入和输出。连到某个条目的输入，就从该条目开始。选项和条件节点从节点的 `Before` 输入进入，各项分别提供输出；条件支持等于、不等于、大于、小于。

首个创建的节点自动成为入口，也可以右键节点选择 **Set as Entry** 更换。**条目有输出端口但没有连接时，对话结束；没有输出端口时，顺序执行下一项，列表结束则结束对话。** 条件节点没有匹配条件时也会结束。事件和条件会连续同步执行，不需要逐次点击，但应避免无法退出的自动循环。

Sprite 直接通过对象字段选择。事件和条件先选择已打开场景中的目标物体，再选择组件的方法或字段。目标以 `场景名/根物体/子物体` 路径保存，运行时也需要加载该场景；更改目标名称、层级或绑定成员后，需要重新选择。

如果节点太长，可以点击 **Fold** 折叠，只显示摘要，原有连线仍然保留；点击 **Expand** 展开后继续编辑和连接。节点绿色表示入口，黄色表示正在播放，红色表示有问题，问题状态优先显示；条目也会通过黄色或红色边线标示播放位置或问题。悬停顶部问题数量可查看明细。目标场景未加载时暂不检查组件绑定，需打开场景后确认。

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (4).png" style="width: 49%; object-fit: contain;" />
  <img src="img (5).png" style="width: 49%; object-fit: contain;" />
</div>

### 使用

先加载或在 Inspector 中引用制作好的 `DialogueGraph` 资产，再实例化使用，不要直接推进资产本身。通常只需要 `Next()`、`dialogueInfo` 和 `optionInfo`：`Next()` 返回当前步骤类型，随后读取对应的数据交给自己的 UI。

下面是读取首项结果的示例，`asset` 是你引用的 `DialogueGraph` 资产：

```csharp
using DialogueEditor;
using UnityEngine;

DialogueGraph graph = Object.Instantiate(asset);
switch (graph.Next())
{
    case DialogueGraph.DataType.Dialogue:
        Sprite sprite = graph.dialogueInfo.sprite;
        string text = graph.dialogueInfo.name + ":\n" + graph.dialogueInfo.context;
        // 将 sprite 和 text 显示到你的 UI。
        Debug.Log(text);
        break;
    case DialogueGraph.DataType.Option:
        for (int i = 0; i < graph.optionInfo.Count; i++)
            Debug.Log(i + ": " + graph.optionInfo[i]);
        // 显示选项，选择后调用 graph.Next(choiceIndex)。
        break;
    case DialogueGraph.DataType.End:
        // 关闭对话 UI。
        break;
}
```

首次 `Next()` 从入口开始，后续对话点击调用 `Next()` 或 `Next(-1)` 继续；选择选项时调用 `Next(choiceIndex)`，序号从 0 开始。每次调用后都按上面的方式处理返回结果。**显示选项时应等待玩家选择，不要再用无参数的 `Next()` 推进**，这部分由你自己的 System 控制。

切换语言使用 `DialogueSettings.Language = languageIndex`，序号对应 Project Settings 中的语言列表。订阅 `DialogueSettings.LanguageChanged`，在回调中调用 `graph.GetInfo()` 并刷新 UI，即可更新当前文本而不推进剧情；不再使用时取消订阅。语言由项目共享，Play 中切换不会改写配置资产，请保留唯一的 `Resources/DialogueSettings.asset`。

停止并释放播放实例时，调用 `graph.Stop()`，再 `Object.Destroy(graph)`。重新开始或切换图时创建新实例。

#### 跨图跳转

`Demo/DialogueSystem.cs` 只是一个接入示例，不是编辑器的依赖，可以按自己的需要编写 System。示例使用 uGUI 的 `Image` 和 `Text`，配置 `cha`（角色图片）、`bg`（含文字的对话框）、`options`（含文字的选项物体数组）和 `graphs`（图资产数组），即可参考 Demo 场景运行。

示例的 `Init(0)` 开始第一张图，`Next()` 推进对话，`Next(0)` 选择第一个选项；等待选项时会忽略无参数的 `Next()`。`Start()` 已调用 `Init()`，语言切换也已接好刷新。

要通过事件跨图，在 Event 节点选择挂载该 `DialogueSystem` 的物体，选择 **Init(int)**，参数填目标图在 `graphs` 中的序号。它会停止并释放旧实例，立即显示新图首项；跳回同一张图则重新开始。自定义 System 也可以公开自己的切图方法，但应先停止旧实例，避免事件返回后旧流程继续执行。
