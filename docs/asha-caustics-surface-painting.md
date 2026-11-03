# Asha Caustics Surface Painting — implementation contract

Status: design locked; implementation pending. This document does not enable the feature.

## Non-negotiable compatibility
- Preserve approved game PLS/bloom, water behaviour, HUD and existing animated caustics pattern.
- Keep existing Caustics Effect bulb path as a fallback until surface painting is compiled, exported, tested in-game, and approved.
- Do not repurpose existing TR4 light direction/range fields for painted face metadata.
- Preserve compatibility with old project files and levels.

## Editor interaction
- Dedicated Caustics mode beside Texturing and Lighting; enter in untextured 3D view, preserving previous viewport mode.
- Paint/erase individual sector faces with the existing face-picking conventions.
- Purple overlay shows stored painted coverage only in Caustics mode; Textured and Lighting modes show a nonanimated preview over actual textures.
- Controls: Brightness integer 1–5 (default 3), Edge Fade integer 1–5 (default 3), Inherit bool (default off until object support is validated), Colour RGB (default neutral white).
- Support Undo/Redo, room copy/paste, project save/load and face topology changes; avoid silently losing paint data.

## Edge and intensity semantics
- Face selection is a coverage mask, not a light or texture replacement.
- Connected coplanar or smoothly joined painted faces share boundaries; never fade each face independently.
- Edge Fade 1 is hard coverage; Fade 5 is widest soft transition. Fade width must be specified in world units, not in number of tiles, and must be bounded to avoid swallowing narrow regions.
- Different brightness/colour regions blend continuously; no additive blowout. Sharp edge permitted when Fade 1 is chosen.
- A surface with caustics disabled is a true coverage boundary.
- Define joins for walls/floors, diagonals, triangles, sloped faces and portals; do not accidentally bridge unrelated nearby surfaces.
- Store a stable face identifier plus per-face settings; triangulation/texture changes must not silently change coverage.
- Compute a single world-space coverage field consumed by both editor preview and game renderer; preserve existing animated pattern phase.

## Inheritance
- Inherit is enabled on source faces, not on every target object.
- Projection follows source face orientation: floor downward, wall along outward normal; ceiling by its normal.
- The receiver must intersect the projected painted footprint and range. Clip by coverage and edge fade, not just distance to source face.
- For floor source, top-facing/exposed object surfaces receive the effect; wall source projects laterally. Avoid illuminating the back side of objects.
- Multiple painted sources blend with a bounded contribution; no double brightness.
- Start with static meshes, then investigate movable items separately. Inherit must not affect unrelated objects.
- Projection distance needs an internal, tested limit; if a fixed automatic distance proves inadequate, expose an advanced override later rather than introducing another default control.

## Export and test gates
1. Verify actual editor architecture: face-picking, tool mode, room face persistence and file format extension points.
2. Add editable/persisted per-face settings with round-trip and Undo/Redo tests.
3. Add purple overlay and static preview; test across rooms, sloped faces and mixed texture modes.
4. Add versioned export metadata with explicit face IDs, geometry and settings; test old levels and round trips.
5. Game renderer consumes the painted mask and performs world-space boundary fading without modifying the approved caustics animation.
6. Add static mesh projection with clipping and normal handling.
7. Only after compiled editor and game CI builds plus visual approval, remove/deprecate bulb-based placement.

## Regression test scenes
- Adjacent equal-setting tiles: no seam.
- Brightness 4/Fade 1 centre and Brightness 2/Fade 4 outer ring: continuous transition.
- Bottom half of wall painted: upper half unlit; statue inherits only within matching projected height.
- Painted floor beneath statue: top-down projection, not entire statue glow.
- Wall and floor both painted: no doubled intensity.
- Diagonal and sloped faces, room portals, waterline, shadows, colour changes, and project save/reload.
