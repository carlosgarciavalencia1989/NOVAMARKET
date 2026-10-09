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
        Simulation/    Future simulation systems (empty)
        UI/            Future UI scripts (empty)
    Prefabs/
        Buildings/  Vehicles/  Equipment/  Characters/  Environment/ (Tree_Blockout)
    Materials/         Project materials (URP); Blockout/ holds greybox M_* materials
    Models/            Imported 3D models
    UI/                UI assets (sprites, UXML/USS, fonts)
    Data/              ScriptableObject asset instances
    Documentation/     Project documentation (this file)
```

Code uses the `NovaMarket.*` namespaces matching the `Scripts/` subfolders.

## Scenes

| Scene | Role |
|---|---|
| `Scenes/CEDI_Medellin.unity` | **Main scene** of the first educational scenario |
| `Scenes/Test_NOVAMARKET.unity` | Connection/tooling test scene; not part of the game |

`Assets/Scenes/SampleScene.unity` is the original template scene and is kept untouched.

`CEDI_Medellin` is the first scene in the build settings (index 0).

Current `CEDI_Medellin` hierarchy (greybox blockout, primitives + `Materials/Blockout/`):

```
Main Camera                        elevated overview, (-48, 52, -104)
Directional Light
CEDI_Medellin                      root of all scenario content
    Environment
        Terrain/ValleyFloor        valley floor, scale 300 x 300
        Mountains                  north/south ridges and foothills + west/east closures
        MainRoad                   Road_Main (2900 m x 12 m at z = -60), lane lines, AccessRoad_ToGate
        DistantBuildings           Industrial_01-07, Tower_01-05, Block_01-04
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
- Final art for CEDI Medellín (current environment is a greybox blockout)
- Vehicles, characters and equipment prefabs
- ERP, Excel integration, database
- AI, Mentor
- S&OP, advanced simulation
- Missions and events
