using System.Collections.Generic;
using System.Threading.Tasks;
using Engine;
using Game;
using GameEntitySystem;

namespace GiganteCode
{
    // Registra el spawn natural de los tres gigantes:
    // GigantePequeno, GigantePequenoCanibal y GigantePequenoMuerto.
    // Esta clase REEMPLAZA al GiganteCode.dll original (ya registra los tres).
    public class GiganteSpawnLoader : ModLoader
    {
        private static readonly System.Random s_random = new System.Random();

        // Peso de aparicion del gigante muerto: 0.45 de dia y 0.75 de noche.
        // La noche empieza en 0.80 del ciclo y termina cuando empieza a amanecer (0.20).
        private const float PesoMuertoDia = 0.45f;
        private const float PesoMuertoNoche = 0.75f;
        private const float NocheInicio = 0.80f;
        private const float AmanecerInicio = 0.20f;

        private static bool EsDeNoche(float horaDelDia)
        {
            return horaDelDia >= NocheInicio || horaDelDia < AmanecerInicio;
        }

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
            ModsManager.RegisterHook("OnMinerHit", this);
        }

        // Aniquilador de gigantes: poder 20 contra los gigantes, el normal (4) contra lo demas.
        public override void OnMinerHit(
            ComponentMiner miner, ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection,
            ref float attackPower, ref float playerProbability, ref float creatureProbability,
            out bool Hitted)
        {
            Hitted = false;

            // Nauseas: el gigante canibal y el muerto tienen 25% de causar nauseas al jugador.
            // Solo se aplica si el golpe realmente le quita vida: si la armadura lo absorbe, no pasa nada.
            string atacante = miner.Entity.ValuesDictionary.DatabaseObject.Name;
            if (atacante == "GigantePequenoCanibal" || atacante == "GigantePequenoMuerto")
            {
                Entity objetivo = componentBody.Entity;
                ComponentHealth salud = objetivo.FindComponent<ComponentHealth>();
                ComponentSickness nauseas = objetivo.FindComponent<ComponentSickness>();
                if (salud != null && nauseas != null
                    && objetivo.FindComponent<ComponentPlayer>() != null
                    && s_random.NextDouble() < 0.25)
                {
                    float saludAntes = salud.Health;
                    Task.Delay(150).ContinueWith(tarea => Dispatcher.Dispatch(() =>
                    {
                        if (salud.Health < saludAntes)
                        {
                            nauseas.StartSickness();
                        }
                    }));
                }
            }

            if (Terrain.ExtractContents(miner.ActiveBlockValue) == AniquiladorGigantesBlock.Index)
            {
                string name = componentBody.Entity.ValuesDictionary.DatabaseObject.Name;
                if (name.StartsWith("GigantePequeno"))
                {
                    attackPower = AniquiladorGigantesBlock.PoderContraGigantes;
                }
            }
        }

        public override void InitializeCreatureTypes(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            SubsystemTerrain subsystemTerrain =
                subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
            SubsystemTimeOfDay subsystemTimeOfDay =
                subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTimeOfDay>(true);
            System.Random random = new System.Random();

            // nombre de plantilla, peso de spawn, min y max de criaturas por grupo
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequeno", () => 0.6f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoCanibal", () => 0.4f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoMuerto",
                () => EsDeNoche(subsystemTimeOfDay.TimeOfDay) ? PesoMuertoNoche : PesoMuertoDia,
                1, 2);
        }

        private static void AddGiant(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            SubsystemTerrain subsystemTerrain,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes,
            System.Random random,
            string templateName, System.Func<float> weight, int minCount, int maxCount)
        {
            SubsystemCreatureSpawn.CreatureType type =
                new SubsystemCreatureSpawn.CreatureType(
                    templateName, SpawnLocationType.Surface, true, false);

            // Adecuado si hay suelo solido debajo, aire donde aparece (2 bloques de alto) y no hay agua
            type.SpawnSuitabilityFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int below = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                int here = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
                int above = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z));

                if (below == WaterBlock.Index || here == WaterBlock.Index || above == WaterBlock.Index)
                {
                    return 0f;
                }
                if (!BlocksManager.Blocks[below].IsCollidable)
                {
                    return 0f;
                }
                if (BlocksManager.Blocks[here].IsCollidable || BlocksManager.Blocks[above].IsCollidable)
                {
                    return 0f;
                }
                return weight();
            };

            type.SpawnFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int count = random.Next(minCount, maxCount + 1);
                return subsystemCreatureSpawn.SpawnCreatures(creatureType, templateName, point, count).Count;
            };

            creatureTypes.Add(type);
        }
    }
}
