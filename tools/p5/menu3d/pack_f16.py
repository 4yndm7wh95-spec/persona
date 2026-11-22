"""Pack menu3d Export --fullbody output without changing model coordinates."""

import argparse
import json
from pathlib import Path

import numpy as np


VISIBLE = {
    "c0001_body_051_UI_main", "c0001_hand_051B_UI_main",
    "c0001_faceUI_main", "c0001_hair_002a", "eye l0_main", "eye r0_main",
    "card", "smartphone", "school_bag", "gun_main", "gun_outline",
    *(f"ef line_obj_{i:02d}" for i in range(1, 9)),
}
REQUIRED = {"main_0015", "main_0016", "main_0017", "main_0113", "blend_0000"}
DECIMAL_MB = 1_000_000


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def json_bytes(data):
    return (json.dumps(data, separators=(",", ":"), allow_nan=False) + "\n").encode()


def pack(scratch, output, check_plan=None):
    source_meshes = read_json(scratch / "meshes.json")
    names = {mesh["name"] for mesh in source_meshes}
    if not VISIBLE <= names:
        raise ValueError(f"Missing visible nodes: {sorted(VISIBLE - names)}")
    meshes, ranges = [], []
    source_vertices = vertices = 0
    for mesh in source_meshes:
        count = mesh["vertices"]
        if mesh["name"] in VISIBLE:
            meshes.append(dict(mesh, offset=vertices))
            ranges.append(np.arange(source_vertices, source_vertices + count))
            vertices += count
        source_vertices += count
    indexes = np.concatenate(ranges)

    source_clips = read_json(scratch / "clips.json")
    clips = [
        c for c in source_clips if c["duration"] > 0 and (
            c["name"] in {"main_0113", "blend_0000"}
            or (c["name"].startswith("main_") and int(c["name"][5:]) < 100)
        )
    ]
    if not REQUIRED <= {c["name"] for c in clips}:
        raise ValueError("Required animation clips missing")
    for c in clips:
        frames = c["frames"]
        if c["unmapped"] or frames != list(range(len(frames))):
            raise ValueError(f"Missing frames or unmapped tracks: {c['name']}")
        if abs((len(frames) - 1) / 30 - c["duration"]) > 1 / 30 + 1e-6:
            raise ValueError(f"Invalid final frame: {c['name']}")

    def manifest(selected):
        return [dict(name=c["name"], frames=len(c["frames"]), duration=c["duration"],
                     speed=c["speed"], vertices=vertices) for c in selected]

    mesh_data = json_bytes(meshes)

    def size(selected):
        return len(mesh_data) + len(json_bytes(manifest(selected))) + sum(
            len(c["frames"]) * vertices * 6 for c in selected
        )

    omitted = []
    if size(clips) > 120 * DECIMAL_MB:
        omitted = [c["name"] for c in clips if c["name"].startswith("main_")
                   and 2 <= len(c["frames"]) <= 3 and c["name"] not in REQUIRED]
        clips = [c for c in clips if c["name"] not in omitted]
    if size(clips) > 120 * DECIMAL_MB:
        raise ValueError("Dataset exceeds 120 MB after short-clip filtering")
    if max(len(mesh_data), len(json_bytes(manifest(clips))),
           *(len(c["frames"]) * vertices * 6 for c in clips)) > 20 * DECIMAL_MB:
        raise ValueError("An output exceeds 20 MB")

    output.mkdir(parents=True, exist_ok=True)
    (output / "meshes.json").write_bytes(mesh_data)
    (output / "clips.json").write_bytes(json_bytes(manifest(clips)))
    for c in clips:
        name = c["name"]
        with (output / f"{name}.f16").open("wb") as stream:
            for frame in c["frames"]:
                original = np.fromfile(scratch / f"{name}_{frame:04d}.bin", dtype="<f4")
                if original.size != source_vertices * 3:
                    raise ValueError(f"Wrong source frame size: {name} {frame}")
                packed = original.reshape(-1, 3)[indexes].astype("<f2")
                if not np.isfinite(packed).all():
                    raise ValueError(f"Non-finite half coordinates: {name} {frame}")
                stream.write(packed.tobytes())

    if check_plan:
        # Keep all original surface slots so both captures use identical rendering.
        name, frame = "main_0015", 5
        original = np.fromfile(scratch / f"{name}_{frame:04d}.bin", dtype="<f4")
        restored = original.copy().reshape(-1, 3)
        with (output / f"{name}.f16").open("rb") as stream:
            stream.seek(frame * vertices * 6)
            restored[indexes] = np.frombuffer(stream.read(vertices * 6),
                                              dtype="<f2").reshape(vertices, 3)
        restored.tofile(scratch / "check_f16_0005.bin")
        # This manifest entry exists only in scratch, never in the deliverable.
        augmented = [c for c in source_clips if c["name"] != "check_f16"]
        augmented.append(dict(name="check_f16", frames=[frame]))
        (scratch / "clips.json").write_bytes(json_bytes(augmented))
        plan = [dict(folder="verify", clips=[name, "check_f16"],
                     frames={name: [frame], "check_f16": [frame]},
                     position=[-12, 158, 75], rotation_degrees=[0, -10, 0],
                     fov=44, h_offset=-14)]
        check_plan.write_bytes(json_bytes(plan))
        print("max coordinate delta:", float(np.max(np.abs(original - restored.ravel()))))
    print(json.dumps(dict(vertices=vertices, meshes=len(meshes), clips=len(clips),
                          frames=sum(len(c["frames"]) for c in clips),
                          bytes=size(clips), omitted=omitted)))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scratch", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--check-plan", type=Path)
    args = parser.parse_args()
    pack(args.scratch, args.output, args.check_plan)

