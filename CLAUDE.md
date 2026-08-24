# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

OSMCity is a Unity project that reconstructs 3D city models (buildings, roofs, roads, terrain) from OpenStreetMap data, predicting building heights where they aren't tagged in OSM. State-of-the-art and user-manual docs are in French PDFs at the repo root (`Rapport OpenStreetMap.pdf`, `Etat de l'art OpenStreetMap.pdf`).

Unity version: `6000.4.7f1` (see `ProjectSettings/ProjectVersion.txt`) — open/build/run exclusively through the Unity Editor (Play mode for the scene, Build Settings for a build). There is no CLI build, lint, or test command in this repo; there are no automated tests.

## Repository structure — two separate codebases

This repo bundles two unrelated compilation units:

1. **`Assets/*.cs`** — the actual Unity game code, compiled into `Assembly-CSharp` (per `Assembly-CSharp.csproj`). This is what runs in the Editor/game.
2. **Root-level `.cs` files** (`PBFOsmFile.cs`, `ObjectPicture.cs`, `HeightPredictor.cs`) — standalone offline tooling, **not** referenced by any Unity `.csproj` and not compiled into the game. They depend on NuGet packages declared in `packages.config` (`OsmSharp` for PBF parsing, `Microsoft.ML` for the height-regression model, `MathNet.Numerics`) and are meant to be run as a separate .NET tool/console app to preprocess data before it's consumed by the Unity project. When editing these, don't assume Unity APIs are available, and don't expect Unity to pick up changes.

Do not conflate the two — e.g. a fix to `HeightPredictor.cs` (root) has no effect on in-Editor gameplay, and `Assets/Building.cs`'s picture-based height prediction is a separate, independent mechanism from the ML pipeline in `HeightPredictor.cs`.

## Data flow / architecture

The pipeline, end to end:

1. **Offline (outside Unity, root-level tools)**: `PBFOsmFile.cs` reads a `.osm.pbf` extract (via OsmSharp) and presumably exports it to the XML format the Unity loaders expect (nodes/ways/relations, tags split out). `HeightPredictor.cs` trains an ML.NET `FastTree` regression model on buildings with known `height` tags (features: ground area, perimeter, normalized perimeter index, floor count, net internal surface, neighbor count, length, width, type) and writes predicted heights for the rest to a `id: height` text file.
2. **Data files** live under `Assets/Resources/XmlFiles/` (OSM XML exports) and `Assets/Resources/TxtFiles/` (predicted-heights files, e.g. `liechtenstein_heights.txt`), loaded via `Resources.Load`-relative paths defined in `Main.XmlPath` / `Main.TxtPath`.
3. **Loading (`Loader` → `BuildingLoader` / `HighwayLoader`)**: `Loader` (abstract, `Assets/Loader.cs`) streams the OSM XML files (main elements file, plus separate nodes/ways/relations/tags files) with `XmlReader` into `Node`/`Way`/`Relation` (`Assets/OsmElement.cs`). `BuildingLoader` and `HighwayLoader` subclass it, each pointed at its own set of file names via a `*LoaderFields` component (`BuildingLoaderFields`, `HighwayLoaderFields`) looked up on a `/Loaders` GameObject in the scene.
4. **OSM object graph (`OsmObject` → `OsmBuilding` / `OsmHighway`)**: wraps a raw `OsmElement` and, for ways/relations, recursively resolves its member nodes/ways/sub-relations from the loader's parsed lists (`Assets/OsmObject.cs`).
5. **Scene/game objects (`CityObject` → `Building` / `Highway`)**: `MonoBehaviour`s that turn an `OsmObject` into an actual ProBuilder mesh in the scene — ground polygon extrusion, roof generation (`Roof.cs`, `QuadCreator.cs` — many roof shapes: gabled, hipped, mansard, dome, etc.), materials/colors derived from OSM tags (`building:colour`, `building:material`, `surface`...), and geometric attributes (ground area, perimeter, barycenter, orientation) computed from node positions.
6. **`Main.cs`** orchestrates the whole load: spawns the two loaders, waits for `FinishedLoading` on each, then computes overall terrain bounds and spawns `TerrainGenerator`. It also owns lat/lon ↔ Unity-world-space conversion (`GetTerrainCoords`, `GetEarthCoords`) using per-degree-latitude distance tables, and drives the on-screen coordinate/distance-to-destination HUD.

## Building height resolution order

`Building.Height` (`Assets/Building.cs`) resolves height in this priority: (1) an explicit OSM `height` tag (parsing `"12m"`, feet/inches like `12'6"`, or bare numbers) → (2) `GetHeight(predMethod)`, which branches on `Building.predMethod` (`PredictionMethod.None|Text|Picture`):
   - `Text`: looks up the building's OSM id in `BuildingLoader.Heights`, populated from the `heightsFile` written by the offline ML pipeline.
   - `Picture`: estimates height from a reference photo (`Building.picture`, loaded from `Resources/Pictures/<name-or-id>`) by pixel-based edge/marker detection (`GetHeightFromPicture`), optionally corrected for the player controller's viewing angle.
   - `None` (default): falls back to `height * yMeterScale * NFloors`, i.e. a per-floor height guess.

## Coordinate/scaling conventions

- `Main.xMeterScale` / `yMeterScale` / `zMeterScale` scale real-world meters into Unity units for X/Y/Z independently; almost every geometric getter in `CityObject`/`Building` multiplies by these, so a "meters" value in this codebase is not automatically the same as a Unity unit.
- Lat/lon → world space goes through `Main.GetEarthCoords`/`GetTerrainCoords`, which use a per-integer-latitude-degree distance lookup (`Main.LatDegDists`, interpolated in `GetLatDegDist`) rather than a flat conversion, to account for the earth's ellipsoid shape.

## Multi-polygon buildings

Relation-based (multi-polygon) buildings are handled in `Building.CreateMultiPolygon`, which walks `outer`/`inner` way members. Note: `MultiPolygon.cs`'s `MultiPolygon`/`Ring` classes are dead code (explicitly commented "Currently not used" — the real multi-polygon logic lives inline in `Building.cs`), and the ring-joining loop at the end of `CreateMultiPolygon` is an empty stub — multi-polygon buildings with multiple adjacent outer ways per ring are not fully implemented yet.

## Scenes and per-run configuration

- `Assets/Scenes/MyCity.unity` is the main scene; `Test.unity` is a secondary/test scene.
- Per-loader input filenames and load limits (`nBuildingWays`, `nHighwayNodes`, etc. — negative means "load all") are set on `BuildingLoaderFields`/`HighwayLoaderFields` components on the `/Loaders` GameObject in the scene, not in code.
- `Main.destID` lets you designate a target building/object by OSM id in the Inspector; the HUD then shows live distance to it from the FPS controller.
