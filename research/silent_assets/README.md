# Vanilla Silent animation resources and scenes

- Game: Slay the Spire 2 v0.111.0, commit `41cef1ea` (local `release_info.json`).
- Source: `C:\Users\kkk\sts2_extract`, the existing read-only recovery of `SlayTheSpire2.pck` made with GDRE Tools v2.7.3; see `research/REPORT.md`. The original GDRE command was not recorded in that report. This export copies from that recovery, and did not rerun GDRE.
- The recovered `.tscn` and `.tres` are GDRE reconstructions; the prior recovery report notes that some conversions were lossy.
- All copied files were compared with the recovery using SHA-256.

## Copy commands

```powershell
$src = 'C:\Users\kkk\sts2_extract'
$dst = 'research\silent_assets'
foreach ($rel in 'animations\characters\silent','animations\merchant\silent','animations\rest_site\silent') {
  New-Item -ItemType Directory -Force -Path (Join-Path $dst $rel) | Out-Null
  Get-ChildItem -LiteralPath (Join-Path $src $rel) -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dst $rel) }
}
foreach ($scene in 'scenes\creature_visuals\silent.tscn','scenes\merchant\characters\silent_merchant.tscn','scenes\rest_site\characters\silent_rest_site.tscn') {
  $target = Join-Path $dst $scene
  New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
  Copy-Item -LiteralPath (Join-Path $src $scene) -Destination $target
}
```

## Files

Paths are original `res://` paths; sizes are bytes. UIDs list every UID appearing in each `.tres` or `.import`, including referenced resources.

| Original path | Bytes | UIDs in file |
| --- | ---: | --- |
| `res://animations/characters/silent/silent_skel_data.tres` | 1075 | ``uid://bqlhf04mk2866``, ``uid://dq7y3dlq1mmdq``, ``uid://cbaww6gd7g743`` |
| `res://animations/characters/silent/silent.atlas` | 2556 | — |
| `res://animations/characters/silent/silent.atlas.import` | 386 | ``uid://dq7y3dlq1mmdq`` |
| `res://animations/characters/silent/silent.png` | 253186 | — |
| `res://animations/characters/silent/silent.png.import` | 950 | ``uid://bvmfj1e5yasw2`` |
| `res://animations/characters/silent/silent.skel` | 228650 | — |
| `res://animations/characters/silent/silent.skel.import` | 341 | ``uid://cbaww6gd7g743`` |
| `res://animations/merchant/silent/silent_merchant_skel_data.tres` | 465 | ``uid://d4li4q7d3n0nb``, ``uid://dcgjjiwexpujq``, ``uid://cbaww6gd7g743`` |
| `res://animations/merchant/silent/silent_shop.atlas` | 3025 | — |
| `res://animations/merchant/silent/silent_shop.atlas.import` | 399 | ``uid://dcgjjiwexpujq`` |
| `res://animations/merchant/silent/silent_shop.png` | 847204 | — |
| `res://animations/merchant/silent/silent_shop.png.import` | 963 | ``uid://cjs44cd5yac4a`` |
| `res://animations/rest_site/silent/rest_site_silent_skel_data.tres` | 496 | ``uid://cusnivp587wn0``, ``uid://chhushd3ctbab``, ``uid://clktoc871r750`` |
| `res://animations/rest_site/silent/restsite_silent.atlas` | 770 | — |
| `res://animations/rest_site/silent/restsite_silent.atlas.import` | 412 | ``uid://chhushd3ctbab`` |
| `res://animations/rest_site/silent/restsite_silent.png` | 212463 | — |
| `res://animations/rest_site/silent/restsite_silent.png.import` | 976 | ``uid://b6n6lfy1rklgq`` |
| `res://animations/rest_site/silent/restsite_silent.skel` | 52560 | — |
| `res://animations/rest_site/silent/restsite_silent.skel.import` | 367 | ``uid://clktoc871r750`` |
| `res://scenes/creature_visuals/silent.tscn` | 1259 | — |
| `res://scenes/merchant/characters/silent_merchant.tscn` | 654 | — |
| `res://scenes/rest_site/characters/silent_rest_site.tscn` | 6494 | — |

## References

- The rest-site scene references `res://animations/rest_site/silent/rest_site_silent_skel_data.tres`, included above.
- The merchant skeleton data references `res://animations/characters/silent/silent.skel`, included above; there is no separate merchant skeleton in the recovered merchant directory.
- The three scenes reference no additional Silent-specific `.tres`, `.atlas`, `.skel`, or `.png` outside the listed directories. Shared scripts and the selection reticle are not Silent-specific.
- All specifically requested files and directories were found in the recovery.
