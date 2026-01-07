# Custom WebGL Template with Azure CSP Support

This custom WebGL template fixes Unity 6's Content Security Policy (CSP) issue that blocks WebSocket connections to external servers.

## The Problem

Unity 6 WebGL builds inject a strict CSP that prevents WebSocket connections to domains outside Unity's whitelist. Your Azure game server `wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net` is blocked by default.

## The Solution

This template provides **server-side CSP headers** that override Unity's default CSP. Unity 6 overrides CSP meta tags in HTML, so server headers are required.

## Files Included

- **web.config** - For IIS/Azure App Service (Windows servers)
- **.htaccess** - For Apache servers
- **_headers** - For Netlify, Cloudflare Pages, and similar static hosts
- **index.html** - Template HTML (CSP set via headers, not meta tag)

## How It Works

When you build for WebGL in Unity, these configuration files are included in the build output. When deployed to a web server, they set HTTP headers that allow connections to:

✅ **Production Server**: `wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net`
✅ **Dev Server**: `ws://127.0.0.1:5503`
✅ **HTTPS Connections**: `https://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net`
✅ **All Unity's Default Domains**: (Analytics, CDN, etc.)

## Usage

### For Azure App Service (Your Production Environment)

1. Build WebGL in Unity with this template
2. Deploy to Azure App Service
3. **web.config** will automatically be used
4. WebSocket connections will work without CSP errors

### For Local Testing

Use the included Python test server that sets proper CSP headers:

```bash
# From project root
python3 serve-webgl-with-csp.py
```

Then open: http://localhost:8000

### For Other Hosting Platforms

- **Apache**: The `.htaccess` file will be used automatically
- **Netlify/Cloudflare Pages**: The `_headers` file will be used
- **Nginx**: Add CSP to your nginx configuration (see below)

## Nginx Configuration

If deploying to Nginx, add this to your server block:

```nginx
location / {
    add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-eval' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self' blob: https://cdn.play.unity.com https://cdn.staging.play.unity.com https://*.struckd.com https://*.cloudfront.net https://*.gstatic.com https://*.sentry-cdn.com https://*.googletagmanager.com https://*.sentry.io https://cdp.cloud.unity3d.com https://config.uca.cloud.unity3d.com https://collect.analytics.unity3d.com https://player-auth.services.api.unity.com https://*.googleapis.com wss://*.exitgames.com:* https://*.cesium.com https://*.virtualearth.net wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net ws://127.0.0.1:5503 https://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net; img-src 'self' data: blob:; font-src 'self'; worker-src 'self' blob:;";
}
```

## Updating Server URLs

If your Azure server URL changes, update all three config files:
- `web.config` (line ~6)
- `.htaccess` (line ~5)
- `_headers` (line ~4)

Search for `tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net` and replace with your new domain.

## Verifying CSP in Browser

1. Open your WebGL build in browser
2. Press F12 (DevTools)
3. Go to **Network** tab
4. Reload the page
5. Click on the **index.html** request
6. Check **Headers** → **Response Headers** → Look for `content-security-policy`
7. Verify it includes your Azure domain

## Troubleshooting

**CSP still blocking connections:**
- Verify the config file for your server type is included in build
- Check browser DevTools → Network → Headers to see actual CSP
- Ensure your web server is configured to read the config files

**Config file not working:**
- **IIS/Azure**: Ensure `web.config` is in the root of your deployed files
- **Apache**: Ensure `.htaccess` is enabled (`AllowOverride All`)
- **Static Hosts**: Check platform documentation for header file support

---

**Created**: 2025-12-24
**Unity Version**: 6000.2.7f2
**Purpose**: Override Unity 6 CSP to allow Azure WebSocket connections
