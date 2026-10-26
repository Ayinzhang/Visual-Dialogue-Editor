## Visual Dialogue Editor

[中文版](ReadMe_zh.md)

This is a simple Unity visual dialogue editor based on [xNode](https://github.com/Siccity/xNode), with dialogue, choices, events, conditions, and localization. If you have used Visual Scripting or Amplify Shader Editor, the node editing workflow should feel familiar.

### Create

Right-click in the Project window and choose **Create > Dialogue Graph**, then double-click the asset to open the editor.

Before editing, use **Settings** in the top toolbar to open **Dialogue Settings**. Add languages in the upper section, then select a graph and language to edit speaker names in the lower section. Languages are shared across the project; speakers belong to individual graphs. Languages can be edited without selecting a graph.

Use **Language** in the upper-right corner to edit dialogue, choices, and speaker names in each language. Index 0 is the default language; empty translations stay empty during playback. The default language can be renamed but not moved or removed. Removing or reordering other languages updates translations in all saved dialogue graphs. New dialogue and choice items have one translation slot per language. Reordering speakers updates dialogue references; removing a speaker leaves its dialogue lines without a speaker.

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (1).png" style="width: 33%; object-fit: contain;" />
  <img src="img (2).png" style="width: 33%; object-fit: contain;" />
  <img src="img (3).png" style="width: 33%; object-fit: contain;" />
</div>

### Edit

Right-click an empty area of the canvas to create a node with one empty item, and connect ports to build the dialogue flow. The list's add button copies the selected item, or creates an empty one when nothing is selected. There are four node types:

| Node | Purpose |
|------|---------|
| Dialogue | Set a Sprite, speaker, and text for each dialogue line. |
| Option | Present choices and follow the player's selected choice. |
| Event | Invoke a public instance method on a scene component, with no argument or one `int`, `float`, `bool`, `string`, or enum argument. |
| Check | Read a public instance field on a scene component, using the same supported types, and follow the first matching condition. |

Each Dialogue and Event item can configure its own ports: `None` for no ports, `Input` for input only, `Output` for output only, and `All` for both. Connecting to an item's input starts at that item. Option and Check nodes use a node-level `Before` input with separate outputs for their items. Checks support equal, not equal, greater than, and less than.

The first created node becomes the entry; right-click a node and choose **Set as Entry** to change it. **An item with an unconnected output ends playback. An item without an output advances to the next item, or ends playback at the end of its list.** A Check node with no matching condition also ends playback. Events and checks run synchronously without individual clicks, so avoid automatic cycles that cannot exit.

The Graph remembers the scene when assigning a target. Opening the Graph asset opens that scene, or makes it active if already loaded. Before replacing scenes, Unity asks about unsaved changes; Cancel leaves the current scenes intact. Automatic scene opening is disabled during Play, and the record is editor-only: runtime playback never loads it.

If a node is too long, click **Fold** to show only its summary while keeping its connections. Click **Expand** to edit and connect it again. Node colors indicate entry (green), current playback (yellow), and issues (red), with issues taking priority. Items also use yellow or red side borders for playback or issues. Hover over the issue count in the top toolbar for details. When no relevant scene/System is available, saved bindings are marked **unverified**, not colored red or treated as valid. Unassigned targets, duplicate Systems, and broken bindings in the relevant scene remain errors.

<div style="display: flex; justify-content: space-between; gap: 10px;">
  <img src="img (4).png" style="width: 49%; object-fit: contain;" />
  <img src="img (5).png" style="width: 49%; object-fit: contain;" />
</div>

### Use

Assign `DialogueGraph` assets to a `DialogueSystemBase` or derived System. The System creates playback instances; do not advance the assets themselves. Call `system.Next()` to advance, then read `system.graph.dataType`, `dialogueInfo`, and `optionInfo` for your own UI. `Next()` returns no value.

#### Graph Switching

To switch graphs through an event, select the object containing `DemoDialogueSystem` (or your derived System), choose **Switch(int)**, and enter the target's index in `graphs`. In an Event, call only `Switch()`, not `Next()`: the existing `Next()` loop starts the new graph, returns to the caller when it ends, and continues until dialogue, choices, or the overall end. This supports nested graphs and event-only child graphs without recursive advancement. Put the event at the end of a flow to finish the caller after returning. Switching to the same asset creates a new instance. Custom switching methods should change `graph` without calling `Next()`.
