# BW2 Stuff Explorer

A modern, OpenIV-inspired, read-only asset explorer for *Black & White 2* `*.stuff` archives.

BW2 Stuff Explorer is being built as a comfortable front end for understanding the game's packed assets: browse the archive, inspect known formats, follow proven relationships and export files without modifying the original STUFF archive.

## Current release: 0.6.0

Version 0.6 expands the asset-inspection layer introduced in 0.5.

### Highlights

- Browse `Everything.stuff` in an Explorer-style folder tree with navigation, search, sorting and grouping.
- Export individual files, selections, folders or the complete archive with progress, cancellation and atomic writes.
- Classify all 13 format families found in the verified retail archive.
- Inspect factual DDS metadata, including dimensions, compression, mip levels, declared color-space information and supported BC1/BC2/BC3 alpha analysis.
- Inspect BWM v5/v6 metadata and complete material rows through the **Contents** tab.
- Follow bidirectional BWM ↔ image relationships with explicit `Exact`, `Unique`, `Not found` and `Ambiguous` resolution states.
- Inspect bounded metadata for TGA, BMP and BW2 `.555` sky textures.
- Keep unsupported or unresolved data neutral instead of guessing.
- Keep the source archive strictly read-only.

The retail corpus used for regression contains 3,928 archive entries, including 822 BWM entries, 106 TGA files, 19 BMP files and nine `.555` sky textures.

## Format support

All 13 known `Everything.stuff` extensions are recognized and receive friendly classifications. Specialized structural inspection is currently deeper for the formats listed below.

| Format | Current 0.6 support |
| --- | --- |
| DDS | Header details, compression/mips, alpha capability and supported payload alpha analysis, BWM relationship targets |
| BWM | v5/v6 metadata, static/skinned detection, complete material contents and image references |
| TGA | Dimensions, encoding/RLE, origin, pixel layout and TGA 2.0 storage details |
| BMP | DIB header, dimensions, row order, bit depth, compression and pixel offset |
| `.555` | Verified 256 × 256 X1R5G5B5 sky-texture layout and raw header facts |
| BIK, CAM, EXC, CSK, CHA, CCS, ABN, DAN | Friendly classification with neutral fallback inspection; deeper providers are planned on the path to 1.0 |

Image preview is intentionally not part of 0.6. The next priority is to finish evidence-based providers for the remaining STUFF formats before expanding into the loose Black & White 2 installation data.

## Download and run

Download the latest Windows package from the [GitHub Releases page](https://github.com/FoxToolworks/BW2StuffExplorer/releases).

Requirements:

- Windows 10 or Windows 11
- x64 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

Extract the complete ZIP and start `BW2StuffExplorer.exe`. Keep the included `.dll`, `.deps.json` and `.runtimeconfig.json` files beside the executable.

The public Windows build is currently unsigned, so Microsoft Defender SmartScreen may identify it as an unknown publisher. Release SHA-256 hashes are published with each release for verification.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+O` | Open a STUFF archive |
| `Ctrl+F` | Focus the search field |
| `Ctrl+E` | Export selected file(s) |
| `Enter` | Open Properties for the focused file |
| `F5` | Refresh the current view |
| `Esc` | Clear search or cancel an active export |

## Build from source

Requirements: Visual Studio 2022 or the .NET 8 SDK with the Windows Desktop workload.

```powershell
dotnet build .\BW2StuffExplorer.sln -c Release
dotnet run --project .\tests\StuffCore.SelfTest\StuffCore.SelfTest.csproj -c Release
```

An optional strict regression can be run against a user-owned retail archive:

```powershell
dotnet run --project .\tests\StuffCore.SelfTest\StuffCore.SelfTest.csproj -c Release -- ".\Black & White 2\Data\Everything.stuff"
```

## Project structure

- `StuffCore` — archive parsing, validation, extraction, asset classification, metadata providers and relationship analysis.
- `StuffExplorer` — WPF desktop interface.
- `StuffCore.SelfTest` — dependency-free synthetic tests plus the optional retail-corpus regression.

## Documentation

- [STUFF format notes](docs/FORMAT.md)
- [Research index](docs/research/README.md)
- [Roadmap](docs/ROADMAP.md)
- [Changelog](CHANGELOG.md)

Research documentation separates confirmed binary facts, corpus-supported interpretations and open questions. Historical reports are retained where useful, with newer findings clearly marked when they supersede earlier terminology.

## Scope and safety

BW2 Stuff Explorer does not include original game assets and does not modify the source archive. Current releases are intentionally read-only. Future write/import functionality, if implemented, will remain a separate later milestone with explicit validation and safe-output rules.

## License

BW2 Stuff Explorer is released under the [MIT License](LICENSE).
