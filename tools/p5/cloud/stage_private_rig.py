"""Stage Joker's desktop Spine resources under paths other skins cannot replace."""

import argparse
import json
import re
import shutil
from pathlib import Path


RIGS = {
    "animations/characters/silent": "p5joker/rig/combat",
    "animations/rest_site/silent": "p5joker/rig/rest_site",
}
DESCRIPTORS = {
    "animations/characters/silent/silent_skel_data.tres": "p5joker/rig/combat/silent_skel_data.tres",
    "animations/merchant/silent/silent_merchant_skel_data.tres": "p5joker/rig/merchant/silent_merchant_skel_data.tres",
    "animations/rest_site/silent/rest_site_silent_skel_data.tres": "p5joker/rig/rest_site/rest_site_silent_skel_data.tres",
}


def stage_private_rig(stage: Path) -> None:
    imported = stage / "p5joker/rig/imported"
    imported.mkdir(parents=True, exist_ok=True)

    for source_dir, private_dir in RIGS.items():
        (stage / private_dir).mkdir(parents=True, exist_ok=True)
        for source_import in sorted((stage / source_dir).glob("*.import")):
            filename = source_import.name.removesuffix(".import")
            source = f"{source_dir}/{filename}"
            private = f"{private_dir}/{filename}"
            metadata = source_import.read_text(encoding="utf-8")
            match = re.search(r'^path="res://([^"]+)"$', metadata, re.MULTILINE)
            if match is None:
                raise ValueError(f"Missing imported artifact for {source}")
            artifact = stage / match[1]
            private_artifact = imported / artifact.name
            if artifact.suffix == ".spatlas":
                atlas = json.loads(artifact.read_text(encoding="utf-8"))
                atlas["source_path"] = f"res://{private}"
                private_artifact.write_text(json.dumps(atlas, ensure_ascii=False), encoding="utf-8")
                shutil.copyfile(stage / source, stage / private)
            else:
                shutil.copyfile(artifact, private_artifact)

            metadata = re.sub(r'^uid="[^"]*"\n', "", metadata, flags=re.MULTILINE)
            metadata = metadata.replace(f'res://{source}"', f'res://{private}"')
            metadata = metadata.replace(f"res://{match[1]}", "res://" + private_artifact.relative_to(stage).as_posix())
            (stage / f"{private}.import").write_text(metadata, encoding="utf-8")

    for source, private in DESCRIPTORS.items():
        descriptor = (stage / source).read_text(encoding="utf-8")
        descriptor = re.sub(r' uid="uid://[^"]*"', "", descriptor)
        for source_dir, private_dir in RIGS.items():
            descriptor = descriptor.replace(f"res://{source_dir}/", f"res://{private_dir}/")
        target = stage / private
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(descriptor, encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", type=Path)
    stage_private_rig(parser.parse_args().stage)
