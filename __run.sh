#!/bin/bash

git submodule update --init --recursive
clear
echo "Running on: $(glxinfo | grep 'OpenGL renderer' | sed 's/.*: //' | sed 's/ (.*//')"
pkill -9 Content.Server
echo
echo "Building server..."
dotnet build Content.Server --configuration Tools
if [ $? != 0 ]; then
    echo "Server build failed! Check logs and try again."
    exit 1
fi

echo
echo "Building client..."
dotnet build Content.Client --configuration Tools
if [ $? != 0 ]; then
    echo "Client build failed! Check logs and try again."
    exit 1
fi

konsole -e "dotnet run --project Content.Server --configuration Tools --no-build" &
dotnet run --project Content.Client --configuration Tools --no-build
pkill -9 Content.Server
