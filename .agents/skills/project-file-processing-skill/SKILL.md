---
name: project-file-processing-skill
description: >
  Work with GibLab `.project` files in the XncOptimizerUI .NET app: read and modify
  parts, bands, sheets, products and `<operation typeId="XNC|CS|EL">` elements; decode,
  edit and re-serialise the escaped XNC machining `program` sub-document (tools, bores,
  groovings, milling contours/arcs, rectangular pockets, `<var>` expressions); convert
  grooves <-> mills and re-order mill passes; and merge data from several `.project`
  files (id remapping, band/sheet dedup). Covers both direct `XDocument`/`XElement`
  editing and the `IProjectService` / `GibLabProjectService` API. Use whenever a task
  touches a `*.project` file or the `Services/Xnc` model layer. Format facts reflect
  `XncOptimizerUI/TestData/*.project`; not ground truth for every GibLab dialect.
---

# Processing GibLab `.project` files

## When to use this skill

- reading or changing **parts, bands, sheets, products** or **operations** in a `*.project` file;
- anything inside an `<operation typeId="XNC">` **`program`** attribute — bores, groovings,
  milling, pockets, tool lists, `<var>` expressions;
- **converting** programs (groove <-> mill, mill-pass ordering) or writing new transforms;
- **merging** several `.project` files into one;
- extending `GibLabProjectService` / `IProjectService` or the `MVVM/Models/Xnc` classes.

## Reference files (read on demand)

| file | what it holds |
|---|---|
| `references/project-file-schema.md` | outer `.project` XML: `<project>`, every `<good typeId=...>`, `<operation typeId=...>`, `<part>`, all attributes, id cross-references, encoding rules |
| `references/xnc-program-format.md` | the escaped `program` sub-document: two-layer parse, coordinate frame, value/expression resolution, full `<tool>/<var>/<bf..br>/<gr>/<ms>/<ml>/<mac>/<mr>` element reference, worked example |
| `references/dotnet-api.md` | `IProjectService` / `GibLabProjectService` methods, `XContainersExtensions` accessors, `MVVM/Models` + `MVVM/Models/Xnc` classes, `AppOptions`/`ConfigService`, headless-usage pattern, existing transforms, merge recipe |

## The 60-second model

```
project  (UTF-8, no namespaces, single line, version="23051701")
├── good typeId="product"      id,name,count      -> <part> (0..n): panels to machine
├── good typeId="tool.cutting" swSawthick=kerf                     (one per file)
├── good typeId="tool.edgeline"                                    (0..n, edgebander)
├── good typeId="sheet"        l,w,t,count        -> <part>: stock blank
├── good typeId="band"         name,t,w          edgebanding tape
├── operation typeId="CS"   tool1->tool.cutting  <material id->sheet>  <part id->product part>... <part id->sheet part>
├── operation typeId="EL"   tool1->tool.edgeline elSymbol A/B  <material id->band>  <part id->product part>
└── operation typeId="XNC"  side, turn, @program (escaped XML)   <part id->product part>   ONE per machined face
```

All links are integer `id` attributes, **except** `part/@elt|elb|ell|elr = "@operation#<EL id>"` (split on `#`).

Two writer conventions produce different **attribute order** (Bazis = alphabetical,
hand-authored = natural). Never depend on order; never rewrite nodes you did not change.

## Load / save

**Via the service (preferred when the app is in the loop):**

```csharp
var svc = new GibLabProjectService(config, timeProvider);
svc.OpenProject(path);
svc.ReadBands();          // REQUIRED before ReadParts — fills the band cache
svc.ReadSheets();         // REQUIRED before ReadParts — sheet-id resolution
var parts = svc.ReadParts();
string log = "";
svc.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection.GroovesToMills, processPockets: false);
```

- `SaveProject()` **overwrites the input file** (used only by `UpdatePart`).
- Every batch transform instead writes a **new** file next to the input and re-points `FullPath`:
  `_opt` (`GroupIdenticalElements`), `_ren` (`ReplaceXncPrograms`), `_gm` (`ConvertGroovesAndMills`),
  `_mo` (`OptimizeMillTraversal`); a name clash gets `(1)`, `(2)`, ...
- `AppendDescription(text)` stamps a timestamped audit line into `project/@description`.

**Via raw LINQ-to-XML (new transforms, merges, one-offs):**

```csharp
var doc = XDocument.Load(path);
var project = doc.Element("project")!;                 // == doc.GetProject()
foreach (var xnc in project.GetOperations().Where(o => o.GetTypeIdValue() == "XNC"))
{
    var program = XDocument.Parse(WebUtility.HtmlDecode(xnc.GetProgramValue()!)).Element("program")!;
    // ... edit program ...
    xnc.SetAttributeValue("program", program.ToString());   // LINQ-to-XML re-escapes on Save
}
doc.Save(outPath);
```

Prefer the `XContainersExtensions` accessors (`GetIdIntValue`, `GetTypeIdValue`,
`GetProgramValue`, `SetLengthValue`, ...) over hand-written `Attribute("...")?.Value` — they are
the project idiom and already handle the `#`-token banding refs and the string-typed XNC attributes.

## Editing recipes

### Modify a part (dimensions, banding, name)
`references/project-file-schema.md` §Part lists every attribute and which of `l/dl/cl/jl`
(and the `w`-family) must move together. Through the service: mutate the `Part` model, call
`svc.UpdatePart(ref log, part)` (writes in place via `SaveProject`). Raw: set `l`+`dl` with
`SetLengthValue`/`SetDLengthValue`, `w`+`dw` with `SetWidthValue`/`SetDWidthValue`. Banding
refs are `elt/elb/ell/elr` -> `@operation#<id>` **plus** the `*Mat` name string — keep them
consistent, and make sure a matching `EL` operation + `band` good exist.

### Edit the XNC program
1. Decode (two-layer parse, above). 2. Walk `program.Elements()` **in document order** — it is
stateful: every `<tool>`/`<var>` seen so far is context for later elements; a milling contour is
`<ms>` + the following run of `<ml>`/`<mac>`. 3. Resolve every coordinate/size attribute with
`XncExpressionEvaluator.Evaluate(value, symbols)` — a value is a literal, an expression
(`dy-35-40`, `tool.dia/2`), or a bare `<var>` name. 4. Re-serialise with `program.ToString()`
and write it back to `@program`. Full element/attribute tables: `references/xnc-program-format.md`.

To *read* programs for inspection, use `svc.ReadXncPrograms(partId)` -> `IReadOnlyList<XncProgram>`
(fully resolved). That model is **read-only** — it cannot round-trip edits back to XML.

### Convert programs
- **Bores <-> mills:** `ConvertBoresAndMills(ref log, parts, direction, useEllipse)`. Face bores
  (`<bf>`) wider than 35 mm <-> round mills cut with a fixed 6 mm `Mill6`: default is a closed
  `<ms>` + two `<mac>` half-circle contour, `useEllipse` emits an `<me>` (l/w = radius). The
  reverse also accepts `<ma>` arcs and `l==w` ellipses; it makes a `Bore<diameter>` tool. Saves
  `_bm.project`. Details: `references/dotnet-api.md` §Conversions.
- **Grooves <-> mills:** `ConvertGroovesAndMills(ref log, parts, direction, processPockets)`.
  Only axis-parallel, primary-pass (`<gr>` `p` absent/`0`), non-through elements convert;
  `GroovesToMills` needs `AppOptions.MillingToolDiams` non-empty (exact-diameter -> single mill
  pass; off-size -> rectangular pocket with the smallest cutter). Mills->grooves uses a
  hard-coded 2.8 mm grooving tool. Algorithm + edge-overshoot rules: `references/dotnet-api.md`
  §Conversions.
- **Mill-pass ordering:** `OptimizeMillTraversal(ref log, parts)` — greedy nearest-neighbour
  re-sequence of straight axis-parallel passes, per operation per tool.

### Merge several `.project` files
No built-in API — implement with the pattern in `references/dotnet-api.md` §Merge recipe:
load each `XDocument`, pick a base, offset/renumber colliding `id`s and rewrite **every**
reference, dedupe `tool.*`/`sheet`/`band` goods, append foreign `<good>`/`<operation>` nodes
(goods before operations), `AppendDescription`, save `_merged.project`. Model the renumber-and-
remap on `GroupIdenticalElements`.

## Gotchas

- `ReadParts()` blanks banding + sheet fields unless `ReadBands()` **and** `ReadSheets()` ran first.
- XNC attributes are **strings on purpose** (they hold expressions). Don't `double.Parse` them
  blindly — go through `XncExpressionEvaluator`.
- `<mac>` has no radius/sweep/start — derive from centre + previous point; `dir="false"` = CW
  (still unconfirmed across every dialect).
- `<mr>` `x/y` is the rectangle **centre**.
- `AppOptions.GroovingToolWidths` exists but is **not wired** to anything; mills->grooves is
  fixed at 2.8 mm.
- A second `EL` operation (the `!СмЧт ...` band) can have **no `<part>` child**.
- Empty products (`<good typeId="product">` with zero `<part>`) are legal (`td-pocket-to-groove.project`).
