# Mossvale – Complete Farm Environment (Blender → Unity 6 URP)

A single cohesive 50 × 50 m farm level, built as real, editable low-poly geometry (no textures, flat-colour materials).
Scale: **metres**, ground surface at **Y = 0** (Blender Z = 0). ~58 k polygons / ~89 k triangles for the entire level.

## What's in this folder
| Path | What it is |
|---|---|
| `Mossvale_Environment.blend` | Master Blender project (Blender 5.x). Everything placed, cameras + lights included. |
| `FBX/Mossvale_Level.fbx` | The complete level, 377 meshes, one FBX (instances share meshes). Lights/cameras not included. |
| `FBX/Assets/<Category>/*.fbx` | 86 reusable assets, each at the origin with the pivot at bottom-centre: `Buildings`, `Crop_Beds`, `Nature` (trees, bushes, flowers, grass, mushrooms, rocks), `Fences_and_Gates`, `Props`, `Water`. |
| `Previews/` | `Mossvale_Preview_Isometric.png`, `Mossvale_Preview_TopDown.png`, `Mossvale_Preview_Hero_Cottage.png` |
| `Materials_and_Textures/` | No external textures. `Mossvale_Palette.csv` lists every material colour. |
| `Source/` | The Python scripts that generate the whole scene (see "Regenerating"). |

## Blender Outliner
`Environment_Ground` · `Buildings` · `Farm_Plants` · `Trees_and_Nature` · `Fences_and_Paths` · `Props` · `Water` · `Lighting`
plus `Asset_Library` (hidden): the master prototype of every reusable asset. Everything in the level is a linked duplicate of one of these (shared mesh data), so editing a prototype's mesh updates every instance.
Cameras `Cam_Isometric`, `Cam_TopDown`, `Cam_Hero_Cottage` live in `Lighting`.

## Layout (Blender X = east, Y = north; the entrance is at the south edge)
- **Entrance arch** (3, −24) → winding dirt road north, past the **market stall** (−3.5, −19) at the entrance.
- **Pond** + dock + reeds/lilies/rocks on the west (−14.5, −10.5); pond path branches off the road.
- **Crop garden** east of the road: fenced 20 × 16 m compound, 6 raised beds (pumpkin, cabbage, carrot/lettuce, tomato, wheat, sunflower), scarecrow, **well**, **greenhouse**, west + north gates.
- **Cottage** (−5, 9.8): the focal point, with a picket-fenced flower garden, porch, flower boxes, chimney, stone path to the gate.
- **Barn** (14, 14.6) with silo, storage yard (hay bales, crates, barrels, sacks, cart); **windmill** (4.5, 19.5) and water tower in the background.
- Forest ring of 7 tree types, bushes, flowers, grass tufts, mushrooms and rocks around the edge; free space is left for walking/interaction.

## Unity 6 (URP) import notes
- Axis mapping: **Blender (x, y, z) → Unity (x, z, y)** – the level appears in Unity oriented like the previews (entrance at −Z, north = +Z). Rotations are baked: objects arrive at rotation 0 / scale 1 (tree/bush/grass instances keep a small uniform scale variation).
- Model import: Scale Factor 1, "Convert Units" on. Materials arrive as `MV_*`; if they come in as Built-in, remap/convert them to **URP/Lit** (Window ▸ Rendering ▸ Render Pipeline Converter).
- `MV_Glass` (greenhouse): set Surface Type = **Transparent**. `MV_LanternGlow`: has warm emission; add real Point Lights in Unity (lights aren't exported).
- `Windmill.fbx` / the level contain `Windmill_Tower` and `Windmill_Blades` as separate children – the sails' pivot is the hub, so to animate them rotate `Windmill_Blades` around its local **Z** axis in Unity (local Y in Blender).
- Colliders are not generated – add Mesh/Box colliders as needed (the ground is one flat plane at Y = 0 with a 0.9 m soil "skirt" below it for the diorama edge; delete it if not wanted).
- Not yet opened in Unity itself – the FBX files were verified by re-importing into Blender (extents ≈ 50 × 50 m + tree crowns, ground at 0).

## Regenerating / editing procedurally
`Source/` needs Blender's Python module (`pip install bpy`) or run inside Blender:
`python build_all.py <out_dir>` rebuilds the .blend · `python export_all.py <blend> <out_dir>` re-exports all FBX · `python render_view.py <blend> <camera> <png> <w> <h> <samples>` renders a camera.
Layout coordinates are all in `mv_scene.py` (`Level.layout`), asset geometry in `mv_buildings.py`, `mv_nature.py`, `mv_props.py`.
