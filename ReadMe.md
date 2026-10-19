## Visual Dialogue Editor

[中文版](ReadMe_zh.md)

A Unity visual dialogue editor based on [xNode](https://github.com/Siccity/xNode), with dialogue, choices, events, conditions, localization, and editor-only JSON/YAML import/export. The graph asset remains the runtime playback source.

### Installation and dependencies

**YamlDotNet.Uni is optional**: without it, graph editing and dialogue playback remain available; only JSON/YAML import/export is disabled. Click **Install YamlDotNet** in the graph Inspector or **Edit > Install YamlDotNet** in the graph window. After confirmation, Unity Package Manager downloads it from GitHub, requiring Git and network access. Installation progress and failures are reported in the editor; import/export is enabled automatically after compilation.

Alternatively, install it manually through **Add package from git URL**: `https://github.com/epishev-m/YamlDotNet.UniEx.git`. Importing this plugin's `.unitypackage` does not automatically download dependencies.

### Create and edit

Right-click in the Project window and choose **Create > Dialogue Graph**, then double-click the asset. Manage languages and speakers in the graph Inspector or through **Edit > Edit Languages / Edit Speakers** in the graph window. The language dropdown switches the text being edited; missing translations fall back to the default language.

The checkbox beside non-default text enables that translation, including explicitly empty text. Uncheck it to use the fallback; editing text checks it automatically. Legacy empty translations retain their fallback behavior. Graphs are read-only during playback, including nodes, items, connections, languages, and speakers; playback highlights, panning, and zoom remain available.

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
- Missing Sprite or scene/component/member references retain their configuration and are highlighted in place. Repair the reference rather than re-entering the entire configuration.
- When a target scene is not loaded, binding checks are deferred with an informational message rather than a red error. Paths and member configuration remain intact and are checked again when the scene is opened. Missing targets in loaded scenes remain errors; scenes are not loaded automatically.
- Click a Sprite preview to select an asset, drag a Sprite onto it, or right-click to clear it. Preview drawing and status borders follow graph zoom.

The two graphs in `Demo` use the new entry and folding logic. There is no automatic migration for the old folding representation.

### Playback

**Graph advances the dialogue and provides results; DialogueSystem only selects graphs and updates UI.** Graph's `Next()` uses `Init()`, `MoveOn()`, `GetNodeAndIndex()`, and `GetInfo()` to initialize the entry, advance nodes, invoke events, evaluate conditions, and read data. The Event's `InvokeMethod()` and Check's `GetNextStr()` remain on their respective Nodes.

As in the original version, instantiate an asset before playback: `Instantiate(graphs[graphIndex])`. The current node, item index, playback language, and results belong to that playback instance. Node definitions remain read-only, without activity flags written into shared nodes; multiple Systems can play the same asset without overwriting each other's positions.

Attach `Demo/DialogueSystem.cs` to a scene object and assign `cha`, `bg`, `options`, and `graphs`. `Start()` calls `Init()` to select the first graph and display its first item. Dialogue clicks call `Next()`; option buttons call `Next(choiceIndex)` with a zero-based index. `Next(-1)` leaves a pending choice unchanged. No `Update()` or cross-frame continuation is required.

```csharp
dialogueSystem.Init(0); // Switch/restart a graph and display its first item
dialogueSystem.Next();  // Advance when dialogue is clicked
dialogueSystem.Next(1); // Call when the second option is selected
```

As in the original version, System uses a single `bool option` to wait for a choice and updates UI from Graph's `DataType.Dialogue / Option / End` result; it does not traverse or identify concrete nodes. `graphs` stores the asset list, while the nonserialized `graph` refers to the current playback instance; `graph.node` and `graph.index` hold its playback position. The editor associates playback highlights with the entry node's owning asset rather than comparing the playback instance directly with an asset. The editor still validates and highlights configuration issues. Execution exceptions are reported directly in the Unity Console, without a separate error-handling flow in the demo script.

For custom UI integration, call the playback instance's `Next(num)` and read `dialogueInfo.sprite / name / context` or `optionInfo` according to its return type. Set the instance's `languageIndex` to an index in `Languages` for playback. Before switching, call the instance's `Stop()` to end the old flow, then destroy it. Do not advance the Graph asset directly.

### Switching graphs with Event nodes

`DialogueSystem` exposes `DialogueGraph[] graphs`, `Init(int graphIndex = 0)`, and `Next(int num = -1)`. Assign the first graph to `graphs[0]`. `Init(index)` stops and releases the old playback instance, instantiates the target graph, and displays its first item; do not call `Next()` again just to start it. Language appears between Bg and Options in the Inspector. The selected numeric index is used directly in each target graph, so language ordering must be consistent across graphs.

Language is a dropdown generated dynamically from `graphs[0].Languages`, backed by the integer `languageIndex`, not a C# enum. `DialogueSystemEditor` generates this dropdown rather than merely rearranging fields; removing it would remove the language-selection UI.

To jump from an Event node, select the GameObject containing your `DialogueSystem`, choose its **Init(int)** method, and supply the target's zero-based index in `graphs`. `Init` switches graphs and displays the first item immediately; after the event returns, the old graph's remaining items/connections are not executed. Switching back to the same graph restarts it. No extra jump node is needed.

### JSON / YAML import/export

Use **Import** or **Export** in the graph Inspector or the graph window's **Edit** menu, outside Play Mode. Import accepts `.json` and `.yaml`. On Windows, select JSON or YAML from the **Save as type** dropdown in the export save dialog; the file extension and encoding format follow that selection. On other editor platforms, choose the format before selecting the save location. Import replaces the selected saved graph's contents while retaining its asset identity, asks for confirmation, and supports Undo. Parsing and structural validation happen before replacement; malformed input does not overwrite the graph. Missing bindings are preserved and reported for repair.

Both are **editor interchange formats**, intended for creating/importing dialogue with AI and similar tools, not runtime loaders. They share the schema below and can be converted by importing one format and exporting the other.

#### Minimal example

```json
{
  "schemaVersion": 1,
  "entryNodeId": "start",
  "defaultLanguage": "en",
  "languages": ["en", "zh"],
  "speakers": [],
  "nodes": [
    {
      "id": "start",
      "type": "dialogue",
      "lines": [
        {
          "id": "greeting",
          "portType": "output",
          "text": { "en": "Continue?", "zh": null },
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
          "text": { "en": "Again" },
          "next": { "nodeId": "start", "itemId": null }
        },
        {
          "id": "finish",
          "text": { "en": "Finish" },
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
defaultLanguage: en
languages: [en, zh]
speakers: []
nodes:
  - id: start
    type: dialogue
    lines:
      - id: greeting
        portType: output
        text:
          en: Continue?
          zh: null
        next: {nodeId: choice, itemId: null}
  - id: choice
    type: option
    options:
      - id: again
        text: {en: Again}
        next: {nodeId: start, itemId: null}
      - id: finish
        text: {en: Finish}
        next: null
```

Use `|` for multiline text and quote strings that look like numbers, booleans, or null (for example `"001"`, `"true"`, `"null"`). Numbers use JSON decimal/exponent syntax; booleans are `true`/`false`, and null can be `null`, `~`, or an empty value. Comments are accepted but are not preserved on export. JSON syntax is also accepted by the YAML reader. This interchange format supports a single document with string-keyed mappings, sequences, and scalar values; YAML anchors, aliases, and explicit tags are intentionally not supported.

#### Shared format reference

| Scope | Fields and rules |
|-------|------------------|
| Document | Required: `schemaVersion: 1`, `entryNodeId`, `defaultLanguage`, nonempty `languages`, nonempty `nodes`. Optional `speakers` defaults to `[]`. |
| Speaker | Required `id` and `names`, a language-to-text object such as `{"en":"Guide","zh":null}`. |
| Every node | Required unique `id` and `type` (`dialogue`, `option`, `event`, `check`). Optional `name` and `editor`. |
| Editor metadata | Optional `position: [x, y]`, `collapsed: true/false`, `summary: "..."`. Affects editing, not execution. |
| Dialogue | Required nonempty `lines`. Each line requires `id`, `portType`, `text`, and `next`. Optional `speakerId` and `sprite`. |
| Option | Required nonempty `options`. Each option requires `id`, `text`, and `next`. |
| Event | Required nonempty `events`. Each event requires `id`, `portType`, `target`, `componentType`, `method`, and `next`. Optional `parameter`; omit it or use `null` for a method without arguments. |
| Check | Required `target`, `componentType`, `field`, `valueType`, and nonempty `conditions`. Optional `enumType`. Each condition requires `id`, `operator`, `value`, and `next`. |
| Connection | `next` must be present, including when `null`. Dialogue/event targets accept item inputs only: `itemId` identifies an input/both item. Option/check targets retain node inputs and use `itemId: null`. For legacy documents, an omitted/null dialogue/event `itemId` maps to the first item and enables its Input flag; new exports use explicit item IDs. `next: null` ends an output branch; for normal/input dialogue/event items it instead continues list-order execution. Each output has at most one destination. |
| Port type | `normal`, `input`, `output`, `both`. Only `output`/`both` can have a non-null `next`. |
| Localized text | `text`/`names` require a non-null entry for the default language; `""` is valid text. Other languages can be omitted or `null` for fallback. Keys must be declared in `languages`. |
| Sprite | Optional `sprite: {"guid":"32-digit asset GUID","fileId":21300000}`; GUID and local file ID must be provided together. Preserve the real asset identifiers, including the Sprite's sub-asset ID. |
| Scene target | `target: {"globalObjectId":null,"path":"Demo Scene/Canvas/ChatPanel"}`. At least one identifier is required. Paths start with the scene name, then root/child names. Editor object IDs are project-specific; use paths for runtime fallback and repair bindings after moving scenes/assets between projects. Conflicting or ambiguous references are errors. |
| Value/parameter | Types: `int`, `float`, `double`, `bool`, `string`, `enum`. Parameter example: `{"type":"int","enumType":null,"value":1}`. Use JSON numbers/booleans, not quoted versions. `enum` needs a fully qualified `enumType` and integer value; other types require omitted/null `enumType`. A string value can be `null`. |
| Condition operator | `equal`, `notEqual`, `greater`, `less`. Conditions are evaluated in list order. |

`componentType` should identify the component type unambiguously; export an existing binding to obtain its type name. Node IDs are unique across the graph; item IDs are unique within their node. Duplicate properties, unknown fields, invalid value types, and broken node/item links are rejected in both formats. Optional fields can be omitted or set to `null` where applicable; export includes optional values, so its formatting may be more verbose than the minimal example.

#### Event and Check example

This complete document uses the demo scene's `DialogueSystem.Print(int)` method and `cnt` field. Open the demo scene to resolve its bindings; in another scene, replace the path and component/member names with real project values. The field names are identical in YAML.

```json
{
  "schemaVersion": 1,
  "entryNodeId": "call",
  "defaultLanguage": "en",
  "languages": ["en"],
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

#### Generating configuration with AI

Give the AI this README, the desired dialogue/branches, and any real scene paths, component types, methods/fields, and asset identifiers it needs. The dialogue/option example and the binding example above cover all four node types. A useful generation instruction is:

> Output one complete schemaVersion 1 document, as raw JSON or YAML without Markdown fences. Use only the documented fields and exact case-sensitive type/port/operator names. Use unique nonempty string IDs; entryNodeId and every next reference must resolve. Include next on every line, option, event, and condition, even when null. For dialogue/event items, use both, not all, for both ports; only output/both may have non-null next. Options and conditions do not have portType. An itemId target must have an input port. Match language keys exactly to languages, include non-null default-language text, and use null/omission for missing translations. Omit unused speakerId and sprite or set them to null; never invent GUIDs, file IDs, object IDs, paths, or component/member names. Include Event/Check bindings only when real binding details are supplied. For a cross-graph jump, use an ordinary Event calling DialogueEditor.DialogueSystem.Init with an int parameter; do not add a new node type or a graphs field to this document.

For text that resembles a number/boolean/null, quote it in YAML. Empty strings display empty text rather than triggering translation fallback. JSON must not contain comments or trailing commas. Structural validity can be checked by importing, but functional bindings require the actual scene/assets; AI cannot infer project-specific identifiers from this README alone.

### Layout

- `Scripts`: graph definition, four node types, localization data, and structural/binding validation.
- `Editor`: graph/node UI, Inspector, languages/speakers, and reference selection.
- `Editor/Serialization`: shared document model, JSON/YAML codecs, graph mapping, and import/export.
- `Demo`: sample scene, two graph assets, the DialogueSystem that reads and executes them, UI, and images.
- `xNode`: bundled graph framework; see `ThirdPartyNotice.txt`.
