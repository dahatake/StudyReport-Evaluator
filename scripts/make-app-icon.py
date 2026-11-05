"""Regenerate the application icon (Assets/AppIcon.ico and AppIcon.png).

Usage: python scripts/make-app-icon.py   (requires Pillow)
"""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[1] / "src" / "StudyReportEvaluator.App" / "Assets"
S = 1024  # master canvas; downscaled for every size


def render() -> Image.Image:
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # background tile: vertical blue gradient clipped to a rounded square
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    top, bottom = (37, 99, 235), (30, 58, 138)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([32, 32, S - 32, S - 32], radius=210, fill=255)
    img.paste(grad, (0, 0), mask)

    # report sheet with folded corner
    x0, y0, x1, y1, fold = 236, 170, 740, 830, 130
    d.polygon([(x0, y0), (x1 - fold, y0), (x1, y0 + fold), (x1, y1), (x0, y1)], fill=(255, 255, 255, 255))
    d.polygon([(x1 - fold, y0), (x1 - fold, y0 + fold), (x1, y0 + fold)], fill=(191, 209, 250, 255))

    # text lines
    for i, w in enumerate((380, 380, 250)):
        y = 400 + i * 100
        d.rounded_rectangle([x0 + 70, y, x0 + 70 + w, y + 40], radius=20, fill=(147, 170, 220, 255))

    # evaluation badge: green circle with check mark
    cx, cy, r = 700, 700, 190
    d.ellipse([cx - r - 28, cy - r - 28, cx + r + 28, cy + r + 28], fill=(30, 58, 138, 255))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(34, 197, 94, 255))
    pts = [(cx - 95, cy + 5), (cx - 25, cy + 80), (cx + 105, cy - 70)]
    d.line(pts, fill=(255, 255, 255, 255), width=54, joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse([p[0] - 27, p[1] - 27, p[0] + 27, p[1] + 27], fill=(255, 255, 255, 255))
    return img


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    master = render()
    master.resize((256, 256), Image.LANCZOS).save(OUT / "AppIcon.png")
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [master.resize((s, s), Image.LANCZOS) for s in sizes]
    frames[-1].save(OUT / "AppIcon.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])


if __name__ == "__main__":
    main()
