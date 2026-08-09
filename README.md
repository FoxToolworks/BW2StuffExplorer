# BW2 Stuff Explorer

An OpenIV-inspired, read-only archive explorer for *Black & White 2* `*.stuff` files.

## Current release: 0.4.0

Version 0.4 keeps the proven WPF application and `StuffCore` implementation as its base, with a polished Windows 11-inspired interface and a new application icon. The archive reader, navigation, search and robust export pipeline remain read-only.

- open an archive from a file dialog or by drag and drop;
- validate its footer and 268-byte table-of-contents entries;
- browse the virtual directory tree and see only a folder's direct files;
- navigate with Back, Forward, Up and clickable path breadcrumbs;
- search paths and filenames responsively and recursively within the selected folder branch;
- show the Path column for recursive search results while keeping normal folder views compact;
- inspect the archived modification time, size, offset and file type;
- export one file, a folder or the complete archive;
- export multiple selected files while preserving their internal paths;
- follow byte- and file-level progress during exports and cancel without freezing the interface;
- write each file atomically so cancellation, disk errors and interrupted reads do not leave incomplete destination files;
- preflight multi-file destinations and confirm once before overwriting existing files;
- use File, Edit, View, Tools and Help menus, context menus and keyboard shortcuts;
- sort by name, modification time, type, size or offset and optionally group files by type;
- inspect entry properties and copy internal paths;
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

- `StuffCore`: format parsing, validation, bounded entry streams and cancellable atomic extraction.
- `StuffExplorer`: WPF desktop interface.
- `StuffCore.SelfTest`: dependency-free smoke test that creates and reads a synthetic archive.

See [docs/FORMAT.md](docs/FORMAT.md) for the currently known archive layout and [docs/ROADMAP.md](docs/ROADMAP.md) for planned stages.
