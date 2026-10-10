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
| `turn` | part orientation relative to the XNC machine coordinate origin: `0`→0°, `1`→90°, `2`→180°, `3`→270°, **clockwise**. The part is rotated about the machine-coords origin; `<program>` `dx`/`dy` are already given in this turned frame. Absent / unparseable → `0`. Rewritten by `RotatePart` (§6.9). |
| `mirHor`, `mirVert` | mirror flags (seen `false`) |
| `code`, `typeName` | part / program identifiers, e.g. `10_08_06x001x1`, `10.08.06.ПАН-600` |
| `countBore`, `countCut`, `countMill` | summary counters (informational) |

### Decoding the `program` attribute (two-layer parse)

The attribute value is XML entity-escaped **once** (`&lt; &gt; &quot;`). LINQ-to-XML
un-escapes it automatically when you read `.Value`, so:

```csharp
// mirrors Services/GibLabProjectService.cs (XDocument.Parse(WebUtility.HtmlDecode(...)) call sites)
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
| `type` | `int` / `double` / `string` / `bool` (seen `"double"`); absent or unknown is read as `double` |
| `expr` | `int`/`double`: formula evaluated against the symbol table at the point it appears; `bool`: `true`/`false`; `string`: any text |
| `comment` | free text, e.g. `Глубина сквозного фрезерования контура` ("through-contour milling depth") |

Only `int`/`double` vars become symbols (`XncProgramReader`, `XncProgramMath.SeedProgramSymbols`);
a `string`/`bool` var is listed (`XncProgram.DeclaredVariables`, `Value = null`) but never
evaluated, so nothing numeric may reference it.

**Editing (Variables table):** `IProjectService.UpdateVariable(ref log, operationId, variableIndex, attribute, value)`
rewrites one attribute of the `variableIndex`-th `<var>` (rules in `Services/Xnc/XncVariableRules.cs`):
- `name` — a letter, then letters/digits/`_`/`.`; unique in the program (any case); not
  `dx`/`dy`/`dz`/`tool.dia`. Every whole-identifier reference in the program's expression
  attributes (`XncProgramMath.ExpressionAttributes`, incl. numeric vars' `expr`; not a string/bool
  var's literal) is renamed with it.
- `type` — one of the four; the current `expr` must fit it; a var referenced by any expression
  cannot become `string`/`bool`.
- `expr` — by type: `double` a numeric expression (same rules as bore values,
  `Services/Xnc/NumericExpression.cs`) over `dx`/`dy`/`dz` and the numeric vars declared **before**
  it; `int` the same, evaluating to a whole number; `bool` `true`/`false`; `string` any text
  (kept as typed, others trimmed).
- `comment` — free text; empty removes the attribute.

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

**Top/bottom duality (confirmed with the user).** Coordinates are Y-down from the part's
top-left corner (a bore at `y=35` is drawn above one at `y=65`), but plane names are visual:
`<bt>` (and groove `p="3"`) is the screen-**top** edge, i.e. `y = 0`; `<bb>` (groove `p="4"`)
is the screen-**bottom** edge, `y = dy`. `TestData/td-grooving-preview.project` agrees (`p=3`
grooves run at `y=0`, `p=4` at `y=200`).

| element | surface | fixed coordinate | position given by | `td-2.project` count |
|---|---|---|---|---|
| `bf` | panel **face** (vertical drill) | — (face = operation `side`) | `x`, `y` — centre on the face | 4150 |
| `bt` | **top** edge (screen top, `y = 0`), runs along X | `y = 0` | `x` (along edge), `z` (through thickness) | 384 |
| `bb` | **bottom** edge (screen bottom, `y = dy`), runs along X | `y = dy` | `x`, `z` | 684 |
| `bl` | **left** edge (`x = 0`), runs along Y | `x = 0` | `y` (along edge), `z` (through thickness) | 595 |
| `br` | **right** edge (`x = dx`), runs along Y | `x = dx` | `y`, `z` | 570 |

(Set confirmed by `Services/GibLabProjectService.cs` `ElementIsBore` and its mirror
`switch`, where `bf/bl/br` flip `y` and `bt`↔`bb` swap tag.)

Attributes:

| attribute | on | meaning |
|---|---|---|
| `name` | all | tool reference → `<tool>` |
| `dp` | all | **drill depth**, mm, into the surface |
| `x`, `y` | `bf` | **centre** of the hole on the face |
| `y`, `z` | `bl`, `br` | `y` = distance along the edge; `z` = through-thickness position of the horizontal hole (omitted when `m="true"`) |
| `x`, `z` | `bt`, `bb` | `x` = distance along the edge; `z` = through-thickness position (omitted when `m="true"`) |
| `ver` | `bl` (seen `2`) | element schema version |
| `ac` | all (seen `1`) | repeated-bore **array count**, integer, `1` by default (no repetition) |
| `as` | all | repeated-bore **array step**, mm, `null`/absent by default |
| `av` | all (bool, seen `false`) | repeated-bore **array is vertical** — step direction for the array (`ac`/`as`), unrelated to hole depth |
| `m` | edge bores `bt`/`bb`/`bl`/`br` (bool) | **middle** flag (confirmed by the user): `true` ⇒ the hole sits at mid-thickness, `z = dz/2`, and no `z` attribute is needed (`m` wins over any `z`); `false`/absent ⇒ `z` is required. `TestData/td-bl-65-bore.project` has `m="true"` with no `z` (`dz=18` ⇒ `z=9`) |

`ac`/`as`/`av` together describe one `<bf>`/`<bt>`/… element expanding into a row (or
column, when `av="true"`) of `ac` evenly-spaced holes starting at `(x, y)`, step `as` mm
apart. With `ac="1"` (the default seen everywhere so far) there is no repetition and
`as`/`av` are moot. **Not implemented by `XncProgramReader`** — it only reads the first
hole of the array; if array bores turn up in real data, expanding them into `ac`
separate `XncBore`s is future work.

Through-ness is not carried by any attribute: a bore is through when its `dp` (drill
depth) reaches the panel dimension it drills into — `dz` for a face bore (`bf`), `dx`
for an edge bore drilled from the left/right (`bl`/`br`), `dy` for one drilled from the
top/bottom (`bt`/`bb`).

**What to extract:** side = element name (+ operation `side` for `bf`); tool = `name`;
centre coordinates = `bf` → `(x, y)`, `bl`/`br` → `(edgeConst, y, z)` with `edgeConst ∈ {0, dx}`,
`bt`/`bb` → `(x, edgeConst, z)` with `edgeConst` = `0` for `bt`, `dy` for `bb`; depth = `dp`.

`bt` / `bb` / `br` are implemented in `XncProgramReader.cs` exactly as documented above
(edge pinned to `0`/`dx`/`dy`, other axis + `z` read from the element) — confirmed against
`td-2.project`, `td.project`, and `td-bores-displaying.project`.

**Editing (bores table):** `IProjectService.UpdateBore(ref log, operationId, boreIndex, attribute, expression)`
rewrites one attribute of the `boreIndex`-th bore element (document order among
`bf`/`bt`/`bb`/`bl`/`br`) of the operation's program. Editable attributes per element:
`bf` → `x`, `y`; `bt`/`bb` → `x`, `z`; `bl`/`br` → `y`, `z`; all → `dp` (the pinned edge
coordinate is never an attribute). The text is written **as typed** (trimmed), so an expression
such as `dx-32` stays parametric. Accepted input (`Services/Xnc/BoreExpression.cs`): digits,
one `.` per number, `dx`/`dy`/`dz` only (no `<var>`s, no `tool.dia`), `+ - * /`, parentheses;
a `+`/`-` sign only at the start or right after `(`; must evaluate to a finite number; `dp > 0`.
Writing `z` removes `m="true"` (an explicit `z` replaces the middle pin).

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
| `p` | part plane, `0` = front, `1` = left, `2` = right, `3` = top, `4` = bottom |
| `z` | **through-thickness position** into the edge band, mm - only present when `p` is `1`-`4` (edge-plane); absent for `p="0"` |
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
| `fwd` | traversal direction of the whole mill (absent = `true`); see "Traversal direction" below |

**side** comes from the operation's `side`. A program may contain several `<ms>` contours in a
row (the fixture has three straight ones followed by the circle); each `<ms>` closes the
previous contour and opens a new one.

For every newly created milling operation, set `in="0" out="1"` and `fwd="true"`:

- a **straight / linear** contour (`<ms>` + `<ml>`), an ellipse (`<me>`, clockwise) and a
  rectangle (`<mr>`, clockwise) carry `fwd="true"`;
- a **round** contour (bore -> mill) starts at the circle top `(cx, cy-r)` and is declared
  counter-clockwise on screen with four `<mac dir="false">` quarter arcs (left, bottom, right,
  top); with `fwd="true"` it also travels counter-clockwise (see §6.8).

#### Traversal direction (confirmed with the user)

- The traversal direction belongs to the mill's head element (`<ms>`, `<me>`, `<mr>`) through
  `fwd` (absent = `true`) and is the same for every `<ml>` / `<mac>` / `<ma>` of that `<ms>`.
  An arc's `dir` only defines the arc's geometry (which arc is drawn), never the traversal.
- **Closed** `<ms>` + `<ml>` / `<mac>` / `<ma>` contours (pockets, `c="3"`, included):
  `fwd="true"` ⇒ counter-clockwise, `fwd="false"` ⇒ clockwise.
- **Rectangles (`<mr>`) and ellipses (`<me>`)** (pockets included) run the other way:
  `fwd="true"` ⇒ clockwise, `fwd="false"` ⇒ counter-clockwise.
- **Open** paths: `fwd="true"` ⇒ from the `<ms>` entry through each segment end to the last
  one; `fwd="false"` ⇒ the reverse (last segment end back to the entry).
- Clockwise is meant in the operator's view, which is the raw XNC frame mirrored in Y: the
  GibLab-authored circles in `TestData/td.project` use `dir="false"` (counter-clockwise) while
  their raw math angle decreases. `Services/Xnc/ContourOffsetGeometry.cs`
  (`TravelsCounterClockwise`) encodes these rules for both "Offset mill path" and the part
  preview's mill overlay.

#### Tool side of a closed `<ms>` contour (confirmed with the user)

Whether the tool runs inside or outside the shape's perimeter follows the **declaration order**
of the segment end points alone (as seen on screen), never `fwd`:

- declared counter-clockwise: `c="1"` (right) ⇒ tool **outside**, `c="2"` (left) ⇒ tool **inside**;
- declared clockwise: the reverse - `c="1"` ⇒ inside, `c="2"` ⇒ outside;
- `c="3"` (pocket) ⇒ always inside (pockets are declared counter-clockwise).

In other words `c="1"`/`c="2"` are right/left of the declared order; `fwd` only changes the
travel direction and start point. Open contours: `c="1"`/`c="2"` are right/left of travel.
`<mr>` / `<me>`: `c="1"`/`c="2"` are right/left of their travel (`fwd` above), `c="3"` inside.
Implemented by the part preview (`MillPreviewGeometry`); "Offset mill path" does not use `c`.

> Generated mills follow this rule: `<mr>` / `<me>` with `fwd="true"` travel clockwise, a
> generated round `<ms>` contour with `fwd="true"` counter-clockwise.

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
| `dir` | arc sweep direction (bool): `dir="true"` = **clockwise** sweep, `false` = counter-clockwise |
| `dp` | optional depth at the endpoint; absent means keep the current contour depth |

The centre is reconstructed from the implicit start point, endpoint, radius, and `dir`
(`XncProgramMath.TryReconstructArcCentre`, used by both `XncProgramReader` and the mill-offset
rewriter). Do not infer an `<ma>` centre from comments or from the face on which the operation
appears. An `<ma>` whose radius cannot span its chord is a format error.

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
| `a` | rotation angle, degrees; positive turns **clockwise** in the operator's view (on screen) - confirmed with the user |
| `dp` | milling depth |
| `c` | tool-to-path or pocket positioning mode |
| `in`, `out` | entry and exit movement codes |
| `sxy` | XY start/path offset |
| `fwd` | traversal direction: `true` = clockwise, `false` = counter-clockwise (see §6.4 "Traversal direction") |
| (start) | cutting starts at the local `y-` end of the `w` semi-axis, turned by `a` (confirmed with the user) |

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
| `a` | rotation angle, degrees; positive turns **clockwise** in the operator's view (on screen) - confirmed with the user |
| `r` | corner radius, mm (seen `0` = sharp corners) |
| `dp` | milling **depth**, mm |
| `c` | **tool-to-centre-line position**: `0` = center, `1` = right, `2` = left, `3` = pocket (the fixture uses `3`) |
| `in`, `out` | entry and exit movement codes; newly created mills use `0` and `1` |
| `sxy` | XY start/path offset; pocket-type mills must use `tool.dia/2` |
| `fwd` | traversal direction: `true` = clockwise, `false` = counter-clockwise (see §6.4 "Traversal direction") |
| (start) | cutting starts at the middle of the local `y-` side, turned by `a` (confirmed with the user) |

**side** comes from the operation's `side`. Attribute meanings are inferred from the single
fixture instance — confirm `x/y` reference and `a`/`r` units against `td-2.project`.

### 6.8 Generated milling defaults

When a converter creates any milling primitive, it must set `in="0" out="1"` and `fwd="true"`:

- **linear** contour starts (`<ms>` + `<ml>`), ellipses (`<me>`) and rectangles (`<mr>`) carry
  `fwd="true"` (`<mr>` / `<me>` therefore travel clockwise);
- a **round** contour (bore -> mill) is `<ms x=cx y=cy-r fwd="true">` followed by four
  `<mac dir="false">` quarter arcs ending at `(cx-r, cy)`, `(cx, cy+r)`, `(cx+r, cy)`,
  `(cx, cy-r)` - declared and travelled counter-clockwise on screen. A through bore uses
  `c="2"` (tool inside the hole, per "Tool side of a closed `<ms>` contour"), a blind one `c="3"`.

For every pocket-type mill, also set:

```xml
sxy="tool.dia/2"
```

This includes rectangular pockets (`<mr c="3">`), elliptical pockets (`<me c="3">`), and
contour-based pockets. Keep `sxy` as the expression so it follows the active tool diameter.
The `c` attribute selects centre-line/tool-position or pocket mode; it is distinct from `in`,
`out`, `fwd`, and `sxy`.

### 6.9 Rotating a program (`turn` rewrite)

`IProjectService.RotatePart(ref log, partId, targetTurn)` (geometry in
`Services/Xnc/XncProgramRotator.cs`) turns a part to an absolute `turn`. Every XNC operation of
the part gets `turn = targetTurn`; its program is rotated by
`k = (targetTurn − ownTurn) mod 4` clockwise quarter steps (operations already at the target are
untouched, so a discordant part ends consistent). Both faces (`side` true/false) use the same
transform, matching the preview, which overlays them in one frame.

One clockwise step, in the Y-down frame with the origin kept at the top-left corner:
`(x, y) → (dy − y, x)`, then `dx`/`dy` swap.

| element | rewrite per step |
|---|---|
| `<program>` | `dx` ↔ `dy` |
| `<bf>`, `<ms>`, `<ml>`, `<ma>` | point `x,y` (`r`, `dir`, `fwd` unchanged) |
| `<mac>` | points `x,y` and `cx,cy` (`dir` unchanged — rotation keeps handedness) |
| `<bt>/<br>/<bb>/<bl>` | tag moves clockwise `bt→br→bb→bl→bt`; the edge point is rotated and the new along-edge coordinate (`x` on top/bottom, `y` on left/right) taken from it; `z`, `dp` unchanged |
| bore array (`ac>1`) | step vector turns with the part (face bore: `av` flips); a step that would point backwards restarts the array from its last hole so `as` stays positive |
| `<gr>` `p="0"` | points `x1,y1` / `x2,y2`; order kept (`c` is relative to start → end); `c`, `t`, `dp` unchanged |
| `<gr>` `p` 1..4 | points rotated, then swapped when `x1 > x2` or `y1 > y2` so start ≤ end (GibLab's own behavior, confirmed by the user); `p` moves clockwise `3→2→4→1→3`; `c` Right ↔ Left flips on each step that leaves top/bottom (`p` 3/4) — an edge groove's `c` is relative to a fixed travel per plane (+X for `p` 3/4, −Y for `p` 1/2, the convention `GroovePreviewRenderer.ResolveTravelPosition` draws), which a clockwise step reverses from top/bottom to right/left but keeps from right/left to bottom/top. GibLab itself never flips `c` on a turn (a GibLab bug, per the user); `RotatePart(..., flipEdgeGrooveTcl: false)` — the "Never flips TCL on program turning" option — reproduces that. `z`, `t`, `dp` unchanged |
| `<mr>`, `<me>` | centre `x,y`; `l` ↔ `w` (same `a` — the shape is centrally symmetric) |

Values are resolved against the original symbols (`dx`, `dy`, `dz`, `<var>`s, per-element
`tool.dia`). A coordinate whose value changes is written as a number rounded to 4 decimals; any
other value keeps its authored text. On an odd number of steps every expression attribute
(`x y z x1 y1 x2 y2 cx cy dp t l w r a sxy as` and `<var expr>`) has whole `dx`/`dy` identifiers
swapped (`dx+10` → `dy+10`, `dp="dx"` → `dp="dy"`), so it evaluates to the same length in the
turned frame. Verified by `XncOptimizerUI.Test/PartRotationTests.cs` (`td-rotation.project`,
`td-bl-65-bore.project`: a `bl` bore at `y=65` on `500×200` becomes a `bt` bore at `x=135` on
`200×500`).

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
| 20–23 | `<mac>` ×4 | arcs, all centre `(250, 400)`, `dir=false` (counter-clockwise sweep): end `(232.5,400)`, `(250,417.5)`, `(267.5,400)`, `(250,382.5)` — closes an `r = 17.5` circle at `(250, 400)` |
| 24 | `<mr>` | rectangle pocket, `Mill6`: origin `(100, 100)`, `l=100`, `w=20`, `a=0`, `r=0`, `dp=8`, `sxy = tool.dia/2 = 3`, `c=3` (pocket) |

### Operation `id=4`, `side="false"` → `<program dx="1380" dy="600" dz="19">`

| # | element | resolved reading |
|---|---|---|
| 1–2 | `<tool>` ×2 | `Bore15` d=15, `Bore8` d=8 |
| 3 | `<bf>` | face bore, `Bore8`, centre `(1353, 65)`, depth `dp=12` (blind: `12 < dz=19`) |
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

- `<mac>`/`<ma>` `dir`: **`dir="true"` = clockwise sweep** — confirmed by the user (geometry
  only; see §6.4 "Traversal direction"). `XncProgramReader` reads it that way:
  `XncArcSegment.Clockwise = dir`, clockwise in the operator's view (= the Y-down part preview).
- `<mr>` — whether `x`/`y` is a corner or the centre; units of `a` (degrees assumed) and `r`.
  The part preview assumes centre and degrees; `a` turns clockwise on screen (confirmed).
- `in` / `out` lead-code enumeration (only `0` and `1` seen).
- `p` on `<gr>` (only `0` seen; not modelled).
- Confirm no milling segment types beyond `<ml>` and `<mac>`.
- Whether an `<ms>` with no following segment is a valid point operation (reader keeps it as
  an empty contour).
