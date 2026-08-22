# BW2 Stuff Explorer

An OpenIV-inspired, read-only archive explorer for *Black & White 2* `*.stuff` files.

## Release 0.5.0 — File-type analysis and asset inspection

Version 0.5 expands BW2 Stuff Explorer from a basic archive browser into an asset-aware exploration tool. It introduces automatic asset classification, BWM v5/v6 metadata parsing, DDS technical information and alpha analysis, as well as bidirectional relationships between BWM models and referenced textures.

File properties have been redesigned with dedicated General, Details and References views, while texture roles and asset contexts are kept separate from their actual file types. Browsing, grouped views and search performance have also been improved for large folders.

The application remains fully read-only, preserving the existing navigation, search and export workflow while laying the foundation for additional BW2 formats and future asset previews.

- open an archive from a file dialog or by drag and drop;
- validate its footer and 268-byte table-of-contents entries;
- browse the virtual directory tree and see only a folder's direct files;
- navigate with Back, Forward, Up and clickable path breadcrumbs;
- search paths and filenames responsively and recursively within the selected folder branch;
- show the Path column for recursive search results while keeping normal folder views compact;
- inspect the archived modification time, size, offset and friendly file type;
- export one file, a folder or the complete archive;
- export multiple selected files while preserving their internal paths;
- follow byte- and file-level progress during exports and cancel without freezing the interface;
- write each file atomically so cancellation, disk errors and interrupted reads do not leave incomplete destination files;
- preflight multi-file destinations and confirm once before overwriting existing files;
- use File, Edit, View, Tools and Help menus, context menus and keyboard shortcuts;
- sort by name, modification time, friendly type, size or offset and optionally group files by file type or asset category;
- inspect General, Details and References property tabs and copy displayed values;
- inspect factual DDS dimensions, texture kind, compression, FourCC/DXGI format, mip levels, color-space declaration, format alpha capability and actual non-opaque BC1/BC2/BC3 alpha data;
- inspect factual BWM signature, magic, version, model type, material count and texture-reference count;
- inspect BWM-to-DDS and DDS-to-BWM reference rows with material index, role, stored reference, resolution status and resolved target or candidates;
- start each newly displayed folder, search and properties tab at the top-left while retaining normal scrolling for genuinely overflowing content;
- use an English interface with strings separated for future localization;
- never modify the source archive.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+O` | Open a STUFF archive |
| `Ctrl+F` | Focus the search field |
| `Ctrl+E` | Export selected file(s) |
| `F5` | Refresh the current view |
| `Esc` | Clear the search field, or cancel an active export |

## Build

Requirements: Windows 10/11, Visual Studio 2022 or .NET 8 SDK with the Windows Desktop workload.

```powershell
dotnet build .\BW2StuffExplorer.sln -c Release
dotnet run --project .\tests\StuffCore.SelfTest\StuffCore.SelfTest.csproj -c Release
```

The GUI executable is produced below `src\StuffExplorer\bin\Release\net8.0-windows`.

## Run the release build

The prebuilt Windows package requires the x64 .NET 8 Desktop Runtime. Extract the complete ZIP and start `BW2StuffExplorer.exe`; the accompanying `.dll`, `.deps.json` and `.runtimeconfig.json` files must stay beside it.

## Architecture

- `StuffCore`: format parsing, validation, bounded entry streams, cancellable atomic extraction, bounded BWM/DDS metadata parsing, compressed DDS alpha analysis, shared metadata providers and the UI-independent bidirectional BW2 asset relationship/classification model.
- `StuffExplorer`: WPF desktop interface.
- `StuffCore.SelfTest`: dependency-free smoke test that creates and reads a synthetic archive.

See [docs/FORMAT.md](docs/FORMAT.md) for the currently known archive layout and [docs/ROADMAP.md](docs/ROADMAP.md) for planned stages.
The release verification sequence is recorded in [docs/V0.5-RELEASE-CHECKLIST.md](docs/V0.5-RELEASE-CHECKLIST.md).
