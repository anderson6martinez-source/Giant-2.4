using System.Collections.Generic;
using System.Threading.Tasks;
using Engine;
using Game;
using GameEntitySystem;

namespace GiganteCode
{
    // Jabali salvaje, serpiente y erizo: aparicion natural, defensa entre los de su especie y veneno de la serpiente.
    // Las estadisticas (vida, dano, velocidad) estan en el .xdb (plantillas "JabaliSalvaje" y "Serpiente").
    //
    // JABALI SALVAJE: version mejorada del jabali. Es neutral: si le pegas, te ataca el y los jabalies de
    //                 alrededor (tambien los jabalies normales, plantilla "Boar").
    // SERPIENTE:      su mordida envenena al jugador. Si atacan a una, las serpientes cercanas la defienden.
    // ERIZO:          sus espinas (ComponentErizoEspinas.cs) lastiman a quien lo golpea. Los erizos cercanos tambien lo defienden.
    //
    // Archivo nuevo: sube este .cs junto a los demas; se compila todo junto en GiganteCode.dll.
    public class SalvajesLoader : ModLoader
    {
        // ---- plantillas (Name en el .xdb) ----
        private const string PlantillaJabali = "JabaliSalvaje";
        private const string PlantillaJabaliNormal = "Boar";      // jabali del juego: se defienden entre ellos
        private const bool JabaliNormalSeUne = true;              // false = solo los jabalies salvajes se ayudan entre si
        private const string PlantillaSerpiente = "Serpiente";
        private const string PlantillaErizo = "Erizo";

        // ---- aparicion ----
        private const float PesoJabali = 0.5f;
        private const int JabaliMin = 2, JabaliMax = 4;
        private const float PesoSerpiente = 0.45f;
        private const int SerpienteMin = 1, SerpienteMax = 2;
        private const float PesoErizo = 0.45f;
        private const int ErizoMin = 1, ErizoMax = 2;

        // ---- defensa entre ellos ----
        private const float RadioJabali = 22f;        // bloques a la redonda que oyen el chillido
        private const float RadioSerpiente = 14f;
        private const float RadioErizo = 12f;
        private const float RangoDefensa = 40f;       // hasta donde persiguen al agresor
        private const float TiempoDefensa = 30f;      // segundos maximos de persecucion

        // ---- veneno de la serpiente (la salud va de 0 a 1: 0.1 = 1 corazon) ----
        private const float VenenoPorTick = 0.05f;    // medio corazon por segundo
        private const double VenenoSegundos = 6.0;    // 6 s = 3 corazones en total
        private const double PausaVeneno = 1.0;

        private SubsystemCreatureSpawn creaturas;
        private SubsystemTime tiempo;
        private double proximaRevision;
        private readonly HashSet<Entity> suscritos = new HashSet<Entity>();
        private readonly Dictionary<ComponentCreature, double> venenoHasta = new Dictionary<ComponentCreature, double>();
        private readonly Dictionary<ComponentCreature, double> venenoSiguiente = new Dictionary<ComponentCreature, double>();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
            ModsManager.RegisterHook("OnMinerHit", this);
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnProjectDisposed", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
        }

        // ---------- aparicion ----------
        public override void InitializeCreatureTypes(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            SubsystemTerrain subsystemTerrain =
                subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
            System.Random random = new System.Random();

            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaJabali, PesoJabali, JabaliMin, JabaliMax);
            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaSerpiente, PesoSerpiente, SerpienteMin, SerpienteMax);
            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaErizo, PesoErizo, ErizoMin, ErizoMax);
        }

        private static void AgregarTipo(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            SubsystemTerrain subsystemTerrain,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes,
            System.Random random,
            string plantilla, float peso, int minimo, int maximo)
        {
            SubsystemCreatureSpawn.CreatureType type =
                new SubsystemCreatureSpawn.CreatureType(
                    plantilla, SpawnLocationType.Surface, true, false);

            // suelo solido debajo, 2 bloques de aire y sin agua (igual que los demas)
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
                return peso;
            };

            type.SpawnFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int cantidad = random.Next(minimo, maximo + 1);
                return subsystemCreatureSpawn.SpawnCreatures(creatureType, plantilla, point, cantidad).Count;
            };

            creatureTypes.Add(type);
        }

        public override void OnProjectLoaded(Project project)
        {
            creaturas = project.FindSubsystem<SubsystemCreatureSpawn>(true);
            tiempo = project.FindSubsystem<SubsystemTime>(true);
            proximaRevision = 0;
            suscritos.Clear();
            venenoHasta.Clear();
            venenoSiguiente.Clear();
        }

        public override void OnProjectDisposed()
        {
            creaturas = null;
            tiempo = null;
            suscritos.Clear();
            venenoHasta.Clear();
            venenoSiguiente.Clear();
        }

        // ---------- grupos ----------
        private static string NombrePlantilla(ComponentCreature c)
        {
            return c.Entity.ValuesDictionary.DatabaseObject.Name;
        }

        // grupo al que pertenece una criatura (null = no tiene)
        private static string Grupo(ComponentCreature c)
        {
            string plantilla = NombrePlantilla(c);
            if (plantilla == PlantillaJabali) return "jabali";
            if (JabaliNormalSeUne && plantilla == PlantillaJabaliNormal) return "jabali";
            if (plantilla == PlantillaSerpiente) return "serpiente";
            if (plantilla == PlantillaErizo) return "erizo";
            return null;
        }

        private static bool EstaViva(ComponentCreature c)
        {
            return c != null && c.ComponentHealth != null && c.ComponentHealth.Health > 0f && c.ComponentBody != null;
        }

        // ---------- actualizacion ----------
        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || tiempo == null) return;
            ActualizarVeneno();

            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + 0.5;

            // cada miembro avisa a los demas cuando lo atacan (se suscribe una sola vez)
            foreach (ComponentCreature criatura in creaturas.Creatures)
            {
                if (suscritos.Contains(criatura.Entity)) continue;
                if (Grupo(criatura) == null || criatura.ComponentHealth == null) continue;

                suscritos.Add(criatura.Entity);
                ComponentCreature victima = criatura;
                criatura.ComponentHealth.Injured += (Injury lesion) => AlSerAtacado(victima, lesion.Attacker);
            }
        }

        // alguien fue atacado: el mismo y los de su grupo que esten cerca atacan al agresor
        private void AlSerAtacado(ComponentCreature victima, ComponentCreature agresor)
        {
            if (creaturas == null || !EstaViva(agresor) || !EstaViva(victima)) return;

            string grupo = Grupo(victima);
            if (grupo == null || Grupo(agresor) == grupo) return; // fuego amigo: no se atacan entre ellos

            float radio = grupo == "jabali" ? RadioJabali : (grupo == "erizo" ? RadioErizo : RadioSerpiente);

            // la victima se defiende
            ComponentChaseBehavior propia = victima.Entity.FindComponent<ComponentChaseBehavior>();
            if (propia != null)
            {
                propia.Attack(agresor, RangoDefensa, TiempoDefensa, true);
            }

            foreach (ComponentCreature aliado in creaturas.Creatures)
            {
                if (aliado == victima || !EstaViva(aliado) || Grupo(aliado) != grupo) continue;
                if (Vector3.Distance(victima.ComponentBody.Position, aliado.ComponentBody.Position) > radio) continue;

                ComponentChaseBehavior persecucion = aliado.Entity.FindComponent<ComponentChaseBehavior>();
                if (persecucion == null) continue;
                if (persecucion.Target != null && EstaViva(persecucion.Target)) continue; // ya esta peleando

                persecucion.Attack(agresor, RangoDefensa, TiempoDefensa, true);
            }
        }

        // ---------- veneno de la serpiente ----------
        // Solo envenena si la mordida realmente quita vida: si la armadura la absorbe, no pasa nada.
        public override void OnMinerHit(
            ComponentMiner miner, ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection,
            ref float attackPower, ref float playerProbability, ref float creatureProbability,
            out bool Hitted)
        {
            Hitted = false;

            if (miner.Entity.ValuesDictionary.DatabaseObject.Name != PlantillaSerpiente) return;

            ComponentPlayer jugador = componentBody.Entity.FindComponent<ComponentPlayer>();
            if (jugador == null || jugador.ComponentHealth == null) return;

            ComponentPlayer victima = jugador;
            float saludAntes = victima.ComponentHealth.Health;
            Task.Delay(150).ContinueWith(tarea => Dispatcher.Dispatch(() =>
            {
                if (tiempo == null || !EstaViva(victima)) return;
                if (victima.ComponentHealth.Health < saludAntes)
                {
                    if (!venenoHasta.ContainsKey(victima))
                    {
                        venenoSiguiente[victima] = tiempo.GameTime + PausaVeneno;
                        Mensaje(victima, "¡Te envenenó una serpiente!");
                    }
                    venenoHasta[victima] = tiempo.GameTime + VenenoSegundos;
                }
            }));
        }

        private void ActualizarVeneno()
        {
            if (venenoHasta.Count == 0 || tiempo == null) return;
            double ahora = tiempo.GameTime;
            List<ComponentCreature> lista = new List<ComponentCreature>(venenoHasta.Keys);
            foreach (ComponentCreature v in lista)
            {
                if (!EstaViva(v) || ahora >= venenoHasta[v])
                {
                    venenoHasta.Remove(v);
                    venenoSiguiente.Remove(v);
                    continue;
                }
                if (ahora >= venenoSiguiente[v])
                {
                    venenoSiguiente[v] = ahora + PausaVeneno;
                    v.ComponentHealth.Injure(VenenoPorTick, null, false, "Veneno de serpiente");
                }
            }
        }

        private static void Mensaje(ComponentCreature victima, string texto)
        {
            ComponentPlayer jugador = victima as ComponentPlayer;
            if (jugador == null || jugador.ComponentGui == null) return;
            try
            {
                jugador.ComponentGui.DisplaySmallMessage(texto, new Color((byte)120, (byte)255, (byte)120), false, false);
            }
            catch (System.Exception e)
            {
                Log.Warning("Salvajes: no se pudo mostrar el mensaje: " + e.Message);
            }
        }
    }
}
