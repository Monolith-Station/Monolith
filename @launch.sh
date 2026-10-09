#!/bin/bash

git submodule update --init --recursive
clear
echo "Running on: $(glxinfo | grep 'OpenGL renderer' | sed 's/.*: //' | sed 's/ (.*//')"
pkill -9 Content.Server
konsole -e "dotnet run --project Content.Server --configuration Tools --no-build" &
dotnet run --project Content.Client --configuration Tools --no-build
pkill -9 Content.Server
