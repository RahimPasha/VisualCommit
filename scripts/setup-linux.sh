#!/usr/bin/env bash
#
# Prepares a Linux machine or container to build and test VisualCommit: the .NET 10 SDK and the
# system libraries the tests need. Written for Ubuntu 24.04 running as root, which is what a
# Claude Code cloud session runs on. CI runs this script in a bare ubuntu:24.04 container on
# every push that changes code, and then builds and tests there.
#
#   bash scripts/setup-linux.sh
#
# It is safe to run again: it skips what is already there. It does not touch git's configuration.
#
# Everything comes from the system's own package servers where they have it. That matters in a
# Claude Code cloud session, whose default network setting allows Ubuntu's package servers but
# not the server that Microsoft's own .NET installer downloads the SDK from.
#
# SETUP_LINUX_SDK_SOURCE=installer skips the package servers for the SDK and uses Microsoft's
# installer straight away. CI sets it in one of its two container jobs, so that both ways of
# getting the SDK stay proven.

set -euo pipefail

SDK_MAJOR="10"
SDK_PACKAGE="dotnet-sdk-10.0"
SDK_CHANNEL="10.0"

say() { printf 'setup-linux: %s\n' "$*"; }
fail() { printf 'setup-linux: %s\n' "$*" >&2; exit 1; }

if [ "$(uname -s)" != "Linux" ]; then
  say "this script is for Linux; nothing to do on $(uname -s)."
  exit 0
fi

if [ "$(id -u)" -eq 0 ]; then
  as_root=""
elif command -v sudo >/dev/null 2>&1; then
  as_root="sudo"
else
  as_root="unavailable"
fi

can_use_apt() { command -v apt-get >/dev/null 2>&1 && [ "$as_root" != "unavailable" ]; }

apt_lists_refreshed=false
refresh_apt_lists() {
  [ "$apt_lists_refreshed" = false ] || return 0
  say "refreshing the package lists"
  # A list that cannot be fetched (a package source the network does not allow) is no reason
  # to stop: what is needed comes from the distribution's own servers.
  $as_root apt-get update -qq || say "note: not every package list could be refreshed; going on with the lists there are."
  apt_lists_refreshed=true
}

apt_install() {
  $as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends "$@"
}

# --- System packages -------------------------------------------------------------------------
#
#   git, curl, ca-certificates  the tests run real git; curl and the certificates are for downloads
#   libicu                      .NET needs it for culture data (the build does not switch it off)
#   libfontconfig1 and a font   the headless UI tests render with Skia, which loads fontconfig
#   locales, en_US.UTF-8        the app starts git with LC_ALL=en_US.UTF-8 (decision D30);
#                               git answers in English without it too, so this one is optional

# The lists are read into variables first: with "pipefail", piping a long list into "grep -q"
# can report a failure although the text was found. On Debian a normal user's PATH has no
# /sbin, where ldconfig lives.
ldconfig_command="$(command -v ldconfig || echo /sbin/ldconfig)"
libraries="$("$ldconfig_command" -p 2>/dev/null || true)"
has_library() { grep -q "$1" <<<"$libraries"; }
has_locale() { grep -qi '^en_US\.utf-\?8$' <<<"$(locale -a 2>/dev/null || true)"; }

packages=()
command -v git >/dev/null 2>&1 || packages+=(git)
command -v curl >/dev/null 2>&1 || packages+=(curl)
[ -e /etc/ssl/certs/ca-certificates.crt ] || packages+=(ca-certificates)
has_library 'libfontconfig\.so\.1' || packages+=(libfontconfig1 fonts-dejavu-core)
need_icu=false
has_library 'libicuuc\.so' || need_icu=true

if [ "${#packages[@]}" -gt 0 ] || [ "$need_icu" = true ]; then
  missing="${packages[*]}"
  [ "$need_icu" = false ] || missing="${missing:+$missing }libicu"
  command -v apt-get >/dev/null 2>&1 \
    || fail "these are missing and this is not a Debian or Ubuntu system, so install them by hand: $missing"
  [ "$as_root" != "unavailable" ] \
    || fail "these are missing and installing them needs root or sudo: $missing"

  refresh_apt_lists

  if [ "$need_icu" = true ]; then
    # The package is named after its version: libicu74 on Ubuntu 24.04, libicu70 on 22.04.
    icu_package="$(apt-cache search --names-only '^libicu[0-9]+$' | awk '{print $1}' | sort -V | tail -n 1)"
    [ -n "$icu_package" ] || fail "no libicu package was found in the package lists."
    packages+=("$icu_package")
  fi

  say "installing: ${packages[*]}"
  apt_install "${packages[@]}"
fi

if ! has_locale; then
  if can_use_apt; then
    # The "locales" package brings the locale's source files and locale-gen.
    if [ ! -e /usr/share/i18n/locales/en_US ]; then
      refresh_apt_lists
      say "installing: locales"
      apt_install locales || true
    fi
    say "generating the en_US.UTF-8 locale"
    # Ubuntu's locale-gen takes the locale as an argument; Debian's does not, and localedef
    # works on both.
    $as_root locale-gen en_US.UTF-8 >/dev/null 2>&1 || true
    has_locale || $as_root localedef -i en_US -f UTF-8 en_US.UTF-8 >/dev/null 2>&1 || true
  fi
  has_locale || say "note: the en_US.UTF-8 locale is missing and could not be generated; git still answers in English."
fi

# --- .NET SDK --------------------------------------------------------------------------------

has_sdk() {
  command -v dotnet >/dev/null 2>&1 || return 1
  grep -q "^${SDK_MAJOR}\." <<<"$(dotnet --list-sdks 2>/dev/null || true)"
}

# Microsoft's installer, for systems whose package servers do not carry the SDK. It downloads
# from builds.dotnet.microsoft.com.
install_sdk_with_microsofts_installer() {
  local install_dir installer
  if [ "$(id -u)" -eq 0 ]; then
    # /usr/share/dotnet is where a built program looks for .NET by itself, so the test
    # programs start without DOTNET_ROOT being set in every shell.
    install_dir="/usr/share/dotnet"
  else
    install_dir="$HOME/.dotnet"
  fi

  say "installing the .NET $SDK_CHANNEL SDK into $install_dir with the installer from dot.net"
  installer="$(mktemp)"
  if ! curl -fsSL --retry 2 https://dot.net/v1/dotnet-install.sh -o "$installer"; then
    rm -f "$installer"
    fail "the .NET installer could not be downloaded. dot.net hands the request on to builds.dotnet.microsoft.com. In a Claude Code cloud session that server is reachable only after it is added to the environment's allowed domains."
  fi
  if ! bash "$installer" --channel "$SDK_CHANNEL" --install-dir "$install_dir" --no-path; then
    rm -f "$installer"
    fail "the .NET SDK could not be downloaded. The installer needs builds.dotnet.microsoft.com. In a Claude Code cloud session that server is reachable only after it is added to the environment's allowed domains."
  fi
  rm -f "$installer"

  if [ "$(id -u)" -eq 0 ]; then
    ln -sf "$install_dir/dotnet" /usr/local/bin/dotnet
    if [ ! -e /etc/dotnet/install_location ]; then
      mkdir -p /etc/dotnet
      printf '%s\n' "$install_dir" > /etc/dotnet/install_location
    elif [ "$(cat /etc/dotnet/install_location)" != "$install_dir" ]; then
      say "note: /etc/dotnet/install_location names another .NET. If test programs cannot find .NET $SDK_MAJOR, run: export DOTNET_ROOT=$install_dir"
    fi
  else
    export DOTNET_ROOT="$install_dir"
    export PATH="$install_dir:$PATH"
    say "not root, so nothing was put on the PATH for other shells. Add these to your shell profile:"
    say "  export DOTNET_ROOT=$install_dir"
    say "  export PATH=$install_dir:\$PATH"
    if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
      # Inside a Claude Code SessionStart hook this file carries variables into the session.
      printf 'export DOTNET_ROOT=%q\nexport PATH=%q:"$PATH"\n' "$install_dir" "$install_dir" >> "$CLAUDE_ENV_FILE"
    fi
  fi
}

if has_sdk; then
  say ".NET SDK $(dotnet --version) is already installed."
else
  installed_from_packages=false
  if [ "${SETUP_LINUX_SDK_SOURCE:-}" = "installer" ]; then
    say "SETUP_LINUX_SDK_SOURCE=installer: not asking the package servers for the SDK."
  elif can_use_apt; then
    refresh_apt_lists
    if apt-cache show "$SDK_PACKAGE" >/dev/null 2>&1; then
      say "installing $SDK_PACKAGE from the system's package servers"
      if apt_install "$SDK_PACKAGE"; then
        installed_from_packages=true
      else
        say "note: installing $SDK_PACKAGE failed; trying Microsoft's installer instead."
      fi
    else
      say "note: the package servers have no $SDK_PACKAGE (Ubuntu 24.04 and newer have it); trying Microsoft's installer instead."
    fi
  fi

  if [ "$installed_from_packages" = false ]; then
    install_sdk_with_microsofts_installer
  fi

  has_sdk || fail "the .NET $SDK_MAJOR SDK was installed, but 'dotnet' ($(command -v dotnet || echo 'not on the PATH')) does not list it."
fi

# --- Summary ---------------------------------------------------------------------------------

say "ready: .NET SDK $(dotnet --version), $(git --version)"
command -v gh >/dev/null 2>&1 \
  || say "note: the GitHub CLI (gh) is not installed. CI results can still be read; see 'Commands' in CLAUDE.md."
say "next: dotnet build, then dotnet test"
