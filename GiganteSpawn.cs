using System.Collections.Generic;
using System.Threading.Tasks;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace GiganteCode
{
    // Registra el spawn natural de los tres gigantes (GigantePequeno, GigantePequenoCanibal
    // y GigantePequenoMuerto), las nauseas del canibal y el muerto, y el lanzamiento de lanzas.
    public class GiganteSpawnLoader : ModLoader
    {
        private static readonly System.Random s_random = new System.Random();

        // ---- Lanzas de madera ----
        private const float RangoMinimo = 3f;        // mas cerca que esto pelean cuerpo a cuerpo
        private const float RangoMaximo = 20f;       // distancia maxima de lanzamiento
        private const float VelocidadLanza = 22f;

        // ---- Mutacion del gigante muerto en luna llena (como el hombre lobo) ----
        // Fase de luna llena (0 a 7). Si en tu mundo la luna llena es otra fase, cambia este numero.
        private const int LunaLlena = 4;
        private static bool s_mutado;                 // true: es de noche y hay luna llena
        private static Texture2D s_texturaMuerto;
        private static Texture2D s_texturaMutante;

        // Gigantes muertos cuyo botin ya se solto
        private static readonly HashSet<Entity> s_botinEntregado = new HashSet<Entity>();

        private static int s_generacion;
        // Gigantes que ya lanzaron su unica lanza (no vuelven a tener hasta recargar el mundo)
        private static readonly HashSet<Entity> s_sinLanza = new HashSet<Entity>();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
            ModsManager.RegisterHook("OnMinerHit", this);
        }

        // Nauseas: el gigante canibal y el muerto tienen 25% de causar nauseas al jugador.
        // Solo se aplica si el golpe realmente le quita vida: si la armadura lo absorbe, no pasa nada.
        public override void OnMinerHit(
            ComponentMiner miner, ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection,
            ref float attackPower, ref float playerProbability, ref float creatureProbability,
            out bool Hitted)
        {
            Hitted = false;

            string atacante = miner.Entity.ValuesDictionary.DatabaseObject.Name;
            if (atacante == "GigantePequenoCanibal" || atacante == "GigantePequenoMuerto")
            {
                Entity objetivo = componentBody.Entity;
                ComponentHealth salud = objetivo.FindComponent<ComponentHealth>();
                ComponentSickness nauseas = objetivo.FindComponent<ComponentSickness>();
                if (salud != null && nauseas != null
                    && objetivo.FindComponent<ComponentPlayer>() != null
                    && s_random.NextDouble() < (atacante == "GigantePequenoMuerto" && s_mutado ? 0.5 : 0.25))
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
        }

        public override void InitializeCreatureTypes(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            SubsystemTerrain subsystemTerrain =
                subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
            System.Random random = new System.Random();

            // nombre de plantilla, peso de spawn, min y max de criaturas por grupo
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequeno", 0.6f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoCanibal", 0.4f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoMuerto", 0.5f, 1, 2);

            IniciarLanzas(subsystemCreatureSpawn.Project);
        }

        private static void AddGiant(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            SubsystemTerrain subsystemTerrain,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes,
            System.Random random,
            string templateName, float weight, int minCount, int maxCount)
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
                return weight;
            };

            type.SpawnFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int count = random.Next(minCount, maxCount + 1);
                return subsystemCreatureSpawn.SpawnCreatures(creatureType, templateName, point, count).Count;
            };

            creatureTypes.Add(type);
        }

        // ---- Lanzamiento de lanzas ----
        // Cada medio segundo revisa a los gigantes que persiguen a alguien y, si el objetivo esta
        // a distancia, le lanzan su unica lanza de madera. El gigante muerto no tiene lanza.
        // La lanza queda en el suelo y el jugador puede recogerla.

        private static void IniciarLanzas(Project project)
        {
            s_generacion++;
            s_sinLanza.Clear();
            s_botinEntregado.Clear();
            ProgramarRevision(project, s_generacion);
        }

        private static void ProgramarRevision(Project project, int generacion)
        {
            Task.Delay(500).ContinueWith(tarea => Dispatcher.Dispatch(() =>
            {
                if (generacion != s_generacion)
                {
                    return;
                }
                try
                {
                    RevisarGigantes(project);
                }
                catch (System.Exception)
                {
                    return; // el mundo se cerro: se detiene el ciclo
                }
                ProgramarRevision(project, generacion);
            }));
        }

        private static void RevisarGigantes(Project project)
        {
            SubsystemProjectiles proyectiles = project.FindSubsystem<SubsystemProjectiles>(true);
            SubsystemSky cielo = project.FindSubsystem<SubsystemSky>(true);
            bool noche = cielo.SkyLightIntensity < 0.3f;
            bool mutado = noche && cielo.MoonPhase == LunaLlena;
            s_mutado = mutado;

            if (s_texturaMuerto == null)
            {
                s_texturaMuerto = ContentManager.Get<Texture2D>("Textures/Creatures/GigantePequenoMuerto");
                s_texturaMutante = ContentManager.Get<Texture2D>("Textures/Creatures/GigantePequenoMutante");
            }

            foreach (Entity entidad in project.Entities)
            {
                string nombre = entidad.ValuesDictionary.DatabaseObject.Name;

                // Gigante muerto: de noche corre mas y pega mas fuerte. En luna llena MUTA:
                // textura de mutante (ojos rojos), mas vida, mas rapido y mas dano. Al amanecer vuelve a la normalidad.
                // Todo se recalcula cada vez, asi que no hace falta guardar estado.
                if (nombre == "GigantePequenoMuerto")
                {
                    ComponentLocomotion mov = entidad.FindComponent<ComponentLocomotion>();
                    ComponentMiner mano = entidad.FindComponent<ComponentMiner>();
                    ComponentHealth vida = entidad.FindComponent<ComponentHealth>();
                    ComponentCreatureModel modelo = entidad.FindComponent<ComponentCreatureModel>();

                    // Si acaba de morir, suelta su botin (normal o de mutado) una sola vez
                    if (vida != null && vida.Health <= 0f)
                    {
                        if (s_botinEntregado.Add(entidad))
                        {
                            SoltarBotinMuerto(project, entidad, mutado);
                        }
                        continue;
                    }
                    if (mov != null) mov.WalkSpeed = mutado ? 6.0f : (noche ? 4.6f : 2.2f);
                    if (mano != null) mano.AttackPower = mutado ? 9f : (noche ? 6f : 3f);
                    if (vida != null) vida.AttackResilience = mutado ? 600f : 150f;
                    if (modelo != null)
                    {
                        Texture2D textura = mutado ? s_texturaMutante : s_texturaMuerto;
                        if (modelo.TextureOverride != textura)
                        {
                            modelo.TextureOverride = textura;
                        }
                    }
                    continue;
                }

                // Solo el gigante normal y el canibal tienen lanza; el muerto la perdio
                if (nombre != "GigantePequeno" && nombre != "GigantePequenoCanibal")
                {
                    continue;
                }
                if (s_sinLanza.Contains(entidad))
                {
                    continue;
                }

                ComponentCreature gigante = entidad.FindComponent<ComponentCreature>();
                ComponentChaseBehavior persecucion = entidad.FindComponent<ComponentChaseBehavior>();
                if (gigante == null || persecucion == null || persecucion.Target == null)
                {
                    continue;
                }

                ComponentBody cuerpo = gigante.ComponentBody;
                ComponentBody cuerpoObjetivo = persecucion.Target.ComponentBody;
                Vector3 origen = cuerpo.Position + new Vector3(0f, cuerpo.BoxSize.Y * 0.75f, 0f);
                Vector3 destino = cuerpoObjetivo.Position + new Vector3(0f, cuerpoObjetivo.BoxSize.Y * 0.6f, 0f);
                Vector3 direccion = destino - origen;
                float distancia = direccion.Length();
                if (distancia < RangoMinimo || distancia > RangoMaximo)
                {
                    continue;
                }
                direccion /= distancia;

                // Compensa la caida por gravedad (aprox. 10 m/s^2) para que la lanza llegue al objetivo
                float tiempo = distancia / VelocidadLanza;
                Vector3 velocidad = direccion * VelocidadLanza;
                velocidad.Y += 5f * tiempo;

                // Lanza de madera (dato 0 = lanza de madera)
                int lanza = Terrain.MakeBlockValue(SpearBlock.Index, 0, 0);
                proyectiles.FireProjectile(lanza, origen + direccion * 1.2f, velocidad, Vector3.Zero, gigante);

                s_sinLanza.Add(entidad); // ya uso su unica lanza
            }
        }

        // ---- Botin del gigante muerto (se suelta por codigo para poder variar segun la mutacion) ----
        // Normal:  cuero 1-2, carne podrida 0-1 y 1 alma de gigante.
        // Mutado:  50% de 1-2 diamantes, 5 de cuero, 12 de carne podrida y 3 almas de gigante.
        private static void SoltarBotinMuerto(Project project, Entity entidad, bool mutado)
        {
            SubsystemPickables objetos = project.FindSubsystem<SubsystemPickables>(true);
            ComponentBody cuerpo = entidad.FindComponent<ComponentBody>();
            Vector3 posicion = cuerpo.Position + new Vector3(0f, 0.8f, 0f);

            if (mutado)
            {
                if (s_random.NextDouble() < 0.5)
                {
                    Soltar(objetos, DiamondBlock.Index, s_random.Next(1, 3), posicion);
                }
                Soltar(objetos, LeatherBlock.Index, 5, posicion);
                Soltar(objetos, RottenMeatBlock.Index, 12, posicion);
                Soltar(objetos, AlmaGiganteBlock.Index, 3, posicion);
            }
            else
            {
                Soltar(objetos, LeatherBlock.Index, s_random.Next(1, 3), posicion);
                Soltar(objetos, RottenMeatBlock.Index, s_random.Next(0, 2), posicion);
                Soltar(objetos, AlmaGiganteBlock.Index, 1, posicion);
            }
        }

        private static void Soltar(SubsystemPickables objetos, int bloque, int cantidad, Vector3 posicion)
        {
            if (cantidad <= 0)
            {
                return;
            }
            Vector3 velocidad = new Vector3(
                (float)(s_random.NextDouble() - 0.5) * 3f, 3f, (float)(s_random.NextDouble() - 0.5) * 3f);
            objetos.AddPickable(Terrain.MakeBlockValue(bloque), cantidad, posicion, velocidad, null);
        }
    }
}
