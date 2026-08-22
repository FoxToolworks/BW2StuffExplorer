# Changelog

## 0.5.0 — File-type analysis and asset inspection

- completes the Version 0.5 Windows regression and final presentation pass;
- aligns final version metadata, About text, roadmap and packaging documentation;
- applies the final 4.4.1 properties polish with a compact header, refined spacing and a consistent References selection style;
- verifies all 13 formats and 3,928 entries in the supplied real-archive inventory remain covered by the classification layer;
- applies the same property-row template to General and File details so spacing, label width, wrapping, copying and tooltips remain visually synchronized;
- groups General into File and Archive sections while preserving every existing value;
- groups File details into factual File, Image, Pixel format, Storage and Model sections without adding or interpreting metadata;
- presents technical labels and selectable values in a consistent bordered two-column surface, with wrapped full-value tooltips;
- uses one stable References schema for both BWM and DDS: material index, role, model entry, stored reference, status and resolved texture/candidates;
- shortens resolution evidence to `Exact`, `Unique`, `Not found` and `Ambiguous`, with the complete archive-scoped meaning preserved in tooltips;
- removes frozen reference columns so genuine horizontal overflow uses a conventional full-width scrollbar;
- rebalanced the References table so material index and resolution status remain readable while source and target paths share the remaining width proportionally;
- adds wrapped full-value tooltips to truncated reference paths, candidates and statuses;
- retains user-resizable/reorderable columns and adds recycling virtualization to the reference surface;
- widened the properties window's first-open default to 960 × 540 while preserving session-only user size and position memory;
- resets General, File details and References to the top-left when properties open or the selected tab changes;
- resets the main file list to the top-left after a completed folder or search result change instead of inheriting the previous view's scroll offset;
- replaced the edge-filling default Name column with compact, stable user-resizable default widths, removing the tiny horizontal overflow at the normal main-window size while retaining automatic horizontal scrolling when content genuinely does not fit;
- introduced one UI-independent metadata-provider interface and inspection-result model for DDS, BWM, fallback formats and future readers;
- made the properties window consume typed inspection results instead of selecting DDS/BWM readers itself;
- added shared archive-entry and loose-file asset sources so the same DDS and BWM inspectors can later operate in a complete BW2 root context;
- kept relationship evidence optional and separate from byte access, preventing loose-file inspection from inventing unproven links;
- prepared optional preview descriptors in the provider result while keeping the Preview tab hidden until a real viewer is available;
- expanded self-tests with provider selection, archive relationship preservation, loose DDS/BWM parsing, neutral fallback behavior, extension registration and preview-descriptor propagation;
- added factual BWM File details for signature, magic, supported version, numeric/static/skinned model type, material count and texture-reference count;
- replaced the References placeholder with a scrollable table for BWM and DDS entries;
- shows BWM-to-DDS rows with zero-based material index, material-slot role, stored reference, exact/unique-filename/missing/ambiguous status and resolved target or candidates;
- shows reverse DDS-to-BWM rows for both resolved uses and explicitly marked ambiguous candidates without turning filename-family inference into a file reference;
- preserves detailed invalid-BWM reasons and keeps every unsupported format on the neutral reference placeholder;
- added a separate `Non-opaque alpha data` fact for DDS entries instead of treating format capability as proof of actual alpha content;
- scans BC1/DXT1, BC2/DXT2/DXT3 and BC3/DXT4/DXT5 blocks, including matching DX10 formats, and stops after the first valid non-opaque pixel;
- checks every declared mip level and subresource while ignoring padding texels outside partial edge blocks;
- reports `Not applicable` for formats that cannot carry alpha and `Unknown` for unsupported alpha-capable layouts, invalid subresource declarations or truncated payloads;
- redesigned entry properties around separate General, File details and References tabs, leaving room for a future Preview tab without crowding metadata;
- added a bounded DDS reader that reads only the 128-byte legacy header and optional 20-byte DX10 extension;
- exposed factual DDS dimensions, texture kind, pixel format, FourCC or DXGI format, mip levels, pitch/linear size, array size and declared cubemap faces where present;
- reports classic DXT color space as `Unknown` because the legacy header does not store an sRGB/linear declaration;
- reports sRGB or linear only for explicit recognized DXGI format variants in a valid DX10 extension;
- distinguishes format alpha capability from proof that meaningful alpha data is actually present;
- handles truncated, malformed and incomplete DX10 DDS headers as invalid metadata without crashing the properties window;
- keeps the future Preview tab hidden until a real viewer exists;
- enabled row and column virtualization, recycling and virtualization while grouped to avoid materializing every row in large folders;
- increased the cancellable search debounce to let multi-character queries finish before rebuilding the result view;
- deferred single punctuation-only search terms such as `.` so they cannot trigger an immediate full-archive result rebuild;
- added a central, UI-independent classification model that keeps archive format, asset category and file type separate;
- classified all 13 formats currently known in `everything.stuff`, with safe fallbacks for unknown and extensionless entries;
- replaced extension-only values in the Type column and properties window with friendly descriptions such as `Creature Hair/Fur Data (CHA)`;
- changed type sorting and grouping to use the friendly file-type descriptions;
- added optional grouping by broader asset categories such as `3D Models`, `Textures & Images` and `Creature Data`;
- added a bounded BWM version 5/6 metadata reader that validates signature, stored size, magic and material-table bounds without decoding geometry;
- refined valid BWM entries to `Static Model (BWM)` or `Skinned Model (BWM)` and safely labels every invalid payload `Unknown Model Data (BWM)`;
- built an archive-wide, case-insensitive relationship index for the six confirmed BWM material slots;
- stored diffuse, light, growth, specular, animated and normal roles for uniquely resolved DDS entries;
- preserved every confirmed role for multi-role DDS entries instead of selecting an arbitrary role;
- preserved missing and ambiguous references without guessing;
- added exact landscape-family suffix rules for diffuse, baked and normal DDS textures below `data/landscape/`;
- added exact creature-family rules for diffuse, normal/bump, specular, scale/bias and strand DDS textures below `data/ctr/`;
- kept BWM material evidence above filename rules and left unmatched files without a detected role;
- separated semantic texture roles from file types so every DDS is displayed and grouped as `Texture (DDS)`;
- stored model, landscape and creature context separately so a later details tab or optional role/context grouping can use it;
- expanded the dependency-free self-test with classification coverage, synthetic BWM v5/v6 payloads, all six material roles, family suffix rules, context preservation, BWM priority, invalid BWM fallback, missing references, ambiguous filenames, multiple-role preservation, legacy/DX10 DDS headers, cubemap flags, malformed DDS cases and BC1/BC2/BC3 alpha payload cases.

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
