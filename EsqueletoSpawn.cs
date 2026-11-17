using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace GiganteCode
{
    // Esqueletos (aparecen en manada de 4 a 8) y zombis indigenas: estadisticas de dia y de noche.
    //
    // ESQUELETO   dia: 15 de vida, 0.5 de dano, 15 km/h     noche: 25 de vida, 1 de dano, 15 km/h, ojos rojos
    // ZOMBI INDIGENA  dia: 23 de vida, 1.5 de dano, 9 km/h  noche: 35 de vida, 2 de dano, 14 km/h, ojos rojos
    // INDIGENA (normal, plantilla "Indigena"): usa los valores del .xdb (18 de vida, 1.8 de dano, 12 km/h),
    //   sale en tribus de 2 a 4, se defiende con los de su tribu y caza presas terrestres (jabalies, etc.)
    //
    // La distancia a la que ven al jugador (15 bloques el esqueleto, 25 el indigena) y los sonidos
    // estan en el .xdb (el esqueleto no tiene sfx; el indigena usa los del gigante muerto).
    public class EsqueletoSpawnLoader : ModLoader
    {
        // ---- Esqueleto ----
        private const string PlantillaEsqueleto = "Esqueleto";   // Name de la plantilla del .xdb
        private const float PesoEsqueleto = 0.5f;                // frecuencia de aparicion
        private const int ManadaMin = 4;
        private const int ManadaMax = 8;

        private const float KmhAMs = 1f / 3.6f;                  // el juego usa metros por segundo

        // ---- Zombi indigena (plantilla "ZombiIndigena" del .xdb) ----
        // Se reconoce por nombre: cualquier plantilla o DisplayName que contenga "indigena".
        private const string PlantillaIndigena = "ZombiIndigena";
        private const string TextoIndigena = "indigena";
        private const float PesoIndigena = 0.4f;     // frecuencia de aparicion
        private const int GrupoIndigenaMin = 1;
        private const int GrupoIndigenaMax = 2;

        // ---- Indigena normal (plantilla "Indigena" del .xdb) ----
        // No es zombi: no tiene buff de noche ni ojos rojos; usa las estadisticas del .xdb.
        private const string PlantillaTribu = "Indigena";
        private const float PesoTribu = 0.5f;         // frecuencia de aparicion
        private const int TribuMin = 2;               // salen en tribus de 2 a 4
        private const int TribuMax = 4;
        private const bool TribuCazaPresas = true;    // cazan jabalies y otras presas terrestres
        private const float RadioCazaPresas = 20f;    // a que distancia buscan presas (bloques)

        // Vida (AttackResilience), dano (AttackPower) y velocidad (km/h) por tipo y momento del dia
        private const float EsqVidaDia = 15f, EsqDanoDia = 0.5f, EsqKmhDia = 15f;
        private const float EsqVidaNoche = 25f, EsqDanoNoche = 1f, EsqKmhNoche = 15f;
        private const float IndVidaDia = 23f, IndDanoDia = 1.5f;
        private const float IndVidaNoche = 35f, IndDanoNoche = 2f;
        private const float IndKmhDia = 9f, IndKmhNoche = 14f;

        // ---- Instinto de manada ----
        // Si atacan a uno, los de su grupo que esten cerca atacan al agresor.
        //   grupo "esqueleto": los esqueletos entre si
        //   grupo "indigena":  zombis indigenas + gigante muerto (y mutante) se defienden mutuamente
        //   los otros gigantes (pequeno, canibal, hielo, volador): sin manada
        private const float RadioManada = 25f;        // bloques a la redonda que oyen el grito
        private const float RangoDefensa = 40f;       // hasta donde persiguen al agresor
        private const float TiempoDefensa = 40f;      // segundos maximos de persecucion
        // Los indigenas salen a cazar a todos los gigantes menos al gigante muerto / mutante
        private const bool IndigenasCazanGigantes = true;  // los indigenas cazan a los otros gigantes (no muerto/mutante)
        private const bool OtrosGigantesSeDefienden = false; // los otros gigantes NO se ayudan ni entre su propia especie
        private const float RadioCazaGigantes = 25f;
        private const double PausaCaza = 1.0;         // cada cuanto buscan gigantes (segundos)
        private const string PrefijoGigantes = "Gigante";

        private const float LuzNoche = 0.3f;                     // SkyLightIntensity por debajo de esto = noche

        private class Base
        {
            public float Vida;
            public float Dano;
            public float Velocidad;
            public Texture2D Textura;
        }

        private Project proyecto;
        private SubsystemCreatureSpawn creaturas;
        private SubsystemSky cielo;
        private SubsystemTime tiempo;
        private double proximaRevision;
        private double proximaLimpieza;
        private double proximaCaza;
        private double proximaCazaPresas;
        private readonly HashSet<Entity> suscritos = new HashSet<Entity>();

        // valores originales (los de la plantilla) de cada criatura la primera vez que se la ve
        private readonly Dictionary<Entity, Base> bases = new Dictionary<Entity, Base>();
        // textura de ojos rojos por plantilla ("Textures/Creatures/<Plantilla>Ojos"); null = no existe
        private readonly Dictionary<string, Texture2D> texturasOjos = new Dictionary<string, Texture2D>();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
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

            // esqueletos: manada de 4 a 8
            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaEsqueleto, PesoEsqueleto, ManadaMin, ManadaMax);
            // zombis indigenas: grupos pequenos
            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaIndigena, PesoIndigena, GrupoIndigenaMin, GrupoIndigenaMax);
            // indigenas normales: tribus de 2 a 4
            AgregarTipo(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                PlantillaTribu, PesoTribu, TribuMin, TribuMax);
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

            // Igual que los gigantes: suelo solido debajo, 2 bloques de aire y sin agua
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
            proyecto = project;
            creaturas = project.FindSubsystem<SubsystemCreatureSpawn>(true);
            cielo = project.FindSubsystem<SubsystemSky>(true);
            tiempo = project.FindSubsystem<SubsystemTime>(true);
            proximaRevision = 0;
            proximaLimpieza = 0;
            proximaCaza = 0;
            proximaCazaPresas = 0;
            suscritos.Clear();
            bases.Clear();
            texturasOjos.Clear();
        }

        public override void OnProjectDisposed()
        {
            proyecto = null; creaturas = null; cielo = null; tiempo = null;
            suscritos.Clear();
            bases.Clear();
            texturasOjos.Clear();
        }

        // ---------- dia / noche ----------
        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || cielo == null || tiempo == null) return;
            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + 0.5;

            bool noche = cielo.SkyLightIntensity < LuzNoche;

            foreach (ComponentCreature criatura in creaturas.Creatures)
            {
                string plantilla = criatura.Entity.ValuesDictionary.DatabaseObject.Name;
                bool esqueleto = plantilla == PlantillaEsqueleto;
                bool indigena = !esqueleto && EsIndigena(plantilla, criatura.DisplayName);
                if (!esqueleto && !indigena) continue;

                ComponentHealth vida = criatura.ComponentHealth;
                ComponentLocomotion movimiento = criatura.ComponentLocomotion;
                ComponentCreatureModel modelo = criatura.ComponentCreatureModel;
                ComponentMiner mano = criatura.Entity.FindComponent<ComponentMiner>();
                if (vida == null || movimiento == null || modelo == null || mano == null) continue;
                if (vida.Health <= 0f) continue;

                // la primera vez que se ve, guarda sus valores originales (los del .xdb)
                Base original;
                if (!bases.TryGetValue(criatura.Entity, out original))
                {
                    original = new Base
                    {
                        Vida = vida.AttackResilience,
                        Dano = mano.AttackPower,
                        Velocidad = movimiento.WalkSpeed,
                        Textura = modelo.TextureOverride
                    };
                    bases[criatura.Entity] = original;
                }

                float nuevaVida, nuevoDano, nuevaVelocidad;
                if (esqueleto)
                {
                    nuevaVida = noche ? EsqVidaNoche : EsqVidaDia;
                    nuevoDano = noche ? EsqDanoNoche : EsqDanoDia;
                    nuevaVelocidad = (noche ? EsqKmhNoche : EsqKmhDia) * KmhAMs;
                }
                else
                {
                    // zombi indigena: de dia 23 de vida y 1.5 de dano; de noche 35 y 2
                    nuevaVida = noche ? IndVidaNoche : IndVidaDia;
                    nuevoDano = noche ? IndDanoNoche : IndDanoDia;
                    nuevaVelocidad = (noche ? IndKmhNoche : IndKmhDia) * KmhAMs;
                }

                vida.AttackResilience = nuevaVida;
                mano.AttackPower = nuevoDano;
                movimiento.WalkSpeed = nuevaVelocidad;

                // ojos rojos de noche; de dia, la textura original
                Texture2D textura = original.Textura;
                if (noche)
                {
                    Texture2D ojos = ObtenerOjos(plantilla);
                    if (ojos != null) textura = ojos;
                }
                if (textura != null && modelo.TextureOverride != textura)
                {
                    modelo.TextureOverride = textura;
                }
            }

            RevisarManadas();
            CazarPresas();

            // limpieza de criaturas que ya no existen (cada ~10 s)
            if (tiempo.GameTime >= proximaLimpieza)
            {
                proximaLimpieza = tiempo.GameTime + 10.0;
                var vivas = new HashSet<Entity>();
                foreach (ComponentCreature c in creaturas.Creatures) vivas.Add(c.Entity);
                var viejas = new List<Entity>();
                foreach (Entity e in bases.Keys)
                {
                    if (!vivas.Contains(e)) viejas.Add(e);
                }
                foreach (Entity e in viejas) bases.Remove(e);
                suscritos.IntersectWith(vivas);
            }
        }

        // ---------- instinto de manada ----------
        private static string NombrePlantilla(ComponentCreature c)
        {
            return c.Entity.ValuesDictionary.DatabaseObject.Name;
        }

        private static bool EsZombiGigante(string plantilla)
        {
            return plantilla == "GigantePequenoMuerto" || plantilla == "GigantePequenoMutante";
        }

        // grupo al que pertenece una criatura (null = no tiene manada)
        private static string Grupo(ComponentCreature c)
        {
            string plantilla = NombrePlantilla(c);
            // indigenas normales: se defienden solo entre ellos (la tribu)
            if (plantilla == PlantillaTribu) return "tribu";
            // esqueletos: se defienden solo entre ellos
            if (plantilla == PlantillaEsqueleto) return "esqueleto";
            // gigantes zombis (muerto y mutante) + zombis indigenas: se defienden mutuamente
            if (EsZombiGigante(plantilla) || EsIndigena(plantilla, c.DisplayName)) return "indigena";
            // los otros gigantes (pequeno, canibal, hielo, volador): no se defienden ni entre ellos
            // (cambia OtrosGigantesSeDefienden a true si algun dia quieres que se ayuden)
            if (OtrosGigantesSeDefienden && plantilla.StartsWith(PrefijoGigantes)) return "gigante";
            return null;
        }

        private static bool EstaViva(ComponentCreature c)
        {
            return c != null && c.ComponentHealth != null && c.ComponentHealth.Health > 0f && c.ComponentBody != null;
        }

        private void RevisarManadas()
        {
            // 1) cada miembro de una manada avisa a los demas cuando lo atacan (se suscribe una sola vez)
            foreach (ComponentCreature criatura in creaturas.Creatures)
            {
                if (suscritos.Contains(criatura.Entity)) continue;
                if (Grupo(criatura) == null || criatura.ComponentHealth == null) continue;

                suscritos.Add(criatura.Entity);
                ComponentCreature victima = criatura;
                // OJO: en 2.4 ComponentHealth.Attacked ya no se dispara nunca. El evento real (el mismo que usan
                // ChaseBehavior y HerdBehavior del juego) es ComponentHealth.Injured, que trae el Injury con el agresor.
                criatura.ComponentHealth.Injured += (Injury lesion) => AlSerAtacado(victima, lesion.Attacker);
            }

            // 2) los indigenas buscan gigantes (todos menos el muerto y el mutante) y los atacan
            if (!IndigenasCazanGigantes) return;
            if (tiempo.GameTime < proximaCaza) return;
            proximaCaza = tiempo.GameTime + PausaCaza;

            foreach (ComponentCreature indigena in creaturas.Creatures)
            {
                if (!EstaViva(indigena)) continue;
                if (!EsIndigena(NombrePlantilla(indigena), indigena.DisplayName)) continue;

                ComponentChaseBehavior persecucion = indigena.Entity.FindComponent<ComponentChaseBehavior>();
                if (persecucion == null) continue;
                if (persecucion.Target != null && EstaViva(persecucion.Target)) continue; // ya tiene objetivo

                ComponentCreature masCercano = null;
                float mejor = RadioCazaGigantes;
                foreach (ComponentCreature otro in creaturas.Creatures)
                {
                    if (!EstaViva(otro)) continue;
                    string plantilla = NombrePlantilla(otro);
                    if (!plantilla.StartsWith(PrefijoGigantes) || EsZombiGigante(plantilla)) continue;

                    float distancia = Vector3.Distance(indigena.ComponentBody.Position, otro.ComponentBody.Position);
                    if (distancia < mejor)
                    {
                        mejor = distancia;
                        masCercano = otro;
                    }
                }
                if (masCercano != null)
                {
                    persecucion.Attack(masCercano, RangoDefensa, TiempoDefensa, false);
                }
            }
        }

        // los indigenas normales cazan presas terrestres (jabalies, vacas...) cercanas
        private void CazarPresas()
        {
            if (!TribuCazaPresas) return;
            if (tiempo.GameTime < proximaCazaPresas) return;
            proximaCazaPresas = tiempo.GameTime + PausaCaza;

            foreach (ComponentCreature cazador in creaturas.Creatures)
            {
                if (!EstaViva(cazador) || NombrePlantilla(cazador) != PlantillaTribu) continue;

                ComponentChaseBehavior persecucion = cazador.Entity.FindComponent<ComponentChaseBehavior>();
                if (persecucion == null) continue;
                if (persecucion.Target != null && EstaViva(persecucion.Target)) continue; // ya tiene objetivo

                ComponentCreature masCercano = null;
                float mejor = RadioCazaPresas;
                foreach (ComponentCreature otro in creaturas.Creatures)
                {
                    if (!EsPresa(otro)) continue;
                    float distancia = Vector3.Distance(cazador.ComponentBody.Position, otro.ComponentBody.Position);
                    if (distancia < mejor)
                    {
                        mejor = distancia;
                        masCercano = otro;
                    }
                }
                if (masCercano != null)
                {
                    persecucion.Attack(masCercano, RangoDefensa, TiempoDefensa, false);
                }
            }
        }

        // presa = animal terrestre "LandOther" que no es jugador, ni gigante, ni de ninguna manada
        private static bool EsPresa(ComponentCreature c)
        {
            if (!EstaViva(c)) return false;
            if (c.Category != CreatureCategory.LandOther) return false;
            if (c.Entity.FindComponent<ComponentPlayer>() != null) return false;
            if (NombrePlantilla(c).StartsWith(PrefijoGigantes)) return false;
            return Grupo(c) == null;
        }

        // alguien de la manada fue atacado: los de su grupo que esten cerca atacan al agresor
        private void AlSerAtacado(ComponentCreature victima, ComponentCreature agresor)
        {
            if (creaturas == null || !EstaViva(agresor) || !EstaViva(victima)) return;

            string grupo = Grupo(victima);
            if (grupo == null || Grupo(agresor) == grupo) return; // fuego amigo: no se atacan entre ellos

            foreach (ComponentCreature aliado in creaturas.Creatures)
            {
                if (aliado == victima || !EstaViva(aliado) || Grupo(aliado) != grupo) continue;
                if (Vector3.Distance(victima.ComponentBody.Position, aliado.ComponentBody.Position) > RadioManada) continue;

                ComponentChaseBehavior persecucion = aliado.Entity.FindComponent<ComponentChaseBehavior>();
                if (persecucion == null) continue;
                if (persecucion.Target != null && EstaViva(persecucion.Target)) continue; // ya esta peleando

                persecucion.Attack(agresor, RangoDefensa, TiempoDefensa, true);
            }
        }

        private static bool EsIndigena(string plantilla, string nombre)
        {
            // el indigena normal NO es zombi: no recibe el buff de noche ni caza gigantes
            if (plantilla == PlantillaTribu) return false;
            return Contiene(plantilla, TextoIndigena) || Contiene(nombre, TextoIndigena);
        }

        private static bool Contiene(string texto, string buscado)
        {
            if (string.IsNullOrEmpty(texto)) return false;
            string t = texto.ToLowerInvariant().Replace('\u00ed', 'i'); // i con tilde -> i
            return t.Contains(buscado);
        }

        // Textura de ojos rojos: "Textures/Creatures/<Plantilla>Ojos" (como GigantePequenoMuertoOjos).
        // Si no existe, no se rompe nada: simplemente no cambia la textura de noche.
        private Texture2D ObtenerOjos(string plantilla)
        {
            Texture2D textura;
            if (texturasOjos.TryGetValue(plantilla, out textura)) return textura;
            try
            {
                textura = ContentManager.Get<Texture2D>("Textures/Creatures/" + plantilla + "Ojos");
            }
            catch (System.Exception e)
            {
                textura = null;
                Log.Warning("EsqueletoSpawn: falta Textures/Creatures/" + plantilla + "Ojos (" + e.Message + ")");
            }
            texturasOjos[plantilla] = textura;
            return textura;
        }
    }
}
