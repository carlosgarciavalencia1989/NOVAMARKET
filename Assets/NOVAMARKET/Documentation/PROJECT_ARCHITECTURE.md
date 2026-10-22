# Supply Chain Run — Project Architecture

## Purpose

**Supply Chain Run** is an educational supply-chain game built in Unity around a distribution center
(CEDI) in Medellín. The current game design is `Documentation/DISENO_JUEGO_V2.md`.

**Naming:** "Supply Chain Run" is the player-facing game name (start menu, Player Settings Product
Name, window title, dialogue fallback). **NOVAMARKET** is the cosmetics company the player works for
("NOVAMARKET Cosmética · CEDI Medellín", with its NOVA ERP) and stays the internal project name: the
`Assets/NOVAMARKET/` folder, the repository, the `NovaMarket.*` namespaces and asmdefs, the
**NOVAMARKET** Editor menu and the `NOVAMARKET.CampaignProgress` PlayerPrefs key keep that name.

It is a game of **roles and sectors**. The player picks a **role** (a link of the Atlas supply
chain map: Planeador, Abastecer, Inventarios, Almacenar, Pedidos, Transporte, Última milla,
Logística inversa) and an **industrial sector** (a campaign: cosmetics, spare parts, food,
pharma, electronics). **This version enables only Planeador + Cosmética**; the others show as
"Próximamente". Roles and sectors are data, so new ones are added without changing the engine.
Roles that are not playable yet are played by the game as NPC characters.

The Planeador's worlds follow the PLANEAR modules of the Atlas (1 Forecasting, 2 Demand
management, 3 Materials MRP · DDMRP); each step of a module's flowchart is a level. The player
computes and the game validates: raw data in the simulated **NOVA ERP** → "Descargar a hoja" →
the **planner sheet** (an in-game spreadsheet with Spanish Excel formulas) → decision → negotiation
→ consequence, shown in the 3D CEDI scene.

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
        Interaction/   Selectable objects and selection (IInteractable, InteractableObject, CediFacility,
                       InteractableLink, ISelectionDetailsProvider, SelectionController, SelectionRing)
        CameraControl/ Strategy camera (StrategyCameraRig logic + StrategyCameraController)
        Simulation/    Plain C# operation state shown by the HUD (OperationState, MissionState, ResourceState,
                       NotificationFeed, FacilityDirectory, SampleOperationData, HudFormat) and the CEDI
                       operation (CediOperationSimulation, TruckAgent, ForkliftAgent, CediLayout,
                       ForkliftNetwork, RouteMover, Polyline, DockManager, GateLane, CediCatalog)
            Planning/  Season planning engine, namespace NovaMarket.Simulation.Planning (PlanningSimulation,
                       PlanningSeason, DemandPlan, AtlasSuggestion, PlanningPolicies, SeasonResult,
                       PlanningLessons, PlanningOperationBridge, PlanningLevelData, catalogue and rules)
            Campaign/  Roles, sectors and characters as plain C# (CampaignCatalog, RoleDefinition,
                       SectorDefinition, CharacterProfile, NpcCharacter with trust)
            Portfolio/ Sector portfolio (SalesHistoryCsv, PortfolioGenerator, SectorProfiles, LeadTimeTable,
                       SeriesStatistics, SalesCalendar)
            Sheet/     Planner sheet engine (Workbook, Worksheet, FormulaLexer/Parser/Shifter, SheetFunctions)
            Erp/       NOVA ERP (NovaErp, ErpScreens, ErpTable, ErpDataSet, DataQualityInjector, DataIssue)
            Levels/    World 1 levels (LevelSession, GatherHistoryLevel 1.1, CleanDataLevel 1.2, FormulaCard,
                       CleaningImpact, LevelDialogue (DialogueBook), LevelResult, CampaignProgress,
                       CampaignHudData, ForecastWorld)
        Operation/     Scene side of the operation (CediOperationRunner, VehicleVisual, GateBarrier, RackStockDisplay)
        UI/            HUD (HudController, HudDataSource, *View classes, HudIcon, minimap) and the planning
                       level UI (PlanningLevelController, PlanningPanelView, AtlasDialogView, EventDialogView,
                       SeasonResultView, ModalLayer, CharacterBadge, HudElements)
            Campaign/  Planner campaign UI, namespace NovaMarket.UI.Campaign (CampaignController, StartMenuView,
                       WorldMapView, WorkspaceBarView, NpcBadge, DialogueBoxView, IssueMarkView,
                       FormulaCardView, AtlasHelpView, LevelResultView)
            Erp/       NOVA ERP window (ErpWindowView, ErpTableView)
            Sheet/     Planner sheet window (SheetWindowView, SheetGridView)
        Editor/        Editor-only tools (CediEnvironmentBuilder + its .Infrastructure/.Urban parts, EnvironmentMeshes,
                       CediHudSetup + its .Hud part)
    Tests/
        EditMode/      NUnit tests run from the Test Runner (camera rig, Simulation/ and Simulation/Planning/,
                       Interaction/, UI/)
    Prefabs/
        Buildings/  Characters/
        Vehicles/      Truck_Receiving, Turbo_Dispatch, Forklift (simple shapes, pivot at ground centre, front +Z)
        Equipment/     Pallet
        Environment/   Tree_Blockout + generated terrain tree prefabs (Tree_Broadleaf_*, Tree_Guayacan_* ...)
    Materials/         Project materials (URP)
        Blockout/      Greybox M_* materials of the original blockout
        Vehicles/      M_Vehicle_* and M_Pallet_* (vehicle and pallet shapes)
        UI/            M_UI_SelectionRing (unlit gold selection ring)
        Environment/   Generated: houses, buildings, warehouses, river/metro, tree canopies, skybox
            Textures/  Generated facade, tower window, cladding and metal roof textures
    Models/            Imported 3D models
        Generated/     Generated meshes (Trees/, Barrios/, Infrastructure/, Urban/)
    Terrain/           Generated: TerrainData, terrain layers and their textures
    Rendering/         Volume profiles (VP_CEDI_Medellin)
    UI/                UI assets
        Hud/           Hud.uxml, Hud.uss, Planning.uss, Campaign.uss (menu, map, bar), Erp.uss, Sheet.uss,
                       HudTheme.tss, HudPanelSettings
        Fonts/         Oswald (HUD) and Roboto (sheet), both SIL OFL (licences in *-OFL.txt), and their
                       *_SDF font assets (made by CediHudSetup)
    Data/              ScriptableObject asset instances: Campaign.asset (CampaignDatabase), Roles/, Sectors/
                       (Sector_Cosmetica + Cosmetica/Historia_Atlas_Cosmetica.csv), Characters/
    Documentation/     Project documentation: this file, DISENO_JUEGO_V2.md (current game design: roles,
                       sectors, NOVA ERP, planner sheet, worlds) and SIMULACION_PLANEACION.md (spec of
                       the v1 season level, future boss level)
```

Code uses the `NovaMarket.*` namespaces matching the `Scripts/` subfolders; a nested folder adds its
name (`Scripts/Simulation/Planning/` → `NovaMarket.Simulation.Planning`, tests in
`NovaMarket.Tests.Simulation.Planning`).

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
        Gate                       GatePad, inbound/outbound lanes, GateIsland, stop lines,
                                   GuardBooth (booth + barrier arms)
        ContainerYard              YardLink, ContainerPad, Container_01-06
        Parking                    ParkingPad 36 m x 18 m, StallLines
        Office                     2-storey admin office 14 m x 9 m x 7 m at (-48, 0, -23)
    Systems
        Selection                  SelectionController (left click selects, Escape / ground click clears)
        SelectionRing              SelectionRing mesh, M_UI_SelectionRing
        MinimapCamera              MinimapRenderer: top-down orthographic camera, renders a few frames
        EventSystem                EventSystem + InputSystemUIInputModule
        Operation                  CediOperationRunner + RackStockDisplay; spawn Vehicles/, StagedPallets_*
                                   and RackPallets/ in Play
        HUD                        UIDocument (Hud.uxml) + HudDataSource (scenario: PlannerCampaign) +
                                   HudController + CampaignController + PlanningLevelController (v1)
    Debug
        ScaleMarkers               1 m cube, 5 m and 10 m poles
```

Flow inside the building: Receiving → Storage → Picking → Shipping. `Building`, `Gate`, `Office`
and `ContainerYard` carry a `CediFacility`; `Receiving`, `Storage`, `Picking` and `Shipping` carry an
`InteractableLink` to the warehouse, so clicking any zone selects it. Office, booth, facilities and
`Systems` are created by **NOVAMARKET → HUD → Setup CEDI Gameplay (HUD, Selection, Operation)**
(`CediHudSetup`, safe to run again).

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
15 m above the terrain and 10 m above building roofs (the generated `*_Buildings_*` city chunks
get a MeshCollider on the Ignore Raycast layer at start, so they never block clicks). All values are in `StrategyCameraSettings`, editable in the Inspector.
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

- `CediFacility`: an `InteractableObject` that only stores a facility id (`CediFacilityIds`);
  its data lives in the operation state, not in the scene.
- `InteractableLink`: forwards clicks on its colliders to another interactable.
- `SelectionController`: left click raycasts from the camera; the hit collider resolves to an
  `InteractableLink` target or the nearest `IInteractable` parent. Ground clicks and Escape
  clear the selection; clicks over the HUD are ignored (`IsPointerBlocked`, set by the HUD).
- `SelectionRing`: flat elliptical gold band around the selection, measured in the target's own
  axes; it follows moving vehicles and turns with them.
- `ISelectionDetailsProvider`: objects whose panel data changes while selected (vehicles); the
  HUD polls `GetDetails()` every frame. A destroyed selection (a truck that left) is cleared.

## Operation state (Simulation)

`OperationState` is the single object the HUD reads. It groups `MissionState` (level, counter,
objectives, score), `ResourceState` (positions, budget in COP, trucks, operators, service
level), `NotificationFeed` (timed notices; time is passed in, so it is testable) and
`FacilityDirectory` (`FacilityInfo` per facility id: stats and orders). Player orders go through
`OperationState.IssueOrder` (raises `OrderIssued`); dispatches through `RegisterDispatch`.
All of it is plain C# with no `UnityEngine`, covered by EditMode tests.

`SampleOperationData` gives the starting numbers (level III, S&OP); the CEDI operation below
then updates the same state as trucks and forklifts work. Panels read `SelectionDetails`
(title, rows, optional progress bar, orders) from facilities (`FacilityInfo.ToDetails`) and vehicles.

## CEDI operation

`CediOperationSimulation` (plain C#, `Tick(deltaTime, clock)`) runs the yard and the warehouse:

- **Receiving trucks** (16 m, the first one is Don Hernán's import container) and **dispatch
  turbos** (9 m) arrive on the highway, queue at the inbound stop line (`GateLane`), wait for
  the document check and a free dock (`DockManager`), the barrier lifts, they cross the yard,
  reverse to the dock, unload / load pallet by pallet, U-turn and leave through the outbound
  gate. States: `TruckState`. Routes are truck-centre polylines in `CediLayout` (right-hand
  traffic; inbound yard road z = -16, outbound z = -21), driven by `RouteMover` (accelerates,
  brakes, reverses). Trucks keep front and rear on the route (chord pose), so the body never
  cuts a corner; routes keep `CediLayout.BuildingMargin` (1 m) from the warehouse, canopy and
  office (`CediLayoutTests`). Trucks arrive from the east and leave to the west (one-way flow)
  and park with the rear bumper at `DockWallX` (23.3 m, the dock bumpers).
- **Forklifts** (4) take pallets from the receiving staging lanes to random rack slots
  (put-away, +1 position) and from racks to the shipping lanes of docked turbos (pick,
  -1 position), driving only on the one-way aisle graph of `ForkliftNetwork` (directed Dijkstra, rounded
  corners).
- Each event updates the `OperationState`: occupied positions, trucks in the lot, dispatches
  (`RegisterDispatch` when a loaded turbo crosses the outbound barrier), the Don Hernán
  objective, notices ("Camión de Don Hernán en portería.", "Despacho a Envigado en camino.")
  and live warehouse / gate data (published once per second).
- **Traffic** (`CediOperationSimulation.Traffic.cs`): every vehicle has a `Footprint` (oriented
  rectangle; forklifts include the forks). Each tick `TrafficLimit` looks a braking distance
  ahead and stops the vehicle before it comes within the gap of another one (0.3 m trucks,
  0.7 m forklifts). On top of that: `TrafficZone` crossings take one vehicle at a time (gate
  junction for trucks, aisle merges for forklifts); each side of docks has a manoeuvre turn
  (taken at the gate, released when the truck docks or has left the dock area); a forklift
  reserves the staging lane or rack face it works at, and the place it backs out to stays free.
  If two vehicles wait for each other for 3 s, one goes first (never the one backing out); it
  only drops the gap, bodies never touch. `TrafficReport()` explains who waits for whom.
- Tuning (sizes, speeds, handling times, arrival intervals): `CediOperationSettings`.
  `CediOperationSimulationTests` runs 30 minutes of operation with several seeds and checks that
  no two vehicles ever overlap and nobody waits in traffic for a minute.

Scene side (`Scripts/Operation/`): `CediOperationRunner` (on `Systems/Operation`; Inspector:
simulation speed and seed) ticks the simulation, instantiates one prefab per vehicle and shows
staged pallets; `VehicleVisual` copies an agent's pose and is selectable; `GateBarrier` lifts the
barrier arms on their hinges. To use real models later, replace the prefabs in
`Prefabs/Vehicles/` keeping the pivot at the ground centre and the front towards +Z.

## HUD

UI Toolkit, Age of Empires III style: dark panels with a bevelled gold frame, Oswald font. The sheet
cells, headers, formula bar and the formula cards use Roboto (clear ";" vs ":", same-width digits).
Layout in `UI/Hud/Hud.uxml`, styles in `Hud.uss`, scaling 1920 × 1080 reference
(`HudPanelSettings`). Icons are vector `HudIcon` elements drawn with Painter2D.

| Area | Content | Script |
|---|---|---|
| Top right | Level bar, dispatch counter, primary/secondary objectives with checkboxes, score | `ObjectivesView` |
| Bottom right | Resource bar (positions, budget, trucks, operators, service level) | `ResourceBarView` |
| Bottom right | Round minimap: valley picture, CEDI marker, camera view; click/drag to travel | `MinimapView`, `MinimapRenderer`, `MinimapOverlay`, `MinimapProjection` |
| Bottom left | Selected object: icon, name, data rows, progress bar (unloading/loading), order buttons | `SelectionPanelView` |
| Left | Notices that slide in and fade out by themselves | `NotificationsView` |

`HudController` (on `Systems/HUD`) wires the views to `HudDataSource` (owner of the
`OperationState`; shows a welcome notice), the
`SelectionController` and the `StrategyCameraController` (`FocusOn` makes the camera glide).
Views subscribe to the state's `Changed` events, so they refresh only when data changes.

## Planner v2: campaign data, portfolio, sheet, ERP and world 1 levels

Plain C# engine of DISENO_JUEGO_V2.md (the UI comes next: start menu, ERP window, sheet view).

- **Campaign data** (`Data/Campaign.asset` → `CampaignDatabase.ToCatalog()`): 8 roles (links of the
  Atlas chain map; only `planeador` playable), 5 sectors (only `cosmetica`), 10 characters. A
  character's `linkedRoleId` is the role it performs: `CreateCast(playerRole)` returns everyone else as
  `NpcCharacter` (trust 0–100), so a role can become playable without engine changes.
- **Dialogues** (`Data/Dialogues/Dialogue_<level>_<character>.asset`, type `DialogueData`, listed in
  `Campaign.asset`): one file per character and level with lines `key` (intro, exito, pista-<tema>,
  impacto-…, see `DialogueKeys`), `order`, `mood` and `text` with `{placeholders}` (`{codes}`,
  `{skus}`, `{missing}`, `{code}`, `{player}`, `{clean}`, `{month}`). Files with a `roleId` and/or
  `sectorId` replace the general ones for that role or sector, so another sector brings its own lines
  without code. `DialogueBook` resolves them; a missing line shows as `[level/key]` and
  `CampaignDataTests` checks that 1.1 and 1.2 have every line they say.
- **Portfolio**: `Sector_Cosmetica.CreatePortfolio()` reads the 14 real Atlas SKUs (ene-23…ago-26,
  isolated zeros already imputed by the Atlas) and `PortfolioGenerator` adds 186 synthetic SKUs
  (seed 2026) by family, with the family template's seasonal shape, log-normal volume (top 20 % ≈ 80 %
  of units) and six patterns. Master data follows §8.1; lead time is the sum of the §3.1 phases.
- **Planner sheet** (`Sheet/`): Excel in Spanish (";" between arguments, decimal comma, "4.695"),
  references A1, $A$1, `Datos!H4`, `'Serie limpia'!B3`; SUMA, PROMEDIO, ABS, SI, DESVEST.P; on-demand
  recalculation with a version cache, circular references, Spanish errors; `Fill` (drag) moves
  relative references and continues number series. `SheetClipboard` copies and pastes (formulas move
  their references; values only; a whole row trims to its used columns; tab-separated text from other
  programs). `SheetErrorHelp` explains an error in plain Spanish naming the cells (which cells read
  each other in a circular reference, which cell an error comes from). `SheetHistory`
  (`Workbook.History`) is undo/redo (Ctrl+Z / Ctrl+Y, last 100 actions): the UI wraps each action of
  the player in `History.Record` and every cell it touches is copied before it changes; the level adds
  its own steps (problem marks) with `History.Add`. Edits of the game (downloads, setup) are not
  recorded. `FormulaEditing` places a clicked reference at the cursor of the formula being typed, and
  `FormulaLexer.Normalize` writes function names and references in capitals (=promedio → =PROMEDIO).
- **NOVA ERP** (`Erp/`): screens with their SAP code (world 1: Facturación, Maestro de materiales);
  billing by month includes the projection column by default (the Atlas trap); "Descargar a hoja"
  puts months in columns C…AT, as the Atlas. `DataQualityInjector` plants problems with their
  accepted answers (Atlas step p2 rules).
- **Levels** (`Levels/`): `GatherHistoryLevel` (1.1, guided) and `CleanDataLevel` (1.2, empty boxes):
  validation ±2 %, three stars (the first one is the core deliverable), gem per level, "Excel de
  planeador" badge, Atlas help that costs the third star, formula cards, Andrés/Sofía lines and, in
  1.2, Andrés's comparison of the level and seasonal index a model would learn. `LevelBalanceTests`
  plays 200 seeds of each level.

### Planner campaign UI (scenario `PlannerCampaign`, the default)

`HudDataSource.scenario = PlannerCampaign` (default; `SeasonPlanning` still plays the v1 season):
the CEDI runs with background trucks (`CediOperationSettings.ForCampaign()`) and
`CampaignController` (on `Systems/HUD`; Inspector: `campaign` = Data/Campaign.asset, `fixedSeed`,
0 = random) drives the flow, built in code into the `menuLayer`, `workspaceBar` and `windowLayer`
containers of `Hud.uxml`:

- `StartMenuView`: role cards by lane and sector cards from the campaign data (disabled ones
  "PRÓXIMAMENTE"), the NPC team of the chosen role, "COMENZAR".
- `WorldMapView`: world tabs (worlds 2–3 locked), level nodes 1.1–1.7 along a path (`Painter2D`,
  solid up to the last built level, dotted after) and the locked boss; a detail card (Atlas step,
  mode, characters, gem, stars) with "JUGAR NIVEL".
- In a level: `WorkspaceBarView` (top left: NOVA ERP, Hoja del planeador, Conversación, Ayuda,
  Entregar, Mapa) and the level checklist in the objectives panel (`CampaignHudData` resets the
  `MissionState` and ticks steps from the sheet; the score is `CampaignProgress.TotalPoints`).
- `DialogueBoxView`: the characters' lines in a box at the bottom centre (badge, name, role, mood,
  "Anterior / Cerrar / Siguiente"); "Conversación" reopens the history.
- Level 1.2: "Marcar problema" in the sheet toolbar opens `IssueMarkView` for the selected cell,
  range or whole row of a billing tab or «Serie limpia» (`CleanDataLevel.Locate`): one kind, action
  and note for every cell, saved as one undo step; marked cells turn orange. "Quitar marca" removes
  the marks of the selection. Marks on one problem of the data (the months before a launch), or on
  consecutive good months marked alike, count as one problem in Andrés's warning and in the review. "Tarjetas de fórmula" (`FormulaCardView`) shows the Atlas formula with empty boxes,
  marks wrong boxes with their hint and writes the result into «Serie limpia» (the "Excel de
  planeador" badge still needs the player's own formulas).
- `AtlasHelpView`: per topic a free hint (dialogue key `pista-<topic>`) and the solved Atlas step,
  which costs the "Sin ayuda del Atlas" star.
- `LevelResultView`: "¿Entregar a Andrés?" confirmation, then stars with the reason of each one, gem,
  Excel badge, in 1.2 a block per problem (`CleaningReview`: where, type, action and value yours /
  right, what happened and the Atlas explanation) and the good cells marked or changed by mistake,
  Andrés's review with the level / seasonal index table (1.2), lessons and "Seguir
  trabajando / Repetir con otros datos / Volver al mapa". `CampaignProgress` keeps the best result
  per level in PlayerPrefs (`NOVAMARKET.CampaignProgress`; Inspector toggle `saveProgress`) and the
  map shows its stars. Before delivering 1.2 Andrés warns about marked problems still uncorrected
  (`CleanDataLevel.UncorrectedWarning`); after a delivery «Serie limpia» shows in red the cells he
  flagged (`ReviewNotes`), with the reason in the status bar and on hover, and "Quitar marca" removes
  a mark. Texts follow one rule: full sentences saying what happened, where (SKU and month) and why.
- `ErpWindowView`: own enterprise look (navy bar with transaction code and SAP equivalent, screen menu
  with locked screens, selection criteria with material suggestions, ALV-like grid, invoice detail per
  month, "Descargar a hoja" with a link to the tab, F8 runs the query).
- `SheetWindowView` + `SheetGridView`: spreadsheet window (formats, formula bar with name box and fx,
  A B C headers and row numbers, tabs, status bar with the error explanation or sum/average/count).
  The grid draws only visible cells, edits in place or in the formula bar, picks references by
  clicking while a formula is typed (also on another tab), and drags the fill handle with
  auto-scroll at the edges. Ctrl+C / Ctrl+V / Ctrl+Shift+V (values) and the toolbar buttons copy
  and paste (also with the system clipboard); resting the pointer on a cell with an error or a note
  shows its explanation.

While a menu or window is open `StrategyCameraController.InputSuspended` keeps WASD and the wheel
for the UI. USS note: the default theme gives labels vertical margins and lets flex children shrink;
the campaign styles reset both so panels keep their size (`.menu-screen > * { flex-shrink: 0 }`).

## Planning level: "Temporada de fin de año" (v1, future boss level)

First playable level, specified in `Documentation/SIMULACION_PLANEACION.md`. With the v2 design it
is kept as the base of the Planeador's **boss level** (16 weeks, whole PLANEAR lane every week);
its "Plan de pedidos" panel will be replaced by the ERP → sheet → purchase requisition flow, and
the engine extended (staged lead times, events with hidden distributions, trust, FVA). The player orders
boxes of 4 real SKUs for 16 weeks (September–December) and closes each week; the results are
compared with the Atlas reference policy played with the same seed.

To play it, set `HudDataSource.scenario` to `SeasonPlanning` (`PlanningLevelData`); `PlannerCampaign` is the
default and `SampleOperation` shows the earlier illustrative S&OP numbers with random trucks.

Engine (`Scripts/Simulation/Planning/`, plain C#):

- `PlanningCatalog` (SKUs and monthly history), `DemandPlan` (statistical weekly mean, rack
  capacity), `PlanningRules` (calendar, cost rates, Z = 1.65, game-over and star thresholds).
- `PlanningSeason(seed)`: event week shift, clipped demand noise, event multipliers, purchase cap.
  The same seed gives the same season to the player and to the reference.
- `PlanningSimulation`: inventory, transit, costs, service level; `CloseWeek(orders)` follows the
  spec order (orders → demand and sales → costs → arrivals → closing → events). Events
  (`.Events` part) open an `EventDialog` that must be answered (`ResolveEvent`) before the next
  close; after each week a character comments (`WeekComment`). Game over: cumulative service
  below 85 % from week 4, or 3 weeks in a row with boxes on the floor.
- `AtlasSuggestion`: periodic review with order-up-to level, with `Explain()` for the player.
- `PlanningPolicies` (Atlas reference, scaling to the purchase cap), `SeasonResult` (stars,
  gem, comparison) and `PlanningLessons` (automatic lessons).
- `PlanningOperationBridge`: each arrived lot becomes a receiving truck (split above 12 pallets),
  the week's sales become dispatch turbos (up to 7 pallets each), and the HUD shows positions
  (boxes → pallets per SKU), budget (`SeasonBudget` minus costs), service level, closed weeks,
  objectives and notices. The operation runs with `CediOperationSettings.ForPlanning()`: no random
  trucks, positions owned by the plan, at most 12 trucks waiting to set off.
- Tests in `Tests/EditMode/Simulation/Planning/`, including `PlanningBalanceTests` (200 seeds:
  reference ≈ 92.6 % service, informed player ≈ 96.4 % and ≈ 19 % cheaper with mostly 3 stars,
  no orders ≈ 16.7 %). Ignoring the events can end in game over (≈ 8 % of seeds for a player that
  only copies the suggestion), which is the lesson of the level.

UI (`Scripts/UI/`, styles in `UI/Hud/Planning.uss`, containers `planningPanel` and `modalLayer` in
`Hud.uxml`): `PlanningLevelController` (on `Systems/HUD`; Inspector: `fixedSeed`, 0 = random)
creates the season and wires `PlanningPanelView` (order plan, top centre: stock vs rack, transit
with arrival week, plan of the next L+1 weeks with announced events marked, order field,
"Sugerido del Atlas", "Usar todos los sugeridos", purchase cap, last comment, "Cerrar semana"),
`AtlasDialogView`, `EventDialogView` and `SeasonResultView` (stars, gem, game over, table you vs
Atlas, lessons, replay with a new seed). The office order "Abrir plan de pedidos" reopens the
panel. `RackStockDisplay` (on `Systems/Operation`) puts each SKU's pallets on top of its rack row
(RackRow_01 = labial … RackRow_04 = crema) and the overflow on the aisle floor.

The Oswald font has no σ, Σ, ⌈⌉, ★ or →: texts spell them out (stars are `HudIcon` drawings).

## Not implemented yet

- Sheet: insert rows/columns, more functions (RAIZ, SI.ERROR…), saved progress
- Concrete data types (CompanyData, ScenarioData, ... RoleData); the planning SKUs live in code
- Forecasting, MRP and DDMRP levels (only the season planning level exists)
- Negotiation with arguments, character trust, FVA, staged import lead times, shipment board
- Box/drag selection, people, order effects in the world, truck-to-truck collision avoidance in the yard
- Final art for CEDI Medellín (environment is a procedural blockout; no purchased assets)
- Final vehicle models (current prefabs are simple shapes), characters
- Database
- AI, Mentor
- S&OP, advanced simulation
- Mission/level selection, saved progress and gems between levels; character art (badges are initials)

## Scene life: ambient traffic

Approved plan (4 stages): 0 common base, 1 traffic (1a–1d), 2 metro, 3 people and CEDI operators,
4 wind, smoke, lights and ambient sound (temporary sound placeholders until the Kenney CC0 packs
arrive). Behaviour is separated from the visual model so the basic shapes can be swapped for
Kenney / Quaternius / Synty / Mixamo packs without touching the logic.

- **Logic (plain C#, `Scripts/Simulation/Ambient/`, namespace `NovaMarket.Simulation.Ambient`):**
  `CityRoadLayout` turns the street grid into a `RoadNetworkDefinition` (highway, two-way streets,
  one-way 6 m half streets with their lane shifted onto the asphalt, avenues x = ±450 and
  z = 106 / −297, bridges, lights at the highway bridges and avenue crossings, bus stops, the
  "do not block" box at the CEDI gate). `RoadNetwork` builds lanes, turn movements and crossings
  (`TrafficSignal`, priority + "PARE", conflicts). `AmbientTraffic` drives cars, taxis, motos (35 %),
  buses and trucks inside a circle around the camera: car following, crossing bookings, giving way,
  moto filtering and moto box at lights, buses at stops, overtaking parked vehicles through the
  opposite lane. At lights the last 8 s of each green belong to left turners (straight and right
  wait, and give up a booking they can still stop for), so they are not starved by a steady oncoming
  flow. Without lights a moto filtering at the side of a lane waits 3.5 m back with the cars (long
  vehicles turning in sweep that corner); a vehicle may wait up to two light
  cycles at a busy crossing (the safety valve removes one stuck 120 s out of close view; the tests
  check it is never needed). Tests in `Tests/EditMode/Simulation/Ambient/`.
- **CEDI link (stage 1d):** `ISharedRoad` / `ISharedRoadVehicles` (`Scripts/Simulation/SharedRoad.cs`);
  `CediOperationSimulation.SharedRoad.cs` makes CEDI trucks brake for traffic, stop at red lights,
  spawn only on a free road and wait for a gap before leaving the gate. They appear and vanish at
  x = ±300 (`CediLayout.SpawnDistanceX`), halfway between two crossings, never inside one. While a
  truck moves through the gate box (turning in or pulling out) no traffic enters it.
- **Metro (stage 2):** `MetroLine` (plain C#, `MetroLineDefinition` + `MetroSettings`) runs 4 trains of
  3 cars on the viaduct: right-hand running (eastbound on the south track), they brake to stop centred
  on each platform (x = −330 and 420), stand 25 s with the doors open, keep 150 m from the train ahead
  and at each end of the line (x = ±1450) wait 30 s and return on the other track. `MetroLineData`
  (geometry exported by **NOVAMARKET → Ambient → Export Metro Line**, service values editable),
  `MetroRunner` + `MetroTrainView` on `Systems/Ambient` (prefab `Prefabs/Vehicles/Ambient/Metro_Train`,
  doors change material while open). The trains are no longer part of the `Metro_Line` mesh:
  **NOVAMARKET → Environment → Rebuild Metro Line Only** rebuilds just the viaduct.
- **People (stage 3):** `CityWalkLayout` builds the walk network from the blocks recorded by the
  environment builder (`CityBlocksData`, exported on each full rebuild): the 2.5 m sidewalk around city and
  downtown blocks (path 1.25 m from the kerb, 18 cm high), the edge of industrial blocks, park perimeters
  and crossing paths (the plazas) and zebra crossings 3 m back from each corner between neighbouring
  blocks (the lanes' stop line moves behind them: `TrafficLane.StopLineBack`). `AmbientPedestrians` keeps
  80 / 150 / 300 people around the camera (Low / Medium / High): they keep right, stop now and then and
  cross when the crossed street has red (and the other street still has 5 s of green) or, without lights,
  when there is a gap; while crossing they are bodies for the traffic (`AmbientTraffic.SharedPedestrians`)
  and step back if a vehicle stays in their way. `MetroPassengers` waits on the platforms, gets off and on
  through the open doors; `CediWorkers` is the CEDI staff (guard on the gate island, pickers between the
  pick stations, staff between parking and office who wait while a truck is near the door, two people
  chatting). `PeopleRunner` + `PedestrianView` draw all of them from one pool (prefabs in
  `Prefabs/Characters/`; a humanoid with an Animator float `Speed` works as is, the simple model swings
  its legs and arms); `Crosswalk_Zebra` paints the crossings.
- **Data:** `RoadNetworkData` (exported by **NOVAMARKET → Ambient → Export Road Network**, also run
  by the environment rebuild), `AmbientSettingsData` (Low 60 / Medium 120 / High 200 vehicles,
  vehicle mix), `AmbientVisualProfile` (prefab and colours per vehicle kind, traffic light, bus stop).
  All in `Data/Ambient/`.
- **Scene (`Scripts/Ambient/`, namespace `NovaMarket.Ambient`):** `AmbientTrafficRunner` on
  `Systems/Ambient` (simulation at 20 steps/s, models interpolated in between, one loop for all
  views, `ViewPool` reuse), `AmbientVehicleView` (pose, wheels, brake lights, moto lean; `Model` child
  to fix pivots of other packs), `TrafficLightView`, `AmbientDebugOverlay` (**F3**: FPS and ms).
  Prefabs in `Prefabs/Vehicles/Ambient/` and `Prefabs/Environment/` have two LODs, no colliders
  (traffic is not selectable). **NOVAMARKET → Ambient → Setup Ambient Traffic** rebuilds all of it.

**Status (2026-10-21): stages 0–3 done** (traffic, metro, people), waiting for the author's approval
before stage 4 (trees moving with the wind, smoke, lights ready for dusk, ambient sound with temporary
placeholders until the Kenney CC0 packs arrive).
Measured in the Editor (Game view 1920×888, start view): 0 vehicles 139 FPS, 60 → 119, 120 → 117,
200 → 99; logic ≈1.9 ms per step at 120 vehicles, models ≈0.3 ms per frame.
