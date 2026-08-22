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

## Completed Version 0.5

### 0.5 — file-type analysis

- [x] introduce separate format, asset-category and file-type classifications;
- [x] replace terse extension-only names with friendly descriptions;
- [x] add grouping by friendly file type and broader asset category;
- [x] refine BWM entries as static, skinned or unknown model data from their headers;
- [x] infer confirmed DDS roles from BWM material references;
- [x] restore grouped-list virtualization and responsive incremental search for large result sets;
- [x] add conservative landscape and creature DDS family roles while storing asset context separately;
- [x] keep DDS file type generic while retaining detected texture roles for details and optional role grouping;
- [x] add General, File details and References properties tabs plus factual DDS header metadata;
- [x] distinguish DDS alpha capability from actual non-opaque BC1/BC2/BC3 payload data;
- [x] add BWM metadata and populate the References table in both model-to-texture and texture-to-model directions;
- [x] introduce a shared metadata-provider interface, neutral asset sources and optional preview descriptors for additional formats and future root loading;
- [x] widen entry properties, reset new views to the top-left and remove the normal-width file-list overflow;
- [x] refine the References table for narrow-window readability and long paths;
- [x] polish File details spacing and visual hierarchy without changing factual metadata;
- [x] give General the same grouped property surface while keeping general and provider facts separate;
- [x] complete final 0.5 regression checks and source release packaging;
- [x] keep unknown formats neutral and preserve read-only behavior.

## Next milestone

### 0.6 — additional format providers

- add factual metadata providers for CHA, ABN, CSK and other prioritized STUFF formats;
- reuse the shared inspection and fallback system introduced in Version 0.5;
- add relationships only where binary evidence confirms them;
- keep unknown fields and unsupported formats neutral instead of guessing.

## V1.0 — read-only STUFF Explorer

- safe archive reader and validation;
- folder tree, file table and instant search;
- selective folder/file export and full extraction;
- the completed 0.3 robustness work;
- tests against synthetic and user-owned real archives;
- reproducible Windows release package.
- design one cohesive file-icon family after the complete STUFF format/provider set has stabilized.

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
