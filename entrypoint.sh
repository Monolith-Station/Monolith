#!/bin/sh
set -e

# Base args: baked config + persistent data dir on the volume.
set -- --config-file /app/server_config.toml --data-dir /data

# Always-on hardening (defense in depth; also set in the TOML).
set -- "$@" --cvar "console.loginlocal=false"

# Optional env overrides.
[ -n "$SS14_HOSTNAME" ]      && set -- "$@" --cvar "game.hostname=$SS14_HOSTNAME"
[ -n "$SS14_HUB_ADVERTISE" ] && set -- "$@" --cvar "hub.advertise=$SS14_HUB_ADVERTISE"
[ -n "$SS14_AUTH_MODE" ]     && set -- "$@" --cvar "auth.mode=$SS14_AUTH_MODE"
[ -n "$SS14_HOST_USER" ]     && set -- "$@" --cvar "console.login_host_user=$SS14_HOST_USER"

# Domain-derived launcher routing (HTTPS status via proxy, UDP gameplay direct).
if [ -n "$SS14_DOMAIN" ]; then
  set -- "$@" --cvar "hub.server_url=ss14s://$SS14_DOMAIN"
  set -- "$@" --cvar "status.connectaddress=udp://$SS14_DOMAIN:1212"
fi

echo "Starting Robust.Server with: $*"
exec ./Robust.Server "$@"
