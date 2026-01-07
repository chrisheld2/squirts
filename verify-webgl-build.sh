#!/bin/bash

# WebGL Build Verification Script
# Verifies that the CSP meta tag is present in the built WebGL index.html

echo "=== WebGL Build CSP Verification ==="
echo ""

BUILD_DIR="WebGL Builds"
INDEX_FILE="$BUILD_DIR/index.html"

# Check if build directory exists
if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ Error: Build directory '$BUILD_DIR' not found"
    echo "   Please build the WebGL project first in Unity"
    exit 1
fi

# Check if index.html exists
if [ ! -f "$INDEX_FILE" ]; then
    echo "❌ Error: '$INDEX_FILE' not found"
    echo "   Please build the WebGL project first in Unity"
    exit 1
fi

echo "✅ Found WebGL build at: $BUILD_DIR"
echo ""

# Check for CSP meta tag
if grep -q "Content-Security-Policy" "$INDEX_FILE"; then
    echo "✅ CSP meta tag found in index.html"
    echo ""

    # Check for Azure server URL
    if grep -q "tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net" "$INDEX_FILE"; then
        echo "✅ Azure server URL whitelisted in CSP"
    else
        echo "❌ Azure server URL NOT found in CSP"
        echo "   The CSP may not allow connections to your Azure server"
    fi

    # Check for localhost dev server
    if grep -q "127.0.0.1:5503" "$INDEX_FILE"; then
        echo "✅ Local dev server URL whitelisted in CSP"
    else
        echo "⚠️  Local dev server URL NOT found in CSP"
    fi

    echo ""
    echo "=== CSP Details ==="
    grep -o "Content-Security-Policy[^>]*" "$INDEX_FILE" | head -1
    echo ""

else
    echo "❌ CSP meta tag NOT found in index.html"
    echo "   The build may not use the custom template"
    echo ""
    echo "To fix:"
    echo "1. Open Unity Editor"
    echo "2. Go to: Edit → Project Settings → Player → WebGL"
    echo "3. Select 'CustomTemplate' under WebGL Template"
    echo "4. Rebuild the WebGL project"
    exit 1
fi

echo ""
echo "=== Template Verification ==="
# Check ProjectSettings
if grep -q "webGLTemplate: PROJECT:CustomTemplate" "ProjectSettings/ProjectSettings.asset"; then
    echo "✅ Custom template is selected in ProjectSettings"
else
    echo "⚠️  Custom template may not be selected in ProjectSettings"
    echo "   Current setting:"
    grep "webGLTemplate:" "ProjectSettings/ProjectSettings.asset"
fi

echo ""
echo "=== Build Info ==="
if [ -f "$BUILD_DIR/Build/WebGL Builds.framework.js.unityweb" ]; then
    BUILD_SIZE=$(du -sh "$BUILD_DIR/Build" | cut -f1)
    echo "Build size: $BUILD_SIZE"
fi

if [ -f "$BUILD_DIR/dependencies.txt" ]; then
    echo "Dependencies file found"
fi

echo ""
echo "=== Next Steps ==="
echo "1. Test locally: cd '$BUILD_DIR' && python3 -m http.server 8000"
echo "2. Open: http://localhost:8000"
echo "3. Check browser console (F12) for CSP or WebSocket errors"
echo "4. Try connecting to multiplayer to verify Azure connection works"
echo ""
