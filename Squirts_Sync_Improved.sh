#!/bin/bash

# Set color to white on black
printf '\e[0;37m\e[40m'
clear

locationA="/Users/christopherheld/Documents/Documents - Christopher's Mac mini/Game Development/monsterscoming .nosync"
locationB="/Users/christopherheld/Documents/Documents - Christopher's Mac mini/Game Development/monsterscoming_clone .nosync"

# Function to check if git command succeeded
check_git_status() {
    if [ $? -ne 0 ]; then
        printf '\e[0;31m'  # Red color for errors
        echo "Error: Git operation failed in $1"
        printf '\e[0m'      # Reset color
        exit 1
    fi
}

echo "=== Syncing Unity Project for Multiplayer Testing ==="

# Go to locationA, pull and push changes
echo "📁 Working in main project: $locationA"
cd "${locationA}" || exit

# Pull latest changes first
echo "🔄 Pulling latest changes..."
git pull
check_git_status "locationA pull"

# Check if there are any changes to commit
if [[ -n $(git status --porcelain) ]]; then
    # Generate a more meaningful commit message
    timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    message="Multiplayer test sync - $timestamp"
    
    echo "📝 Committing changes: $message"
    git add .
    git commit -m "$message"
    check_git_status "locationA commit"
    
    echo "⬆️ Pushing changes..."
    git push
    check_git_status "locationA push"
else
    echo "✅ No changes to commit in main project"
fi

# Set color to green
printf '\e[0;32m'
echo ""
echo "🔄 Updating clone for multiplayer testing..."

# Go to locationB, clean up, fetch, reset, and pull
echo "📁 Working in clone: $locationB"
cd "${locationB}" || exit

# Clean up any local changes
echo "🧹 Cleaning local changes..."
git clean -fd
git fetch --all
check_git_status "locationB fetch"

# Get current branch and reset to match remote
branch=$(git rev-parse --abbrev-ref HEAD)
echo "🔄 Resetting to origin/$branch..."
git reset --hard "origin/$branch"
check_git_status "locationB reset"

git pull
check_git_status "locationB pull"

# Reset color to default
printf '\e[0m'

echo ""
echo "✅ Sync complete! Both projects are now synchronized for multiplayer testing."
echo "🎮 Main project: $locationA"
echo "🎮 Clone project: $locationB"
