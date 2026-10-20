# Supply Chain Run: documento de diseño del juego (v2)
## El planeador dentro de la cadena completa

> Estado: **aprobado**. Guardado en `Assets/NOVAMARKET/Documentation/DISENO_JUEGO_V2.md`; Claude Code lo implementa por etapas. Los números marcados con *(calibrar)* son ilustrativos. Se ajustan con datos reales y con simulación.

> **Nombre:** el juego se llama **Supply Chain Run**. **NOVAMARKET** es la empresa de cosmética donde trabaja el jugador ("NOVAMARKET Cosmética · CEDI Medellín", con su NOVA ERP); también sigue siendo el nombre interno del proyecto (carpeta, repositorio y código).

---

## 0. Por qué hay una versión 2

En la primera prueba del nivel "Temporada de fin de año" salieron tres problemas:

1. **No enseñaba a calcular.** El cálculo estaba escondido detrás del botón "Sugerido del Atlas". El jugador solo decidía sobre números que ya venían hechos.
2. **Los eventos no eran realistas.** "La promo multiplica la demanda ×1,9" se aplicaba directo. En una empresa real, lo que dice Comercial se contrasta con la historia, se calcula y se negocia.
3. **No había jugabilidad.** Faltaba tensión: información incierta, gente con intereses distintos y decisiones con consecuencias.

La versión 2 corrige esto. **El jugador hace el trabajo real del planeador, con las herramientas reales, dentro de toda la cadena logística.**

---

## 0.1 Es un juego de roles

Al entrar, cada jugador **escoge su rol**. Los roles son los **eslabones del Mapa de la cadena del Atlas** (pestaña 00): cada eslabón es un rol con sus herramientas, sus KPI (los del catálogo del Atlas para esa categoría) y sus mundos.

| Carril del mapa | Eslabón = rol | Qué cubre | Estado |
|---|---|---|---|
| **PLANEAR** | **Planeador** | Pronóstico → Gestión de la demanda → Materiales (MRP · DDMRP) | **Primero en construirse** |
| EJECUTAR | Abastecer | Compras, proveedores, OC, lead time (incluye la importación: logística internacional y aduanas) | Futuro |
| EJECUTAR | Inventarios | Stock y políticas de inventario | Futuro |
| EJECUTAR | Almacenar | Recibo, almacenamiento y picking en el CEDI | Futuro |
| EJECUTAR | Pedidos | Órdenes de venta y gestión de pedidos | Futuro |
| EJECUTAR | Transporte | Distribución (incluye el transporte nacional desde el puerto) | Futuro |
| EJECUTAR | Última milla | Entrega final al cliente | Futuro |
| RETORNAR | Logística inversa | Devoluciones y recuperación a inventario | Futuro |

**HABILITAR no es un rol jugable.** Finanzas, Riesgo y sostenibilidad, y Tecnología y datos son **personajes dentro del juego** que acompañan a todos los roles:
- **Finanzas:** Marcela (presupuesto, capital de trabajo, topes de compra).
- **Riesgo y sostenibilidad:** personaje que trae alertas de continuidad (proveedor en riesgo, CO₂e del modo de transporte). Nombre por definir.
- **Tecnología y datos:** personaje que administra el ERP y los datos maestros (LT, MOQ y unidades mal cargados; permisos y descargas). Nombre por definir.

**El rol del planeador sigue el mismo orden que el Atlas.** Cada uno de sus tres módulos es un mundo, y cada paso del flujograma del módulo es un nivel (ver sección 8).

Mientras un rol no esté disponible, sus personajes los maneja el juego (NPC). Por ejemplo, en la versión del planeador:
- Paula (Abastecer), con Mateo y Lucía para la importación.
- Wilmer (Transporte).
- Julián y Sofía (Almacenar e Inventarios).
- Marcela (Finanzas).
- Valeria (Comercial / cliente).

Son personajes del juego con quienes el planeador coordina y negocia. Cuando se construya el rol Abastecer, el jugador podrá ser Paula, y el planeador pasará a ser un NPC que le envía solicitudes de pedido. Así se cumple la flecha del mapa: "órdenes planificadas → SolPed / OC".

**Diseño técnico desde ya:** cada personaje se define como "rol" con sus datos y su lógica separados de la interfaz, de modo que más adelante un rol pueda pasar de NPC a jugable sin rehacer el motor. A futuro, varios jugadores podrían jugar roles distintos en la misma cadena (modo multijugador), pero eso no está dentro del alcance actual.

**Todo este documento describe el rol del planeador.**

---

## 1. Principios de diseño

1. **El jugador calcula; el juego valida.** Ningún número llega masticado. El flujo siempre es dato crudo en el ERP → descarga → cálculo en la hoja del planeador → decisión → negociación → consecuencia.
2. **Cada personaje tiene un rol real, un incentivo y un sesgo.** Comercial quiere vender, Finanzas quiere caja, Compras quiere precio y el proveedor quiere quedar bien. El jugador aprende a leerlos con datos.
3. **La información es incierta.** Las fechas prometidas no siempre se cumplen, el inventario del sistema no siempre es el físico y las estimaciones de Comercial tienen sesgo.
4. **Andamiaje.** Las fórmulas vienen guiadas, luego con casillas vacías y luego libres. La ayuda del Atlas siempre existe, pero **cuesta**.
5. **Medición continua con los KPI del Atlas.** El error del pronóstico, el servicio y el costo se miden cada semana, y hay game over si se descontrolan.
6. **Todo se ve en la escena.** Las decisiones se convierten en barcos, camiones, aduana, muelles y estibas en el CEDI de Medellín, con la estética base de Age of Empires III.

---

## 2. El rol del jugador y la semana tipo

El jugador es el **planeador de demanda y abastecimiento del CEDI Medellín**. Cada semana del juego es un **mini ciclo de S&OP**. Los pasos se desbloquean por mundos: el mundo 1 solo tiene los pasos 1 y 2, y el nivel final los tiene todos.

| Paso | Día | Qué hace el jugador | Herramienta | Concepto del Atlas |
|---|---|---|---|---|
| 1. Descargar y medir | Lunes | Descarga del ERP las ventas reales contra el pronóstico y calcula el error | ERP → hoja del planeador | WAPE, bias, tracking signal, MAD |
| 2. Pronosticar | Martes | Escoge el método y los parámetros, y ve el ajuste contra la historia | Hoja del planeador (laboratorio de pronóstico) | Promedio móvil, SES, Holt, Holt-Winters, índice estacional, CV, R² |
| 3. Consenso | Miércoles | Reunión con Comercial y Finanzas: analiza la evidencia, negocia y acuerda el plan de demanda | Sala de reuniones + historial del ERP | FVA, sesgo comercial, ABC-XYZ |
| 4. Plan de abastecimiento | Jueves | Calcula el stock de seguridad, el punto de reorden y el pedido, y crea la solicitud de pedido en el ERP | Hoja del planeador + ERP | SS, ROP, EOQ, MOQ, posición de inventario |
| 5. Coordinación con la cadena | Viernes | Compras negocia con el proveedor. Se gestionan las excepciones de la cadena: modo de transporte, aduana, transporte nacional, recibo | Tablero de embarques + diálogos | Lead time por etapas, variabilidad del LT, costo de expeditar |
| 6. Cierre de semana | — | La simulación corre: llegan camiones, se vende, se cobran costos y se actualizan los KPI | Escena 3D + HUD | Servicio, costo total, ocupación |

**Ritmo:** cada paso es un panel corto, de 1 a 3 minutos. Los niveles iniciales duran **6 a 8 semanas**; el nivel final, 16.

---

## 3. La cadena y sus personajes

| Personaje | Rol | Qué hace en el juego | Información que aporta | Sesgo / personalidad | Decisiones del jugador |
|---|---|---|---|---|---|
| **Andrés** | Director de S&OP | Mentor y tutor. Explica las fórmulas en los niveles guiados y evalúa el desempeño | Retroalimentación, lecciones | Exigente pero justo | — |
| **Valeria** | Comercial / ventas | Trae promociones, clientes nuevos y la competencia | Su estimación de venta | **Optimista**: sobreestima entre **5 % y 50 %**, según el evento. Presiona para tener inventario, pero **si el jugador sustenta con datos, accede** | Aceptar, contraofertar con evidencia o escalar a Andrés |
| **Marcela** | Finanzas | Controla el presupuesto de compras y el capital de trabajo | Topes de compra, costo de capital | **Conservadora**: recorta cuando la cobertura sube | Priorizar SKU con el tope, justificar el inventario |
| **Paula** | Compras y abastecimiento | Convierte las solicitudes en órdenes de compra, negocia precio, MOQ y fechas con los proveedores, y hace seguimiento | Opina con base en lead time, inventario en sitio, MOQ y confiabilidad del proveedor | Sus restricciones (MOQ, consolidación, precio, fechas) **dependen del proveedor y de la negociación**: cada proveedor del juego tiene su propia ficha de condiciones, y algunas se pueden negociar | Aceptar su consolidación o partir entregas; pedir que expedite |
| **Don Hernán** | Proveedor importado (perfume) | Produce, confirma fechas y declara la carga lista | Fecha prometida, avance de producción | **Optimista con las fechas**: avisa tarde los retrasos | Exigir confirmación, buscar un proveedor alterno |
| **Proveedores nacionales** | Labial (proveedor nacional), set (maquila local), crema (planta propia) | Entregas nacionales con LT corto | Capacidad y fechas | Más confiables, con capacidad limitada en temporada | Reservar capacidad con anticipación |
| **Mateo** | Logística internacional | Desde que el proveedor declara la carga lista: recogida, traslado a puerto u aeropuerto de origen, reserva con la naviera o aerolínea, tránsito y llegada a destino. Cotiza y gestiona el modo que el jugador escoja (marítimo, aéreo o courier/paqueteo) | Cotizaciones de tiempo y costo por modo, estado del embarque, Incoterm | Pragmático. Avisa los riesgos (congestión en puerto, falta de espacio en el buque) | **Escoger el modo de transporte**, consolidar o despachar ya |
| **Lucía** | Aduanas (agencia de aduanas) | Revisa documentos, presenta la declaración de importación ante la DIAN, atiende la inspección y obtiene el levante | Estado de documentos, tipo de inspección, días estimados | Rigurosa. Si los documentos llegan tarde o con errores, la mercancía se queda en puerto | Enviar documentos con anticipación, corregir errores, pagar trámites anticipados |
| **Wilmer** | Transporte nacional | Lleva la mercancía del puerto o aeropuerto de destino (Cartagena, Buenaventura o Rionegro) al CEDI Medellín | Disponibilidad de vehículos, estado de las vías, citas de cargue | Realista: cierres de vía, falta de vehículos en temporada | Vehículo dedicado o carga consolidada, cambiar de ruta |
| **Julián** | Jefe del CEDI | Recibo, almacenamiento y despacho. Administra los muelles, las posiciones y los montacargas | Capacidad de recibo por día, ocupación | Se queja si llegan demasiados camiones el mismo día | Agendar citas de recibo, escalonar llegadas |
| **Sofía** | Control de inventarios | Conteos cíclicos y exactitud del inventario | Diferencias entre el sistema y el físico | Meticulosa | Ajustar el inventario, investigar causas |

### 3.1 El lead time deja de ser un número fijo

El lead time de una importación **depende del origen y de la modalidad de transporte**, y se descompone en las mismas fases que se siguen en el ERP (sección 4.1):

```
LT total = producción + carga lista → recogida (según Incoterm) + puerto/aeropuerto origen
         + tránsito internacional + llegada a destino + aduana (levante)
         + transporte nacional + recibo y revisión de calidad en el CEDI
```

La simulación trabaja en días y luego los agrupa en semanas de juego.

#### Tránsito internacional por origen y modalidad (días, puerto a puerto o aeropuerto a aeropuerto)

| Origen | Marítimo FCL | Marítimo LCL (consolidado) | Aéreo | Courier / paqueteo | Puerto de entrada típico |
|---|---|---|---|---|---|
| **China** (Shanghai, Ningbo, Shenzhen) | 25–35 | 30–40 | 3–6 | 3–6 | Buenaventura (Pacífico) o Cartagena (vía Panamá) |
| **Japón** (Yokohama, Tokio) | 28–38 | 33–43 | 3–6 | 3–6 | Buenaventura |
| **Estados Unidos** (Miami, Houston, Nueva York) | 4–8 | 6–10 | 1–2 | 2–3 | Cartagena |
| **Europa** (España, Francia, Países Bajos) | 18–30 | 25–36 | 2–4 | 3–5 | Cartagena |
| **Nacional** (proveedor en Colombia) | — | — | — | — | Directo por carretera, 1–3 días |

Fuentes de referencia para los tránsitos: rutas publicadas China–Colombia, incluida la ruta directa Shanghai–Buenaventura de ~25 días, y tiempos de navieras y transitarios. Son rangos típicos, no un itinerario real.

#### Las demás fases (días)

| Fase | Marítimo | Aéreo | Courier | Personaje |
|---|---|---|---|---|
| Producción hasta carga lista | Según el proveedor: 7–30 | igual | igual | Proveedor / Paula |
| Carga lista → recogida → puerto u aeropuerto de origen (depende del Incoterm: EXW la recoge el comprador, FOB la entrega el proveedor a bordo) | 3–7 | 2–4 | 1–2 | Mateo |
| Llegada a destino → descargue y disponibilidad para aduana | 2–4 | 1–2 | incluido | Mateo |
| **Aduana hasta el levante, sin problemas** | 2–4 | 1–3 | 1–2 (lo hace el courier) | Lucía |
| **Problema aduanero (no siempre ocurre)** | **+2 a +10** | **+2 a +10** | +1 a +5 | Lucía |
| Transporte nacional al CEDI Medellín | Buenaventura 1–2 · Cartagena 2–3 | Rionegro (aeropuerto José María Córdova) 0,5–1 | incluido | Wilmer |
| Recibo, desempaque y revisión de calidad hasta inventario disponible | 1–3 | 1–3 | 1–2 | Julián / Sofía |
| **Costo relativo del flete** | 1× | ~4–6× | ~8–10×, con límite de peso y volumen | |

**Puertos (experiencia de Carlos):** se usan **Buenaventura y Cartagena**. **Casi todo entra por Buenaventura**: en el juego es el puerto por defecto para el marítimo (~80 % de los embarques *(calibrar)*). Cartagena queda para algunos orígenes, como Estados Unidos o Europa por el Atlántico, o como alternativa cuando el jugador decide cambiar de puerto.

**Riesgos de las vías nacionales (experiencia de Carlos):** el transporte del puerto al CEDI se ve afectado por **protestas o paros** y por **cierres viales por derrumbes**. En el juego, Wilmer avisa y el jugador decide:
- **Esperar** a que abran la vía: los camiones quedan detenidos y se suman días variables *(calibrar, 1–5)*.
- **Tomar una ruta alterna más larga:** se suman menos días, pero sube el costo del flete.

Son eventos con probabilidad por embarque, más frecuentes en temporada de lluvias *(calibrar)*. En la escena 3D se ven como camiones detenidos en la vía y un pin rojo en el mapa.

**Regla de aduana (experiencia de Carlos):** el mayor impacto de los problemas aduaneros es sobre el lead time. Hay que sumarle **de 2 a 10 días**, de forma variable, y **no siempre se presentan**. En el juego, cada importación tiene una probabilidad de problema (documentos, inspección o permisos *(calibrar, ~20–30 %)*) y, si ocurre, suma un valor aleatorio entre 2 y 10 días.

**Comunicación con aduanas:** en la vida real se habla mucho con la agencia para saber si la mercancía ya está nacionalizada o para reportar el problema. En el juego:
- Lucía avisa cuando hay un problema y cuando sale el levante.
- El jugador puede **preguntarle el estado** en cualquier momento. Si confía en ella, le da una fecha estimada más precisa.
- El estado "nacionalizada / con levante" se ve en el seguimiento del ERP.

*(Los valores siguen siendo calibrables con simulación, pero ya parten de rangos reales.)*

Esto permite enseñar el stock de seguridad **con variabilidad del lead time**:

```
SS = Z · √( LT · σd² + d² · σLT² )
```

El jugador ve en el ERP el **historial de lead times reales** del proveedor (promesa contra real) y calcula la σLT.

---

## 4. El ERP del juego ("NOVA ERP")

Es un ERP simulado dentro del juego, inspirado en el flujo de un ERP real. Cada pantalla muestra en pequeño su **equivalente en SAP**, como dato educativo, porque el jugador puede usar ese conocimiento en el trabajo real. La interfaz y la marca son propias; no se copia la apariencia de ningún producto.

| Pantalla NOVA ERP | Para qué sirve | Equivalente SAP (referencia) |
|---|---|---|
| Maestro de materiales | Ver LT, MOQ, unidad de empaque, costo, proveedor | MM03 |
| Stock por almacén | Inventario disponible, bloqueado y en calidad | MMBE / MB52 |
| Historial de ventas | Ventas por semana y mes, con promociones marcadas | Informes de ventas / consumo |
| Situación de necesidades | Stock, entradas y salidas futuras por fecha | MD04 |
| Crear solicitud de pedido | El jugador crea la SolPed (SKU, cantidad, fecha requerida) | ME51N |
| Órdenes de compra | Paula convierte la SolPed en OC. Se ven las fechas confirmadas | ME21N / ME23N |
| Entregas pendientes | Seguimiento de las OC abiertas y su etapa en la cadena | ME2M / ME2N |
| **Seguimiento de importaciones** | Todas las fases de la OC, de la creación al inventario disponible (ver 4.1) | Transacción de seguimiento de importaciones (Z) que usaba Carlos |
| Facturación | La demanda real se mira en la facturación | Informes de facturación (VF05 / análisis de ventas) |
| Entradas de mercancía | Registro del recibo en el CEDI | MIGO |
| Historial de lead times | Fecha prometida contra fecha real, por proveedor y por etapa | Informes de compras |
| **Descargar a hoja** | Lleva cualquier consulta a la hoja del planeador, como una pestaña nueva, sin salir del juego | Exportar a hoja de cálculo |

### 4.1 Seguimiento de importaciones (pantalla estrella del ERP)

Es la transacción que Carlos usaba en su trabajo: sigue cada orden **desde que se crea la OC hasta que entra al inventario disponible**. En el juego es la pantalla principal del mundo 3 y la fuente de datos del tablero de embarques (sección 7.2).

**Columnas por línea de OC:**

| Grupo | Campos |
|---|---|
| Material | Material (código SKU), descripción, precio unitario, cantidad en unidades, cantidad en cajas |
| Origen | Proveedor, país de origen, Incoterm negociado, modalidad (marítimo FCL/LCL, aéreo, courier) |
| Fases (fecha plan contra fecha real) | 1. Creación de la OC · 2. Carga lista · 3. Recogida según el Incoterm · 4. Llegada a puerto u aeropuerto de origen · 5. Despacho / zarpe · 6. Tránsito (marítimo o aéreo) · 7. Llegada a puerto u aeropuerto de destino · 8. Levante (nacionalizada) · 9. Recogida por el transporte nacional · 10. Llegada al CEDI o planta · 11. Desempaque / revisión de calidad · 12. Ingreso a inventario disponible |
| Costos | Costo del envío (flete), **factor de importación**, costo puesto en CEDI |
| Estado | Fase actual, días de atraso contra el plan y semáforo |

**Factor de importación:**

```
Factor = (precio FOB + flete + seguro + aranceles + gastos de aduana + transporte nacional) ÷ precio FOB
```

Es el costo puesto en CEDI dividido entre el precio de compra. Por ejemplo, un factor de 1,35 significa que cada $100 de compra cuestan $135 puestos en el CEDI. En el juego enseña por qué traer por avión cuesta mucho más que el flete solo: sube el factor y, con él, el costo de cada caja.

**Usos en el juego:**
- **Mundo 3:** saber cuándo llega de verdad lo pedido (no la fecha prometida), calcular la σLT con el historial de fases y decidir si expeditar.
- **Calidad de datos:** fases sin fecha real, fechas plan que nunca se actualizaron, levante sin registrar.
- Descargable a la hoja del planeador, como todas las consultas.

### 4.2 Calidad de datos

**Calidad de datos (para enseñar):** el ERP a veces tiene datos malos que el jugador debe detectar antes de calcular:
- OC sin confirmar con fechas viejas.
- Inventario fantasma (lo que descubre Sofía).
- Ventas atípicas, como un pedido único de un cliente.
- Unidades que no corresponden (unidades contra cajas).

**El jugador nunca sale del juego.** Todo el cálculo pasa dentro de la hoja del planeador (sección 5), que se comporta como un Excel. La única salida opcional es un **reporte del nivel en CSV** al terminar, para quien quiera analizar su partida o un profesor que quiera revisar resultados. Nunca se usa a mitad del nivel.

---

## 5. La hoja del planeador (mecánica de cálculo)

Es un **Excel dentro del juego**: así el jugador no sale de la experiencia, y lo que aprende le sirve en su trabajo real.

**Se comporta como Excel:**
- Celdas con columnas (A, B, C…) y filas, y pestañas por consulta descargada del ERP.
- Fórmulas con la sintaxis de Excel en español: `=SUMA(B2:B9)`, `=PROMEDIO()`, `=ABS()`, `=RAIZ()`, `=REDONDEAR.MAS()`, `=SI()`, `=MAX()`, `=MIN()`, `=DESVEST()`, `=SUMAPRODUCTO()`, `=DISTR.NORM.ESTAND.INV()`.
- Referencias a celdas y arrastre para copiar una fórmula a toda la columna.
- Formato básico: números, %, decimales.
- Mini gráfica de línea para ver el real contra el pronóstico.

**El jugador escoge cómo calcular:**
- **Con tarjetas de fórmula** (modo aprendizaje): la fórmula del Atlas con casillas, como se describe abajo.
- **Con fórmulas de Excel** (modo experto): escribe sus propias fórmulas en las celdas.

En los dos modos, al final entrega el **resultado** en la celda marcada (por ejemplo "WAPE de la semana") y el juego lo valida. Lo que importa es que el número esté bien, no el camino.

**Bonus de habilidad:** si el jugador resuelve con fórmulas propias de Excel, gana una insignia de "Excel de planeador", porque esa es una habilidad real del puesto.

**Tarjetas de fórmula.** Cada tarjeta muestra:
- La fórmula del Atlas.
- Las casillas que el jugador llena.
- Los datos descargados del ERP a la izquierda.

**Niveles de andamiaje:**

| Modo | Qué ve el jugador | Dónde se usa |
|---|---|---|
| **Guiado** | La fórmula con la mayoría de casillas llenas. Andrés explica cada término y el jugador llena 1 o 2 | Primer nivel de cada mundo |
| **Casillas vacías** | La fórmula completa sin valores. Si se equivoca, se resalta la casilla mala con una pista | Niveles intermedios |
| **Libre** | Solo los datos y el resultado final que debe ingresar | Niveles finales y nivel jefe |

**Validación:** el resultado es correcto con una tolerancia de ±2 % *(calibrar)*, para no castigar los redondeos.

**Ayuda del Atlas:** botón "Pedir ayuda al Atlas". Muestra el cálculo resuelto paso a paso, pero **cuesta**: resta puntos de la estrella de "Cálculo propio" y queda registrado en el reporte final.

**Fórmulas por mundo:** el mundo 1 trae error y pronóstico; el mundo 2, inventario; el mundo 3, LT variable y costo de modos; el mundo 4, MRP; el mundo 5, DDMRP. La lista completa está en la sección 8.

---

## 6. Mecánica de consenso y negociación

Esta mecánica es el corazón de la jugabilidad.

### 6.1 Cómo funciona

1. Un personaje hace una **afirmación**, por ejemplo: *"La promo 2×1 va a duplicar la venta del labial."*
2. Cada afirmación tiene, por dentro:
   - El **valor dicho** (+100 %).
   - La **distribución real** del efecto, oculta y basada en la historia (por ejemplo +50 % ± 12 %).
   - La **evidencia disponible** en el ERP.
3. El jugador puede **investigar** con consultas al ERP: historial de promociones, estimaciones pasadas de Valeria contra lo real, inventario y LT.
4. El jugador **propone un número** y escoge **argumentos** (fichas de evidencia): "Historial de promos", "Costo del exceso", "Lead time de reposición", "Capacidad del CEDI", "Presupuesto".
5. El personaje responde según qué tan buenos son los argumentos y la relación. Puede aceptar, contraofertar o escalar a Andrés.
6. Se cierra un **acuerdo**, que puede llevar condiciones. Por ejemplo: "Si a mitad de la promo la venta va más alta, reponemos; el labial tiene LT de 1 semana."
7. La demanda real sale de la distribución real, no de lo que dijo el personaje.

### 6.2 Ejemplo de diálogo (nivel de consenso, mundo 1)

> **Valeria:** "Arrancamos la promo 2×1 del labial en la semana 6. Esto va a explotar: pide el doble."
>
> *El jugador abre el historial de promociones en el ERP:*
>
> | Promo | Valeria dijo | Real |
> |---|---|---|
> | Madres 2025 | +100 % | +45 % |
> | Amor y amistad 2025 | +80 % | +60 % |
> | Madres 2026 | +90 % | +38 % |
>
> *El jugador propone **+50 %** con los argumentos "Historial de promos" y "Lead time de reposición".*
>
> **Valeria:** "Mmm… las últimas no se vendieron como pensaba, tienes razón. Pero si nos quedamos sin labial en la promo, el cliente me mata."
>
> *El jugador ofrece la condición: "Revisamos la venta del lunes de la semana 6 y, si va por encima, pido el martes; llega en una semana."*
>
> **Valeria:** "Trato hecho."

### 6.3 Confianza por personaje

Cada personaje tiene un medidor de **confianza** (0–100):
- **Sube** cuando el jugador acierta con argumentos o cumple los acuerdos.
- **Baja** cuando ignora a alguien y se equivoca, o cuando incumple.
- Con confianza baja, el personaje **escala a Andrés** o **deja de compartir información temprano**. Por ejemplo, Don Hernán avisa los retrasos todavía más tarde.

### 6.4 FVA (valor agregado del pronóstico)

Cada semana se comparan tres pronósticos contra la venta real:
1. El estadístico (paso 2).
2. El de Comercial (lo que dijo Valeria).
3. El de consenso (lo que acordó el jugador).

El reporte muestra **quién agregó valor**. Este es el aprendizaje clave: la opinión comercial ayuda solo si se ajusta con datos.

---

## 7. Abastecimiento y seguimiento de la cadena

### 7.1 Ciclo de vida de una orden

```
SolPed (jugador) → OC (Paula) → confirmación del proveedor (fecha prometida)
→ carga lista (proveedor) → recogida → puerto/aeropuerto origen (Mateo)
→ tránsito → puerto/aeropuerto destino → aduana: documentos, declaración, inspección, levante (Lucía)
→ transporte nacional (Wilmer) → cita y recibo en el CEDI (Julián) → disponible para vender
```

### 7.2 Tablero de embarques

Es un tablero tipo rastreador, inspirado en WareTrack:
- **Una línea por OC**, con las etapas como hitos y la etapa actual resaltada.
- **Fecha prometida contra fecha estimada actual**, con semáforo.
- En la escena 3D, los embarques activos se ven como **pines** en el mapa (puerto, vía, CEDI).

### 7.3 Decisiones de excepción (ejemplos)

| Situación | Quién avisa | Opciones del jugador | Qué se aprende |
|---|---|---|---|
| Don Hernán se atrasa 2 semanas en producción | Paula (si la confianza es alta, avisa temprano) | Esperar · partir la orden · traer una parte en aéreo | Costo de expeditar contra venta perdida |
| La carga está lista y hay que escoger el modo | Mateo, con cotizaciones | Marítimo · aéreo · courier | Trade-off entre tiempo y costo, y efecto sobre el SS |
| Inspección física en aduana | Lucía | Esperar · pagar un trámite prioritario | Variabilidad del LT por aduana |
| Documentos con error (por ejemplo el permiso sanitario) | Lucía | Corregir (+días) · ya se habían enviado con anticipación (sin impacto) | Gestión documental preventiva |
| Protesta o paro en la vía Buenaventura–Medellín | Wilmer | Esperar (días variables) · ruta alterna más larga (+días, +costo) | Riesgo del transporte nacional |
| Cierre vial por derrumbe (más frecuente en temporada de lluvias) | Wilmer | Esperar a que habiliten la vía · ruta alterna más larga | Variabilidad del LT por la última fase |
| Llegan 4 camiones el mismo día | Julián | Reagendar citas · pagar horas extra | Capacidad de recibo y escalonamiento |
| Paula propone consolidar 3 pedidos por un descuento | Paula | Aceptar · rechazar · parcial | EOQ contra costo de mantener y espacio |

---

## 8. Mundos y niveles

Los mundos del planeador son **los tres módulos del carril PLANEAR del Atlas**. Los niveles siguen **los pasos de cada flujograma**, en el mismo orden. Lo que sale de un mundo entra al siguiente, igual que en el mapa: pronóstico → plan → órdenes planificadas.

**Mundo 1: Pronóstico** (historia → estimación). Entra la venta real; sale el pronóstico estadístico.

| Nivel | Paso del Atlas | Qué hace el jugador |
|---|---|---|
| 1.1 | p1 Reunir la historia | Descarga del ERP las ventas por SKU y por mes |
| 1.2 | p2 Limpiar los datos | Detecta ceros, atípicos y meses antes del lanzamiento |
| 1.3 | p3 Diagnosticar el patrón | Calcula CV, pendiente con R² e índices estacionales |
| 1.4 | p4 Elegir métodos candidatos | El patrón decide el método (decisión) |
| 1.5 | p5–p6 Calcular y optimizar | Pronóstico a un paso y búsqueda de α y β |
| 1.6 | p7–p8 Medir el error y seleccionar | WAPE, sesgo y tracking signal; escoge el método (decisión) |
| 1.7 | p9 Proyectar el horizonte | Entrega el pronóstico estadístico a Gestión de la demanda |

Personajes: Andrés (tutor) y Sofía (le pasa la data del ERP con problemas de calidad).

**Mundo 2: Gestión de la demanda** (plan aprobado). Entra el pronóstico estadístico; sale el plan de demanda aprobado.

| Nivel | Paso del Atlas | Qué hace el jugador |
|---|---|---|
| 2.1 | d1 Recibir el pronóstico estadístico | Revisa su error y su sesgo |
| 2.2 | d2 Segmentar ABC-XYZ | Asigna una política por segmento |
| 2.3 | d3 Enriquecer con información comercial | Promociones, lanzamientos y eventos: **negociación con Valeria** (sección 6) |
| 2.4 | d4 Consenso S&OP | Reunión con Comercial, Operaciones y Finanzas para acordar un número (decisión) |
| 2.5 | d5 Integrar los pedidos firmes | Consumo del pronóstico |
| 2.6 | d6 Prometer pedidos (ATP) | Qué se puede comprometer y cuándo |
| 2.7 | d7 Monitorear el plan | Cumplimiento, sesgo, alarmas y FVA |

Personajes: Valeria (Comercial), Marcela (Finanzas) y Andrés (dirige el S&OP).

**Mundo 3: Materiales** (MRP · DDMRP). Entra el plan aprobado; salen las órdenes planificadas, la SolPed y la OC.

| Nivel | Paso del Atlas | Qué hace el jugador |
|---|---|---|
| 3.1 | m1 Reunir las entradas | Plan maestro, BOM, inventario y parámetros desde el ERP |
| 3.2 | m2 Definir parámetros de inventario | Stock de seguridad (también con LT variable de la importación), punto de reorden y lote |
| 3.3 | m3–m4 Explotar la BOM y netear | Requerimientos brutos y netos (planta propia de crema) |
| 3.4 | m5–m6 Dimensionar el lote y desfasar | Lote a lote, cantidad fija, mínimo o EOQ; liberar según el LT |
| 3.5 | m7–m8 Proyectar y emitir | Inventario proyectado; crear SolPed y manejar las excepciones con la cadena (Paula, Mateo, Lucía, Wilmer, Julián) |
| 3.6 | b1–b3 DDMRP: buffers | Posicionar buffers y dimensionar las zonas TOR, TOY y TOG |
| 3.7 | b4–b6 DDMRP: ejecutar | Ajuste dinámico, flujo neto, órdenes y prioridad por % de penetración |

Personajes: Paula (Abastecer) y la cadena de importación, más Julián y Sofía (CEDI).

**Nivel jefe del planeador: "Temporada de fin de año".** Son 16 semanas y 4 SKU, y se recorre el carril PLANEAR completo cada semana (sección 2). El nivel que ya existe se convierte en este jefe final y se rehace con todos los pasos.

> Nota: los eventos de la cadena (retraso del proveedor, aduana, transporte, CEDI) aparecen sobre todo en el mundo 3, porque ahí el plan se convierte en órdenes y el planeador coordina con los otros eslabones. En los mundos 1 y 2 aparecen solo como contexto.

---

## 8.1 Sectores industriales y tamaño real del portafolio

**Al entrar, cada jugador escoge su rol (sección 0.1) y su sector industrial.** En la primera versión solo están habilitados **Planeador + Cosmética**. Los demás aparecen en el menú como "Próximamente", y se van incorporando sin cambiar el motor: el rol y el sector se cargan como datos.

Cada **sector industrial** es una campaña con su propia empresa, sus SKU, proveedores, orígenes, estacionalidad y eventos típicos. El rol del planeador (mundos 1–3) se juega en cada sector, y lo que cambia son los datos y los problemas. Se construye **primero cosmética completa**; los demás sectores se agregan después como paquetes de datos, sin rehacer el motor.

| Sector | Portafolio real (SKU) | Rasgos que enseña |
|---|---|---|
| **Cosmética** (primero) | **~200** | Promociones, temporadas (madres, amor y amistad, diciembre), sets de regalo, lanzamientos, importados de Europa y China |
| Repuestos de motos / autos | 2.000–5.000 *(en el juego, una muestra de ~500)* | Demanda intermitente (ADI, Croston), cola larga, ABC-XYZ, importados de China, India y Japón, costo de la moto parada |
| Alimentos / consumo masivo | ~300 | Perecederos, vencimientos, salida FEFO, cadena de frío, alta rotación |
| Farmacéutico | ~400 | Servicio cercano al 100 %, lotes, registros sanitarios, aduana más exigente |
| Tecnología / electrónica | ~150 | Ciclo de vida corto, lanzamientos, obsolescencia, mucho transporte aéreo |

### Cómo se juega con 200 SKU (planear a escala)

Con un portafolio real, el jugador **no calcula SKU por SKU a mano**, y tampoco lo hace un planeador de verdad. La jugabilidad cambia según la etapa:

1. **Aprender el cálculo con pocos SKU.** Los primeros niveles de cada mundo usan 2 a 5 SKU, y ahí el jugador hace cada cálculo en la hoja.
2. **Escalar con fórmulas.** En los niveles siguientes, la consulta del ERP trae los 200 SKU. El jugador arma la fórmula una vez en la hoja del planeador y la **arrastra** a todas las filas, igual que en Excel. El juego valida una muestra de filas.
3. **Gestionar por segmento.** Con ABC-XYZ, el jugador define **políticas por segmento**: método de pronóstico, nivel de servicio (Z), revisión y lote. No ajusta cada SKU.
4. **Gestionar por excepción.** Cada semana, el ERP marca solo los SKU que se salen de control: tracking signal fuera de ±4, cobertura baja, OC atrasada o exceso. El jugador revisa esos (5 a 15 por semana) y deja el resto en automático.
5. **Priorizar el tiempo.** Cada semana de juego tiene un **presupuesto de acciones**, como las horas del planeador. No alcanza para revisar todo, así que hay que escoger: ¿un A con TS fuera de control o un C agotado?

Así se enseña algo que en el trabajo real pesa tanto como las fórmulas: **dónde poner la atención**.

### Datos

- **Cosmética:** se parte de los 14 SKU reales del Atlas (ene-23 a ago-26). Se generan ~186 SKU sintéticos por familias (labiales, perfumes, cremas, sets, brochas, delineadores), con patrones derivados de los reales: estable, estacional, errático, intermitente, lanzamiento y descontinuado. La mezcla es realista: pocos SKU A con mucho volumen y una cola larga de C.
- Cada SKU sintético tiene su proveedor, origen, modalidad, LT, MOQ, unidad de empaque y costo, coherentes con su familia.
- Los demás sectores se generan con el mismo método a partir de perfiles por industria.
- Rendimiento: 200 SKU × 16 semanas es poco para el motor. La hoja del planeador debe mostrar las filas por páginas o con desplazamiento virtual, para que la interfaz no se ponga lenta.

### Origen y modalidad por defecto (cosmética)

| Familia | Origen | Modalidad por defecto | Puerto |
|---|---|---|---|
| Perfumes | Europa | Marítimo (aéreo como opción para expeditar) | Buenaventura (Cartagena como alternativa) |
| Sets de regalo, brochas | China | Marítimo FCL / LCL | Buenaventura |
| Labiales, cremas, delineadores | Nacional (proveedor, maquila o planta propia) | Carretera | — |

---

## 9. KPI, estrellas y game over

### 9.1 KPI que se miden

| Área | KPI |
|---|---|
| Demanda | WAPE, bias, tracking signal, FVA del consenso |
| Servicio | Nivel de servicio (fill rate), venta perdida |
| Inventario | Cobertura en semanas, ocupación del CEDI, exactitud del inventario |
| Abastecimiento | OTIF de proveedores, cumplimiento del LT por etapa |
| Costo | Mantener, desborde, pedidos, fletes (incluido el expedito), liquidación, venta perdida |
| Personas | Confianza por personaje |
| Aprendizaje | % de cálculos propios correctos sin la ayuda del Atlas |

### 9.2 Estrellas por nivel

Cada nivel define 3 objetivos. Por ejemplo, en el nivel 1.4 Consenso:
- ★ WAPE del consenso ≤ 25 % *(calibrar)* y |bias| ≤ 10 %.
- ★ FVA positivo: el consenso mejoró el pronóstico estadístico.
- ★ Cálculos propios ≥ 90 % sin usar el Atlas.

### 9.3 Game over (se repite el nivel con una semilla nueva)

- **Tracking signal fuera de ±4 durante 3 semanas seguidas.** El pronóstico tiene un sesgo sistemático.
- **Servicio acumulado < 85 %** después de la semana 4.
- **Desborde del CEDI 3 semanas seguidas.**
- **Presupuesto agotado** (Marcela congela las compras).

La pantalla de game over muestra:
- El KPI que falló y su evolución en una gráfica.
- La explicación en lenguaje sencillo.
- La sección del Atlas que hay que repasar, con un enlace.
- Lo que habría pasado con otra decisión clave.

---

## 10. Qué se reutiliza de lo que ya está construido

| Ya existe | Qué pasa con eso |
|---|---|
| Escena CEDI Medellín, terreno, ciudad, cámara RTS | Se mantiene. Se agrega la ruta visual del puerto al CEDI (y aeropuerto de Rionegro) para los pines |
| Motor de planeación en C# + 145 pruebas | Se mantiene como núcleo y se extiende: LT por etapas, eventos con distribución oculta, confianza, FVA |
| Camiones y montacargas | Se mantienen. Ahora vienen del transporte nacional y de las citas de recibo |
| HUD | Se mantiene. Se agregan los botones ERP, Hoja del planeador, Reunión y Embarques |
| Panel "Plan de pedidos" | Se reemplaza por el flujo ERP → hoja → SolPed |
| Diálogos de personajes | Se amplían a la mecánica de negociación con argumentos |

---

## 11. Primer entregable propuesto (vertical slice)

Antes de construir todo, se hacen **los dos primeros niveles del mundo 1 (Pronóstico)**, para probar si la jugabilidad funciona.

**Nivel 1.1 "Reunir la historia" y nivel 1.2 "Limpiar los datos" (pasos p1 y p2 del Atlas)**
- Sofía le pasa al jugador la consulta de ventas del ERP con problemas:
  - ceros que no son reales,
  - un pedido atípico de un cliente,
  - meses antes del lanzamiento,
  - un SKU en unidades mezclado con cajas.
- El jugador descarga la consulta a la hoja, detecta cada problema, lo corrige (imputa, excluye o convierte) y entrega la serie limpia.
- Andrés valida: si la serie quedó mal, muestra cómo eso dañaría el pronóstico más adelante.
- **Estrellas:** encontrar todos los problemas · corregirlos bien · sin ayuda del Atlas.
- **Por qué empezar aquí:** prueba el ERP, la hoja tipo Excel, los personajes y la validación, que son la base de todos los niveles. Y es el primer paso real del trabajo del planeador.

Justo después se hace el nivel de medición del error, que es el paso p7 del Atlas (nivel 1.6 de la sección 8), en la versión pequeña que se describe a continuación. Ahí entra el game over por tracking signal.

**Nivel 1.6 "Medir el error" (versión pequeña)**
- **Duración:** 6 semanas, con 2 SKU (labial y crema).
- **Personajes:** Andrés (tutor) y Valeria (presiona porque "el pronóstico siempre falla").
- **Flujo semanal:**
  1. Abrir NOVA ERP → historial de ventas → descargar a la hoja.
  2. Calcular el WAPE, el bias y el tracking signal de la semana, en modo guiado las semanas 1–2 y con casillas vacías las semanas 3–6.
  3. Andrés interpreta el resultado y Valeria opina.
  4. Si el TS sale de ±4, el jugador debe ajustar el pronóstico (subir o bajar el nivel) y justificarlo.
- **Game over:** TS fuera de ±4 durante 3 semanas.
- **Estrellas:**
  - ★ Cálculos correctos.
  - ★ Sin ayuda del Atlas.
  - ★ El bias termina dentro de ±5 %.
- **Hoja del planeador básica**: celdas, `=SUMA`, `=PROMEDIO`, `=ABS`, referencias y arrastre, más las tarjetas de WAPE, bias y TS.

Si este nivel se siente bien, se construye el resto del mundo 1 y luego los demás.

---

## 12. Decisiones tomadas con la experiencia de Carlos

| Tema | Decisión |
|---|---|
| Lead times | Dependen del **origen** (China, Japón, Estados Unidos, Europa, nacional) y de la **modalidad** (FCL, LCL, aéreo, courier). Tablas en la sección 3.1, con rangos reales investigados |
| Sesgo comercial | Varía entre **5 % y 50 %** de sobreestimación en volumen, según el evento y el SKU. Si el jugador **sustenta con datos**, Comercial accede |
| Compras | Las restricciones (MOQ, consolidación, precio, fechas) **dependen del proveedor y de la negociación**. Cada proveedor tiene su propia ficha de condiciones |
| Puertos y vías | Buenaventura y Cartagena; **casi todo entra por Buenaventura**. Protestas y derrumbes en las vías nacionales hacen esperar a los camiones o tomar rutas alternas más largas |
| Aduanas | El impacto principal es en el lead time: **+2 a 10 días** variables cuando hay problema, y no siempre lo hay. Comunicación frecuente con la agencia para saber si la mercancía ya está nacionalizada o para reportar el problema |
| ERP | Demanda = **facturación**. Inventario. **Seguimiento de importaciones** con todas sus fases, cantidades en unidades y cajas, costo del envío y factor de importación (sección 4.1) |
| Nombres | Aprobados: Paula, Mateo, Lucía y Wilmer. Faltan los nombres de los personajes de Riesgo y sostenibilidad, y de Tecnología y datos |

### Pendiente
- ~~Origen por SKU~~: decidido (sección 8.1).
- Calibrar con simulación la probabilidad de problema aduanero y los rangos de producción por proveedor.
