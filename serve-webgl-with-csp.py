#!/usr/bin/env python3
"""
Local WebGL test server with proper CSP headers for Azure WebSocket connection
"""

import http.server
import socketserver
import os

PORT = 8000

class CSPHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    """HTTP handler that adds CSP header allowing Azure WebSocket connections"""

    def end_headers(self):
        # Add CSP header that includes Unity's default domains + Azure server
        csp_policy = (
            "default-src 'self'; "
            "script-src 'self' 'unsafe-eval' 'unsafe-inline'; "
            "style-src 'self' 'unsafe-inline'; "
            "connect-src 'self' blob: "
            "wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net "
            "https://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net "
            "ws://127.0.0.1:5503 "
            "https://cdn.play.unity.com "
            "https://cdn.staging.play.unity.com "
            "https://*.struckd.com "
            "https://*.cloudfront.net "
            "https://*.gstatic.com "
            "https://*.sentry-cdn.com "
            "https://*.googletagmanager.com "
            "https://*.sentry.io "
            "https://cdp.cloud.unity3d.com "
            "https://config.uca.cloud.unity3d.com "
            "https://collect.analytics.unity3d.com "
            "https://player-auth.services.api.unity.com "
            "https://*.googleapis.com "
            "wss://*.exitgames.com:* "
            "https://*.cesium.com "
            "https://*.virtualearth.net; "
            "img-src 'self' data: blob:; "
            "font-src 'self'; "
            "worker-src 'self' blob:;"
        )

        self.send_header('Content-Security-Policy', csp_policy)

        # Set proper MIME types for Unity files
        if self.path.endswith('.unityweb'):
            self.send_header('Content-Type', 'application/octet-stream')
            self.send_header('Content-Encoding', 'gzip')
        elif self.path.endswith('.wasm'):
            self.send_header('Content-Type', 'application/wasm')
        elif self.path.endswith('.data'):
            self.send_header('Content-Type', 'application/octet-stream')

        http.server.SimpleHTTPRequestHandler.end_headers(self)

def main():
    # Change to WebGL Builds directory
    webgl_dir = "WebGL Builds"

    if not os.path.exists(webgl_dir):
        print(f"❌ Error: '{webgl_dir}' directory not found")
        print(f"   Please build the WebGL project in Unity first")
        return 1

    os.chdir(webgl_dir)

    with socketserver.TCPServer(("", PORT), CSPHTTPRequestHandler) as httpd:
        print("=" * 70)
        print(f"🚀 WebGL Test Server with CSP Running")
        print("=" * 70)
        print(f"")
        print(f"📡 Server: http://localhost:{PORT}")
        print(f"📁 Serving: {os.getcwd()}")
        print(f"")
        print(f"✅ CSP Header: Includes Azure WebSocket domain")
        print(f"   wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net")
        print(f"")
        print(f"🔍 To verify CSP in browser:")
        print(f"   1. Open: http://localhost:{PORT}")
        print(f"   2. Press F12 (DevTools)")
        print(f"   3. Go to Network tab")
        print(f"   4. Reload page")
        print(f"   5. Click on 'index.html' request")
        print(f"   6. Check Headers → Response Headers → content-security-policy")
        print(f"")
        print(f"Press Ctrl+C to stop the server")
        print("=" * 70)

        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n\n✅ Server stopped")
            return 0

if __name__ == "__main__":
    exit(main())
