# `.project` file — outer XML schema

Reflects `XncOptimizerUI/TestData/*.project` (12 fixtures). Producer: GibLab panel CAM/nesting,
exported from "Базис-Мебельщик" (Bazis). Not ground truth for every dialect.

## Document rules

- Plain XML text, **not** JSON/zip. Prolog `<?xml version="1.0" encoding="UTF-8"?>`, no BOM.
  Files are written as a **single line**, no indentation.
- Root `<project>`. **No namespaces**, no XSD/DTD, no processing instructions.
- `<good>` and `<operation>` are flat, repeated, direct children of `<project>` — **all goods
  first, then all operations**.
- All cross-references are integer `id` attributes, except the part->EL banding refs
  (`@operation#<id>` token form).
- Two attribute-ordering conventions exist (Bazis alphabetical vs hand-authored natural). Do not
  rely on order. `XDocument` round-trips whatever order it is given; only touch nodes you change.

## `<project>` (root)

| attribute | example / notes |
|---|---|
| `version` | `23051701` — schema stamp, identical in every fixture |
| `project.uuid` | GUID, unique per file |
| `cost`, `costMaterial`, `costOperation` | always `0` in fixtures |
| `date`, `orderDate` | Unix epoch **milliseconds**; absent on some hand-made fixtures |
| `importBMV` | e.g. `1.84` — only on Bazis-imported files |
| `description` | timestamped audit trail, itself HTML-escaped (`&lt;br/&gt;`); appended by `GibLabProjectService.AppendDescription` |

## `<good typeId="product">` + product `<part>`

```xml
<good typeId="product" id="1" name="L512" count="1" code="512" product.import="bm.1.84"
      cost="0" costMaterial="0" costOperation="0">
  <part l="1380" w="300" dl="1380" dw="300" jl="1378" jw="298" cl="1380" cw="300"
        count="2" usedCount="0" minusCount="0" txt="false" name="as-is" id="1"
        part.code="07.01" part.designation="07.01" part.position="1"
        elt="@operation#1" eltMat="Крайка 22х0,8мм" elb="@operation#1" elbMat="Крайка 22х0,8мм"
        ell="@operation#1" ellMat="Крайка 22х0,8мм" elr="@operation#1" elrMat="Крайка 22х0,8мм"/>
</good>
```

`<good typeId="product">`: `id`, `name`, `count`, `cost*`; optional `code`, `product.import`.
A product may have **zero** `<part>` children (empty placeholder — legal).

### §Part — product `<part>` attributes

| group | attributes | meaning |
|---|---|---|
| finished size | `l`, `w` | length / width, mm (thickness comes from the sheet `t`) |
| detail (drawing) size | `dl`, `dw` | |
| cut size | `cl`, `cw` | size as sawn |
| trimmed size | `jl`, `jw` | after edge trim (`jl=1378` when `l=1380`) |
| quantities | `count`, `usedCount`, `minusCount` | |
| identity | `id`, `name` | `id` is the ref target for operations |
| grain | `txt` (`true`/`false`) | texture / grain-direction flag (`Part.ConsiderTexture`) |
| Bazis identity | `code`, `part.code`, `part.designation`, `part.position` | imported files only |
| edge banding ref | `elt` / `elb` / `ell` / `elr` = `@operation#<EL id>` | top / bottom / left / right edge |
| edge banding name | `eltMat` / `elbMat` / `ellMat` / `elrMat` | material-name string, must match the EL op's band |

When editing size, keep the family consistent: a real resize normally moves `l`+`dl` (and often
`cl`,`jl`) together — check what the source used. When editing banding, update **both** the
`el*` ref and the `el*Mat` name, and ensure the referenced `EL` operation and `band` good exist.

## `<good typeId="tool.cutting">` (saw / nesting params — exactly one)

```xml
<good typeId="tool.cutting" id="2" swSawthick="4.4" swSawthickDiv="-1" swPackageHeight="40"
      swMaxturns="6" swComplexBand="true" swTrimIncludeSaw="false" swSort="0" swSortInBand="3"
      swMinSizeBand="0" swMaxSizeBand="0" swMinPruning="0" swMaxLengthBand="0"/>
```

`swSawthick` = saw kerf (mm). `id` varies across files — referenced by `operation[CS]/@tool1`.

## `<good typeId="tool.edgeline">` (edgebander line params — 0..n, usually in pairs)

```xml
<good typeId="tool.edgeline" id="4" elWidthPreJoint="1" elRestSide="0" elMinSize="0" elCost="0"/>
```

Referenced by `operation[EL]/@tool1`. Present only when the file has edge banding.

## `<good typeId="sheet">` (stock board) + stock `<part>`

```xml
<good typeId="sheet" id="3" name="Green" code="-10" l="2800" w="2070" t="19" count="1.82159" cost="0">
  <part l="2800" w="2070" count="10000" usedCount="0" id="10"/>
</good>
```

`t` = board thickness. `count` = boards consumed (fractional). The inner `<part>` is the sheet
blank (`count` 1000/10000); its `id` is referenced **last** in each `operation[CS]`.

## `<good typeId="band">` (edgebanding material — 0..n)

```xml
<good typeId="band" id="5" name="Крайка 22х0,8мм" code="ST003" t="1" w="22" count="5" cost="0"/>
```

`t` = tape thickness, `w` = tape width (mm). A name prefixed `!СмЧт ` marks the "other drawing
side" band; it usually pairs with a second `EL` op and `count="0"`. Referenced by
`operation[EL]/material/@id`.

## `<operation typeId="CS">` (cutting / nesting)

```xml
<operation typeId="CS" id="1" l="2800" w="2070" t="19" tool1="2" csDirectCut="0" csTexture="true"
           cFillRep="0.9" cTrimL="10" cTrimW="10" cSizeMode="1" cMaterialAmountP="0.91079" ...
           costMaterial="0" costOperation="0" costTotal="0">
  <material id="3" count="0.9108"/>          <!-- id -> good typeId="sheet" -->
  <part id="1"/><part id="2"/><part id="2"/> <!-- product parts, then the sheet part id LAST -->
</operation>
```

`c*` attributes are nesting-planner parameters (informational for most edits). `@tool1` ->
`tool.cutting/@id`.

## `<operation typeId="EL">` (edge banding)

```xml
<operation typeId="EL" id="1" tool1="4" elSymbol="A" elColor="#000000" elLength="3.96"
           w="22" t="1" elWastePRC="0.13" printable="true" startNewPage="true"
           costMaterial="0" costOperation="0" costTotal="0">
  <material id="5" count="5"/>               <!-- id -> good typeId="band" -->
  <part id="1"/>
</operation>
```

Files with two edge materials have two EL ops (`elSymbol="A"` / `"B"`, distinct `elColor`). The
second op can have `elLength="0"`, `count="0"` and **no `<part>` child**. Parts point back via
`elt/elb/ell/elr="@operation#<id>"`.

## `<operation typeId="XNC">` (drill / rout / groove program)

```xml
<operation typeId="XNC" id="3" side="true" turn="0" mirHor="false" mirVert="false"
           code="10_08_06x001x1" typeName="10.08.06.ПАН-600" bySizeDetail="true" count="1"
           countBore="4" countCut="21" countMill="4.89444"
           program="&lt;?xml ...&gt;&lt;program dx=&quot;1380&quot; dy=&quot;600&quot; dz=&quot;19&quot;&gt; ... &lt;/program&gt;"
           cost="0" costTotal="0" price="0">
  <part id="1"/>                             <!-- the single product part being machined -->
</operation>
```

| attribute | meaning |
|---|---|
| `program` | the whole CNC program as an **XML-entity-escaped XML sub-document** (`&lt; &gt; &quot;`), single-level escaping. Only place machining geometry lives. See `xnc-program-format.md`. |
| `side` | `true` / `false` — which panel face; every bore/groove/mill inside inherits it |
| `turn` | face rotation code (`0`; `1` seen with a rotated program `dx`/`dy`) |
| `mirHor`, `mirVert` | mirror flags |
| `code`, `typeName` | program / part identifiers |
| `count` | copies |
| `countBore`, `countCut`, `countMill` | informational summary counters |
| `optimized`, `groupCode` | **not** in source fixtures — added by `GroupIdenticalElements` on output |

"One XNC operation per machined face" — a two-sided part has two XNC ops (e.g.
`td-programs.project` id=3 `side="true"` + id=4 `side="false"`).

## Id cross-reference map

| from | to |
|---|---|
| `operation/part/@id` | product `part/@id` |
| `operation[CS]/material/@id` | `good[typeId="sheet"]/@id` |
| last `operation[CS]/part/@id` | `good[typeId="sheet"]/part/@id` (the stock blank) |
| `operation[EL]/material/@id` | `good[typeId="band"]/@id` |
| `operation/@tool1` | `good[typeId="tool.cutting"` or `"tool.edgeline"]/@id` |
| `part/@elt` `@elb` `@ell` `@elr` | `"@operation#<EL id>"` — parse as `value.Split('#')[1]` |

## Fixture cheat-sheet (what each test file exercises)

| file | payload |
|---|---|
| `td-grooving.project` | 1 axis-parallel through `<gr>` (`c="1"`) |
| `td-grooving-diagonal.project` | 1 diagonal `<gr>` — non-convertible |
| `td-grooving-interior.project` | 1 axis-parallel `<gr>` fully inside the edges |
| `td-grooving-mixed.project` | one convertible `<gr>` + one diagonal |
| `td-grooving-secondary-pass.project` | 1 `<gr>` with `p="1"` (secondary pass) |
| `td-milling.project` | XNC id2 `<ms c="2">`+`<ml>` groove-as-drill; XNC id3 one `<mr c="3">` pocket |
| `td-milling-pocket.project` | 5 `<mr>` (normal / narrow / `a="45"` / `c="0"` / through `dp>dz`) |
| `td-milling-shallow.project` | 1 shallow through `<ms c="2">`+`<ml>` |
| `td-mills-optimization.project` | 2 parts (`as-is` / `optimized`), 6 parallel mill passes each; edge banding present |
| `td-missing-ml-error.project` | contour with `<mac>` arc + an `<ml>` with no `dp` |
| `td-pocket-to-groove.project` | 8 products (7 empty); contour-traced thin pockets (`c="3"`); `turn="1"` |
| `td-programs.project` | coverage: `<tool>`, `<ms>/<ml>`, `<bl>`, `<gr>` (c=0/1/2), `<var>`, `<mac>` circle, `<mr>`; 2nd op = `<bf>` face bores |
