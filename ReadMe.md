## Visual Dialogue Editor

[中文版](ReadMe_zh.md)

A Unity visual dialogue editor based on [xNode](https://github.com/Siccity/xNode), with dialogue, choices, events, conditions, and localization. Edit dialogues directly in Unity; the graph asset is the runtime playback source.

### Create and edit

Right-click in the Project window and choose **Create > Dialogue Graph**, then double-click the asset. Manage project languages in **Project Settings > Dialogue Editor**; **Edit > Edit Languages** opens the same settings. Speakers remain local to each graph and are managed in its Inspector or **Edit > Edit Speakers**. The language dropdown changes the shared project language in every graph; missing translations fall back to the default language.

The top toolbar stays fixed while zooming or panning. Toolbar drawing and playback edit protection live in the plugin's `Editor` folder; the bundled `xNode` folder matches the `9402be4` baseline without the later read-only patches.

Leave non-default dialogue text, options, or speaker names empty to use the default language. Graph content and language structure are read-only during playback; playback highlights, panning, zoom, and switching the current language remain available. Switching languages refreshes the displayed dialogue or options without advancing playback.

Project languages and graph speakers use reorderable lists. The default language stays above the language list and cannot be moved or removed. Adding, removing, or reordering languages updates text slots in every saved graph, including unopened assets; deletion requires confirmation. Read-only assets prevent the entire structural operation, and Undo restores the settings and affected graphs together. Speaker structure can be changed in the default language; reordering speakers also updates dialogue references.

| Node | Purpose |
|------|---------|
| Dialogue | Display a Sprite, speaker, and text. Items can have input/output ports. |
| Option | Present choices and follow the selected choice's connection. |
| Event | Invoke a public component method, with no argument or one supported argument. |
| Check | Compare a public component field and follow the first matching condition. |

- The first created node becomes the entry. Right-click another node and choose **Set as Entry** to change it.
- An output port has at most one destination. An unconnected output ends playback; dialogue/event items without an output port advance to the next item in their list. No matching check condition also ends playback.
- Cycles are allowed. Event/check execution is synchronous, without a step limit or cross-frame continuation. Ensure automatic cycles eventually exit; an infinite automatic cycle will block the main thread.
- **Show less** displays only the summary; item connections are drawn from the node's `before`/`after` ports. Folding does not rewrite connections; expand the node to edit them. The summary field is hidden when expanded.
- Status changes affect **borders**, not node background colors: green for entry, yellow for the current playback node/item, red for errors. Error status takes priority on the node border.
- Sprites use Unity's asset references directly. Missing scene/component/member references retain their configuration and are highlighted in place. Scene object IDs and paths are kept for reference recovery; graph content does not require additional string IDs.
- When a target scene is not loaded, binding checks are deferred with an informational message rather than a red error. Paths and member configuration remain intact and are checked again when the scene is opened. Missing targets in loaded scenes remain errors; scenes are not loaded automatically.
- Click a Sprite preview to select an asset, drag a Sprite onto it, or right-click to clear it. Preview drawing and status borders follow graph zoom.

The two graphs in `Demo` use the new entry and folding logic. There is no automatic migration for the old folding representation.

### Playback

**Graph advances the dialogue and provides results; DialogueSystem only selects graphs and updates UI.** Graph's `Next()` initializes the entry, then uses `MoveOn()`, `GetNodeAndIndex()`, and `GetTypeInfo()` to advance nodes, invoke events, evaluate conditions, and read data. The Event's `InvokeMethod()` and Check's `GetNextStr()` remain on their respective Nodes.

As in the original version, instantiate an asset before playback: `Instantiate(graphs[graphIndex])`. The current node, item index, and results belong to that playback instance; the current language is shared by the project. Node definitions remain read-only, without activity flags written into shared nodes; multiple Systems can play the same asset without overwriting each other's positions.

Attach `Demo/DialogueSystem.cs` to a scene object and assign `cha`, `bg`, `options`, and `graphs`. `Start()` calls `Init()` to select the first graph and display its first item. Dialogue clicks call `Next()`; option buttons call `Next(choiceIndex)` with a zero-based index. `Next(-1)` leaves a pending choice unchanged. No `Update()` or cross-frame continuation is required.

```csharp
dialogueSystem.Init(0); // Switch/restart a graph and display its first item
dialogueSystem.Next();  // Advance when dialogue is clicked
dialogueSystem.Next(1); // Call when the second option is selected
```

As in the original version, System uses a single `bool option` to wait for a choice and updates UI from Graph's `DataType.Dialogue / Option / End` result; it does not traverse or identify concrete nodes. `graphs` stores the asset list, while the nonserialized `graph` refers to the current playback instance; `graph.node` and `graph.index` hold its playback position. The editor associates playback highlights with the entry node's owning asset rather than comparing the playback instance directly with an asset. The editor still validates and highlights configuration issues. Execution exceptions are reported directly in the Unity Console, without a separate error-handling flow in the demo script.

For custom UI integration, call the playback instance's `Next(num)` and read `dialogueInfo.sprite / name / context` or `optionInfo` according to its return type. Set `DialogueSettings.Language` to an index in `DialogueSettings.Instance.languages`. Subscribe to `DialogueSettings.LanguageChanged`, call the instance's `GetTypeInfo()`, and redraw the current result without calling `Next()`; unsubscribe when disabled. The demo System already does this. Before switching graphs, call the instance's `Stop()` to end the old flow, then destroy it. Do not advance the Graph asset directly.

Dialogue text, options, and speaker names each use `LocalizedText`, with one string list indexed by project language, including the default at index 0. `Get(language)` reads with fallback; `GetRaw(language)` reads the editable slot, and `Set(language, value)` writes it. Playback results remain plain strings. Reads do not initialize or modify text data. `GetSpeaker(index, language)` and `SetSpeakerName(index, language, value)` take an explicit language index. Use `AddSpeaker(name)` to add a speaker. `EnsureLocalizationData()` aligns text slots when opening/editing a graph or adding content, not during reads. Change the language directory only through Project Settings so every graph stays aligned. Only the current data format is supported; no legacy translation or assembly-name migration is performed.

The single `Resources/DialogueSettings.asset` stores the project language list and edit-time selection, which is also the initial playback language. Runtime selection is nonserialized and resets on each Play session, including when domain reload is disabled; switching it does not rewrite the asset. Keep this asset unique. Both sample graphs include English at index 0 and Chinese at index 1.

### Switching graphs with Event nodes

`DialogueSystem` exposes `DialogueGraph[] graphs`, `Init(int graphIndex = 0)`, and `Next(int num = -1)`. Assign the first graph to `graphs[0]`. `Init(index)` stops and releases the old playback instance, instantiates the target graph, and displays its first item; do not call `Next()` again just to start it. The shared project language is retained when switching graphs.

Graphs and Systems do not serialize their own language selection. The shared language dropdown is in graph windows and Project Settings; Systems use Unity's default Inspector.

Graph canvas state, the fixed toolbar, and the graph Inspector/speaker list are kept in `DialogueGraphEditor`, `DialogueGraphToolbar`, and `DialogueGraphInspector`. Node canvas editors share `DialogueNodeEditorBase`; `DialogueNodeAssetEditor` only provides the node asset's Edit graph button and read-only Inspector fields during playback.

To jump from an Event node, select the GameObject containing your `DialogueSystem`, choose its **Init(int)** method, and supply the target's zero-based index in `graphs`. `Init` switches graphs and displays the first item immediately; after the event returns, the old graph's remaining items/connections are not executed. Switching back to the same graph restarts it. No extra jump node is needed.

### Layout

- `Scripts`: graph definition, four node types, localization data, and structural/binding validation.
- `Resources`: the single project-wide dialogue settings asset.
- `Editor`: graph/node UI, Inspector, languages/speakers, and reference selection.
- `Demo`: sample scene, two graph assets, the DialogueSystem that reads and executes them, UI, and images.
- `xNode`: bundled graph framework; see `ThirdPartyNotice.txt`.
