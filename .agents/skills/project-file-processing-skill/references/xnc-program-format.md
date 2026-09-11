# Reading XNC machining programs

This is the folded content of the former `.claude/xnc-program-read.md`. It documents the escaped
`program` sub-document only; the outer `.project` schema is in `project-file-schema.md`, the
.NET side in `dotnet-api.md`.

## 1. Purpose & scope

A `.project` file produced by GibLab (furniture / panel CAM & nesting, exported from
"Базис-Мебельщик") carries, for each machined panel face, a full CNC program embedded as an
**escaped XML sub-document** inside the `program` attribute of an `<operation typeId="XNC">`
element. This document explains how to get to that sub-document and how to interpret every
element it contains:

- **tools** — name and diameter
- **bores** — surface ("side"), tool, centre coordinates, depth
- **groovings** — side, tool, start/end coordinates, depth, width, tool-to-centre-line position
- **milling operations** — contour starts (`<ms>`), ellipses (`<me>`), and rectangles (`<mr>`)
- **milling segments** — straight (`<ml>`) and arcs (`<mac>` / `<ma>`) and their parameters
- **milling rectangles** — the `<mr>` pocket/frame primitive (length, width, angle, corner radius)
- **variables / expressions** — `dx`, `dy`, `dz`, `tool.dia`, and custom `<var>` such as
  `contMillDepth`; all identifiers are **case-insensitive**

### Attribute precedence

`comment` attributes are descriptive text only. They must never be used to determine geometry,
depth, tool behavior, entry/exit behavior, direction, or operation type. Readers and converters
must use the structural element name and machining attributes (`x`, `y`, `cx`, `cy`, `r`, `dp`,
`in`, `out`, `fwd`, `sxy`, `l`, `w`, `a`, `c`, and so on). If a comment conflicts with an
attribute, the attribute is authoritative.

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

`TestData/td-programs.project` has two XNC operations. `id=3` (`side="true"`) is the coverage
fixture: 4 edge bores, 3 groovings (one per `c` position), 3 straight milling contours, 1 arc
milling contour (a full circle) and 1 `<mr>` rectangle pocket. `id=4` (`side="false"`) is face
bores only. **One XNC operation per machined face.**

### Operation-level attributes worth reading

| attribute | meaning |
|---|---|
| `side` | boolean string `true` / `false` — which panel face this program machines. Every bore / groove / milling inside inherits this as its "side"; the sub-elements have no `side` of their own. |
| `turn` | part orientation relative to the XNC machine coordinate origin: `0`→0°, `1`→90°, `2`→180°, `3`→270°, **clockwise**. The part is rotated about the machine-coords origin; `<program>` `dx`/`dy` are already given in this turned frame. Absent / unparseable → `0`. (seen `0`) |
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
  that comes after it. A milling contour is the run of `<ml>` / `<mac>` / `<ma>` elements
  immediately following an `<ms>`.
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
   | `<me>` | close any open contour; emit a standalone elliptical milling operation |
   | `<ml>` | append a line segment to the open contour |
   | `<mac>` | append an arc segment to the open contour |
   | `<ma>` | append a radius-defined arc segment to the open contour |
   | `<gr>` | close any open contour; emit a standalone grooving |
   | `<mr>` | close any open contour; emit a standalone rectangle pocket |
   | `<bf> <bt> <bb> <bl> <br>` | close any open contour; emit a standalone bore (surface = element name) |

   Any element other than `<ml>` / `<mac>` / `<ma>` closes the open contour (as does end of
   program). A `<me>` is self-contained and does not consume following segment elements.

4. For every coordinate / depth attribute, **resolve the value** (§5): a plain number, or an
   expression string, or a bare variable name.

## 5. Value resolution — literals, variables, expressions

Any of `x y x1 y1 x2 y2 cx cy dp z t l w a r sxy` (and `<var expr>`) is **either**:

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

`+  -  *  /` and parentheses. Unary minus occurs (`-10`). Evaluated by
`Services/Xnc/XncExpressionEvaluator.cs` (§8).

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
| `c` | **tool-to-centre-line position**: `0` = center, `1` = right, `2` = left (all three occur in the fixture) |
| `p` | part plane, `0` = front, `1` = right, `2` = left, `3` = top, `4` = bottom (seen `0`) |
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
| `in` | entry movement code; `0` means no special lead-in |
| `out` | exit movement code; `1` means lead-out movement is enabled |
| `sxy` | optional start offset in the XY plane, expression, e.g. `tool.dia/2` |
| `fwd` | machining direction flag for closed paths; `true` means clockwise |

**side** comes from the operation's `side`. A program may contain several `<ms>` contours in a
row (the fixture has three straight ones followed by the circle); each `<ms>` closes the
previous contour and opens a new one.

For every newly created milling operation, set `in="0" out="1"` and encode a **clockwise**
traversal:

- a **straight / linear** contour (`<ms>` + `<ml>`), an ellipse (`<me>`) and a rectangle
  (`<mr>`) carry `fwd="true"`;
- a **curved** contour's arc segments (`<mac>` / `<ma>`) carry `dir="true"`.

Do not generalize the meaning of `fwd` to open contours beyond the source dialect's semantics.

### 6.5 Milling segments

A segment's **start point is implicit** — it is the end point of the previous element (the
`<ms>` entry for the first segment, otherwise the previous segment's end).

#### Line — `<ml>`

```xml
<ml x="dx+10" y="dy-35-40" dp="40"/>   <!-- dp present: ramp -->
<ml x="196" y="190"/>                  <!-- dp absent: keep the current depth -->
```

| attribute | meaning |
|---|---|
| `x`, `y` | segment **end** point (literal or expression) |
| `dp` | **optional** depth at the end of the segment. Present → a (possibly ramped) cut to that depth. Absent → carry the contour's current depth forward (the `<ms>` entry depth, or the previous segment's). |

#### Center-defined arc — `<mac>`

```xml
<mac x="232.5" y="400" cx="250" cy="400" dir="false"/>
```

| attribute | meaning |
|---|---|
| `x`, `y` | arc **end** point |
| `cx`, `cy` | arc **centre** |
| `dir` | sweep direction (bool). `dir="true"` = **clockwise** (the value a converter emits for a CW arc). |

There is **no explicit radius, sweep angle, or start point**. Derive them:

- `radius = distance(centre, start) = distance(centre, end)` (equal within rounding).
- sweep goes from `start` to `end` around `(cx, cy)` in the sense given by `dir` (`true` = CW).
- `dp` on `<mac>` is optional (none seen in the fixtures) → the arc holds the contour's current depth.

#### Radius-defined arc — `<ma>`

`<ma>` is also a contour segment. Its start point is implicit and its endpoint is `x/y`, as
with `<mac>`, but its radius is explicit:

```xml
<ma x="dx/2" y="dy/2+20" dp="15" r="20" dir="true"/>
```

| attribute | meaning |
|---|---|
| `x`, `y` | arc end point |
| `r` | arc radius |
| `dir` | arc sweep direction flag (bool) |
| `dp` | optional depth at the endpoint; absent means keep the current contour depth |

The centre is reconstructed from the implicit start point, endpoint, radius, and `dir`. Do not
infer an `<ma>` centre from comments or from the face on which the operation appears.

The reader tracks a running depth per contour: seeded from the `<ms>` entry `dp`, replaced
whenever an `<ml>`, `<mac>`, or `<ma>` carries its own `dp`, and stamped onto every segment as
`XncMillingSegment.Depth`.

### 6.6 Elliptical milling — `<me>`

`<me>` is a self-contained milling operation. It does not open a segment contour and is not
followed by `<ml>`, `<mac>`, or `<ma>` segments.

```xml
<me x="150" y="200" dp="20" in="0" out="1" sxy="tool.dia/2"
    fwd="true" l="20" w="20" a="0" c="1" name="Mill6"/>
```

| attribute | meaning |
|---|---|
| `name` | milling tool |
| `x`, `y` | ellipse reference position |
| `l`, `w` | ellipse **semi-axes** (radii), mm — a round `<me>` has `l == w` and diameter `2*l` (confirmed by `TestData/td-br-ml-conversion.project`, where an "R20" circle is `l="20" w="20"`) |
| `a` | rotation angle |
| `dp` | milling depth |
| `c` | tool-to-path or pocket positioning mode |
| `in`, `out` | entry and exit movement codes |
| `sxy` | XY start/path offset |
| `fwd` | for a closed ellipse, `true` means clockwise machining |

Newly created ellipse mills must set `in="0"`, `out="1"`, and `fwd="true"` (CW). A mill created
over a **blind** feature is a pocket (`c="3"`); one over a **through** feature keeps its
tool-to-centre-line position (`c="1"`). This blind/through rule applies to every generated
milling form (`<me>`, and the `<ms>` of a generated contour), not just ellipses. Any pocket
also sets `sxy="tool.dia/2"`.

### 6.7 Milling rectangles — `<mr>`

A self-contained rectangular milling primitive (a pocket or frame), **not** a contour of
segments — it has no following `<ml>`/`<mac>`/`<ma>`.

```xml
<mr x="100" y="100" dp="8" in="0" out="1" sxy="tool.dia/2" fwd="true" l="100" w="20" a="0" r="0" c="3" name="Mill6"/>
```

| attribute | meaning |
|---|---|
| `name` | tool reference |
| `x`, `y` | rectangle reference point (centre) |
| `l`, `w` | rectangle length / width, mm (literal or expression) |
| `a` | rotation angle, degrees (seen `0`) |
| `r` | corner radius, mm (seen `0` = sharp corners) |
| `dp` | milling **depth**, mm |
| `c` | **tool-to-centre-line position**: `0` = center, `1` = right, `2` = left, `3` = pocket (the fixture uses `3`) |
| `in`, `out` | entry and exit movement codes; newly created mills use `0` and `1` |
| `sxy` | XY start/path offset; pocket-type mills must use `tool.dia/2` |
| `fwd` | for a closed rectangle or pocket, `true` means clockwise machining |

**side** comes from the operation's `side`. Attribute meanings are inferred from the single
fixture instance — confirm `x/y` reference and `a`/`r` units against `td-2.project`.

### 6.8 Generated milling defaults

When a converter creates any milling primitive, it must set `in="0" out="1"` and encode a
**clockwise** traversal:

- **linear** contour starts (`<ms>` + `<ml>`), ellipses (`<me>`) and rectangles (`<mr>`) carry
  `fwd="true"`;
- the arc segments of a **curved** contour (`<mac>` / `<ma>`) carry `dir="true"`.

For every pocket-type mill, also set:

```xml
sxy="tool.dia/2"
```

This includes rectangular pockets (`<mr c="3">`), elliptical pockets (`<me c="3">`), and
contour-based pockets. Keep `sxy` as the expression so it follows the active tool diameter.
The `c` attribute selects centre-line/tool-position or pocket mode; it is distinct from `in`,
`out`, `fwd`, and `sxy`.

## 7. Worked example — `TestData/td-programs.project`

### Operation `id=3`, `side="true"` → `<program dx="1380" dy="600" dz="19">`

| # | element | resolved reading |
|---|---|---|
| 1–2 | `<tool>` ×2 | `Bore8` d=8, `Bore10` d=10 |
| 3 | `<ms>` | contour 1 entry, tool `Bore10` (`tool.dia`=10): `x=-10`, `y = dy-35-40 = 525`, `dp=4`, `sxy = tool.dia/2 = 5`, `c=0` (center), `fwd=true` |
| 4 | `<ml>` | contour 1 line to `x = dx+10 = 1390`, `y = 525`, `dp=40` (ramps 4 → 40) |
| 5 | `<ms>` | contour 2 entry, `Bore10`: `y = dy-35-120 = 445`, `dp=4`, `c=2` (left) |
| 6 | `<ml>` | contour 2 line to `(1390, 445)`, `dp=40` |
| 7 | `<ms>` | contour 3 entry, `Bore10`: `y = dy-35-200 = 365`, `dp=4`, `c=2` (left) |
| 8 | `<ml>` | contour 3 line to `(1390, 365)`, `dp=40` |
| 9–12 | `<bl>` ×4 | left-edge bores (`x=0`), tool `Bore8`, `z=10`: `y=65 dp=34`, `y=105 dp=26`, `y=505 dp=26`, `y=545 dp=34` |
| 13 | `<tool>` | `Cut3.2` d=3.2 |
| 14 | `<gr>` | grooving, `Cut3.2`, `Паз15 ()`: `(-10, 565)` → `(dx+10, 565) = (1390, 565)`, `dp=4`, width `t=10`, `c=0` (center) |
| 15 | `<gr>` | grooving, `Cut3.2`: `y1 = 565-80 = 485` → `(1390, 485)`, `c=2` (left) |
| 16 | `<gr>` | grooving, `Cut3.2`: `y1 = 565-160 = 405` → `(1390, 405)`, `c=1` (right) |
| 17 | `<tool>` | `Mill6` d=6 |
| 18 | `<var>` | `contMillDepth = dz + 2.00 = 21` |
| 19 | `<ms>` | contour 4 entry, tool `Mill6` (`tool.dia`=6): `(250, 382.5)`, `dp = contMillDepth = 21`, `c=2` (left), `out=1` |
| 20–23 | `<mac>` ×4 | arcs, all centre `(250, 400)`, `dir=false` (CW): end `(232.5,400)`, `(250,417.5)`, `(267.5,400)`, `(250,382.5)` — closes an `r = 17.5` circle at `(250, 400)` |
| 24 | `<mr>` | rectangle pocket, `Mill6`: origin `(100, 100)`, `l=100`, `w=20`, `a=0`, `r=0`, `dp=8`, `sxy = tool.dia/2 = 3`, `c=3` (pocket) |

### Operation `id=4`, `side="false"` → `<program dx="1380" dy="600" dz="19">`

| # | element | resolved reading |
|---|---|---|
| 1–2 | `<tool>` ×2 | `Bore15` d=15, `Bore8` d=8 |
| 3 | `<bf>` | face bore, `Bore8`, centre `(1353, 65)`, depth `dp=12`, `av=false` (blind) |
| 4 | `<bf>` | face bore, `Bore8`, centre `(1353, 545)`, depth `dp=12` |
| 5 | `<bf>` | face bore, `Bore15`, centre `(34, 65)`, depth `dp=14` |
| 6 | `<bf>` | face bore, `Bore15`, centre `(34, 545)`, depth `dp=14` |

## 8. Implementation

The reader described above is implemented:

| piece | location |
|---|---|
| Parsed models (read-only classes) | `MVVM/Models/Xnc/` — `XncProgram`, `XncTool`, `XncBore` + `BoreSurface`, `XncGrooving`, `XncMillingContour` + `XncMillingSegment`/`XncLineSegment`/`XncArcSegment`, `XncMillingRectangle`, `ToolPosition`, `XncPoint` |
| Parser | `Services/Xnc/XncProgramReader.cs` — `XncProgramReader.Read(XElement xncOperation)`; two-layer parse mirroring `GibLabProjectService.cs:388-390`. Reads the operation-level `side` → `XncProgram.Side` and `turn` → `XncProgram.Turn` (0..3; `XncProgram.TurnDegrees` = ×90 clockwise) |
| Expression evaluator | `Services/Xnc/XncExpressionEvaluator.cs` + `XncSymbolTable.cs` — recursive-descent `+ - * /`, parens, unary sign, dotted identifiers; case-insensitive symbols; no new dependency |
| Attribute getters | `Extensions/XContainersExtensions.cs` `#region XNC program sub-document` |
| Service entry point | `IProjectService.ReadXncPrograms(int partId)` → `GibLabProjectService` / `FakeProjectService` |
| Error type | `Services/Xnc/XncProgramFormatException.cs` |
| Tests | `XncOptimizerUI.Test/XncProgramReaderTests.cs` (against the fixture) and `XncExpressionEvaluatorTests.cs` |

`ToolPosition` maps `c` directly: `Center = 0, Right = 1, Left = 2, Pocket = 3`. Unknown
`c` values fall back to `Center`. Unknown program elements are ignored.

## 9. Open items — verify against `TestData/td-2.project`

- `<mac>`/`<ma>` `dir`: **`dir="true"` = clockwise** (the value `ConvertBoresAndMills` emits for
  a CW arc; `td-br-ml-conversion.project` uses it). `XncProgramReader` still reads it the other
  way round (`Clockwise = !dir`) — its `XncArcSegment.Clockwise` is inverted for this dialect
  and should not be trusted until the reader is fixed.
- `bt` / `bb` / `br` exact attribute set and the reference for `z` (reader pins the drilled
  edge coordinate to `0` / `dx` / `dy` and reads the other axis + `z`).
- `<mr>` — whether `x`/`y` is a corner or the centre; units of `a` (degrees assumed) and `r`.
- `in` / `out` lead-code enumeration (only `0` and `1` seen).
- `p` on `<gr>` (only `0` seen; not modelled).
- Confirm no milling segment types beyond `<ml>` and `<mac>`.
- Whether an `<ms>` with no following segment is a valid point operation (reader keeps it as
  an empty contour).
