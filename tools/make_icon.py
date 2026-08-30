# -*- coding: utf-8 -*-
# 生成应用图标（黑色主题·分组）：深黑圆角底 + 勾选清单（勾选项由树状竖线串联，表示"勾选服装进组"）
from PIL import Image, ImageDraw

SIZE = 256
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

# 深黑渐变底（#2E2E34 → #101013）
top = (46, 46, 52)
bottom = (16, 16, 19)
grad = Image.new("RGBA", (SIZE, SIZE))
gd = ImageDraw.Draw(grad)
for y in range(SIZE):
    t = y / (SIZE - 1)
    r = int(top[0] + (bottom[0] - top[0]) * t)
    g = int(top[1] + (bottom[1] - top[1]) * t)
    b = int(top[2] + (bottom[2] - top[2]) * t)
    gd.line([(0, y), (SIZE, y)], fill=(r, g, b, 255))

mask = Image.new("L", (SIZE, SIZE), 0)
md = ImageDraw.Draw(mask)
md.rounded_rectangle([8, 8, SIZE - 8, SIZE - 8], radius=56, fill=255)
img.paste(grad, (0, 0), mask)

outline = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
odl = ImageDraw.Draw(outline)
odl.rounded_rectangle([9, 9, SIZE - 9, SIZE - 9], radius=55, outline=(96, 96, 104, 255), width=3)
img = Image.alpha_composite(img, outline)

# 勾选清单层
overlay = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
od = ImageDraw.Draw(overlay)

rows = [80, 128, 176]      # 三行勾选项
box_cx = 74                 # 勾选框中心 x
box = 34                    # 勾选框边长

# 树状竖线：把三个勾选项串起来（分组树）
od.rounded_rectangle(
    [box_cx - 4, rows[0], box_cx + 4, rows[-1]],
    radius=4, fill=(120, 120, 130, 220),
)

for cy in rows:
    # 勾选框
    od.rounded_rectangle(
        [box_cx - box // 2, cy - box // 2, box_cx + box // 2, cy + box // 2],
        radius=9, fill=(20, 20, 24, 255), outline=(232, 232, 238, 255), width=4,
    )
    # 对勾
    cx, cy2 = box_cx, cy
    od.line(
        [(cx - 10, cy2 + 1), (cx - 3, cy2 + 9), (cx + 11, cy2 - 8)],
        fill=(232, 232, 238, 255), width=6, joint="curve",
    )
    # 右侧名称条
    od.rounded_rectangle([108, cy - 6, 204, cy + 6], radius=6, fill=(120, 120, 130, 200))

img = Image.alpha_composite(img, overlay)

img.save(
    "src/BSGroupGenerator/app.ico",
    format="ICO",
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
img.resize((128, 128)).save("tools/icon_preview.png")
print("icon ok")
