# Changelog

## 0.6.0 — Format intelligence

- adds a conditional **Contents** tab for BWM files and preserves all 1,753 retail-corpus material rows, including materials without texture references;
- exposes stored material names plus the six BWM texture slots and corrects the former `Animated Map` label to the evidence-safe `Additional Map`;
- keeps BWM-to-image and reverse image-to-BWM relationships direction-aware while preserving exact, unique, missing and ambiguous resolution states;
- generalizes relationship targets across DDS, TGA, BMP and BW2 `.555` images without inventing new source references;
- adds bounded TGA inspection for observed uncompressed true-color, grayscale and RLE files, including footerless and TGA 2.0 cases;
- adds bounded BMP inspection for the observed `BITMAPINFOHEADER` + `BI_RGB` files without claiming unsupported alpha semantics;
- confirms and inspects the nine retail `.555` sky textures as 256 × 256 little-endian X1R5G5B5 with a clear/unused high bit;
- renames the final References evidence column to `Candidates` and keeps all ambiguous paths visible;
- adds Enter-to-open Properties when the main file list has focus;
- adds an optional strict retail-corpus regression covering 822 BWM entries and all 134 TGA/BMP/`.555` images;
- verifies the known retail totals of 1,753 materials, 3,396 stored image references, 3,266 unique resolutions, 126 missing resolutions and four ambiguous resolutions;
- retains the existing read-only archive reader, navigation, search, grouping and robust export pipeline.

## 0.5.0 — File-type analysis and asset inspection

- adds a central classification model with friendly descriptions for all 13 format families found in the verified retail `Everything.stuff` archive;
- keeps archive format, asset category, file type, texture role and context separate instead of collapsing them into one label;
- adds bounded BWM v5/v6 metadata parsing with static/skinned detection and safe handling for the two zero-filled placeholder models;
- builds an archive-wide BWM material relationship index and preserves exact, unique, missing, ambiguous and multi-role texture evidence;
- adds conservative landscape and creature DDS role/context rules while keeping stronger parsed BWM evidence authoritative;
- redesigns Properties around **General**, **Details** and **References** tabs and introduces the shared metadata-provider/result system used by later formats;
- adds bounded DDS header inspection for dimensions, texture kind, pixel format, FourCC/DXGI format, mip levels, array/cubemap information and declared color-space evidence;
- separates DDS alpha capability from actual non-opaque alpha data and adds supported BC1/BC2/BC3 payload analysis;
- adds bidirectional BWM ↔ DDS reference inspection with stored references and explicit resolution evidence;
- restores responsive grouped browsing and recursive search through DataGrid virtualization/recycling and safer search debounce behavior;
- refines property layout, scrolling, tooltips, selection behavior and long-path handling;
- keeps unsupported formats neutral and all archive access read-only.

## 0.4.0 — Presentation and release preparation

- refreshed the interface with a compact Windows 11-inspired light presentation;
- added a new multi-resolution application icon for the window, taskbar and executable;
- replaced the legacy tree and group expanders with compact Explorer-style chevrons;
- removed the gray fill and divider line from file-type group headers;
- made bound dates, times and numbers follow the current Windows regional settings while keeping the UI language English;
- replaced the malformed archive-root drawing with a clean neutral archive-box icon;
- added the agreed search glyph, which changes to the clear button when text is entered;
- added a Refresh navigation action, refined breadcrumbs and an inline search clear button;
- redesigned entry properties with a file header, selectable values, resizing, Escape/Close handling and session-only bounds memory;
- aligned product metadata and public release packaging;
- kept the proven 0.3 archive reader, tree construction, recursive search, navigation and robust export pipeline unchanged.

## 0.3.0 — Robust long-running operations

- moved single-file, selection, folder and full-archive exports off the UI thread;
- added byte- and file-level progress feedback with a Cancel action and `Esc` support;
- made each exported file atomic by writing to a temporary sibling and moving it into place only after a complete, closed copy;
- preserve an existing destination file when its replacement is cancelled or fails;
- added graceful cancellation and cleanup when the application is closed during an export;
- preflight all multi-file destination paths before writing and reject unsafe or case-insensitively colliding paths;
- added one confirmation for all existing destination files before a multi-file export overwrites them;
- report the exact archive entry and destination when an I/O failure occurs;
- detect source archives that become truncated during extraction;
- expanded the synthetic self-test with malformed tables, out-of-range entries, unsafe paths, duplicate targets and mid-file cancellation cleanup.

## 0.2.2 — Metadata & layout fixes

- identified the final 32-bit entry field as a Unix timestamp from all 3,928 entries in a real `everything.stuff` archive;
- added a Modified column between Name and Type using the current Windows regional date/time format;
- added sorting by the archived modification timestamp from both the column header and View menu;
- added local and UTC modification times to entry properties;
- replaced the repeatedly reapplied star-sized Name and Path columns with stable user-resizable widths;
- documented the timestamp evidence while keeping the exact source-pipeline semantics appropriately qualified.

## 0.2.1 — Navigation & organization

- replaced the large action buttons with Back, Forward and Up navigation;
- added clickable archive-path breadcrumbs;
- changed normal folder browsing to show only files directly inside the selected folder;
- kept searches recursive within the selected folder branch;
- added an Edit menu for copying the selected file's full path or name;
- added View menu controls for sorting by name, type, size or offset in either direction;
- added optional file-type grouping, enabled by default;
- synchronized menu sorting with sortable column headers;
- stabilized useful default widths for the file-list columns;
- show the Path column only for recursive search results;
- debounced searches and update their result list in one operation to keep the UI responsive;
- preserved read-only archive access and recursive folder export behavior.

## 0.2.0 — Explorer UX

- switched the interface to English and centralized UI strings for future localization;
- added File, View, Tools and Help menus;
- added file and folder context menus;
- added multi-file selection and export;
- added keyboard shortcuts for open, search, export, refresh and clearing search;
- added sortable columns and human-readable file sizes;
- added entry properties, clipboard path actions and improved status feedback;
- kept all archive access strictly read-only.

## 0.1.0 — Functional prototype

- added safe STUFF parsing, validation and bounded entry streams;
- added directory browsing, search and file/folder/full extraction;
- added drag-and-drop opening and a synthetic archive self-test.
