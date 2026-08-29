# Research documentation

BW2 Stuff Explorer uses evidence-based format research rather than filename guesses wherever possible. This folder keeps the reasoning that cannot be recovered from implementation code alone.

## Evidence policy

Research notes distinguish three levels:

- **Confirmed** — directly supported by binary structure, complete-corpus validation or equivalent reproducible evidence.
- **Strongly supported** — the corpus strongly favors one interpretation, but no official Lionhead symbol/specification is available.
- **Open** — structure or semantics remain unresolved and must not be presented as fact in the UI.

Explicit stored references outrank filename/path heuristics. Missing and ambiguous references remain visible instead of being silently repaired or guessed.

## Current reports

### [BWM materials](BWM-MATERIALS.md)

Current v0.6 material research for all 820 valid retail BWM models. Covers the 448-byte material record, stored material names, the six texture slots, the `Additional Map` correction, corpus totals and current export-research boundaries.

### [Image formats](IMAGE-FORMATS.md)

Current v0.6 research for all 106 TGA, 19 BMP and nine `.555` files in the verified retail archive, plus the format-neutral image-relationship design and the confirmed `.555` X1R5G5B5 layout.

### [Historical v0.5 format research](V0.5-FORMAT-RESEARCH.md)

The broad format survey that established the initial classification map for all 13 STUFF extensions and the first full BWM relationship scan. It is retained as a historical research handoff. Where terminology differs, the newer BWM/image reports above take precedence.

## Container format

The STUFF table-of-contents layout and timestamp evidence are documented separately in [`docs/FORMAT.md`](../FORMAT.md).

## Corpus and distribution rule

Research is performed against user-owned retail files. Original game assets, extracted binary samples and decoded copyrighted payloads are not included in this repository. Public documentation records format structure, metadata, counts, relationships and original tooling code only.
