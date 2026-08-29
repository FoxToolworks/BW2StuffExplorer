# BW2 Stuff Explorer v0.6 — Image Format Research Report

**Corpus:** extracted `Everything.stuff` tree  
**Research package:** `BW2_Image_Format_Research_20260826_142633.zip`  
**Scan timestamp:** 2026-08-26 14:26:33  
**`.555` validation addendum:** 2026-08-28, using all nine extracted raw samples  
**Research stages:** v0.6.2A–F  
**Scope:** TGA, BMP, `.555`, and explicit BWM material image references

## Executive result

The scan is internally consistent and complete for the requested image corpus:

| Format | Files | Parsed safely | Warnings |
|---|---:|---:|---:|
| TGA | 106 | 106 | 0 |
| BMP | 19 | 19 | 0 |
| `.555` | 9 | 9 observational rows | 0 |

All 3,396 non-empty BWM material image slots contain DDS references. No confirmed BWM slot points to TGA, BMP, or `.555`. This proves only that BWM does not use these formats through its six known material slots; it does **not** prove that the images are unused elsewhere in the archive.

## 1. TGA corpus

### Observed variants

| Image type | Pixel depth | Declared attribute bits | Files |
|---|---:|---:|---:|
| Uncompressed true-color | 24 | 0 | 52 |
| Uncompressed true-color | 32 | 8 | 52 |
| RLE true-color | 32 | 8 | 1 |
| Uncompressed grayscale | 8 | 8 | 1 |

All 106 headers and payload layouts validate. Every image uses bottom-left origin, left-to-right order, no color map, no image ID, and no interleaving.

### TGA version/footer cases

- 102 files contain the normal 26-byte TGA 2.0 footer with no extension area.
- 3 files end immediately after their uncompressed pixels and contain no TGA 2.0 footer:
  - `data\art\textures\t_greek_a_b_body.tga`
  - `data\art\textures\t_greek_a_b_helmet.tga`
  - `data\art\textures\t_greek_m_a_face.tga`
- 1 RLE file contains a 495-byte extension area followed by the 26-byte footer:
  - `data\art\textures\t_greekgarden_itemmfountain_g.lightmap.tga`

The RLE file contains 76,074 valid packets, decodes to exactly 262,144 pixels (512×512), and has no packet crossing a scanline boundary.

### Dimensions

The most common dimensions are 256×256 (58), 128×128 (15), 512×512 (14), 64×64 (6), and 32×32 (3). Ten additional dimensions occur once each, including 1024×1024, 1280×960, 1024×768, 640×480, and several rectangular textures.

### Safe implementation facts

The v0.6 reader may state:

- dimensions;
- image type and whether RLE is used;
- pixel depth;
- declared attribute-bit count;
- origin and direction;
- color-map presence;
- TGA 2.0 footer and extension-area presence;
- structural validity.

`AttributeBits = 8` is only the header declaration. It does not by itself prove that the image contains non-opaque pixels. Actual alpha usage requires a pixel-level scan.

## 2. BMP corpus

All 19 BMP files are structurally valid and use the same conservative Windows layout:

- `BITMAPINFOHEADER` (40 bytes);
- `BI_RGB` / uncompressed;
- pixel-data offset 54;
- bottom-up row order;
- stored file size matches actual file size;
- calculated pixel payload remains within the file;
- no color table is declared.

Eighteen files are 24-bit. `data\load_indicator.bmp` is the single 32-bit file (64×64). A 32-bit `BI_RGB` bitmap does not automatically prove meaningful alpha; its fourth byte must be treated cautiously unless masks or pixel evidence establish alpha semantics.

Observed dimensions:

| Dimensions | Files |
|---|---:|
| 1280×960 | 12 |
| 638×107 | 2 |
| 862×113 | 1 |
| 1018×131 | 1 |
| 256×256 | 1 |
| 64×64 | 1 |
| 800×600 | 1 |

## 3. `.555` sky textures

All nine files are in `data\weathersystem` and cover evil, good, and neutral day/dusk/night skies.

Every file has:

- size 131,088 bytes;
- the same 16-byte prefix;
- a unique SHA-256 hash;
- exactly 131,072 bytes after the prefix, equal to 256×256×2 bytes.

The shared prefix interpreted as little-endian 32-bit values is:

| Offset | Value | Evidence-based interpretation |
|---:|---:|---|
| 0 | 0 | unknown field |
| 4 | 256 | width, strongly supported |
| 8 | 256 | height, strongly supported |
| 12 | `0x019D00B0` | unknown field/flags |

This establishes a 16-byte header and a 256×256, 16-bit-per-pixel payload with high confidence.

### 0.6.2D raw-sample validation

All nine extracted files were matched back to the scanner observations by SHA-256 and analyzed across their complete payloads. Two controlled preview sets interpreted the same little-endian 16-bit values as X1R5G5B5 and X1B5G5R5.

The X1R5G5B5 interpretation produces coherent blue skies, warm sunlight and consistent good/neutral/evil weather variants. Swapping red and blue produces visibly implausible orange skies and cyan sunlight. The confirmed channel layout is therefore:

- bit 15: unused (`X`);
- bits 14–10: red;
- bits 9–5: green;
- bits 4–0: blue.

Bit 15 is zero in all 589,824 examined pixels (`9 × 256 × 256`). Native payload order already produces coherent images; no byte swap, rotation or vertical flip is required to establish the layout.

This confirms little-endian X1R5G5B5 for the supplied retail corpus. It does not establish how the engine would interpret a modified file with bit 15 set. The shared final header value `0x019D00B0` also remains raw and semantically unknown.

## 4. BWM image references

### Confirmed slots

| Material role | Non-empty references |
|---|---:|
| Diffuse Map | 1,571 |
| Light Map | 1,018 |
| Specular Map | 454 |
| Normal Map | 159 |
| Growth Map | 153 |
| Additional Map | 41 |
| **Total** | **3,396** |

All 3,396 stored references end in `.dds`.

### Resolution results

| Status | Rows |
|---|---:|
| Unique file name | 3,266 |
| Missing | 126 |
| Ambiguous file name | 4 |

The 126 missing rows contain 92 distinct stored names. Some are repeated missing lightmaps; others contain suspicious embedded spaces, for example `t_foliage_hackedpalm _neut_.dds` and `t_foliage_mushroom _neut_.dds`. These are evidence of stored source strings, not permission to silently repair the names.

The four ambiguous names each have two real DDS candidates:

- `mesh.dds`
- `t_icon02.dds`
- `t_icon01.dds`
- `lavaball_d.dds`

The explorer should continue to show both candidates and avoid choosing one without stronger path evidence.

### Scanner corpus note

The scan saw 824 BWM files because the extraction root also contains two root-level copies, `m_aztec_altar.bwm` and `m_aztecbeautification_altar.bwm`, in addition to their archive-path entries. The `Everything.stuff` inventory contains the two `data\art\models\...` entries, not the extra root copies. Therefore 822 is the archive inventory count; 824 is the local scan-root count. All four corresponding files produce the same empty/zero first-signature observation and were rejected safely.

## 5. Byte-identical image groups

Four duplicate groups were found:

- two TGA normal maps: `ezroaddesertcobblenormal.tga` and `ezroadgravelnormal.tga`;
- three BMP screens: `load_game_screen.bmp`, `load_screen_1.bmp`, and `save_game_screen.bmp`;
- two TGA grain-pile textures: `t_grainpile.tga` and `t_greek_grainpile.tga`;
- two TGA pattern textures: `ezroaddesertcobblepattern.tga` and `ezroadgravelpattern.tga`.

No `.555` files are byte-identical.

## 6. Format-neutral relationship design

Image relationships should be sourced by the file that stores the reference, not by the image reader:

1. A format provider (BWM, CSK, CHA, CCS, CAM, etc.) parses an explicit stored reference.
2. It emits a neutral relationship record containing source asset, source record/index, semantic role, and raw stored reference.
3. The archive resolver matches that raw value against all supported target assets (DDS, TGA, BMP, `.555`, or another type).
4. The target image receives the same edge as a reverse reference.

Consequences:

- A TGA referenced by CHA appears when the CHA provider learns that field; no BWM involvement is needed.
- A known source string with no matching target is `Missing`.
- Two or more candidates remain `Ambiguous`.
- An image with no proven incoming or outgoing edge truthfully shows no confirmed archive references.
- Filename similarity outside a decoded reference field remains non-evidence.

## 7. Version 0.6 implementation status

The research above was implemented in the final 0.6 release as follows:

### TGA provider

- bounded parsing for the observed TGA types 2, 3 and 10;
- footerless and TGA 2.0 cases supported;
- payload and RLE validation for the verified retail variants;
- factual Details for dimensions, encoding, origin, pixel depth, declared attribute bits and storage structure;
- actual non-opaque alpha remains separate from the header declaration.

### BMP provider

- bounded `BITMAPFILEHEADER` and `BITMAPINFOHEADER` parsing for the observed `BI_RGB` corpus;
- factual Details for dimensions, bit depth, compression, row order, DIB header and pixel offset;
- no alpha semantics are inferred for the 32-bit BMP without additional evidence.

### `.555` provider

- complete-payload high-bit validation across all nine files;
- controlled X1R5G5B5/X1B5G5R5 comparisons established the retail channel order;
- bounded validation of the 16-byte header and 256 × 256 payload;
- the fourth header field remains raw because its meaning is not established.

### Format-neutral image relationships

- relationship targets are generalized from DDS-only to DDS, TGA, BMP and `.555` assets;
- relationships are still emitted only by source providers that decode an explicit stored reference;
- the final UI evidence column is named `Candidates`;
- exact, unique, missing and ambiguous states remain unchanged.

### Corpus regression and UI finish

- all 134 TGA/BMP/`.555` files and all 822 archive BWM entries are covered by the optional strict retail-corpus regression;
- existing BWM Materials/References behavior is retained;
- empty, missing, unique and ambiguous reference states are regression-tested.

## Confidence summary

| Finding | Confidence |
|---|---|
| TGA header/type/dimensions/origin/footer facts | Confirmed |
| TGA RLE payload validity | Confirmed |
| TGA actual non-opaque alpha content | Not yet tested |
| BMP header/dimensions/bit depth/compression | Confirmed |
| BMP 32-bit alpha semantics | Not established |
| `.555` 16-byte header and 256×256×16-bit payload | Confirmed for this corpus |
| `.555` little-endian X1R5G5B5 channel order | Confirmed for this corpus |
| `.555` bit 15 is zero/unused in retail payloads | Confirmed for all 589,824 sampled pixels |
| `.555` engine behavior if bit 15 is set | Not established |
| BWM material slots reference DDS only | Confirmed for this corpus |
| TGA/BMP/`.555` usage by other formats | Pending those providers |
