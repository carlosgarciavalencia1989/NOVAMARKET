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
        Buildings/  Vehicles/  Equipment/  Characters/  Environment/
    Materials/         Project materials (URP)
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

Current `CEDI_Medellin` hierarchy (base only):

```
Main Camera
Directional Light
CEDI_Medellin                      root of all scenario content
    Environment
        TemporaryGround            Plane scaled 10x → 100 m x 100 m, for scale checks only
```

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
- Selection system and any UI
- CEDI Medellín environment: terrain, mountains, roads, buildings, docks, vehicles
- ERP, Excel integration, database
- AI, Mentor
- S&OP, advanced simulation
- Missions and events
