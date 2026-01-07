# WebGL Build Instructions - Azure CSP Fix

## The Problem You Had

Unity 6 WebGL builds inject their own Content Security Policy (CSP) that blocks WebSocket connections to domains outside Unity's whitelist. Your error was:

```
Connecting to 'wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net' violates
the following Content Security Policy directive...
```

## The Solution

**Use server-side HTTP headers** to override Unity's CSP. Unity 6 overrides HTML meta tags, so server headers are the only reliable method.

---

## Quick Start Guide

### Step 1: Build in Unity

1. Open Unity Hub → Open project
2. **Verify Template**: Edit → Project Settings → Player → WebGL
   - Under "WebGL Template" select: **CustomTemplate**
3. **Build**: File → Build Settings → WebGL → Build
4. Save to: `WebGL Builds` folder

### Step 2: Test Locally with CSP

**Don't use simple HTTP server** - it won't set CSP headers!

Instead, use the included Python server:

```bash
python3 serve-webgl-with-csp.py
```

Then open: http://localhost:8000

This server automatically sets the correct CSP headers.

### Step 3: Verify CSP in Browser

1. Open http://localhost:8000
2. Press **F12** (DevTools)
3. Go to **Network** tab
4. Reload page
5. Click **index.html** request
6. Check **Headers** → **Response Headers** → `content-security-policy`
7. **Verify it includes**: `wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net`

### Step 4: Test WebSocket Connection

1. In the game, try connecting to multiplayer
2. Watch browser console (should be open from Step 3)
3. Look for WebSocket connection messages
4. **Success**: No CSP errors!
5. **Still blocked**: Check that CSP header includes your Azure domain

### Step 5: Deploy to Azure

1. Upload **all files** from `WebGL Builds` folder to Azure App Service
2. **Important**: Ensure `web.config` is included in the root
3. Azure App Service (IIS) will automatically use `web.config`
4. Test the deployed version
5. Verify CSP using browser DevTools (same as Step 3)

---

## What Files Were Created

Your custom template at `Assets/WebGLTemplates/CustomTemplate/` includes:

### Server Configuration Files

1. **web.config** - For IIS/Azure App Service
   - Sets CSP via HTTP headers
   - Includes your Azure WebSocket domain
   - Unity will include this in the build

2. **.htaccess** - For Apache servers
   - Alternative if using Apache instead of IIS

3. **_headers** - For Netlify/Cloudflare Pages
   - For static hosting platforms

### Template Files

4. **index.html** - Modified template
   - Removed ineffective CSP meta tag
   - Added comments explaining server-header approach

5. **README.md** - Documentation for the template

### Helper Scripts (Project Root)

6. **serve-webgl-with-csp.py** - Local test server with CSP
7. **verify-webgl-build.sh** - Build verification script

---

## How The Fix Works

### Before (Broken):
```
Browser loads Unity WebGL
↓
Unity injects CSP (blocks Azure)
↓
Your WebSocket connection: ❌ BLOCKED
```

### After (Fixed):
```
Browser requests index.html
↓
Server sends HTTP header: Content-Security-Policy (includes Azure)
↓
Server's CSP overrides Unity's CSP
↓
Your WebSocket connection: ✅ ALLOWED
```

**Key Insight**: HTTP headers take precedence over HTML meta tags and JavaScript-injected CSP.

---

## Testing Checklist

Before deploying to Azure, verify locally:

- [ ] Built WebGL with CustomTemplate selected
- [ ] Started test server: `python3 serve-webgl-with-csp.py`
- [ ] Opened http://localhost:8000
- [ ] Checked DevTools Network headers
- [ ] Confirmed CSP includes: `wss://tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net`
- [ ] Tested multiplayer connection
- [ ] No CSP errors in console

For Azure deployment:

- [ ] Uploaded all files from WebGL Builds
- [ ] Confirmed `web.config` is in root directory
- [ ] Tested on actual Azure URL
- [ ] Checked CSP headers in production
- [ ] Tested multiplayer connection in production

---

## Troubleshooting

### Problem: CSP Still Blocking

**Solution 1**: Verify server config file is present
```bash
ls "WebGL Builds/web.config"  # Should exist
```

**Solution 2**: Check actual CSP in browser
- DevTools → Network → index.html → Headers → Response Headers
- Look for `content-security-policy`
- It should include your Azure domain

**Solution 3**: Azure App Service may need restart
- After deploying web.config, restart the App Service

### Problem: Local test shows CSP error

**Cause**: Using wrong test server

**Solution**: Don't use:
- ❌ `python -m http.server` (doesn't set headers)
- ❌ `npx http-server` (doesn't set headers)

Use instead:
- ✅ `python3 serve-webgl-with-csp.py` (sets correct headers)

### Problem: web.config not working on Azure

**Check 1**: Verify IIS is allowed to read web.config
- Azure App Service uses IIS by default
- web.config should work automatically

**Check 2**: Verify web.config is in correct location
- Must be in the root folder with index.html
- Not in a subfolder

**Check 3**: Check Azure App Service logs
- Azure Portal → Your App Service → Log Stream
- Look for configuration errors

---

## Updating Server URLs

If your Azure server URL changes in the future:

1. Edit these files in `Assets/WebGLTemplates/CustomTemplate/`:
   - `web.config` (line ~6)
   - `.htaccess` (line ~5)
   - `_headers` (line ~4)

2. Search for: `tbp-server-fgh4bebufufactca.centralus-01.azurewebsites.net`

3. Replace with your new Azure domain

4. Rebuild WebGL in Unity

5. Redeploy

---

## For Other Hosting Platforms

### Netlify / Cloudflare Pages
- The `_headers` file will be used automatically
- No additional configuration needed

### Nginx
- Add CSP to your nginx config (see template README.md)
- Rebuild not required, just nginx config change

### Apache
- The `.htaccess` file will be used automatically
- Ensure `AllowOverride All` is enabled

---

**Unity Version**: 6000.2.7f2
**Created**: 2025-12-24
**Problem**: Unity 6 CSP blocks external WebSocket connections
**Solution**: Server-side CSP headers override Unity's default CSP
