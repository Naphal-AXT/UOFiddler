# UoFiddler.Plugin.MultiEditor

## Known gap: no UOP-aware save

`MultiEditor` reads multis from either the legacy `multi.mul`/`multi.idx`
pair or, when present, directly from `MultiCollection.uop` (via
`Ultima.Multis.HasUopFile` / `GetUopComponents`), including multi entries
that only ever exist in the UOP with no legacy `multi.mul` equivalent
(71 such "orphan" entries confirmed on a real Classic client).

Saving, however, only ever goes through `Ultima.Multis.Save(string path)`,
which writes `multi.idx`/`multi.mul` in the legacy format and reads each
entry back via the legacy-only `GetComponents(index)` accessor - never
`GetUopComponents`. Concretely:

- Editing one of the 71 UOP-only orphan multis and saving does not
  persist the change anywhere: it has no legacy slot to write into, and
  `Save` never touches `MultiCollection.uop`.
- Editing a multi that exists in both formats only updates the legacy
  mul/idx copy, leaving `MultiCollection.uop` stale until something
  repacks it (see `UoFiddler.Plugin.HousingEditor`'s
  `MultiCollectionRepacker`, which already has the raw UOP read/write
  code this would need).

No UOP-multi writer exists anywhere in the solution today. This is a
known limitation, not a bug to fix opportunistically - closing it means
extending `MultiCollectionRepacker`'s raw TOC writer to accept edited
entries instead of just carrying orphans forward verbatim, and doing so
carries real risk since multi component lists are used far more broadly
than housing.bin's narrow customization-menu use case.
