These tiny WebP fixtures are generated for this project; no third-party images are used.

- `colors.webp`: lossless 2 × 2, red/green then blue/white.
- `alpha.webp`: lossless 3 × 1, fully transparent / 50% red / opaque RGB(11,22,33).
- `lossy.webp`: lossy 8 × 4, solid red at quality 80 (small encoding color differences are expected).

Tests exercise the embedded decoder without relying on a Windows WebP codec installation.
