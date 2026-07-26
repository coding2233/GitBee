# Maintainer: Your Name <your.email@example.com>

pkgname=gitbee
pkgver=1.0.0
pkgrel=1
pkgdesc="A GUI client for Git"
arch=('x86_64' 'aarch64')
url="https://github.com/coding2233/GitBee"
license=('MIT')
depends=('sdl3' 'glibc' 'gcc-libs' 'curl')
install="${pkgname}.install"
makedepends=('xmake' 'git')
source=("${pkgname}::git+${url}.git")
sha256sums=('SKIP')

prepare() {
  cd "${srcdir}/${pkgname}"
  git submodule update --init --recursive
}

build() {
  cd "${srcdir}/${pkgname}"
  export GITBEE_VERSION="${pkgver}"
  xmake f -y -v
  xmake -y -v
}

check() {
  cd "${srcdir}/${pkgname}"
  xmake run test_gitcore
}

package() {
  cd "${srcdir}/${pkgname}"
  DESTDIR="${pkgdir}" ./install.sh
}
