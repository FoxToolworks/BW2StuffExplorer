# BW2 Stuff Explorer v0.6 — BWM Material Research Report

**Date:** 2026-08-26  
**Research stages:** v0.6.1A–D  
**Corpus package:** `BW2_BWM_Material_Research_20260825_203305.zip`  
**Scope:** Complete BWM material corpus from the retail `Everything.stuff` archive  
**Predecessor:** [Historical v0.5 format research](V0.5-FORMAT-RESEARCH.md)

## 1. Purpose and result

This report records the evidence behind the BWM material model introduced for BW2 Stuff Explorer v0.6.1. It preserves the reasoning that cannot be recovered from the implementation alone.

The research produced two important corrections to the terminology used during v0.5:

1. The seventh 64-byte string in each BWM material record is strongly supported as a **stored material name or material identifier**, not a material/shader type.
2. The fifth texture slot is not exclusively animated content. It is therefore displayed conservatively as **Additional Map** instead of **Animated Map**.

The resulting BWM material layout used by the Explorer is:

| Offset within material | Size | v0.6.1 interpretation | Confidence |
|---:|---:|---|---|
| `0` | 64 bytes | Diffuse Map reference | Confirmed by structure and corpus use |
| `64` | 64 bytes | Light Map reference | Confirmed by structure and corpus use |
| `128` | 64 bytes | Growth Map reference | Confirmed by structure and corpus use |
| `192` | 64 bytes | Specular Map reference | Confirmed by structure and corpus use |
| `256` | 64 bytes | Additional Map reference | Structure confirmed; exact engine semantics open |
| `320` | 64 bytes | Normal Map reference | Confirmed by structure and corpus use |
| `384` | 64 bytes | Stored material name/identifier | Structure confirmed; meaning strongly supported |

Each material definition is exactly **448 bytes**. All seven values are fixed-width, null-terminated or null-padded strings.

## 2. Relationship to the v0.5 report

The v0.5 research report described the final two fields as:

- `Animated texture`
- `Material/shader type`

Those labels reflected the best available understanding at the time and should remain unchanged in the historical v0.5 report. The complete v0.6.1 corpus analysis provides stronger evidence and supersedes them for current code, UI and future research:

| v0.5 term | v0.6.1 term | Reason for change |
|---|---|---|
| Animated Map | Additional Map | The slot contains animated fire textures, blossom/growth textures and a cubemap. |
| Material/shader type | Stored material name | The values behave like freely assigned authoring/export names rather than an enum or fixed shader class. |

No byte offsets, material bounds or relationship-resolution rules changed. Only the interpretation and presentation were corrected.

## 3. Corpus and validation scope

The scan targeted all `.bwm` files extracted from the retail `Everything.stuff` archive.

| Corpus result | Count |
|---|---:|
| Archive BWM entries | 822 |
| Valid parsed BWM models | 820 |
| Confirmed zero-filled placeholder models | 2 |
| Material definitions | 1,753 |
| Static/articulated material definitions | 1,553 |
| Skinned material definitions | 200 |
| Distinct stored material names | 189 |

The research scanner reported 824 `.bwm` files when run over the selected extraction root. The additional two were duplicate loose copies of the two known zero-filled placeholder files located outside the normal `data\art\models` archive tree. They contain no materials and do not alter the archive totals above.

Material-count distribution:

- average: **2.14 materials per valid model**
- maximum: **15 materials in one model**
- **469 models** contain exactly one material
- **141 materials** contain no texture reference in any of the six slots

The 141 texturless materials are a key reason for retaining complete material objects. A relationship-only representation cannot preserve them.

## 4. Stored material name finding

### 4.1 Structural evidence

The seventh 64-byte value at material offset `384` was evaluated independently for every material definition:

- 1,753 of 1,753 fields are populated
- all values are printable, null-terminated ASCII strings
- all unused bytes after the terminator are zero
- no corrupt or non-text bytes were observed
- the longest observed value is 37 bytes, below the 64-byte capacity
- 189 distinct values occur

These properties confirm that the field stores a deliberate string value rather than numeric flags or an opaque binary structure.

### 4.2 Semantic evidence

Representative values include:

```text
_glossy_
_thatch_
_plants_
_walls_
lh_phys
1 - default
material #95
greek building texture
hand_shader
m_japanesetowncentre
musfroom
zdfdfv
```

The mixture is characteristic of names retained from an authoring or export workflow:

- descriptive material names
- preset-like names surrounded by underscores
- default and automatically numbered names
- names derived from a model or texture
- typographical errors and arbitrary temporary names

That variability is inconsistent with a small, controlled material-type or shader-type enum. The value is therefore exposed as **Material name** in the UI and stored conservatively as `StoredName` in the core model.

### 4.3 Confidence statement

The binary existence and string structure are **confirmed**. The interpretation as a stored material name or authoring identifier is **strongly supported by complete-corpus evidence**, but no official Lionhead specification was available to establish the engine's original field name.

The existing community BWM format description and `bwmtool` reader identify the field only as `type`; they do not document the corpus-based semantic interpretation established here. The research contribution is therefore the evidence for its meaning, not the first discovery of the field itself.

## 5. Name correlations and their limits

Several recurring names correlate strongly with texture-slot patterns:

| Stored material name | Materials | Observed correlation |
|---|---:|---|
| `_glossy_` | 283 | Always uses a Specular Map |
| `_plants_` | 80 | Always uses Diffuse, Light, Growth and Normal Maps |
| `_vines_` | 34 | Always uses Diffuse, Light, Growth and Normal Maps |
| `_rock_` | 34 | Always uses Diffuse and Normal Maps |
| `_rock_grey` | 9 | Always uses Diffuse and Normal Maps |
| `_alpha` | 17 | Observed only in skinned models |
| `_tree_` | 29 | Frequently uses Growth and Additional Maps |
| `_align_` | 28 | Frequently uses Specular or Additional Maps |
| `lh_phys` | 91 | Predominantly has no texture reference |

These correlations show that some names carry useful authoring or rendering intent. They do **not** make the name an authoritative replacement for the six stored slots.

Counterexamples include:

- `1 - default` and `2 - default` each occur with five different slot combinations.
- `_glossy_material #24` contains no Specular Map despite its name.

Consequently, material names may support search, diagnostics and later export naming, but they must never create or override a texture-role assignment.

## 6. Additional Map correction

The fifth texture field at material offset `256` was previously labelled `Animated Map`. The complete corpus contains 41 populated uses across 22 models:

| Content family | Uses | Interpretation |
|---|---:|---|
| Animated fire textures | 20 | Consistent with the former label |
| `*_blossom_growth.dds` textures | 20 | Growth/blossom content, not exclusively animation |
| `incloudscubemap.dds` | 1 | Cubemap used by `hand_shader` |

The field is structurally a valid texture-reference slot, but the observed content families do not support one narrow semantic role. `Additional Map` is therefore the safest current label.

This is a deliberate neutral term, not a claim that Lionhead called the slot “Additional.” The following remain unchanged:

- field offset and size
- material slot order
- case-insensitive DDS resolution
- forward model-to-texture relationships
- reverse texture-to-model relationships
- missing and ambiguous-reference handling

The exact runtime interpretation may depend on material name, model subtype or shader path. It remains an open reverse-engineering question.

## 7. Complete material model

v0.6.1 replaces the earlier reference-only view with a complete material object:

```text
Bw2BwmMaterial
├─ Index
├─ StoredName
├─ DiffuseMap
├─ LightMap
├─ GrowthMap
├─ SpecularMap
├─ AdditionalMap
└─ NormalMap
```

The model-level metadata contains the ordered list of all materials. Texture relationships are derived from the six populated slots rather than maintained as a separate source of truth.

This preserves two distinct concepts:

- **Contents:** every stored material, including materials without texture references
- **References:** only non-empty external texture references and their resolution results

This separation prevents UI duplication and provides a reusable foundation for other structured BW2 formats.

## 8. Explorer presentation

For a valid BWM containing at least one material, the dynamic `Contents` tab displays:

| Index | Material name | Diffuse Map | Light Map | Growth Map | Specular Map | Additional Map | Normal Map |
|---:|---|---|---|---|---|---|---|

Presentation rules:

1. Each material appears exactly once and retains its file order.
2. Empty slots display an em dash rather than creating a relationship.
3. Full stored values remain available through tooltips.
4. Texturless materials such as many `lh_phys` records remain visible.
5. Invalid or zero-filled placeholder BWM files do not receive false contents.
6. Formats without structured content do not display an empty `Contents` tab.

The `References` tab remains relationship-focused and records the material index, slot role, stored reference, resolution status and resolved path or candidate set.

## 9. Evidence priority

The v0.6.1 research reinforces the existing evidence hierarchy:

1. Explicit populated BWM texture slot
2. Other explicitly parsed asset reference
3. Stored material name as supporting context only
4. Strong path or filename rule
5. Technical DDS properties
6. Generic texture fallback

Neither a material name nor filename similarity may silently replace an explicit stored slot. Missing and ambiguous references remain first-class diagnostic results.

## 10. Export relevance

The complete material representation is useful groundwork for later OBJ/MTL and FBX export:

- `StoredName` can provide a stable source-derived material label.
- The six ordered texture slots can populate export material channels where a target format has an equivalent concept.
- Materials without textures can still be emitted and referenced.
- Duplicate or generic source names can be disambiguated with material index or model context.
- The original stored name can be preserved as metadata even when a sanitized export name is required.

However, v0.6.1 does **not** establish which mesh, submesh, index range or face set uses each material. That requires decoding and validating the later BWM material-reference or geometry-assignment structures. Until then, the Explorer may expose the material table but must not invent mesh-to-material assignments.

Export mapping must also remain conservative. For example, `Additional Map` cannot automatically become an emissive, animation, growth or environment channel without further evidence.

## 11. Validation and implementation handoff

The v0.6.1 core and UI changes were verified through:

- bounded parsing for BWM versions 5 and 6
- synthetic material records containing all six texture slots
- preservation of stored names such as `lh_phys` and `_glossy_`
- multiple-material and material-index tests
- a texturless material test
- derivation of relationship records from populated material slots
- successful Release build and `StuffCore.SelfTest`
- successful Explorer smoke test on the user's system

The implementation intentionally leaves archive loading, DDS resolution and export behavior unchanged outside the BWM material-model extension.

## 12. Confirmed findings, supported interpretations and open questions

### Confirmed

- Every valid BWM material record is 448 bytes and contains seven 64-byte strings.
- The seventh field is a clean stored ASCII string in all 1,753 retail-corpus materials.
- All six texture slots occur in the retail corpus.
- The fifth slot contains multiple content families and cannot be described as exclusively animated.
- 141 materials have no populated texture slot and require a contents representation independent of relationships.

### Strongly supported

- The seventh field is a freely assigned stored material name or material identifier.
- Some names represent authoring presets or rendering intent, while others are arbitrary exporter-era names.

### Open

- Lionhead's official name for the seventh field
- the precise engine semantics of the Additional Map slot
- whether Additional Map behavior changes by shader/material name
- mesh/submesh/face-to-material assignment structures
- how duplicate material names should be normalized for each future export format
- exact mapping of BW2 texture slots to OBJ/MTL and FBX material channels

## 13. Terminology for future work

Use the following terms consistently in code, UI and research after v0.6.1:

| Concept | Preferred term |
|---|---|
| Seventh material string | Stored material name / `StoredName` |
| User-facing seventh-field label | Material name |
| Fifth texture string | Additional Map / `AdditionalMap` |
| Ordered stored materials | BWM material contents |
| Populated external texture strings | BWM texture references |
| Unresolved exact stored filename | Missing reference |
| More than one exact filename candidate | Ambiguous reference |

Do not reintroduce `Material/shader type` or `Animated Map` as confirmed labels unless new binary or runtime evidence warrants another revision.

## 14. Conclusion

v0.6.1 converts the BWM material table from a texture-reference source into a complete, ordered material model. The seventh field is no longer treated as an unexplained type: the complete retail corpus strongly supports it as a stored material name or identifier. The former Animated Map label has been corrected to Additional Map because its observed contents span fire animation, blossom/growth textures and a cubemap.

These findings justify the new `Contents` tab, preserve 141 previously invisible texturless materials and establish the material-level foundation needed for later DDS inspection and OBJ/FBX export research. The remaining boundary is explicit: material definitions are understood, but geometry-to-material assignment is not yet decoded.

## 15. External technical references

- openblack BWM format documentation: <https://github.com/openblack/bw2-modding/blob/master/file_formats/bwm.md>
- openblack `bwmtool` BWM reader: <https://github.com/openblack/bwmtool/blob/master/BWMTool/BWM.cs>

External sources are used only to establish the prior public naming of the seventh field as `type`. All corpus totals, strings, correlations, slot contents and implementation conclusions in this report are derived from Jacky's retail BWM research package and the validated v0.6.1 implementation.
