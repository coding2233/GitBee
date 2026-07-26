#!/usr/bin/env python3
"""Generate src/app_icon.h and icon assets from bee.ico."""

import struct, zlib, os, sys

def read_ico_pixels(ico_path):
    with open(ico_path, 'rb') as f:
        data = f.read()
    header = struct.unpack('<HHH', data[:6])
    assert header[0] == 0 and header[1] == 1
    entry = struct.unpack('<BBBBHHII', data[6:22])
    w = struct.unpack('<i', data[22+4:22+8])[0]
    h = struct.unpack('<i', data[22+8:22+12])[0]
    size = struct.unpack('<I', data[22:22+4])[0]
    offset = entry[7]
    actual_h = abs(h) // 2
    pixel_off = offset + size
    pixels = data[pixel_off:pixel_off + w * actual_h * 4]
    # BGRA -> RGBA, and flip vertically (ICO/BMP is bottom-up)
    stride = w * 4
    rgba = bytearray(len(pixels))
    for y in range(actual_h):
        src_row = pixels[y*stride:(y+1)*stride]
        dst_y = actual_h - 1 - y  # flip
        for x in range(w):
            si = x * 4
            di = (dst_y * w + x) * 4
            rgba[di+0] = src_row[si+2]
            rgba[di+1] = src_row[si+1]
            rgba[di+2] = src_row[si+0]
            rgba[di+3] = src_row[si+3]
    return w, actual_h, bytes(rgba)

def rgba_to_png(w, h, rgba):
    raw = b''
    stride = w * 4
    for y in range(h):
        raw += b'\x00'
        raw += rgba[y*stride:(y+1)*stride]
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' +
            chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) +
            chunk(b'IDAT', zlib.compress(raw)) +
            chunk(b'IEND', b''))

def resize_rgba(src_w, src_h, rgba, dst_w, dst_h):
    out = bytearray(dst_w * dst_h * 4)
    for dy in range(dst_h):
        for dx in range(dst_w):
            sx = dx * src_w // dst_w
            sy = dy * src_h // dst_h
            si = (sy * src_w + sx) * 4
            di = (dy * dst_w + dx) * 4
            out[di:di+4] = rgba[si:si+4]
    return bytes(out)

def write_header(w, h, rgba, path):
    lines = ['#pragma once', '#include <cstdint>']
    lines.append(f'static const int kAppIconWidth = {w};')
    lines.append(f'static const int kAppIconHeight = {h};')
    lines.append('static uint8_t kAppIconRGBA[] = {')
    for i in range(0, len(rgba), 12):
        chunk = ', '.join(f'0x{b:02x}' for b in rgba[i:i+12])
        lines.append(f'    {chunk},')
    lines.append('};')
    with open(path, 'w') as f:
        f.write('\n'.join(lines) + '\n')

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ico_path = os.path.join(root, 'bee.ico')

    if not os.path.exists(ico_path):
        print("Error: bee.ico not found")
        return 1

    w, h, rgba = read_ico_pixels(ico_path)

    # Generate app_icon.h
    header_path = os.path.join(root, 'src', 'app_icon.h')
    write_header(w, h, rgba, header_path)
    size_kb = os.path.getsize(header_path) / 1024
    print(f"Generated {header_path} ({size_kb:.0f} KB, {w}x{h})")

    # Generate 256x256 PNG
    png = rgba_to_png(w, h, rgba)
    png_path = os.path.join(root, 'assets', 'bee_256.png')
    with open(png_path, 'wb') as f:
        f.write(png)
    print(f"Generated {png_path}")

    # Generate hicolor PNGs
    for size in [16, 24, 32, 48, 64, 96, 128, 256]:
        resized = resize_rgba(w, h, rgba, size, size)
        png = rgba_to_png(size, size, resized)
        dirpath = f'assets/icons/hicolor/{size}x{size}/apps'
        os.makedirs(os.path.join(root, dirpath), exist_ok=True)
        out = os.path.join(root, dirpath, 'gitbee.png')
        with open(out, 'wb') as f:
            f.write(png)
        print(f"Generated {out}")

    # assets/icons/gitbee.png
    shutil = __import__('shutil')
    shutil.copy(os.path.join(root, 'assets', 'bee_256.png'),
                os.path.join(root, 'assets/icons/gitbee.png'))
    print("Generated assets/icons/gitbee.png")

if __name__ == '__main__':
    sys.exit(main())
