#!/bin/sh
# Install GitBee to the system (Linux)
# Usage:
#   sudo ./install.sh          # install to system
#   DESTDIR=/foo ./install.sh  # install to staging dir (for PKGBUILD)

set -e

BUILD_DIR="build/linux/x86_64/release"
DESTDIR="${DESTDIR:-}"

if [ ! -f "$BUILD_DIR/GitBee" ]; then
    echo "Error: GitBee binary not found. Run 'xmake' first."
    echo "  Expected: $BUILD_DIR/GitBee"
    exit 1
fi

echo "Installing GitBee..."

# Binary
install -Dm755 "$BUILD_DIR/GitBee" "${DESTDIR}/usr/lib/gitbee/gitbee"

# Wrapper script
mkdir -p "${DESTDIR}/usr/bin"
cat > "${DESTDIR}/usr/bin/gitbee" << 'WRAPPER'
#!/bin/sh
export GITBEE_DATA="/usr/share/gitbee"
if [ -d "$GITBEE_DATA" ]; then
    cd "$GITBEE_DATA"
fi
exec /usr/lib/gitbee/gitbee "$@"
WRAPPER
chmod 755 "${DESTDIR}/usr/bin/gitbee"

# Data files
mkdir -p "${DESTDIR}/usr/share/gitbee"
cp -r fonts "${DESTDIR}/usr/share/gitbee/"
if [ -d scripts ]; then
    cp -r scripts "${DESTDIR}/usr/share/gitbee/"
fi

# Desktop entry
install -Dm644 assets/gitbee.desktop "${DESTDIR}/usr/share/applications/gitbee.desktop"

# Icons
for size in 16 24 32 48 64 96 128 256; do
    install -Dm644 "assets/icons/hicolor/${size}x${size}/apps/gitbee.png" \
        "${DESTDIR}/usr/share/icons/hicolor/${size}x${size}/apps/gitbee.png"
done

# Update icon cache (only when installing to real root)
if [ -z "$DESTDIR" ] && command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
fi

echo "Done. Run 'gitbee' to start."
