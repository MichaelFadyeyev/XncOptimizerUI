---
name: xnc-program-read
description: >
  How to read XNC machining-program data (tools, bores, groovings, milling entry
  points, milling line/arc segments, variables and expressions) out of the inline
  `program` attribute on `operation[typeId="XNC"]` elements in a GibLab `.project`
  file. Reflects `TestData/td-programs.project` as of 2026-09-05; element/attribute
  vocabulary cross-checked against the larger `TestData/td-2.project` (523 program
  sub-documents). Background reference for branch `009_Read-and-display-xnc-programs`,
  not ground truth for every dialect variation.
---

# Reading XNC machining programs

## 1. Purpose & scope

A `.project` file produced by GibLab (furniture / panel CAM & nesting, exported from
"Базис-Мебельщик") carries, for each machined panel face, a full CNC program embedded as an
**escaped XML sub-document** inside the `program` attribute of an `<operation typeId="XNC">`
element. This document explains how to get to that sub-document and how to interpret every
element it contains:

- **tools** — name and diameter
- **bores** — surface ("side"), tool, centre coordinates, depth
- **groovings** — side, tool, start/end coordinates, depth, width, tool-to-centre-line position
- **milling entry points** — side, tool, coordinates, depth, tool-to-centre-line position
- **milling segments** — straight (`<ml>`) and arc (`<mac>`) and their parameters
- **variables / expressions** — `dx`, `dy`, `dz`, `tool.dia`, and custom `<var>` such as
  `contMillDepth`; all identifiers are **case-insensitive**

## 2. Where XNC programs live in the file

`<project>` has flat `<operation>` children (no `<operations>` wrapper). Filter by `typeId`:
`XNC` (drill / rout), `CS` (saw cut), `EL` (edge banding). Only `XNC` operations carry a
`program` attribute.

```
project
└── operation  typeId="XNC"  side="true|false" ...
    ├── @program   ← escaped XML: <?xml ...?><program dx dy dz> ... </program>
    └── part id="1"   ← references <good typeId="product">/<part id="1">
```

`TestData/td-programs.project` has two XNC operations: `id=3` (`side="true"`, has bores +
groove + milled circle) and `id=4` (`side="false"`, face bores only). **One XNC operation per
machined face.**

### Operation-level attributes worth reading

| attribute | meaning |
|---|---|
| `side` | boolean string `true` / `false` — which panel face this program machines. Every bore / groove / milling inside inherits this as its "side"; the sub-elements have no `side` of their own. |
| `turn` | face rotation code (seen `0`) |
| `mirHor`, `mirVert` | mirror flags (seen `false`) |
| `code`, `typeName` | part / program identifiers, e.g. `10_08_06x001x1`, `10.08.06.ПАН-600` |
| `countBore`, `countCut`, `countMill` | summary counters (informational) |

### Decoding the `program` attribute (two-layer parse)

The attribute value is XML entity-escaped **once** (`&lt; &gt; &quot;`). LINQ-to-XML
un-escapes it automatically when you read `.Value`, so:

```csharp
// mirrors Services/GibLabProjectService.cs:388-390
var xnc          = project.GetOperations().First(o => o.GetTypeIdValue() == "XNC");
var programXml   = xnc.GetProgram()!.Value;             // already real XML text here
var programInner = XDocument.Parse(programXml);         // 2nd parse
var program      = programInner.Element("program")!;
```

(The existing code additionally runs `WebUtility.HtmlDecode` on the value; it is a no-op after
LINQ-to-XML has already decoded the entities, but harmless — keep it for parity if desired.)

## 3. The `<program>` element

```xml
<program dx="1380" dy="600" dz="19"> ... </program>
```

| attribute | is | maps to `<good typeId="product">/<part>` |
|---|---|---|
| `dx` | part **length**, mm | `l` (also `cl` / `dl`) = 1380 |
| `dy` | part **width**, mm  | `w` (also `cw` / `dw`) = 600 |
| `dz` | part **thickness**, mm | `t` = 19 |

- **Children are ordered and stateful.** Process them top-to-bottom. Every `<tool>` and
  `<var>` seen so far defines the context (available tools, symbol values) for every element
  that comes after it. A milling contour is the run of `<ml>` / `<mac>` elements immediately
  following an `<ms>`.
- **Coordinate frame** (inferred from the data — no explicit metadata):
  - origin `(0,0)` at a part corner; **X along `dx`** (length), **Y along `dy`** (width).
  - **Z** measured through the thickness from the working face; `z` on edge bores is a
    position in `[0, dz]`.
  - `dp` ("depth") is measured **into the material** from the surface being machined.
  - **units are millimetres** throughout (no unit attribute anywhere).
  - contour cuts overshoot the panel: `x` runs from `-10` to `dx+10`.

## 4. Reading algorithm

1. Locate the XNC operation, read `side`, decode `program` (§2).
2. Seed a **case-insensitive** symbol table from `<program>`: `dx`, `dy`, `dz`.
3. Walk `program.Elements()` in document order:

   | element | action |
   |---|---|
   | `<tool>` | register / replace tool by `name` (last wins) |
   | `<var>` | evaluate `expr` against the current symbol table, add result under `name` |
   | `<ms>` | **open a new milling contour**; its `name` is the contour tool → set `tool.dia` = that tool's `d` for the contour and its segments |
   | `<ml>` | append a line segment to the open contour |
   | `<mac>` | append an arc segment to the open contour |
   | `<gr>` | emit a standalone grooving |
   | `<bf> <bt> <bb> <bl> <br>` | emit a standalone bore (surface = element name) |

4. For every coordinate / depth attribute, **resolve the value** (§5): a plain number, or an
   expression string, or a bare variable name.

## 5. Value resolution — literals, variables, expressions

Any of `x y x1 y1 x2 y2 cx cy dp z t sxy` (and `<var expr>`) is **either**:

- a numeric literal — `-10`, `382.5`, `65` — parse directly (invariant culture, `.` decimal);
- **or** an expression string — `dy-35-40`, `dx+10`, `tool.dia/2`, `dz+2.00`;
- **or** a bare variable reference — `dp="contMillDepth"`.

### Symbols (all lookups case-insensitive — `DX`, `dx`, `Dx` are the same)

| symbol | source | value in the fixture |
|---|---|---|
| `dx` | `<program dx>` — part length | 1380 |
| `dy` | `<program dy>` — part width | 600 |
| `dz` | `<program dz>` — part thickness | 19 |
| `tool.dia` | diameter `d` of the tool referenced by the **current** element's `name`; for a milling contour it is the `<ms>` tool and stays in scope for that contour's `<ml>` / `<mac>` | e.g. 10 inside the `name="Bore10"` `<ms>` |
| *(user vars)* | each `<var name= expr=>`, resolved when reached; `expr` may use `dx/dy/dz`, `tool.dia`, and earlier vars | `contMillDepth` = 21 |

### Operators

`+  -  *  /` and parentheses. Unary minus occurs (`-10`). A small evaluator is required — see
§8; there is **no** expression evaluator in the repo today.

### `<var>` attributes

| attribute | meaning |
|---|---|
| `name` | variable identifier (case-insensitive) |
| `type` | declared type, seen `"double"` |
| `expr` | formula string, evaluated against the symbol table at the point it appears |
| `comment` | free text, e.g. `Глубина сквозного фрезерования контура` ("through-contour milling depth") |

## 6. Element reference

### 6.1 Tools — `<tool>`

```xml
<tool name="Bore8" d="8"/>
<tool name="Mill6" d="6"/>
```

| attribute | meaning |
|---|---|
| `name` | key; referenced by `name=` on every bore / groove / milling element |
| `d` | **diameter**, mm (`double`) |

Tools are declared inline, just before the elements that use them, and re-declared per group.
No length / tool-number / spindle data is present. `d` is always a numeric literal.

### 6.2 Bores

**The machined surface ("side") is the element name**, not an attribute. The panel face
(for `<bf>`) is the operation's `side`.

| element | surface | fixed coordinate | position given by | `td-2.project` count |
|---|---|---|---|---|
| `bf` | panel **face** (vertical drill) | — (face = operation `side`) | `x`, `y` — centre on the face | 4150 |
| `bt` | **top** edge (`y = dy`), runs along X | `y = dy` | `x` (along edge), `z` (through thickness) | 384 |
| `bb` | **bottom** edge (`y = 0`), runs along X | `y = 0` | `x`, `z` | 684 |
| `bl` | **left** edge (`x = 0`), runs along Y | `x = 0` | `y` (along edge), `z` (through thickness) | 595 |
| `br` | **right** edge (`x = dx`), runs along Y | `x = dx` | `y`, `z` | 570 |

(Set confirmed by `Services/GibLabProjectService.cs:966` `ElementIsBore` and the mirror
`switch` at `:402-427`, where `bf/bl/br` flip `y` and `bt`↔`bb` swap tag.)

Attributes:

| attribute | on | meaning |
|---|---|---|
| `name` | all | tool reference → `<tool>` |
| `dp` | all | **drill depth**, mm, into the surface |
| `x`, `y` | `bf` | **centre** of the hole on the face |
| `y`, `z` | `bl`, `br` | `y` = distance along the edge; `z` = through-thickness position of the horizontal hole |
| `x`, `z` | `bt`, `bb` | `x` = distance along the edge; `z` = through-thickness position |
| `ver` | `bl` (seen `2`) | element schema version |
| `ac` | all (seen `1`) | active / repetition-count flag |
| `av` | `bf` (bool, seen `false`) | boolean flag — treat as through-hole indicator (blind when `false`) |
| `m` | `bl` (bool, seen `false`) | mirrored flag |

**What to extract:** side = element name (+ operation `side` for `bf`); tool = `name`;
centre coordinates = `bf` → `(x, y)`, `bl`/`br` → `(edgeConst, y, z)` with `edgeConst ∈ {0, dx}`,
`bt`/`bb` → `(x, edgeConst, z)` with `edgeConst ∈ {0, dy}`; depth = `dp`.

> The fixture only contains `bf` and `bl`. `bt` / `bb` / `br` are documented from the
> `td-2.project` element census and the existing mirror code — verify their exact attribute
> set and `z` reference against that file when implementing.

### 6.3 Groovings — `<gr>`

```xml
<gr comment="Паз15 ()" x1="-10" y1="565" dp="4" x2="dx+10" y2="565" t="10" c="0" p="0" name="Cut3.2"/>
```

| attribute | meaning |
|---|---|
| `name` | tool reference |
| `x1`, `y1` | **start** point (literal or expression) |
| `x2`, `y2` | **end** point (literal or expression) |
| `dp` | groove **depth**, mm |
| `t` | groove **width**, mm (may exceed the tool `d` → machine makes multiple passes) |
| `c` | **tool-to-centre-line position**: `0` = center, `1` = right, `2` = left |
| `p` | secondary pass / position flag (seen `0`) |
| `comment` | free-text label, e.g. `Паз15 ()` |

**side** comes from the operation's `side` (no attribute on `<gr>`).

> A groove is sometimes authored instead as an `<ms>` + `<ml>` pair using a drill as the
> cutter (the fixture does this: `<ms name="Bore10" sxy="tool.dia/2" ...>` followed by
> `<ml ...>`). Read that as a milling contour, not as a `<gr>`.

### 6.4 Milling entry points — `<ms>`

`<ms>` starts a milling contour. Every `<ml>` / `<mac>` up to the next `<ms>` (or the next
bore / groove / tool / var / end of program) belongs to it.

```xml
<ms x="250" y="382.5" dp="contMillDepth" in="0" out="1" c="2" name="Mill6"/>
```

| attribute | meaning |
|---|---|
| `name` | tool reference for the **whole contour**; sets `tool.dia` for its segments |
| `x`, `y` | **entry point** coordinates (literal or expression) |
| `dp` | milling **depth** at the entry (literal, expression, or a `<var>` name such as `contMillDepth`) |
| `c` | **tool-to-centre-line position**: `0` = center, `1` = right, `2` = left, `3` = pocket |
| `in` | lead-in code (seen `0` = none) |
| `out` | lead-out code (seen `0` = none, `1` = present) |
| `sxy` | optional start offset in the XY plane, expression, e.g. `tool.dia/2` |
| `fwd` | optional contour-direction flag (bool, seen `true`) |

**side** comes from the operation's `side`.

### 6.5 Milling segments

A segment's **start point is implicit** — it is the end point of the previous element (the
`<ms>` entry for the first segment, otherwise the previous segment's end).

#### Line — `<ml>`

```xml
<ml x="dx+10" y="dy-35-40" dp="40"/>
```

| attribute | meaning |
|---|---|
| `x`, `y` | segment **end** point (literal or expression) |
| `dp` | **depth at the end** of the segment — differing from the start depth means a ramped cut |

#### Arc — `<mac>`

```xml
<mac x="232.5" y="400" cx="250" cy="400" dir="false"/>
```

| attribute | meaning |
|---|---|
| `x`, `y` | arc **end** point |
| `cx`, `cy` | arc **centre** |
| `dir` | direction flag (bool) |

There is **no explicit radius, sweep angle, or start point**. Derive them:

- `radius = distance(centre, start) = distance(centre, end)` (equal within rounding).
- sweep goes from `start` to `end` around `(cx, cy)` in the sense given by `dir`.
- In the fixture the four `dir="false"` arcs trace
  `(250,382.5) → (232.5,400) → (250,417.5) → (267.5,400) → (250,382.5)` — a full `r = 17.5`
  circle centred at `(250,400)`, traversed **clockwise** in a Y-up part frame. So
  `dir="false"` ⇒ **CW**, `dir="true"` ⇒ **CCW** — *verify against `td-2.project` before
  relying on it.*
- No `dp` on `<mac>` in the fixture → the segment holds the depth of the previous segment.

`td-2.project` contains only `ml` and `mac` segment element types (no others).

## 7. Worked example — `TestData/td-programs.project`

### Operation `id=3`, `side="true"` → `<program dx="1380" dy="600" dz="19">`

| # | element | resolved reading |
|---|---|---|
| 1–2 | `<tool>` ×2 | `Bore8` d=8, `Bore10` d=10 |
| 3 | `<ms>` | milling/groove entry, tool `Bore10` (`tool.dia`=10). entry `x=-10`, `y = dy-35-40 = 525`, `dp=4`, `sxy = tool.dia/2 = 5`, `c=0` (center), `fwd=true`, `in=0`, `out=0` |
| 4 | `<ml>` | line to `x = dx+10 = 1390`, `y = 525`, `dp=40` (ramps 4 → 40) |
| 5 | `<bl>` | left-edge bore (`x=0`), tool `Bore8`: along-edge `y=65`, `z=10`, depth `dp=34` |
| 6 | `<bl>` | left-edge bore, `Bore8`: `y=105`, `z=10`, `dp=26` |
| 7 | `<bl>` | left-edge bore, `Bore8`: `y=505`, `z=10`, `dp=26` |
| 8 | `<bl>` | left-edge bore, `Bore8`: `y=545`, `z=10`, `dp=34` |
| 9 | `<tool>` | `Cut3.2` d=3.2 |
| 10 | `<gr>` | grooving, tool `Cut3.2`, label `Паз15 ()`: start `(-10, 565)`, end `(dx+10, 565) = (1390, 565)`, depth `dp=4`, width `t=10`, `c=0` (center), `p=0` |
| 11 | `<tool>` | `Mill6` d=6 |
| 12 | `<var>` | `contMillDepth = dz + 2.00 = 21` |
| 13 | `<ms>` | milling entry, tool `Mill6` (`tool.dia`=6): entry `(250, 382.5)`, `dp = contMillDepth = 21`, `c=2` (left), `in=0`, `out=1` |
| 14 | `<mac>` | arc → end `(232.5, 400)`, centre `(250, 400)`, `dir=false` (CW) |
| 15 | `<mac>` | arc → end `(250, 417.5)`, centre `(250, 400)` |
| 16 | `<mac>` | arc → end `(267.5, 400)`, centre `(250, 400)` |
| 17 | `<mac>` | arc → end `(250, 382.5)`, centre `(250, 400)` — closes an `r = 17.5` circle at `(250, 400)` |

### Operation `id=4`, `side="false"` → `<program dx="1380" dy="600" dz="19">`

| # | element | resolved reading |
|---|---|---|
| 1–2 | `<tool>` ×2 | `Bore15` d=15, `Bore8` d=8 |
| 3 | `<bf>` | face bore, `Bore8`, centre `(1353, 65)`, depth `dp=12`, `av=false` (blind) |
| 4 | `<bf>` | face bore, `Bore8`, centre `(1353, 545)`, depth `dp=12` |
| 5 | `<bf>` | face bore, `Bore15`, centre `(34, 65)`, depth `dp=14` |
| 6 | `<bf>` | face bore, `Bore15`, centre `(34, 545)`, depth `dp=14` |

## 8. Recommended C# approach (branch `009_Read-and-display-xnc-programs`)

*Guidance for the follow-up implementation — not built yet.*

- **XML**: `System.Xml.Linq`. Two-layer parse as in `Services/GibLabProjectService.cs:388-390`.
  Add typed getters to `Extensions/XContainersExtensions.cs` (`GetD`, `GetDp`, `GetCx`, …)
  instead of raw `.Attribute()` in the service / view models — house convention.
- **Models** (read-only; `MVVM/Models`, plain classes, file-scoped namespace):
  - `XncProgram` — `Dx/Dy/Dz`, `Side`, `IReadOnlyList<XncTool>`, bores, groovings, contours.
  - `XncTool` — `Name`, `Diameter`.
  - `XncBore` — `Surface` (`BoreSurface` enum `Face/Top/Bottom/Left/Right`), `ToolName`,
    `X/Y/Z`, `Depth`, `Through`.
  - `XncGrooving` — `ToolName`, `Start`, `End`, `Depth`, `Width`, `Position`
    (`ToolPosition` enum), `Comment`.
  - `XncMillingContour` — `ToolName`, `Entry`, `EntryDepth`, `Position`, `LeadIn/LeadOut`,
    `IReadOnlyList<XncMillingSegment>`.
  - `XncMillingSegment` — abstract; `XncLineSegment { End, Depth }`,
    `XncArcSegment { End, Center, Clockwise, Radius }`.
  - `ToolPosition` enum: `Center = 0, Right = 1, Left = 2, Pocket = 3` (matches `c`).
- **Expression evaluator**: small, case-insensitive, `+ - * /` + parens + unary minus + bare
  identifiers. Seed `dx/dy/dz`, resolve `tool.dia` per element, add `<var>` results in order.
  Options: hand-rolled shunting-yard/recursive-descent (**recommended**, no dependency);
  `System.Data.DataTable.Compute` with variable pre-substitution (no dependency, but the
  `tool.dia` dot and culture need care); NCalc / Jace (adds a NuGet package the project has
  so far avoided — only `CommunityToolkit.Mvvm` + `Microsoft.Extensions.DependencyInjection`).
- **Layering**: expose the parsed model through `Contracts/IProjectService`; view models never
  touch `XDocument` — house convention.
- **Tests**: NUnit 4 + NSubstitute against `TestData/td-programs.project`; assert the
  §7 numbers (`dy-35-40 = 525`, `dx+10 = 1390`, `tool.dia/2 = 5`, `dz+2.00 = 21`, circle
  `r = 17.5` at `(250, 400)`).

## 9. Open items — verify against `TestData/td-2.project`

- `dir="false"` / `"true"` ↔ CW / CCW.
- `bt` / `bb` / `br` exact attribute set and the reference for `z`.
- `in` / `out` lead-code enumeration (only `0` and `1` seen).
- `p` on `<gr>` (only `0` seen).
- Confirm no milling segment types beyond `<ml>` and `<mac>`.
- Whether an `<ms>` with no following segment is a valid point operation.
