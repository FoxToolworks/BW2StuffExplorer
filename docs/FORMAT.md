# Known STUFF archive layout

This document describes the archive container facts currently used by BW2 Stuff Explorer. Asset-format research is documented separately under [`docs/research`](research/README.md).

```text
[concatenated file data]
[table of contents: N × 268 bytes]
[4-byte little-endian content length / TOC offset]
```

Each table-of-contents entry is 268 bytes:

| Offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 256 | Null-terminated archive path (single-byte encoding) |
| 256 | 4 | File-data offset, little endian |
| 260 | 4 | File length, little endian |
| 264 | 4 | Unix timestamp in seconds, little endian; likely source modification/export time |

The current reader is strictly read-only. It verifies that the footer points inside the file, that the table length is divisible by 268 and that every entry remains inside the content region before exposing bounded entry streams.

## Timestamp evidence

The final 32-bit entry field was previously documented publicly as unknown. Analysis of all 3,928 entries in the verified retail archive found that every value converts to a valid Unix timestamp between 10 June and 1 September 2005. Related assets frequently share timestamps to the second, consistent with file modification or automated asset-export times.

BW2 Stuff Explorer therefore exposes the field internally as `ModifiedTimestamp` and displays it as `Modified`.

The exact Lionhead pipeline semantic remains unconfirmed: it may be the source-file modification time, the last asset-export time or an equivalent pipeline timestamp. The tool intentionally does not claim more than the corpus supports.

## Current safety boundary

No public release currently writes or rebuilds STUFF archives. Any future writer will be a separate milestone and must write to a new output, validate bounds and paths, reopen the generated archive and verify it before the feature is considered safe.
