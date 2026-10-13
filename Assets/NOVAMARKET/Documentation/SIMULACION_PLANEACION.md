# Simulación de planeación: especificación del motor (Nivel "Temporada de fin de año")

Este documento describe el motor de planeación que ya se diseñó y validó en el prototipo web **Operación CEDI**. Hay que implementarlo en C# puro (sin Unity) en `Scripts/Simulation/`, con pruebas, y conectarlo al `OperationState` existente para que el HUD, los camiones y los montacargas reflejen las decisiones del jugador.

## 1. Idea del nivel

- El jugador planea **16 semanas** (septiembre a diciembre) para **4 SKU reales** del CEDI.
- Cada semana decide **cuántas cajas pedir de cada SKU**. Lo pedido llega después del lead time de cada proveedor.
- La demanda sale de la historia real de ventas, con estacionalidad (diciembre es el pico) y ruido aleatorio con **semilla**: al repetir el nivel, cambian la demanda y el momento de los eventos.
- Los personajes traen eventos (promociones, retrasos, daños, cliente nuevo, tope de compras). El pronóstico estadístico **no** conoce esos eventos: el jugador debe ajustar sus pedidos con esa información.
- Al final, el resultado del jugador se compara con una **política de referencia del Atlas**, que juega la misma temporada con la misma semilla.

## 2. SKU

Unidades en **cajas**. `cost` = costo por caja en COP. `lt` = lead time en semanas. `cv` = coeficiente de variación semanal de la demanda.

| id | Código | Nombre | u/caja | Costo/caja | LT | CV | MOQ | Costo por pedido | Proveedor |
|---|---|---|---|---|---|---|---|---|---|
| lab | CV10009 | Labial Maae coral 3,5 g | 48 | 432.000 | 1 | 0,18 | 0 | 250.000 | Proveedor nacional |
| per | CV10001 | Perfume Malizmor 75 ml | 12 | 960.000 | 3 | 0,26 | 24 | 900.000 | Don Hernán (importado) |
| set | CV10007 | Set regalo Lunaria Tribel | 6 | 270.000 | 2 | 0,40 | 0 | 250.000 | Maquila local |
| cre | CV10021 | Crema facial Rimiri 50 g | 24 | 528.000 | 2 | 0,20 | 0 | 250.000 | Planta propia |

### Historia mensual en unidades (ene-2023 a ago-2026, 44 meses; índice 0 = enero 2023)

```
lab: 7818,6795,6254,10709,10487,6038,7666,6843,9190,5148,12908,9931,7520,9867,7712,10713,7836,5739,6997,9458,11123,8365,8559,9757,6557,6931,5821,9533,10340,7903,5865,8886,7940,8329,5565,8026,8163,7887,9445,11898,9225,8650,12425,8952
per: 2227,1536,2080,2151,2041,3792,1983,1698,2386,2411,2632,4383,1823,2353,2237,1929,2241,3389,1932,1705,2045,2293,2591,3477,1524,1423,1617,1368,1395,2964,1561,1408,2272,1928,1725,1798,1391,1341,1748,1705,1581,2136,1386,1554
set: 39,61,61,67,197,103,48,52,142,83,179,249,46,93,54,84,213,129,82,82,237,97,195,400,75,83,65,116,260,160,85,108,271,103,259,396,53,126,95,125,364,236,136,174
cre: 2004,1857,1806,1923,2215,1434,1684,1778,1479,1656,2160,2033,1330,1400,1530,1517,1998,1386,1952,1642,1565,1912,1648,1462,2054,1273,1324,1309,1494,1789,1254,1525,1393,1033,1575,1604,1116,1546,1452,1251,1441,1227,1708,1115
```

## 3. Demanda

- Mes de cada semana: semanas 1–4 = septiembre, 5–8 = octubre, 9–12 = noviembre, 13–16 = diciembre, semana 17 = enero (solo para el cierre).
- **Media semanal (plan estadístico)** de la semana `w` = promedio de ese mes calendario en los años con venta > 0 ÷ 4,33 ÷ unidades por caja, redondeado a 1 decimal.
- **Capacidad de rack** de cada SKU = promedio de la media semanal de las semanas 1–16 × 5 semanas, redondeado.
- **Ruido**: `z[w]` normal estándar generado con la semilla, recortado a ±2,3.
- **Multiplicador de eventos** `mult[w]` (1 por defecto). Los eventos de demanda lo modifican (ver sección 6).
- **Demanda real** = `max(0, round(media[w] × mult[w] × (1 + cv × z[w])))`.

## 4. Paso semanal (orden exacto)

Al cerrar la semana `w`:
1. Por cada SKU con pedido `q > 0`: se agrega al tránsito `{sku, q, llega = w + lt}` y se suma el costo de pedido.
2. Se genera la demanda de la semana. **Vendido** = `min(en bodega, demanda)`. **Perdido** = demanda − vendido (venta perdida, no se acumula como pendiente).
3. Costos de la semana, con el inventario al cierre (`fin`):
   - Mantener: `fin × costo × 0,004` (0,4% semanal).
   - Desborde: `max(0, fin − capacidad) × costo × 0,012` (cajas en el piso: triple de costo).
   - Venta perdida: `perdido × costo × 0,5` (margen perdido).
4. Se borra el tope de compras de la semana, si había.
5. `semana = w + 1`. Llega todo lo del tránsito con `llega ≤ semana` y se suma a la bodega.
6. Si `semana > 16`, **cierre**: para cada SKU, `inventario = en bodega + en tránsito`, `permitido = round(3 × media de enero)`, `exceso = max(0, inventario − permitido)`, costo de liquidación = `exceso × costo × 0,10`.
7. Se aplican los eventos programados para la nueva semana.

### Estado inicial
- En bodega = `round(media[1] × 2)`.
- Para LT ≥ 2: en tránsito, una llegada por cada semana `k = 2 … lt` con `round(media[k])` cajas.

## 5. "Sugerido del Atlas" (también es la política de referencia)

Política de revisión periódica semanal con nivel objetivo:
- `F` = suma del plan estadístico de las semanas `w … w+L` (L+1 semanas; semanas > 17 usan la media de la semana 17).
- `σ = cv × media[w]`.
- `SS = Z × σ × √(L+1)`, con `Z = 1,65` (95% de nivel de servicio).
- `S = F + SS`.
- `Posición = en bodega + en tránsito`.
- `Pedido = max(0, ceil(S − Posición))`. Si `0 < pedido < MOQ`, se sube al MOQ.
- El juego debe mostrar este cálculo paso a paso al jugador cuando lo pida (es contenido educativo).
- **El sugerido no incluye los eventos anunciados.** Esa es la decisión que toma el jugador.

**Política de referencia**: juega la misma semilla, pide siempre el sugerido, elige la opción por defecto (índice 0) en cada evento y, si hay tope de compras, escala todos los pedidos proporcionalmente hasta cumplirlo.

## 6. Eventos (personajes)

Las semanas tienen una variación aleatoria de 0 o 1 según la semilla (`j`). Solo hay un evento por semana.

| Semana | Tipo | Personaje | Efecto |
|---|---|---|---|
| 3 | Estiba dañada | Julián (enojado) | Crema: pierde el 15% de lo que hay en bodega |
| 4+j | Promoción 2×1 | Valeria (feliz) | Labial: demanda ×1,9 en las semanas (4+j)+2 y (4+j)+3. Se anuncia antes |
| 6+j | Retraso del barco | Don Hernán (preocupado) | Perfume: todo lo que está en tránsito llega 2 semanas tarde. **Decisión**: 0 = esperar; 1 = traer la mitad por avión, que llega la semana siguiente y cuesta 4,5 M |
| 8 | Descuadre en conteo cíclico | Sofía (sorprendida) | Set: pierde el 25% de lo que el sistema decía tener |
| 9 | Cliente nuevo aprobado en S&OP | Andrés (feliz) | Crema: demanda ×1,35 en las semanas 11 a 16 |
| 10 | Tope de compras | Marcela (preocupada) | Esa semana el valor del pedido no puede superar `round(0,55 × Σ(media[10] × costo) / 1e6) × 1e6` |
| 12 | Competencia sin stock | Valeria (sorprendida) | Set: demanda ×1,5 en las semanas 13 a 16 |
| 15 | Aviso de cierre | Marcela | Informa la demanda de enero y la regla de liquidación |

Los multiplicadores de demanda se calculan al crear la temporada, así afectan igual al jugador y a la política de referencia. Los textos de los personajes deben explicar el evento y dar una pista del Atlas (por ejemplo: "Con lead time de 1 semana, lo que pidas en la semana X llega justo para la promo").

Después de cada semana, un personaje comenta el resultado. Prioridad: venta perdida > 5% (Valeria enojada), desborde (Julián enojado), más de 5 semanas de cobertura (Marcela preocupada), o semana limpia (Andrés feliz).

## 7. Resultado del nivel

- **Nivel de servicio** = total vendido / total demandado.
- **Costo total** = mantener + desborde + pedidos + venta perdida + flete aéreo + liquidación.
- **Estrellas**:
  - 0: servicio < 90%
  - 1: servicio entre 90% y 95%
  - 2: servicio ≥ 95% y costo ≤ 110% del costo de la referencia
  - 3: servicio ≥ 95% y costo ≤ costo de la referencia
- Recompensa: gema del nivel.

### Game over (sistema de niveles)
- **Durante el nivel**: nivel de servicio acumulado < 85% después de la semana 4, o desborde de bodega 3 semanas seguidas. El nivel termina y se repite con una semilla nueva.
- **Al final**: 0 estrellas = el nivel no se supera y se repite.
- En ambos casos se muestran el indicador que falló, la explicación y el paso del Atlas a repasar.

### Lecciones automáticas al final (ejemplos)
- Ventas perdidas de labial durante la promo: había que pedir más en la semana anterior.
- Ventas perdidas de perfume después del retraso: stock de seguridad con el lead time real.
- Costo de desborde: el tamaño del lote también depende del espacio.
- Costo de liquidación: bajar los pedidos después del pico (desactivar el DAF).
- Ventas perdidas de crema por el cliente nuevo: los acuerdos de S&OP se suman al plan a mano.
- Comparación con la referencia: "Le ganaste a la política del Atlas por X" o al contrario.

## 8. Balance validado

Simulando 200 temporadas en el prototipo:
- La política de referencia logra en promedio ~93% de servicio (porque no escucha los eventos).
- Un jugador que usa el sugerido y además ajusta por los eventos anunciados logra ~96% de servicio y ~18% menos costo que la referencia, con 3 estrellas en la mayoría de las temporadas.
- No pedir nada da ~16% de servicio.

Se deben replicar estas pruebas en C# (por ejemplo, 200 semillas) para confirmar que el balance se mantiene.

## 9. Conexión con la escena

- Los pedidos del jugador generan los camiones de recibo: cada llegada del tránsito = un camión hacia un muelle de recibo con su SKU y cantidad.
- Las ventas generan los turbos de despacho.
- Inventario en bodega por SKU → "Posiciones" del HUD y estibas visibles en los racks.
- Los eventos llegan como avisos y como ventana de diálogo con el personaje y sus opciones.
- Panel de pedidos por SKU: en bodega, en tránsito (con semana de llegada), plan de las próximas L+1 semanas, campo de cantidad, botón "Sugerido del Atlas" con el cálculo, y botón "Cerrar semana".
