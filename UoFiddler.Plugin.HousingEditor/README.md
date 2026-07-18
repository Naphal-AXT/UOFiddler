# Housing Editor plugin

Editor for Ultima Online's Legacy Housing data - the tables that define
which multi/item pieces are valid walls, doors, floors, stairs, roofs,
teleporters and "misc" pieces (curved walls, etc.) when placing a
custom house.

## Background: two on-disk formats

**Pre-UOP clients (2D)** ship this data as 7 plain tab-separated TXT
files, sitting directly in the client folder:

| File              | Category   |
|-------------------|------------|
| `doors.txt`       | Doors      |
| `walls.txt`       | Walls      |
| `floors.txt`      | Floors     |
| `stairs.txt`      | Stairs     |
| `roof.txt`        | Roof       |
| `misc.txt`        | Misc       |
| `teleprts.txt`     | Teleports  |

Each file has the same shape: line 1 is a column-type hint (ignored),
line 2 is the tab-separated column header, and every line after that
is one record - one buildable piece. A trailing `Comment` column, if
present, is free text and not part of the numeric data.

**Post-UOP clients (Classic)** replace all 7 files with a single
packed resource, `housing.bin`, embedded inside `MultiCollection.uop`
under `Build/MultiCollection/housing.bin` (see
`client/build/multicollection.def` - `resourcetype="20"`,
`fileFormat="packed"`). This is confirmed by hash: `housing.bin`
always resolves to the same UOP hash across builds. Its *contents*
are effectively the same 7 tables, packed and zlib-compressed, but the
post-UOP data isn't guaranteed to be at the exact same version as the
7 TXT files from a same-era pre-UOP client - housing.bin usually has a
few extra entries.

## What the plugin does today

- **Pre-UOP**: fully working. Opens a client folder, parses all 7 TXT
  files generically (column layout is read from the file itself, not
  hardcoded), shows every category/record in a tree, lets you inspect
  and edit field values, add/delete records, and save back to the
  original TXT files.
- **Post-UOP**: reads and zlib-decompresses `housing.bin` out of
  `MultiCollection.uop` (see `Classes/HousingReader.cs`). The plugin
  shows it as a raw hex dump by default. Its per-record binary format
  is confirmed (see below) - use the **Correlate...** button to decode
  categories into normal editable data. Doors, Teleports, Floors, Roof
  and Stairs decode 100% reliably; Walls and Misc decode nearly every
  record (99.1% combined against a same-build client - see "What's
  left" for the 4 records that don't). Whatever a category doesn't
  decode falls back to the raw hex view.

## Architecture

```
UoFiddler.Plugin.HousingEditor/
    HousingEditorPlugin.cs                 Plugin entry point (adds the tab).
    HousingProject.cs                      In-memory model: one project = a
                                            client's housing data, either
                                            Legacy or Uop, made of Categories.
    Classes/
        HousingReader.cs                   Extracts + decompresses housing.bin
                                            out of MultiCollection.uop. Walks
                                            the UOP container directly (hashed
                                            file-name lookup) instead of
                                            extending Ultima's core
                                            UopFileAccessor - this is the only
                                            consumer of that lookup, so it
                                            stays plugin-local rather than
                                            growing the shared Ultima library.
        HousingCategory.cs                 One category (Doors, Walls, ...):
                                            column list + records + original
                                            TypeLine (kept for round-trip).
        HousingRecord.cs                   One record. Generic column/value
                                            dictionary instead of fixed
                                            properties, because column layout
                                            genuinely differs between files
                                            (e.g. misc.txt has extra fields
                                            for curved walls) and between TXT
                                            and housing.bin versions.
        TxtTableReader.cs                  Generic TSV parser matching the
                                            real file shape (type line +
                                            header + rows). Used by all 7
                                            legacy files.
        LegacyReader.cs / LegacyWriter.cs  Load/save the 7 TXT files into/out
                                            of a HousingProject.
        UopReader.cs                       Loads housing.bin (raw bytes) from
                                            a post-UOP client.
        HousingProjectLoader.cs            Picks LegacyReader vs UopReader
                                            based on whether
                                            MultiCollection.uop exists.
        HousingAnalyzer.cs                 Quick integrity report (counts,
                                            duplicate records per category).
        HousingCorrelation.cs              Export helpers: CSV dump of every
                                            TXT record (the "known good" side
                                            of the housing.bin comparison) and
                                            raw binary dump of housing.bin.
        HousingBinCodec.cs                 Decodes housing.bin records using
                                            the confirmed binary layout (see
                                            below). Matches each binary entry
                                            to a legacy TXT row by decoded
                                            value set (3 passes, strictest
                                            first) rather than by position,
                                            since binary entry order doesn't
                                            match TXT row order; still needs
                                            a legacy TXT category as the
                                            source of column names and to
                                            know which rows to look for.
        ClilocResolver.cs                  Resolves a decoded record's
                                            ClilocId to its in-game label via
                                            the client's Cliloc.esp (tried
                                            first) / Cliloc.enu (fallback),
                                            using the core Ultima.StringList
                                            reader. Caches per client path.
    UserControls/HousingEditorControl.cs   The tab's UI: tree + field grid +
                                            property grid + toolbar actions.
```

## Using the plugin

1. **Open** - pick a client folder. If it contains `MultiCollection.uop`
   it's treated as post-UOP, otherwise as pre-UOP. Defaults to the
   client folder currently configured in UOFiddler's settings if one is set.
2. **Tree** - one branch per category, one leaf per record
   (`0042  0x1234` = index and its most likely graphic ID). Post-UOP
   clients additionally show a `housing.bin (raw)` node until you run
   **Correlate...** (see below), after which decoded categories appear
   alongside it.
3. **Select a record** - the right-hand grid lists every column for
   that record with its value; edit a value and tab/click away to
   commit it in memory. The property grid above shows the record's
   comment.
4. **Select the raw `housing.bin` node** (post-UOP only) - the grid
   becomes a read-only hex dump (offset / hex / ASCII), capped at the
   first 1 MB for responsiveness.
5. **Correlate...** (post-UOP only) - pick a pre-UOP client folder
   (ideally the same build, but a nearby version works too) and the
   plugin decodes as many records of each category as it can verify
   against that reference, shown in the tree alongside the raw node.
   A summary reports X/Y records per category; categories/records it
   can't verify are left out rather than guessed. Each decoded record's
   "Cliloc Name" (property grid) is resolved automatically from the
   post-UOP client's own `Cliloc.esp`/`Cliloc.enu` - see "Cliloc name
   resolution" below.
6. **Add / Delete** - operate on the selected category (or the
   category of the selected record) / selected record.
7. **Save** - pre-UOP only: writes every touched category back to its
   original TXT file in the client folder, after a confirmation
   prompt. Post-UOP saving (rebuilding `housing.bin`) isn't
   implemented - see below.
8. **Analyze** - prints a per-category summary (record/column counts,
   duplicate value-sets) - a quick sanity check after editing.
9. **Export CSV** - dumps every loaded record as one flat CSV, one row
   per record, one column per distinct field name seen anywhere in the
   project. This is the "known good" dataset used to decode/verify
   housing.bin (see below).
10. **Export Raw** - post-UOP only: writes the decompressed
    `housing.bin` bytes to a `.bin` file for offline hex-diffing.

## Confirmed binary format (housing.bin)

The structure below is **not independently reverse engineered** - it's
ported from [ModernUO](https://github.com/modernuo/ModernUO)'s own
`Server.Multis.ComponentVerification.LoadFromHousingBin`, a real server
emulator's production parser, found via research rather than guessed.
Confirmed against a real client's housing.bin: parsing it this way
consumes the file's decompressed bytes down to the very last one, with
nothing left over.

```
u32   fileTypeCount
fileTypeCount x {
    u32   fileType     - identifies the category (Stairs=1, Roof=2,
                          Doors=3, Floors=4, Walls=5, Teleports=6,
                          Misc=7 - confirmed by cross-referencing entry
                          counts against a real client's TXT row counts)
    u32   entryCount
    entryCount x {
        u32   categoryId      - NOT in TXT row order, see below
        u32   subcategoryId   - the TXT's Style column, where present
        u32   featureMask     - OR'd with client feature-flag bits the
                                 TXT doesn't represent (notably 0x1,
                                 "T2A"/base game) - masking with
                                 ModernUO's HousingFlags.HousingEJ
                                 (0xFF02D0) recovers the TXT's exact
                                 FeatureMask value
        u32   clilocId        - resolves in Cliloc.esp/Cliloc.enu to the
                                 in-game label (see `ClilocResolver.cs`) -
                                 not needed to round-trip values
        group fields1
        u32   unknown         - ONLY present when fileType == 5 (Walls),
                                 between the two groups
        group fields2
        u32   unknown         - present on every OTHER fileType, after
                                 the second group
    }
}

group: u32 count; count x { u32 direction, u32 staticId }
```

Zero-valued fields are omitted (sparse) rather than stored, so a
group's `count` only covers non-zero fields - confirming the original
observation that "housing doesn't mark zeros". `direction` is some
per-category positional tag that isn't reliable as a column position
across categories (doors.txt preserves original column order,
floors.txt compacts it to be sequential among non-zero values only) -
`HousingBinCodec` assigns column names by matching decoded *values*
back to the correlated legacy record's non-zero columns instead of
trusting `direction`.

**Binary entry order does not match the TXT row order.** walls.txt's
"Celtic Walls" group sits near the *end* of the file (TXT rows ~182 of
186) but its `categoryId` is 47, landing its binary entries near the
*middle* of the Walls section - the TXT appears to have been manually
re-sorted for the in-game UI independently of the game's internal
category numbering. Because of this, `HousingBinCodec` matches binary
entries to TXT rows by decoded *value set*, not by position - this
also transparently handles TXT rows that have no binary entry of their
own (duplicates of data already stored under a different category -
e.g. misc.txt's "Teleporters 1/2" rows are exactly the split values of
teleprts.txt's one real entry, confirmed concretely) since no entry
will match them; they're simply omitted rather than guessed.

Matching runs in three passes, strictest first:

1. **Exact match** - an entry's full value multiset equals one row's
   expected set. Covers the vast majority of records.
2. **Combined-entry match** - a single entry packs two TXT-distinct
   designs into its two field groups at once, one design per group -
   e.g. walls.txt's "Gothic Walls 2" (8 values) and, separately, "Rose
   Window 1" (6 values) are two different TXT rows with different
   `Style` numbers, but a single binary entry's `fields1` + `fields2`
   together contain exactly those 14 values combined. Matched when a
   still-unused entry's full value multiset equals exactly the
   combined multiset of two still-unmatched rows. Runs before pass 3
   so this exact-sum match claims the entry first - otherwise pass 3
   would happily match just one of the two rows and strand the other.
3. **Subset match** - an entry carries extra slot values beyond one
   row's own columns: repeated shared/boundary pieces across direction
   slots that the TXT doesn't list as separate columns. Seen on Walls'
   "Celtic" style-continuation entries, where e.g. Style 1's entry
   carries 12 (direction, value) slots but only 11 distinct values
   appear as separate TXT columns - the 12th slot just reuses a
   graphic already used by an adjacent slot (a real shared corner
   piece, not a decoding artifact). Matched when a still-unmatched
   row's distinct values are fully contained in a still-unused entry's
   distinct values.

Coverage against a real Ultima Online Classic client (which ships
`MultiCollection.uop` *and* the legacy TXT files side by side, same
build - zero version drift):

| Category  | Records decoded |
|-----------|-----------------|
| Doors     | 37/37 (100%)    |
| Teleports | 1/1 (100%)      |
| Floors    | 58/58 (100%)    |
| Roof      | 36/36 (100%)    |
| Stairs    | 19/19 (100%)    |
| Walls     | 185/186 (99.5%) |
| Misc      | 93/96 (97%)     |
| **Total** | **429/433 (99.1%)** |

Cross-checked against an *older* pre-UOP (2D) client's TXT files (a
smaller, earlier version of the same tables) decoding the *same*
Classic `housing.bin`: Doors, Teleports, Floors, Roof, Stairs and
Walls all decode 100% (130/130 for Walls - the remaining gap below is
specific to entries added after that older TXT snapshot); Misc reaches
68/70 (**313/315, 99.4%** overall for this pairing).

### The remaining gap (4 records)

Every value the codec *does* assign was checked field-by-field against
the legacy TXT and matches exactly - the 3-pass matching above
introduced zero false positives on the real client data it was
verified against. What's left are records the binary data genuinely
can't supply, not a matching-logic shortfall - confirmed by searching
*every* entry in *every* category (not just Walls/Misc) for the exact
missing values, so this isn't a case of them being stored somewhere
the codec simply isn't looking:

- **Walls row "Shadowguard Walls"** (`South1/Corner/East1/Post` =
  39889/39888/39890/39891) - its binary entry only carries 3 of those
  4 values (`39890` is simply absent from the entry's slots, even
  though the other 3 - including 2 duplicated across slots - are
  present). `39890` does not appear in any other entry, in Walls or
  any other category, anywhere in the file.
- **Misc rows "Teleporters 1" and "Teleporters 2"** - confirmed to
  have no binary entry of their own at all (same phenomenon as the
  worked "Teleporters 1/2" example above - their values are the split
  halves of teleprts.txt's one real entry). Correctly omitted, not a
  gap to close.
- **Misc row "Miscellaneous Roof Pieces 1"** (8 expected values) - a
  binary entry supplies exactly 6 of them; the other 2
  (`17369`,`17370`) do not appear anywhere else either - not in any
  other Misc entry, and not in Roof's own entries despite the row's
  name suggesting they'd be there.

housing.bin's own real-world consumer confirms none of this is
recoverable: [ModernUO](https://github.com/modernuo/ModernUO)'s
`ComponentVerification.LoadFromHousingBin` (the same production parser
this codec's structure is ported from) doesn't care about entries or
categories at all at runtime - it just writes every `(direction,
staticId)` pair it reads, from every entry, into one flat
`graphicId -> featureMask` table, so it can answer "is this graphic
valid here" with an O(1) lookup while a player is placing a piece. A
graphic that never appears in any entry's fields, like `39890` or
`17369`/`17370`, would be invalid to the live game too under this same
housing.bin - the TXT's own claim that these pieces work is not backed
by this file. This also explains *why* the binary format tolerates
entries spanning two designs or duplicating slots: the per-entry/
per-row structure this codec reconstructs for editing was never needed
by the game itself, only by us.

## Cliloc name resolution

Every decoded housing.bin record carries a `ClilocId` column (see
above). `Classes/ClilocResolver.cs` resolves it to the in-game label
using the client's own `Cliloc.esp`/`Cliloc.enu`, shown in the record's
property grid as "Cliloc Name". Verified against a real client:
223/223 (100%) of decoded records with a `ClilocId` resolve to a
non-empty label.

`Cliloc.esp` (Spanish) is tried first, falling back to `Cliloc.enu`
(English) only when the Spanish file doesn't have that id - the
Spanish localization covers fewer entries (18,247 vs. English's
123,481 on the client this was verified against), so newer
expansion-era pieces (Gargish/Jungle/Shadowguard doors, for example)
resolve in English while everything else resolves in Spanish.

Both files turned out to need **Mythic compression** (a proprietary
BWT + move-to-front scheme, XOR-obfuscated length header) rather than
being plain text as some community documentation assumes for older
clients - confirmed empirically: the classic uncompressed `{u32 id,
u8 flag, u16 length, text}` format produced garbage on this client's
files, and the file's leading 4th byte matches the compressed-cliloc
marker. No new code was needed for this: `Ultima.StringList` (core
library) already auto-detects compressed vs. uncompressed and falls
back between the two, backed by an existing `MythicDecompress` +
move-to-front implementation - `ClilocResolver` is a thin wrapper
around it that just picks esp-then-enu and caches per client path.

## What's left

1. **The 4-record gap above** - not expected to be closeable from this
   housing.bin file's own data; the only way forward would be a
   different client build's housing.bin that happens to include these
   3 graphics, if one exists.
2. **housing.bin writer** (round-trip save) - realistic now that
   decoding doesn't depend on a legacy reference at all (only on
   knowing which TXT rows exist, for column names and to detect which
   rows have no binary entry) - not implemented yet.
3. Further out: building/rebuilding `MultiCollection.uop` and
   `multi.mul/idx` from edited data.
