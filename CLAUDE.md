# CLAUDE.md

NOVAMARKET es un simulador logístico educativo hecho en Unity. Los estudiantes recorren un
centro de distribución (CEDI) y resuelven escenarios logísticos; el primer escenario es
**CEDI Medellín**.

Arquitectura completa: `Assets/NOVAMARKET/Documentation/PROJECT_ARCHITECTURE.md`. Léela antes
de agregar sistemas y actualízala cuando cambien la estructura, las escenas o las convenciones.

## Stack

- Unity **6000.3.25f1** (Unity 6.3), URP 17.3.0 (`Assets/Settings/PC_RPAsset` / `Mobile_RPAsset`)
- Nuevo Input System (`Assets/InputSystem_Actions.inputactions`), AI Navigation, uGUI, Test Framework
- Aún no hay asmdefs: todos los scripts compilan en `Assembly-CSharp`

## Reglas

- **1 unidad de Unity = 1 metro.** Modela a tamaño real; nunca compenses con la escala del
  transform. Usa `NovaMarket.Core.WorldScale` en lugar de factores de conversión escritos a mano.
- Todo el contenido del proyecto vive en `Assets/NOVAMARKET/`. No toques el contenido de la
  plantilla URP (`Assets/Scenes/SampleScene.unity`, `Assets/TutorialInfo/`, `Assets/Readme.asset`).
- Los namespaces son `NovaMarket.<Carpeta>`, igual que `Scripts/<Carpeta>/` (Core, Data,
  Interaction, Simulation, UI).
- Los tipos de datos derivan de `NovaMarket.Data.NovaDataAsset` y usan
  `[CreateAssetMenu(menuName = NovaDataAsset.MenuRoot + "<Tipo>")]`; las instancias van en
  `Assets/NOVAMARKET/Data/`.
- Los objetos seleccionables de la escena implementan `IInteractable`; hereda de
  `InteractableObject` para agregar comportamiento.
- Estilo de código: campos `[SerializeField] private` expuestos con propiedades de solo lectura,
  `<summary>` XML en los tipos públicos, miembros con cuerpo de expresión cuando son cortos.
  Identificadores y comentarios de código en inglés.

## Assets

- Los materiales usan el prefijo `M_` (`M_<Categoría>_<Variante>`); los de blockout están en
  `Materials/Blockout/`. Los prefabs van en la carpeta `Prefabs/<Categoría>/` correspondiente.
- Siempre haz commit de los `.meta` junto con su asset y nunca edites GUIDs a mano.
- Prefiere manejar el Editor (Unity CLI / MCP) antes que editar a mano el YAML de `.unity`,
  `.prefab` o `.mat`.

## Escenas

- `Assets/NOVAMARKET/Scenes/CEDI_Medellin.unity`: escena principal (índice 0 en Build Profiles);
  todo el contenido cuelga del objeto raíz `CEDI_Medellin`.
- `Assets/NOVAMARKET/Scenes/Test_NOVAMARKET.unity`: solo pruebas de herramientas/conexión.

## Git

- Conventional Commits con descripción en español, p. ej. `feat: crea blockout inicial CEDI Medellin`.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` y los `*.csproj` / `*.slnx` generados están ignorados.
