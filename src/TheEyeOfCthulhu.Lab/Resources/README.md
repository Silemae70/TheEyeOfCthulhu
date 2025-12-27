# The Eye of Cthulhu - Icons

Place your icon files here:

- `icon.ico` - Main application icon (256x256, 128x128, 64x64, 48x48, 32x32, 16x16)

## Creating an icon

You can create an .ico file from a PNG using online tools like:
- https://convertico.com/
- https://icoconvert.com/

Or use ImageMagick:
```bash
magick convert icon.png -define icon:auto-resize=256,128,64,48,32,16 icon.ico
```

## Suggested design

A stylized eye with tentacles, in purple/green colors matching the Cthulhu theme.
