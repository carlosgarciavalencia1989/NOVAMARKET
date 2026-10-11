namespace NovaMarket.Simulation
{
    /// <summary>Ids that link the CEDI scene objects with their data in <see cref="FacilityDirectory"/>.</summary>
    public static class CediFacilityIds
    {
        public const string Warehouse = "warehouse";
        public const string Gate = "gate";
        public const string Office = "office";
        public const string ContainerYard = "container-yard";
    }

    /// <summary>
    /// Example data for CEDI Medellín, level III (S&amp;OP), used until the simulation feeds the
    /// HUD. Every number here is illustrative; nothing is computed yet.
    /// </summary>
    public static class SampleOperationData
    {
        public const string DispatchObjectiveId = "dispatch-on-time";

        public static OperationState Create() =>
            new OperationState(CreateMission(), CreateResources(), new NotificationFeed(), CreateFacilities());

        public static MissionState CreateMission()
        {
            var mission = new MissionState(3, "Planeación S&OP", "Despachos completados", 8);
            mission.AddObjective(new Objective(DispatchObjectiveId, "Completa 8 despachos a tiempo", true, 8, 400));
            mission.AddObjective(new Objective("service-level", "Mantén el nivel de servicio sobre 95 %", true));
            mission.AddObjective(new Objective("storage-limit", "No superes 200 posiciones ocupadas", true));
            mission.AddObjective(new Objective("sop-meeting", "Revisa el plan S&OP con Andrés", false, 1, 150));
            mission.AddObjective(new Objective("import-container", "Recibe el contenedor de Don Hernán", false, 1, 200));
            mission.AddObjective(new Objective("forecast-bias", "Baja el sesgo del pronóstico a menos de 5 %", false, 1, 250));
            return mission;
        }

        public static ResourceState CreateResources() =>
            new ResourceState(occupiedPositions: 109, positionCapacity: 200, budget: 845_000_000,
                trucksInYard: 4, operators: 12, serviceLevel: 0.962, serviceLevelTarget: 0.95);

        public static FacilityDirectory CreateFacilities()
        {
            var directory = new FacilityDirectory();

            directory.Set(new FacilityInfo(CediFacilityIds.Warehouse, "Bodega principal", "CEDI Medellín · Jefe: Julián",
                FacilityKind.Warehouse,
                new[]
                {
                    new FacilityStat("Posiciones ocupadas", "109/200"),
                    new FacilityStat("Muelles de recibo", "3 (2 libres)"),
                    new FacilityStat("Muelles de despacho", "3 (1 libre)"),
                    new FacilityStat("Montacargas activos", "3/4"),
                    new FacilityStat("Pedidos en picking", "5")
                },
                new[]
                {
                    new FacilityOrder("replenish-picking", "Reabastecer picking"),
                    new FacilityOrder("cycle-count", "Conteo cíclico"),
                    new FacilityOrder("prioritize-dispatch", "Priorizar despachos")
                }));

            directory.Set(new FacilityInfo(CediFacilityIds.Gate, "Portería", "Control de ingreso y salida",
                FacilityKind.Gate,
                new[]
                {
                    new FacilityStat("Camiones en cola", "2"),
                    new FacilityStat("Ingresos hoy", "14"),
                    new FacilityStat("Salidas hoy", "11"),
                    new FacilityStat("Espera promedio", "12 min")
                },
                new[]
                {
                    new FacilityOrder("admit-truck", "Autorizar ingreso"),
                    new FacilityOrder("call-next", "Llamar siguiente"),
                    new FacilityOrder("check-documents", "Revisar documentos")
                }));

            directory.Set(new FacilityInfo(CediFacilityIds.Office, "Oficina administrativa", "Planeación y finanzas",
                FacilityKind.Office,
                new[]
                {
                    new FacilityStat("Ciclo S&OP", "Octubre · semana 2"),
                    new FacilityStat("Error del pronóstico (WAPE)", "18,4 %"),
                    new FacilityStat("Sesgo del pronóstico", "+3,1 %"),
                    new FacilityStat("Responsable", "Andrés (S&OP)")
                },
                new[]
                {
                    new FacilityOrder("review-forecast", "Revisar pronóstico"),
                    new FacilityOrder("call-sop-meeting", "Convocar S&OP"),
                    new FacilityOrder("finance-report", "Informe financiero")
                }));

            directory.Set(new FacilityInfo(CediFacilityIds.ContainerYard, "Patio de contenedores", "Importaciones · Don Hernán",
                FacilityKind.ContainerYard,
                new[]
                {
                    new FacilityStat("Contenedores en patio", "6"),
                    new FacilityStat("Por descargar", "2"),
                    new FacilityStat("Días promedio en patio", "3"),
                    new FacilityStat("Próximo arribo", "Jueves")
                },
                new[]
                {
                    new FacilityOrder("schedule-unload", "Programar descarga"),
                    new FacilityOrder("inspect", "Inspeccionar carga"),
                    new FacilityOrder("return-empty", "Devolver vacío")
                }));

            return directory;
        }
    }
}
