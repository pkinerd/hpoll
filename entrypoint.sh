#!/bin/sh
# Ensure the data directory exists and is writable by the hpoll user.
# When a bind mount (e.g. ./data:/app/data) creates the host directory,
# it is owned by root. Fix ownership before starting the app.
mkdir -p /app/data
chown -R hpoll:hpoll /app/data

exec runuser -u hpoll -- dotnet Hpoll.Worker.dll
