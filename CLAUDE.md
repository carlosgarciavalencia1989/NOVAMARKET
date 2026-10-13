# CLAUDE.md

NOVAMARKET es un simulador logístico educativo hecho en Unity. Los estudiantes recorren un
centro de distribución (CEDI) y resuelven escenarios logísticos; el primer escenario es
**CEDI Medellín**.

Arquitectura completa: `Assets/NOVAMARKET/Documentation/PROJECT_ARCHITECTURE.md`. Léela antes
de agregar sistemas y actualízala cuando cambien la estructura, las escenas o las convenciones.

**Comunicación:** explica siempre los cambios en español y de forma sencilla; el autor está
aprendiendo Unity (di qué se tocó, dónde verlo en el Editor y por qué).

## Visión del juego

Diseño vigente: `Assets/NOVAMARKET/Documentation/DISENO_JUEGO_V2.md` (manda sobre
`SIMULACION_PLANEACION.md`, que describe el nivel v1 "Temporada de fin de año").

Juego educativo de **roles y sectores** sobre la cadena de suministro, alrededor del CEDI
Medellín (valle de Aburrá). Al entrar, el jugador escoge:

- **Rol:** un eslabón del Mapa de la cadena del Atlas. PLANEAR: Planeador. EJECUTAR: Abastecer,
  Inventarios, Almacenar, Pedidos, Transporte y Última milla. RETORNAR: Logística inversa.
  HABILITAR (Finanzas, Riesgo, Tecnología y datos) no se juega: son personajes.
- **Sector:** una campaña con su empresa, SKU, proveedores y eventos (cosmética ~200 SKU, repuestos,
  alimentos, farmacéutico, tecnología).

**En esta versión solo está habilitado Planeador + Cosmética**; el resto sale como "Próximamente".
Roles y sectores se cargan como datos, sin cambiar el motor.

Los mundos del Planeador son los módulos del carril PLANEAR del Atlas: **1 Pronóstico**,
**2 Gestión de la demanda** y **3 Materiales (MRP · DDMRP)**. Cada paso del flujograma del Atlas
es un nivel (1.1 "Reunir la historia" = p1, 1.2 "Limpiar los datos" = p2…). El nivel v1
"Temporada de fin de año" se conserva como base del futuro **nivel jefe** del Planeador.

Principios de la v2:
- **El jugador calcula; el juego valida.** Dato crudo en el **NOVA ERP** (ERP simulado, con su
  equivalente SAP como dato educativo) → "Descargar a hoja" → cálculo en la **hoja del planeador**
  (un Excel dentro del juego, fórmulas en español: `=SUMA`, `=PROMEDIO`…) → decisión →
  negociación → consecuencia. Tolerancia de validación ±2 %. La ayuda del Atlas existe pero cuesta.
- Andamiaje por nivel: guiado → casillas vacías → libre.
- Los personajes tienen incentivo y sesgo (Valeria sobreestima 5–50 %) y un medidor de confianza.
- El jugador nunca sale del juego (solo un CSV opcional al final del nivel).

### Atlas de Planeación (las fórmulas deben ser correctas)

El juego se basa en el "Atlas de Planeación". Cada fórmula se implementa en C# puro con
pruebas que verifiquen ejemplos numéricos. Si la notación o el signo del Atlas difiere de lo
siguiente, manda el Atlas: pregunta antes de asumir.

- **WAPE** = Σ|Aₜ − Fₜ| / Σ|Aₜ| (A = real, F = pronóstico).
- **Sesgo** = Σ(Fₜ − Aₜ) / ΣAₜ. Positivo = sobrepronóstico. Confirmar la convención de signo.
- **Tracking signal** = Σ(Aₜ − Fₜ) / MAD, con MAD = Σ|Aₜ − Fₜ| / n. Alerta típica fuera de ±4.
- **Stock de seguridad** SS = Z · σ · √LT. σ es la desviación de la demanda por periodo y LT
  va en esos mismos periodos. Con lead time variable: SS = Z · √(LT · σd² + d̄² · σLT²).
- **Limpieza de datos (p2):** un cero de un mes estacional se imputa con el promedio del mismo mes
  en los otros años; sin estacionalidad, con el promedio de los vecinos. Atípico:
  |D(t) − promedio| > 3 × desviación (el Atlas usa `DESVEST.P`). Ejemplo del Atlas: CV10004 jun-24
  = 0 → 4.695 con los otros junios (con los vecinos daría 3.302).
- **Punto de reorden** ROP = d̄ · LT + SS.
- **EOQ** = √(2 · D · S / H). D = demanda anual, S = costo por pedido, H = costo anual de
  mantener una unidad.
- **Neteo MRP**: necesidad neta = max(0, necesidad bruta − inventario proyectado disponible −
  recepciones programadas + SS). Luego se aplica el lote y se desfasa por el lead time.
- **Zonas DDMRP** (ADU = uso diario promedio, DLT = lead time desacoplado, LTF = factor de
  lead time, VF = factor de variabilidad):
  - Rojo = ADU · DLT · LTF · (1 + VF) → **TOR** = rojo.
  - Amarillo = ADU · DLT → **TOY** = TOR + amarillo.
  - Verde = max(ADU · DLT · LTF, MOQ, ADU · ciclo de pedido) → **TOG** = TOY + verde.
- **Flujo neto** = en mano + en tránsito − demanda calificada (vencida + de hoy + picos
  calificados). Si flujo neto ≤ TOY, se pide TOG − flujo neto.

### Estilo gráfico

- **Base:** estrategia tipo *Age of Empires III*. Terreno con textura, luz cálida de tarde y
  vista isométrica en perspectiva.
- **HUD:** panel de objetivos arriba a la derecha. Barra de recursos y minimapa abajo a la
  derecha. Panel de selección con órdenes abajo a la izquierda.
- **Inspiraciones (no son la base gráfica):**
  - *WareTrack*: pines sobre los objetos, seguimiento de envíos por etapas, estado de muelles
    y montacargas.
  - *Police Chief*: interiores en corte, utilería que cuenta la historia y áreas deterioradas
    que se restauran al mejorar la operación. Va ligado a niveles de madurez: bodega reactiva
    → MRP → S&OP → DDMRP.
- **Entorno del CEDI:** autopista, río canalizado, tren elevado y laderas con barrios de ladrillo.

### Personajes

| Personaje | Rol |
|---|---|
| Andrés | Director de S&OP: mentor y tutor, valida y da lecciones |
| Valeria | Comercial: optimista, sobreestima |
| Marcela | Finanzas: presupuesto y topes de compra, conservadora |
| Paula | Compras y abastecimiento (futuro rol jugable "Abastecer") |
| Don Hernán | Proveedor importado de perfume: optimista con las fechas |
| Mateo | Logística internacional: modo de transporte y embarques |
| Lucía | Agencia de aduanas: documentos y levante ante la DIAN |
| Wilmer | Transporte nacional del puerto al CEDI |
| Julián | Jefe del CEDI: recibo, posiciones y montacargas |
| Sofía | Control de inventarios: conteos, exactitud y datos del ERP |

Mientras un rol no sea jugable, sus personajes son NPC. Cada personaje se define como datos
(rol, incentivo, sesgo, confianza) con su lógica en C# puro, separados de la interfaz, para que
un rol pueda pasar de NPC a jugable sin rehacer el motor. Faltan los nombres de los personajes de
Riesgo y sostenibilidad, y de Tecnología y datos.

### Recompensas

Gemas con forma, color y valor distintos por nivel.

## Stack

- Unity **6000.3.25f1** (Unity 6.3), URP 17.3.0 (`Assets/Settings/PC_RPAsset` / `Mobile_RPAsset`)
- Nuevo Input System (`Assets/InputSystem_Actions.inputactions`), AI Navigation, uGUI, Test Framework
- Asmdefs: `NovaMarket.Runtime` (`Scripts/`), `NovaMarket.Editor` (`Scripts/Editor/`, solo Editor) y
  `NovaMarket.Tests.EditMode` (`Tests/EditMode/`). Un script nuevo en `Scripts/` entra solo en
  `NovaMarket.Runtime`; si usa otro paquete, agrega la referencia en el asmdef.

## Reglas

- **1 unidad de Unity = 1 metro.** Modela a tamaño real; nunca compenses con la escala del
  transform. Usa `NovaMarket.Core.WorldScale` en lugar de factores de conversión escritos a mano.
- Todo el contenido del proyecto vive en `Assets/NOVAMARKET/`. No toques el contenido de la
  plantilla URP (`Assets/Scenes/SampleScene.unity`, `Assets/TutorialInfo/`, `Assets/Readme.asset`).
- Los namespaces son `NovaMarket.<Carpeta>`, igual que `Scripts/<Carpeta>/` (Core, Data,
  Interaction, CameraControl, Simulation, Operation, UI). Una subcarpeta agrega su nombre
  (`Scripts/Simulation/Planning/` → `NovaMarket.Simulation.Planning`). Las pruebas usan
  `NovaMarket.Tests.<Carpeta>`.
- Los tipos de datos derivan de `NovaMarket.Data.NovaDataAsset` y usan
  `[CreateAssetMenu(menuName = NovaDataAsset.MenuRoot + "<Tipo>")]`; las instancias van en
  `Assets/NOVAMARKET/Data/`.
- La lógica de simulación (pronósticos, inventario, MRP, DDMRP) va en clases C# puras, sin
  `UnityEngine`, separadas de los `MonoBehaviour`. Siempre lleva pruebas con Unity Test
  Framework. Los `MonoBehaviour` solo conectan esa lógica con la escena y la UI.
- Los objetos seleccionables de la escena implementan `IInteractable`; hereda de
  `InteractableObject` para agregar comportamiento.
- Estilo de código: campos `[SerializeField] private` expuestos con propiedades de solo lectura,
  `<summary>` XML en los tipos públicos, miembros con cuerpo de expresión cuando son cortos.
  Identificadores y comentarios de código en inglés.

## Assets

- Los materiales usan el prefijo `M_` (`M_<Categoría>_<Variante>`); los de blockout están en
  `Materials/Blockout/`. Los prefabs van en la carpeta `Prefabs/<Categoría>/` correspondiente.
- El entorno de CEDI Medellín (terreno, barrios, río, metro, zona industrial, ciudad, árboles de
  terreno, sol, niebla, Global Volume) lo genera `NovaMarket.Editor.CediEnvironmentBuilder`
  (menú NOVAMARKET → Environment; el código está repartido en partes `.Infrastructure` y `.Urban`). Para
  cambiarlo, ajusta el builder y reconstruye; no edites a mano `Terrain/`, `Models/Generated/` ni
  `Materials/Environment/`.
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
