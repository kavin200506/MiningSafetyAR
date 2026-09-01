from PIL import Image
import os

img_path = r'C:\Users\GOKULAKANNAN\.gemini\antigravity-ide\brain\95eafa19-2445-4446-b5db-9db3f20251fb\.user_uploaded\media_1788259302736.png'
out_dir = r'C:\MiningSafetyAR\Assets\UI\Sprites\Icons'

img = Image.open(img_path)
width, height = img.size
cell_w = width // 4
cell_h = height // 2

icons = [
    ('icon_home', 0, 0),
    ('icon_training', 1, 0),
    ('icon_progress', 2, 0),
    ('icon_settings', 3, 0),
    ('module_fire', 0, 1),
    ('module_gas', 1, 1),
    ('module_machinery', 2, 1)
]

for name, col, row in icons:
    left = col * cell_w
    top = row * cell_h
    right = left + cell_w
    bottom = top + cell_h
    
    # Optional: crop a bit tighter to remove text? The user might just want the whole square.
    # We will just crop the cell for now.
    cropped = img.crop((left, top, right, bottom))
    out_path = os.path.join(out_dir, f'{name}.png')
    cropped.save(out_path)
    print(f'Saved {out_path}')
