# Generates app.ico (16-256 px) and a preview image.
# Usage: python make_icon.py ..\app.ico preview.png   (needs Pillow: pip install pillow)
from PIL import Image, ImageDraw
import sys
S = 1024
TOP, BOT = (0, 120, 215), (0, 84, 160)

def background():
    bg = Image.new('RGBA', (S, S))
    gd = ImageDraw.Draw(bg)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(TOP[i] + (BOT[i] - TOP[i]) * t) for i in range(3)) + (255,))
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    mask = Image.new('L', (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([40, 40, S - 40, S - 40], radius=200, fill=255)
    img.paste(bg, (0, 0), mask)
    return img

def draw(size_px):
    img = background()
    d = ImageDraw.Draw(img)
    w = (255, 255, 255, 255)
    small = size_px <= 24
    lw = 72 if small else 46              # outline / cable thickness
    # phone outline
    d.rounded_rectangle([200, 140, 520, 700], radius=64, outline=w, width=lw)
    if not small:
        d.ellipse([336, 196, 384, 244], fill=w)                             # punch-hole front camera (Android style)
    # cable: one continuous path (down, rounded corner, along, rounded corner, up to the plug)
    import math
    cx0, x1, ybot, r = 360, 760, 840, 80
    pts = [(cx0, 690), (cx0, ybot - r)]
    for k in range(1, 21):                       # corner 1: from going down to going right
        a = math.pi - k * (math.pi / 2) / 20
        pts.append((cx0 + r + r * math.cos(a), ybot - r + r * math.sin(a)))
    pts.append((x1 - r, ybot))
    for k in range(1, 21):                       # corner 2: from going right to going up
        a = math.pi / 2 - k * (math.pi / 2) / 20
        pts.append((x1 - r + r * math.cos(a), ybot - r + r * math.sin(a)))
    pts.append((x1, 620))
    d.line(pts, fill=w, width=lw, joint='curve')
    for (px, py) in pts:                         # fill the joints, so the bends have no seams
        d.ellipse([px - lw / 2, py - lw / 2, px + lw / 2, py + lw / 2], fill=w)
    # 3.5 mm jack plug, pointing up
    grip_w = 120 if not small else 150
    d.rounded_rectangle([x1 - grip_w // 2, 430, x1 + grip_w // 2, 640], radius=24, fill=w)   # grip
    shaft_w = 50 if not small else 70
    top_y = 200 if not small else 230
    d.rounded_rectangle([x1 - shaft_w // 2, top_y, x1 + shaft_w // 2, 440], radius=shaft_w // 2, fill=w)
    if not small:
        bg_mid = tuple(int(TOP[i] + (BOT[i] - TOP[i]) * 0.3) for i in range(3)) + (255,)
        for y in (285, 355):                                           # insulating rings between tip/ring/sleeve
            d.rectangle([x1 - shaft_w // 2 - 2, y, x1 + shaft_w // 2 + 2, y + 14], fill=bg_mid)
    return img.resize((size_px, size_px), Image.LANCZOS)

sizes = [16, 24, 32, 48, 64, 128, 256]
imgs = [draw(s) for s in sizes]
imgs[-1].save(sys.argv[1], format='ICO', sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
strip = Image.new('RGBA', (sum(sizes) + 10 * len(sizes), 256), (255, 255, 255, 255))
x = 0
for s, im in zip(sizes, imgs):
    strip.paste(im, (x, 256 - s), im); x += s + 10
# also a dark-background strip (dark taskbar)
dark = Image.new('RGBA', strip.size, (32, 32, 32, 255)); dark.paste(strip, (0, 0), strip)
both = Image.new('RGBA', (strip.width, 520), (255, 255, 255, 255)); both.paste(strip, (0, 0)); 
dstrip = Image.new('RGBA', strip.size, (32, 32, 32, 255)); x = 0
for s, im in zip(sizes, imgs):
    dstrip.paste(im, (x, 256 - s), im); x += s + 10
both.paste(dstrip, (0, 264))
both.save(sys.argv[2])
print('ok')
