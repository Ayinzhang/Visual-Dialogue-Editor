## Visual Dialogue Editor

[中文版](ReadMe_zh.md)

This is a simple Unity visual dialogue editor based on [xNode](https://github.com/Siccity/xNode), with dialogue, choices, events, conditions, and localization. If you have used Visual Scripting or Amplify Shader Editor, the node editing workflow should feel familiar.

### Create

Right-click in the Project window and choose **Create > Dialogue Graph**, then double-click the asset to open the editor.

Before editing, use **Languages** in the top toolbar to open **Project Settings > Dialogue** and add your languages. Use **Speakers** to open the current graph's speaker window and add speaker names. Languages are shared across the project; speakers belong to individual graphs.

Use **Language** in the upper-right corner to edit dialogue, choices, and speaker names in each language. Index 0 is the default language; empty translations fall back to it during playback. The default language can be renamed but not moved or removed. Removing or reordering other languages updates translations in all saved dialogue graphs. Reordering speakers updates dialogue references; removing a speaker leaves its dialogue lines without a speaker.

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (1).png" style="width: 33%; object-fit: contain;" />
  <img src="img (2).png" style="width: 33%; object-fit: contain;" />
  <img src="img (3).png" style="width: 33%; object-fit: contain;" />
</div>

### Edit

Right-click an empty area of the canvas to create a node, add items using its list's add button, and connect ports to build the dialogue flow. There are four node types:

| Node | Purpose |
|------|---------|
| Dialogue | Set a Sprite, speaker, and text for each dialogue line. |
| Option | Present choices and follow the player's selected choice. |
| Event | Invoke a public instance method on a scene component, with no argument or one `int`, `float`, `bool`, `string`, or enum argument. |
| Check | Read a public instance field on a scene component, using the same supported types, and follow the first matching condition. |

Each Dialogue and Event item can configure its own ports: `None` for no ports, `Input` for input only, `Output` for output only, and `All` for both. Connecting to an item's input starts at that item. Option and Check nodes use a node-level `Before` input with separate outputs for their items. Checks support equal, not equal, greater than, and less than.

The first created node becomes the entry; right-click a node and choose **Set as Entry** to change it. **An item with an unconnected output ends playback. An item without an output advances to the next item, or ends playback at the end of its list.** A Check node with no matching condition also ends playback. Events and checks run synchronously without individual clicks, so avoid automatic cycles that cannot exit.

Select Sprites through their object fields. For events and checks, keep one Dialogue System in the active scene, drag a scene object into Target, then select its component method or field as before. The editor automatically creates a hidden logical key in the Graph and stores the component reference in the scene's System; no key entry or extra target scripts are needed. Save both the Graph and the scene. Renaming objects, moving them in the hierarchy, or renaming the scene does not break these references; deleting a target or changing a bound member still requires rebinding. Targets must belong to the active scene. Scene bindings cannot be edited during Play.

The Graph silently remembers the saved scene of its first target assignment. Opening the Graph asset opens that scene, or makes it active if already loaded. Before replacing scenes, Unity asks about unsaved changes; Cancel leaves the current scenes intact. Automatic scene opening is disabled during Play, and the record is editor-only: runtime playback never loads it.

With additive scenes, make the intended scene active. To reuse a Graph in another scene, assign it to that scene's System and drag replacement targets into the existing nodes; missing keys are automatically mapped without changing the Graph's logical references or its remembered scene. Replacement components must have the same types and members.

If a node is too long, click **Fold** to show only its summary while keeping its connections. Click **Expand** to edit and connect it again. Node colors indicate entry (green), current playback (yellow), and issues (red), with issues taking priority. Items also use yellow or red side borders for playback or issues. Hover over the issue count in the top toolbar for details. When no relevant scene/System is available, saved bindings are marked **unverified**, not colored red or treated as valid. Unassigned targets, duplicate Systems, and broken bindings in the relevant scene remain errors.

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (4).png" style="width: 49%; object-fit: contain;" />
  <img src="img (5).png" style="width: 49%; object-fit: contain;" />
</div>

### Use

Load a `DialogueGraph` asset or assign it in the Inspector, then instantiate it before use. Do not advance the asset itself. Generally, you only need `Next()`, `dialogueInfo`, and `optionInfo`: `Next()` returns the current step's type, and you read the corresponding data for your own UI.

This example reads the first result, where `asset` is your referenced `DialogueGraph` asset:

```csharp
using DialogueEditor;
using UnityEngine;

DialogueGraph graph = Object.Instantiate(asset);
switch (graph.Next())
{
    case DialogueGraph.DataType.Dialogue:
        Sprite sprite = graph.dialogueInfo.sprite;
        string text = graph.dialogueInfo.name + ":\n" + graph.dialogueInfo.context;
        // Display sprite and text in your UI.
        Debug.Log(text);
        break;
    case DialogueGraph.DataType.Option:
        for (int i = 0; i < graph.optionInfo.Count; i++)
            Debug.Log(i + ": " + graph.optionInfo[i]);
        // Show choices, then call graph.Next(choiceIndex) after selection.
        break;
    case DialogueGraph.DataType.End:
        // Close the dialogue UI.
        break;
}
```

The first `Next()` starts at the entry. Subsequent dialogue clicks call `Next()` or `Next(-1)`; choices call `Next(choiceIndex)` with a zero-based index. Handle the returned result as above after each call. **While displaying choices, wait for the player rather than calling parameterless `Next()` again.** Your own System controls this.

Set `DialogueSettings.Language = languageIndex` to switch languages, using the list order in Project Settings. Subscribe to `DialogueSettings.LanguageChanged`, call `graph.GetInfo()` in the handler, and refresh your UI to update text without advancing. Unsubscribe when no longer needed. Language selection is shared across the project; switching during Play does not rewrite the settings asset. Keep a single `Resources/DialogueSettings.asset`.

To stop and release a playback instance, call `graph.Stop()`, then `Object.Destroy(graph)`. Create a new instance to restart or switch graphs.

#### Graph Switching

`Scripts/DialogueSystemBase.cs` provides the uGUI player, language refresh, Graph lifetime management, and binding injection. Assign `cha` (character image), `bg` (dialogue panel with text), `options` (choice objects with text), and `graphs` (Graph assets), following the Demo scene. No separate Profile component is needed. Keep one System per scene, including inactive objects; the Graph editor reports duplicates.

`Demo/DemoDialogueSystem.cs` inherits this base and adds only the `cnt` field and `DemoPrint(int)` sample method. Your own System can inherit `DialogueSystemBase` without repeating binding setup. `Init()` and `Next()` are virtual and available to Event nodes; override `Start()` and `DrawInfo()` to replace the default UI. If overriding Unity lifecycle methods, call the base versions for initialization, language subscription, and cleanup. When driving a Graph instance yourself, set `graph.system` before the first `Next()`. Missing runtime bindings fail with the Graph, node, and item in the error.

In the example, `Init(0)` starts the first graph, `Next()` advances dialogue, and `Next(0)` selects the first option. Parameterless `Next()` is ignored while waiting for a choice. `Start()` already calls `Init()`, and language-change refresh is already connected.

To switch graphs through an event, select the object containing `DemoDialogueSystem` (or your derived System) in an Event node, choose the inherited **Init(int)**, and enter the target's index in `graphs`. It stops and releases the old instance and immediately displays the new graph's first item. Switching to the same graph restarts it. A custom switching method should stop the old instance first so the old flow does not continue when the event returns.
