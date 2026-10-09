# NOVAMARKET — Project Architecture

## Purpose

NOVAMARKET is an educational logistics simulator built in Unity. Students explore a
distribution center (CEDI) and its surroundings, inspect facilities, vehicles and products,
and complete logistics scenarios. The first scenario is **CEDI Medellín**.

- Unity: 6000.3.25f1 (Unity 6.3)
- Render pipeline: URP 17.3.0

## Scale rule

**1 Unity unit = 1 meter.** This applies to the whole project: models, prefabs, terrain,
distances, speeds (m/s) and physics. Import models at real-world size; do not compensate
with transform scale. The constants live in `NovaMarket.Core.WorldScale`.

## Folder structure

```
Assets/NOVAMARKET/
    Scenes/            Project scenes
    Scripts/
        Core/          Project-wide conventions and shared utilities (WorldScale)
        Data/          ScriptableObject data definitions (NovaDataAsset and subclasses)
        Interaction/   Selectable / interactable objects (IInteractable, InteractableObject)
        CameraControl/ Strategy camera (StrategyCameraRig logic + StrategyCameraController)
        Simulation/    Future simulation systems (empty)
        UI/            Future UI scripts (empty)
        Editor/        Editor-only tools (CediEnvironmentBuilder + its .Infrastructure/.Urban parts, EnvironmentMeshes)
    Tests/
        EditMode/      NUnit tests run from the Test Runner (StrategyCameraRigTests)
    Prefabs/
        Buildings/  Vehicles/  Equipment/  Characters/
        Environment/   Tree_Blockout + generated terrain tree prefabs (Tree_Broadleaf_*, Tree_Guayacan_* ...)
    Materials/         Project materials (URP)
        Blockout/      Greybox M_* materials of the original blockout
        Environment/   Generated: houses, buildings, warehouses, river/metro, tree canopies, skybox
            Textures/  Generated facade, tower window, cladding and metal roof textures
    Models/            Imported 3D models
        Generated/     Generated meshes (Trees/, Barrios/, Infrastructure/, Urban/)
    Terrain/           Generated: TerrainData, terrain layers and their textures
    Rendering/         Volume profiles (VP_CEDI_Medellin)
    UI/                UI assets (sprites, UXML/USS, fonts)
    Data/              ScriptableObject asset instances
    Documentation/     Project documentation (this file)
```

Code uses the `NovaMarket.*` namespaces matching the `Scripts/` subfolders.

Assemblies: `NovaMarket.Runtime` (everything under `Scripts/`, references `Unity.InputSystem`),
`NovaMarket.Editor` (`Scripts/Editor/`, Editor only, references URP) and
`NovaMarket.Tests.EditMode` (`Tests/EditMode/`). Logic that can be tested lives in plain C#
classes; MonoBehaviours only connect it to the scene, input and UI.

## Scenes

| Scene | Role |
|---|---|
| `Scenes/CEDI_Medellin.unity` | **Main scene** of the first educational scenario |
| `Scenes/Test_NOVAMARKET.unity` | Connection/tooling test scene; not part of the game |

`Assets/Scenes/SampleScene.unity` is the original template scene and is kept untouched.

`CEDI_Medellin` is the first scene in the build settings (index 0).

Current `CEDI_Medellin` hierarchy (CEDI greybox + procedural environment):

```
Main Camera                        StrategyCameraController; starts at 50° looking at the CEDI
Directional Light                  late-afternoon sun from the west-southwest (25° high)
Global Volume                      VP_CEDI_Medellin: ACES, soft bloom, warm grading
CEDI_Medellin                      root of all scenario content
    Environment
        Terrain/Terrain_Aburra     4.4 km Unity Terrain; valley floor at y = 0, ~8,200 terrain trees
        Barrios                    Barrio_01-16: hillside brick houses, one combined mesh each
        MainRoad                   Road_Main (2900 m x 12 m at z = -60), lane lines, AccessRoad_ToGate
        Infrastructure             River_Canal (sunk concrete channel at z = -116, bridges), Metro_Line
                                   (viaduct at z = -84, 2 stations, 2 trains)
        IndustrialZone             Industrial_Ground/Buildings_<chunk>: warehouses, yards, trucks
        City                       City_Ground/Buildings_<chunk>: blocks, downtown towers, parks
        Vegetation                 36 Tree_Blockout prefab instances (Tree_01 ...)
    CEDI
        Building                   FloorSlab 44 m x 26 m, Walls, MainFacade, Roof (cutaway material, y = 8.85)
        Receiving                  floor, StagingLane_01-03, Docks
        Storage                    floor, RackRow_01-04 (16 m x 6 m x 2.2 m)
        Picking                    floor, PickStation_01-03
        Shipping                   floor, StagingLane_04-06, Docks
        Yard                       YardPavement 120 m x 55 m
        Gate                       GatePad, inbound/outbound lanes, GateIsland, stop lines
        ContainerYard              YardLink, ContainerPad, Container_01-06
        Parking                    ParkingPad 36 m x 18 m, StallLines
    Debug
        ScaleMarkers               1 m cube, 5 m and 10 m poles
```

Flow inside the building: Receiving → Storage → Picking → Shipping. The blockout has no
scripts or interactables attached yet.

## Environment builder

`Terrain_Aburra`, `Barrios`, `Infrastructure`, `IndustrialZone`, `City`, the terrain trees, the sun,
fog, skybox and `Global Volume` are generated by `NovaMarket.Editor.CediEnvironmentBuilder` (menu
**NOVAMARKET → Environment → Rebuild CEDI Medellin Environment**). It uses a fixed seed, so a rebuild gives the same result;
tune it by editing its constants (heights, colours, densities) and rebuilding, not by hand.
It never touches the CEDI lot, `MainRoad`, `Vegetation` or `Debug` (the old `DistantBuildings`
blockout boxes are removed: the urban builder replaces them).

- Relief: the valley floor (about ±850 m east/west, ±480 m north/south) stays flat at y = 0;
  walls rise to ~640 m east/west and ~360 m north/south, with ridged noise for crests and
  quebradas, and a flat pass where the transport corridor (highway, metro, river) leaves the valley.
- Terrain layers: light grass (valley), dark grass and forest (slopes, forest in ravines),
  dirt (lot edges, road shoulders, footpaths, barrios, very steep scars).
- Trees are terrain trees (stored in the TerrainData, not as scene objects), including
  yellow and pink guayacanes. The Unity warning "must use the Nature/Soft Occlusion shader"
  only concerns billboards, which these mesh trees do not use.
- Transport corridor (`.Infrastructure` part), parallel to the highway: the canalised river,
  a trapezoidal concrete channel sunk through terrain holes, and the elevated metro line.
- Valley floor (`.Urban` part) on a street grid of 88 m x 68 m blocks and 12 m streets:
  - industrial ring around the CEDI: warehouses of various sizes with metal gable roofs,
    concrete yards, internal lanes, docks, offices, trucks and containers;
  - city further out: perimeter blocks of 2-6 floor brick and cream buildings, parks,
    avenue trees, and downtown towers (9-28 floors) around (380, 300), at least 280 m from
    the CEDI so they never hide it;
  - the CEDI lot plus a buffer (x -120..140, z -60..80, widened to whole blocks) stays free.
  - Everything is merged per 400 m chunk: a ground mesh (no shadows) and a buildings mesh,
    about 65 objects and 56k triangles in total. Facades use small generated textures.
- Assets are updated in place on rebuild; deleting and recreating them at the same path
  makes Unity drop data set in between.

## Strategy camera

`StrategyCameraController` (on `Main Camera`) is the Play-mode RTS camera, Age of Empires III
style. It defines its Input System actions in code and feeds `StrategyCameraRig`, a plain C#
class with all the math (covered by `StrategyCameraRigTests`).

| Input | Action |
|---|---|
| WASD / arrows, pointer at a screen edge | Pan (faster when zoomed out) |
| Middle mouse drag | Grab and drag the ground |
| Mouse wheel | Zoom, 35–450 m from the focus point |
| Q / E | Rotate 45° left / right, eased |

The focus point is clamped to the flat valley floor (x ±780, z ±420 m) and the camera keeps
15 m above the terrain. All values are in `StrategyCameraSettings`, editable in the Inspector.
In the Editor, edge scrolling only works while the mouse is over the Game view.
Right-click the component → **Apply Start View** to preview the start view in Edit mode.

## Data architecture

All data assets derive from `NovaMarket.Data.NovaDataAsset` (a `ScriptableObject` with
`Id`, `DisplayName`, `Description`). Planned concrete types, each in `Scripts/Data/`
with `[CreateAssetMenu(menuName = NovaDataAsset.MenuRoot + "<Type>")]`:

CompanyData, ScenarioData, WarehouseData, VehicleData, ProductData, LocationData,
MissionData, EventData, RoleData.

Asset instances go in `Assets/NOVAMARKET/Data/`.

## Interaction architecture

- `IInteractable`: contract for selectable objects — `DisplayName`, `Description`,
  `Data` (a `NovaDataAsset`), `CanInteract`, `OnSelected()`, `OnDeselected()`, `Interact()`.
- `InteractableObject`: minimal `MonoBehaviour` implementation. Name/description fall back
  to the assigned data asset, then to the GameObject name. Subclass and override to add behaviour.

Future: a selection system (raycast from camera → `IInteractable`) and a UI panel that
shows name, description and data of the selected object.

## Not implemented yet

- Concrete data types (CompanyData, ScenarioData, ... RoleData)
- Selection system and any UI; no `InteractableObject` placed in scenes yet
- Final art for CEDI Medellín (environment is a procedural blockout; no purchased assets)
- Vehicles, characters and equipment prefabs
- ERP, Excel integration, database
- AI, Mentor
- S&OP, advanced simulation
- Missions and events
