#!/usr/bin/env python3
"""Generate src/app_icon.h from bee.ico (the source icon)."""

import struct, zlib, os, sys

def extract_png_from_ico(ico_path):
    with open(ico_path, 'rb') as f:
        data = f.read()
    header = struct.unpack('<HHH', data[:6])
    assert header[0] == 0 and header[1] == 1
    entry = struct.unpack('<BBBBHHII', data[6:22])
    offset = entry[7]
    bmp_data = data[offset:]
    w = struct.unpack('<i', bmp_data[4:8])[0]
    h = struct.unpack('<i', bmp_data[8:12])[0]
    bpp = struct.unpack('<H', bmp_data[14:16])[0]
    size = struct.unpack('<I', bmp_data[:4])[0]
    actual_h = abs(h) // 2
    pixel_off = offset + size
    pixels = data[pixel_off:pixel_off + w * actual_h * 4]
    # BGRA -> RGBA
    rgba = bytearray()
    for i in range(0, len(pixels), 4):
        rgba.extend([pixels[i+2], pixels[i+1], pixels[i+0], pixels[i+3]])
    # Build PNG
    raw = b''
    stride = w * 4
    for y in range(actual_h):
        raw += b'\x00'
        raw += bytes(rgba[y*stride:(y+1)*stride])
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n' +
           chunk(b'IHDR', struct.pack('>IIBBBBB', w, actual_h, 8, 6, 0, 0, 0)) +
           chunk(b'IDAT', zlib.compress(raw)) +
           chunk(b'IEND', b''))
    return png

def png_to_rgba_header(png_data):
    pos = 8
    chunks = {}
    while pos < len(png_data):
        length = struct.unpack('>I', png_data[pos:pos+4])[0]
        ctype = png_data[pos+4:pos+8].decode()
        cdata = png_data[pos+8:pos+8+length]
        if ctype == 'IHDR':
            w, h = struct.unpack('>II', cdata[:8])
            chunks['IHDR'] = (w, h)
        elif ctype == 'IDAT':
            chunks.setdefault('IDAT', []).append(cdata)
        elif ctype == 'IEND':
            break
        pos += 12 + length
    w, h = chunks['IHDR']
    raw = zlib.decompress(b''.join(chunks['IDAT']))
    stride = w * 4 + 1
    rgba = bytearray()
    prev = b'\x00' * stride
    for y in range(h):
        row = raw[y*stride:(y+1)*stride]
        ft = row[0]
        curr = bytearray(row[1:])
        if ft == 0: pass
        elif ft == 1:
            for i in range(4, len(curr)): curr[i] = (curr[i] + curr[i-4]) & 0xff
        elif ft == 2:
            for i in range(len(curr)): curr[i] = (curr[i] + prev[i+1]) & 0xff
        elif ft == 3:
            for i in range(4, len(curr)):
                curr[i] = (curr[i] + (curr[i-4] + prev[i+1]) // 2) & 0xff
        elif ft == 4:
            for i in range(4, len(curr)):
                a, b, c = curr[i-4], prev[i+1], prev[i-3]
                p = a + b - c
                pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                curr[i] = (curr[i] + pr) & 0xff
        rgba.extend(curr)
        prev = row
    return w, h, bytes(rgba)

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ico_path = os.path.join(root, 'bee.ico')
    out_path = os.path.join(root, 'src', 'app_icon.h')

    if not os.path.exists(ico_path):
        print("Error: bee.ico not found")
        return 1

    png = extract_png_from_ico(ico_path)
    w, h, rgba = png_to_rgba_header(png)

    lines = ['#pragma once', '#include <cstdint>']
    lines.append(f'static constexpr int kAppIconWidth = {w};')
    lines.append(f'static constexpr int kAppIconHeight = {h};')
    lines.append('static constexpr uint8_t kAppIconRGBA[] = {')
    for i in range(0, len(rgba), 12):
        chunk = ', '.join(f'0x{b:02x}' for b in rgba[i:i+12])
        lines.append(f'    {chunk},')
    lines.append('};')

    with open(out_path, 'w') as f:
        f.write('\n'.join(lines) + '\n')

    size_kb = os.path.getsize(out_path) / 1024
    print(f"Generated {out_path} ({size_kb:.0f} KB, {w}x{h})")

if __name__ == '__main__':
    sys.exit(main())
