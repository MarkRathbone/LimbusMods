# Limbus Zoom

Mouse-wheel zoom for the story stage-selection map and dungeon map scroll views. A separate BepInEx IL2CPP plugin; Limbus Ultrawide is optional.

## Controls

- Wheel down over a map to zoom out; wheel up to return toward normal size.
- Drag the map to pan using the existing game controls.
- Press Home while the map is open to restore its normal scale.

The wheel replaces the map's slow vertical scrolling. Other menus retain their normal scrolling. Zoom resets when the map scroll view is disabled or a story node is selected; the game then focuses that node at its native scale. Zoom-out and panning are constrained by the background artwork and viewport dimensions. Node selection and stage entry continue through the game's own UI.

## Install

Extract the release ZIP into the Limbus Company folder, or run `./Install.ps1 -GameDir "path/to/Limbus Company"` while the game is closed. Requires a compatible BepInEx 6 IL2CPP installation. Use the mod launcher if your setup disables mods for ordinary Steam launches.

The plugin is installed at `BepInEx/plugins/LimbusZoom/LimbusZoom.dll`. To remove it, close the game and remove that folder.

## Configuration

Launch once to create `BepInEx/config/local.limbus.zoom.cfg`.

| Section | Setting | Default | Purpose |
|---|---|---|---|
| General | Enabled | true | Enable map zoom |
| Maps | Story | true | Story stage-selection map |
| Maps | Dungeons | true | Story and Mirror Dungeon map scroll views |
| Zoom | MinimumScale | 0.25 | Requested 25% floor; artwork coverage can require a higher scale |
| Zoom | MaximumScale | 1 | Wheel up returns to normal scale |
| Zoom | WheelStep | 0.12 | Wheel zoom speed |

Support targets UI map scroll views. Free-roaming 3D RPG cameras and combat cameras are outside this version's scope. Dungeon layouts require in-game verification; a successful build alone does not confirm every dungeon variant.

## Build

From the repository root: `./LimbusBuild/scripts/Build-Release.ps1 -Repository ./LimbusZoom`. Uses the shared build, test and packaging process. Release tag: `LimbusZoom/v0.1.1`.
