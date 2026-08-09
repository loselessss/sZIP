from pathlib import Path
import sys

from PIL import Image


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: generate_windows_icon.py INPUT.png OUTPUT.ico")

    source_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    image = Image.open(source_path).convert("RGBA")
    alpha_box = image.getchannel("A").getbbox()
    if alpha_box is None:
        raise SystemExit("input image has no visible pixels")

    subject = image.crop(alpha_box)
    side = max(subject.width, subject.height)
    padding = max(8, round(side * 0.08))
    canvas_side = side + padding * 2
    canvas = Image.new("RGBA", (canvas_side, canvas_side), (0, 0, 0, 0))
    canvas.alpha_composite(
        subject,
        ((canvas_side - subject.width) // 2, (canvas_side - subject.height) // 2),
    )

    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
             (48, 48), (64, 64), (128, 128), (256, 256)]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, format="ICO", sizes=sizes)


if __name__ == "__main__":
    main()
