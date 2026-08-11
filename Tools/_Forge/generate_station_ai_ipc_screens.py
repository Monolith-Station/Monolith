"""Adapt the south-facing IPC head-screen animations for Station AI cores."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Resources/Textures/_EinsteinEngines/Mobs/Customization/ipc_screens.rsi"
OUTPUT = ROOT / "Resources/Textures/_Forge/Mobs/Silicon/station_ai_ipc_screens.rsi"
FRAME_SIZE = 32
TARGET_X = 3
TARGET_Y = 6
TARGET_WIDTH = 26
TARGET_HEIGHT = 20


def get_frame(atlas: Image.Image, index: int) -> Image.Image:
    columns = atlas.width // FRAME_SIZE
    x = index % columns * FRAME_SIZE
    y = index // columns * FRAME_SIZE
    return atlas.crop((x, y, x + FRAME_SIZE, y + FRAME_SIZE))


def union_alpha_bounds(frames: list[Image.Image]) -> tuple[int, int, int, int]:
    bounds = [frame.getchannel("A").getbbox() for frame in frames]
    visible = [bound for bound in bounds if bound is not None]
    if not visible:
        return 0, 0, 1, 1

    return (
        min(bound[0] for bound in visible),
        min(bound[1] for bound in visible),
        max(bound[2] for bound in visible),
        max(bound[3] for bound in visible),
    )


def adapt_frames(frames: list[Image.Image]) -> list[Image.Image]:
    bounds = union_alpha_bounds(frames)
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    scale = max(1, min(TARGET_WIDTH // width, TARGET_HEIGHT // height))
    scaled_size = width * scale, height * scale
    target_x = TARGET_X + (TARGET_WIDTH - scaled_size[0]) // 2
    target_y = TARGET_Y + (TARGET_HEIGHT - scaled_size[1]) // 2

    adapted: list[Image.Image] = []
    for frame in frames:
        cropped = frame.crop(bounds).resize(scaled_size, Image.Resampling.NEAREST)
        output = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
        output.alpha_composite(cropped, (target_x, target_y))
        adapted.append(output)

    return adapted


def main() -> None:
    source_meta = json.loads((SOURCE / "meta.json").read_text(encoding="utf-8"))
    OUTPUT.mkdir(parents=True, exist_ok=True)

    output_states: list[dict[str, object]] = []
    for state in source_meta["states"]:
        name = state["name"]
        delays = state.get("delays")
        south_delays = delays[0] if delays else [1.0]
        south_frame_count = len(south_delays)

        with Image.open(SOURCE / f"{name}.png") as source_image:
            atlas = source_image.convert("RGBA")
            south_frames = [get_frame(atlas, index) for index in range(south_frame_count)]

        adapted = adapt_frames(south_frames)
        output_atlas = Image.new("RGBA", (FRAME_SIZE * len(adapted), FRAME_SIZE), (0, 0, 0, 0))
        for index, frame in enumerate(adapted):
            output_atlas.alpha_composite(frame, (FRAME_SIZE * index, 0))
        output_atlas.save(OUTPUT / f"{name}.png")

        output_state: dict[str, object] = {"name": name}
        if len(south_delays) > 1:
            output_state["delays"] = [south_delays]
        output_states.append(output_state)

    output_meta = {
        "version": 1,
        "license": source_meta["license"],
        "copyright": (
            f'{source_meta["copyright"]}. South-facing frames adapted and scaled for '
            "Forge Station AI core screens."
        ),
        "size": {"x": FRAME_SIZE, "y": FRAME_SIZE},
        "states": output_states,
    }
    (OUTPUT / "meta.json").write_text(
        json.dumps(output_meta, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
