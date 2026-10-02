#!/bin/sh
# Starts AATool on Linux and other non-Windows systems.
# Requires Mono (with System.Windows.Forms and libgdiplus) and an X11 or XWayland session.
exec mono "$(dirname "$(readlink -f "$0")")/AATool.exe" "$@"
