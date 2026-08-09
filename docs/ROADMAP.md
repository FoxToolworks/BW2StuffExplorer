# Roadmap

## Completed milestones

### 0.1 — format and functionality proof

- safe read-only archive parser;
- directory tree, search and selective extraction;
- dependency-free synthetic archive self-test.

### 0.2 — explorer UX

- English UI with centralized strings for future localization;
- File, View, Tools and Help menus;
- file and folder context menus;
- multiple selection, keyboard shortcuts and sortable columns;
- human-readable sizes, entry properties and improved status feedback.

### 0.2.1 — navigation and organization

- direct-content folder browsing with recursive search;
- Back, Forward and Up navigation with clickable breadcrumbs;
- Edit menu clipboard actions;
- configurable sorting and file-type grouping.

### 0.2.2 — metadata and layout fixes

- identified and exposed the archived Unix modification timestamp;
- added Modified display, sorting and entry-property details;
- stabilized user-resizable file-list column widths.

### 0.3 — robust long-running operations

- responsive byte- and file-level progress for every export mode;
- cancellation from the status bar, `Esc` and graceful application close;
- atomic per-file writes with incomplete-file cleanup;
- destination preflight, overwrite confirmation and detailed I/O errors;
- expanded malformed-archive and cancellation self-tests.

### 0.4 — presentation and first public release

- compact Windows 11-inspired interface and application icon;
- refined navigation, breadcrumbs, search and entry properties;
- regional date, time and number formatting;
- release metadata, documentation and packaging preparation.

## Planned milestones

### 0.5 — file-type analysis

- identify common Black & White 2 asset types beyond filename extensions;
- replace terse extension-only group names with friendly descriptions;
- add specialized file visuals where the format is understood;
- keep unknown formats neutral and preserve read-only behavior.

## V1.0 — read-only STUFF Explorer

- safe archive reader and validation;
- folder tree, file table and instant search;
- selective folder/file export and full extraction;
- the completed 0.3 robustness work;
- tests against synthetic and user-owned real archives;
- reproducible Windows release package.

## V1.1 — asset inspection

- DDS preview and technical metadata;
- hexadecimal/text viewers for unknown formats;
- improved sorting and file-type filters.

## V2 — mod workspace

- keep modifications outside original game archives;
- project manifests, comparison and conflict detection;
- safe install/uninstall of loose files.

## V3 — optional STUFF writer

- replace/add/remove entries;
- rebuild to a new archive without overwriting the original;
- reopen and byte-verify every generated archive.

Photoshop and generative upscaling automation are intentionally outside this project.
