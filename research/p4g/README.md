# Persona 4 Golden references for P4Yu

Source is the local 32-bit PC archive set in `G:\Gamebox\New folder\ZS664\Persona.4.Golden.v1.0`. Neither local executable exposes a Windows file or product version. The folder label is v1.0. The install is outside the Steam libraries, and an exact Steam build ID could not be verified. Only the CPK, PAC and sound bank data were used for the exports.

## Catalog and 2D art

- [file_list.txt](file_list.txt) is UTF-8 tab-separated. Each row gives a CPK, its complete internal path, the uncompressed byte count, stored byte count, and the backing PAC. A path shared by multiple CPKs appears once in each corresponding catalog.
- `bustup/yu/` contains the two verified party-menu portraits for Yu. The catalog has no matching `bustup/b1_*` or `bustup/b01_*` dialogue portrait entries. Other costume and expression art was not verified.
- `persona/` contains the Izanagi and Izanagi-no-Okami compendium and card portraits (IDs 001 and 0bd), plus the protagonist's summon cut-in layers.
- `cutin/`, `tarot/`, `ui/battle/`, `ui/menu/` and `fx_tex/` contain battle overlays, card faces, interface and effect texture references. The selection of battle effects includes numbered texture sets whose exact skill names are unconfirmed.
- Each art folder has an index with the exact archive path, dimensions and a content description. An `@` suffix is the byte offset of an embedded TMX within its container. PNGs are RGBA. Two 16-bit effect TMXs were converted from the GS RGB5551 pixel layout. One selected cut-in and one selected UI container produced no supported textures.

The catalog uses the [TGE preappfile source at eadddcd](https://github.com/tge-was-taken/preappfile/tree/eadddcd2e679fc6a66caf1b633f75f7812e728e2) on .NET SDK 10.0.401. The temporary C# front end linked CpkFile and DwPackFile, used read-only streams, resolved localized `data_*.cpk` entries against `dataNNNNN.pac`, and read sizes from the PAC header. A tab-separated allowlist of CPK and internal path limited the entries whose payloads were read. The texture conversion used Python 3.12 and Pillow 13.0.0. The script read TMX0 headers, restored PS2 palette order for indexed textures, scanned selected containers for embedded TMX0 headers, supported DDS through Pillow and wrote RGBA PNGs. The 8-bit palette order was cross-checked against [P4GTmxConv](https://github.com/puggsoy/MiscTools/blob/master/P4GTmxConv/src/Main.hx).

```powershell
& $dotnet build "$temp/catalog/Catalog.csproj" --nologo
& $dotnet "$temp/catalog/bin/Debug/net10.0/Catalog.dll" $game "$temp/file_list.txt"
& $dotnet "$temp/catalog/bin/Debug/net10.0/Catalog.dll" $game "$temp/art_extraction_catalog.txt" "$temp/targets.tsv"
py -3 "$temp/art.py" $temp
```

## Audio

- `sfx/menu/` contains the `comse00` cue group from both sound banks. `sfx/unsorted/` contains the battle and common effects whose numbered cue names do not reliably label individual moves. The indexes retain the bank, original cue name or number, stream index and duration.
- `voice/yu/jp/` and `voice/yu/en/` contain the `bc001_*` battle cue sets located through `COMSE.xsb` and `COMSE_E.xsb`, respectively. Both cue sets resolve to different stream indexes in `SND/COMSE.xwb`.
- The additional `snd04_comse.acb` common-effects bank contains a CRI CPK inside the AWB field, with ATRAC9 WAVE records. The conversion read the bank's ITOC and aligned offsets and resolved direct and synth cue names where available. ACB index paths include the byte offset of the original waveform.
- No files from the BGM banks are included.

The XWB streams were decoded with [vgmstream](https://github.com/vgmstream/vgmstream) r2117 and converted to Vorbis with FFmpeg 9.0.2. The ACB waveforms were passed individually to FFmpeg as WAVE input.

```powershell
& $vgmstream -m -s 1 -S 0 "$game/SND/COMSE.xwb"
& $vgmstream -i -s $index -o $wav "$game/SND/COMSE.xwb"
& $ffmpeg -v error -i $wav -map 0:a:0 -c:a libvorbis -q:a 4 $ogg
& $ffmpeg -v error -f wav -i $atrac9Wave -c:a libvorbis -q:a 4 $ogg
```

## Models and cutscenes

| Export | Source in data.cpk | Skeletons | Animation clips |
| --- | --- | ---: | ---: |
| `model/yu.glb` | `model/pack/bc001.pac` | 1 | 25 |
| `model/izanagi.glb` | `model/persona/ps001.amd` | 1 | 5 |
| `model/yu_golf_club.glb` | `model/weapon/wp001.amd` | 0 | 0 |
| `model/yu_sword.glb` | `model/weapon/wp004.amd` | 0 | 0 |

Each model has a front and three-quarter transparent PNG. These are source-derived reference exports. The intermediate FBX export and glTF conversion may differ from the in-game material and animation playback.

The GMO records were copied from the AMD or PAC by reading the `OMG.00.1PSP` signature and root chunk length. Noesis 4.428 came from the [portable P4GModelConverter 1.6 release](https://github.com/ShrineFox/P4GMOdelConverter/releases/tag/1.6). Blender 4.5.9 imported the FBX, exported GLB with actions enabled and rendered the previews using an orthographic camera and a transparent film.

```powershell
& $noesis '?cmode' $gmo $fbx '-fbxmultitake'
& $blender -b --factory-startup --python "$temp/model_final.py" -- "$temp/model_stage" "$temp/model_final"
```

The `movie_frames/overview_*.jpg` sheets cover 41 USMs in archive order. Each row has eight 320-pixel-wide frames sampled at the centers of equal intervals. `p4ct013.usm` has one decoded frame, so its row repeats that frame. The index maps each row to its source. Three folders each contain 30 full-resolution frames (1920×1080).

| Folder | Source in movie.cpk |
| --- | --- |
| `movie_frames/golden_opening/` | `p4ctop3.usm` |
| `movie_frames/original_opening/` | `p4ctop1.usm` |
| `movie_frames/first_summon/` | `p4ct005.usm` |

The USMs were decoded with FFmpeg 9.0.2. FFprobe counted video packets, then FFmpeg's `select` filter sampled the frames. Pillow assembled the contact sheets.

```powershell
& $ffprobe -v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets -of json $usm
& $ffmpeg -v error -i $usm -map 0:v:0 -an -vf $filter -fps_mode passthrough -frames:v $count -q:v 2 $framePattern
py -3 "$temp/movies.py" $temp $game $ffmpegBin
py -3 "$temp/movies.py" $temp $game $ffmpegBin p4ct005.usm first_summon p4ctop1.usm original_opening
```
