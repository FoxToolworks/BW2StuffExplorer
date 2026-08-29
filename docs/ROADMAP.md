# Roadmap

BW2 Stuff Explorer is being developed in two broad phases: first understand the packed `Everything.stuff` asset set as completely and conservatively as possible, then expand the same inspection model to the loose Black & White 2 installation data.

The project remains read-only through Version 1.0.

## Current state — 0.6

Version 0.6 provides:

- stable read-only STUFF browsing, navigation, search and export;
- friendly classification for all 13 format families in the verified retail archive;
- specialized inspection for DDS, BWM, TGA, BMP and `.555`;
- complete BWM material contents and BWM-driven image relationships;
- a shared provider/inspection model designed for additional formats and loose files;
- synthetic tests plus an optional strict retail-corpus regression.

See the [research index](research/README.md) for the evidence behind the implemented format interpretations.

## Path to 1.0 — finish `Everything.stuff`

The exact minor-version split may change as research reveals natural format groups, but the priority order is:

### Creature formats

- CSK — creature model/morph data;
- CHA — creature hair/fur data;
- CCS — creature support/bounding data;
- add Contents and References only where decoded structures provide explicit evidence.

### Camera and area formats

- CAM — camera/path sample data;
- EXC — camera exclusion zones;
- expose factual records without naming unresolved fields prematurely.

### Animation, dialogue and video

- ABN — advisor animation banks;
- DAN — dialogue annotation/lip-sync packs;
- BIK — factual Bink video metadata where useful;
- preserve explicit stored identifiers and relationships when they can be proven.

### 1.0 completion gate

Version 1.0 should mark the point where every format family in `Everything.stuff` has been investigated and receives the deepest evidence-safe inspection that current research supports.

That does **not** require every unknown byte in every format to be solved. Unknown fields remain unknown until evidence exists.

Before 1.0:

- run a complete retail-corpus regression;
- finish the cohesive file-icon family once the final STUFF format/provider set is stable;
- keep all archive operations read-only.

## First priority after 1.0 — Black & White 2 root integration

After the packed STUFF formats are stable, development moves outward to the complete game installation rather than immediately adding richer previews.

Planned root work includes:

- open/index loose game files alongside `Everything.stuff`;
- build one combined asset index across packed and loose sources;
- resolve cross-source relationships without assuming that STUFF and root data duplicate one another;
- add evidence-safe providers for important loose formats and configuration data;
- preserve source identity so users can see whether an asset comes from STUFF or the installation root.

This is the architectural step from a STUFF archive explorer toward a broader Black & White 2 modding workspace.

## Later milestones

Once STUFF and root assets share one stable inspection model, higher-level tools can build on top of that foundation:

- image preview for DDS, TGA, BMP and `.555`;
- decoded/text/hex viewers for suitable formats;
- 3D preview for BWM and later creature formats;
- export/conversion workflows such as DDS → PNG and BWM → OBJ/FBX;
- research toward safe model import/round-trip workflows;
- mod workspace, manifests, comparison and conflict detection;
- optional writer/import functionality that always writes to a new output and verifies the result.

Version numbers for these post-1.0 milestones are intentionally not fixed yet.

## Out of scope

Photoshop, generative upscaling and the separate texture-remaster automation are intentionally outside BW2 Stuff Explorer.
