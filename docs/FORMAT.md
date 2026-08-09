# Known STUFF archive layout

```text
[concatenated file data]
[table of contents: N × 268 bytes]
[4-byte little-endian content length / TOC offset]
```

Each table-of-contents entry is 268 bytes:

| Offset | Size | Meaning |
|---:|---:|---|
| 0 | 256 | Null-terminated archive path (single-byte encoding) |
| 256 | 4 | File-data offset, little endian |
| 260 | 4 | File length, little endian |
| 264 | 4 | Unix timestamp in seconds, little endian (likely the source asset's modification/export time) |

V1 treats archives as read-only. It verifies that the footer points inside the file, that the table length is divisible by 268 and that every entry stays inside the content region.

## Timestamp evidence

The final field was previously documented as unknown. Analysis of all 3,928 entries in a retail `everything.stuff` archive found that every value converts to a valid Unix timestamp between 10 June and 1 September 2005. Related assets frequently share timestamps to the second, which is consistent with file modification or automated asset-export times.

The reader therefore exposes the field as `ModifiedTimestamp`. The exact Lionhead pipeline semantics—source-file modification time versus last asset-export time—remain unconfirmed, so the value is displayed but is not yet written back to exported files.
