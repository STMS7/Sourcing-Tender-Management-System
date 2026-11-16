# Login success film

## Assets and authorship

- Runtime movie: `wwwroot/login/cinema/olive-to-oil-v2.mp4`, silent H.264,
  960 × 640, 24 fps, 72 frames / 3 seconds, 184,372 bytes.
- Still: `wwwroot/login/cinema/olive-still-v2.webp`, 960 x 640, about 10 KB;
  the film's first frame, shown in the Login scene panel and used as the
  film's poster so the handoff from still to film is seamless.
- Reproducible scene and renderer: `tools/render_login_cinema.py` at repository root.

Both shots are authored procedurally in Blender: textured olive skin and leaves,
soft studio lighting, beveled metal, and transmissive oil with absorption and
IOR 1.47. The pendant drop uses keyframed mesh shapes followed by a gravity-based
fall trajectory. This is rendered CGI, not real camera footage or a full fluid
simulation. No external stock footage or generative cloud service is used.

Rendered locally with official Blender 4.5.15 LTS, Cycles / OptiX, 64 samples,
denoising and AgX. Portable Windows download:
https://download.blender.org/release/Blender4.5/blender-4.5.15-windows-x64.zip

Verified archive SHA-256:
`52dd02fd74877fa08e74b67d7854cf32468a232c02aaa19b6844c91285574f8d`

## Re-render

```powershell
& 'C:/path/to/blender.exe' --background --factory-startup --python-exit-code 1 --python 'tools/render_login_cinema.py' -- --output 'C:/path/to/render' --samples 64
```

`--preview` renders three stills. `--resume` retains existing numbered frames.
The output contains the authored `.blend`, frame PNGs and encoded MP4. Copy the
encoded movie to the runtime asset path above. Convert frame 0001 to the WebP
poster at 768 × 512. Blender is an authoring tool only: the deployed application
serves the resulting movie and needs no Blender installation.

## Runtime contract

`auth-login.js` starts the film only after existing authentication reports success.
`login-celebration.js` guards Login, respects motion preferences, handles playback
errors and cleans up on completion. A 3.3-second media guard and 3.5-second overall
transition guard prevent decoration from trapping navigation. Resizing or pausing
motion ends the decoration. The original redirect destination and authentication
flow are preserved; Home, database schema, roles and controllers are unchanged.

The still and MP4 total approximately 195 KiB. The movie is preloaded while the
user fills Login. No success effect is loaded or replayed by the dashboard.
