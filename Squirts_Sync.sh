#!/bin/bash

# Set color to white on black
printf '\e[0;37m\e[40m'
clear

locationA="/Users/christopherheld/Development/Game Development/Squirts 2D Project"
locationB="/Users/christopherheld/Development/Game Development/Squirts 2D Project CLONE"

# Go to locationA, pull and push changes
cd "${locationA}" || exit

message=$((RANDOM * RANDOM))
git pull

git add .
git commit -m "$message"
git push

# Set color to green
printf '\e[0;32m'

echo "Clone"

# Go to locationB, clean up, fetch, reset, and pull
cd "${locationB}" || exit
git clean -fd
git fetch --all
branch=$(git rev-parse --abbrev-ref HEAD)  # Get the current branch name
git reset --hard "origin/$branch"  # Ensure using the current branch
git pull

# Reset color to default
printf '\e[0m'
