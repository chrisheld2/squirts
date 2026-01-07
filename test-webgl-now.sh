#!/bin/bash

echo "=========================================="
echo "WebGL Test with CSP Headers"
echo "=========================================="
echo ""

# Kill any existing Python servers on port 8000
echo "Stopping any existing servers..."
lsof -ti:8000 | xargs kill -9 2>/dev/null
sleep 1

# Start the CSP-enabled test server
echo "Starting WebGL server with CSP headers..."
cd "WebGL Builds"

python3 -c '
import http.server
import socketserver

PORT = 8000

class CSPHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        # CSP with Azure domain
        csp = "default-src '\''self'\''; script-src '\''self'\'' '\''unsafe-eval'\'' '\''unsafe-inline'\''; style-src '\''self'\'' '\''unsafe-inline'\''; connect-src '\''self'\'' blob: https://cdn.play.unity.com https://cdn.staging.play.unity.com https://*.struckd.com https://*.cloudfront.net https://*.gstatic.com https://*.sentry-cdn.com https://*.googletagmanager.com https://*.sentry.io https://cdp.cloud.unity3d.com https://config.uca.cloud.unity3d.com https://collect.analytics.unity3d.com https://player-auth.services.api.unity.com https://*.googleapis.com wss://*.exitgames.com:* https://*.cesium.com https://*.virtualearth.net wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net ws://127.0.0.1:5503 https://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net; img-src '\''self'\'' data: blob:; font-src '\''self'\''; worker-src '\''self'\'' blob:;"

        self.send_header("Content-Security-Policy", csp)
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")

        if self.path.endswith(".wasm"):
            self.send_header("Content-Type", "application/wasm")
        elif self.path.endswith(".data"):
            self.send_header("Content-Type", "application/octet-stream")
        elif self.path.endswith(".js"):
            self.send_header("Content-Type", "application/javascript")

        super().end_headers()

print("=" * 60)
print(f"🚀 WebGL Server Running on http://localhost:{PORT}")
print("=" * 60)
print("")
print("✅ CSP Headers: Enabled")
print("✅ Cache: Disabled")
print("✅ Azure Domain: Whitelisted")
print("")
print("📝 To test:")
print(f"   1. Open: http://localhost:{PORT}")
print("   2. Press Ctrl+Shift+R (hard refresh)")
print("   3. Press F12 to open DevTools")
print("   4. Check Console for errors")
print("")
print("Press Ctrl+C to stop")
print("=" * 60)

with socketserver.TCPServer(("", PORT), CSPHandler) as httpd:
    httpd.serve_forever()
'
