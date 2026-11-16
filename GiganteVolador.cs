// Gigante volador: no tiene gravedad, se mantiene en el aire, persigue al objetivo y baja a atacar.
// Archivo nuevo e independiente: sube este .cs junto a los demas.
using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;

namespace Game
{
    public class GiganteVoladorLoader : ModLoader
    {
        const string Nombre = "Gigante volador";           // DisplayName del .xdb
        const string Plantilla = "GiganteVolador";         // Name de la plantilla del .xdb
        const float PesoAparicion = 0.45f;

        const float AlturaCrucero = 6f;      // bloques sobre el suelo cuando no hay objetivo
        const float VelocidadVertical = 3.5f; // m/s maxima al subir o bajar
        const float VelocidadPatrulla = 1.4f; // m/s sin objetivo
        const float VelocidadPersecucion = 2.78f; // 10 km/h
        const float DistanciaAtaque = 2.6f;  // bloques
        const float PausaAtaque = 1.3f;      // segundos entre golpes

        SubsystemCreatureSpawn creaturas;
        SubsystemTerrain terreno;
        SubsystemTime tiempo;
        readonly Dictionary<ComponentCreature, double> ultimoGolpe = new Dictionary<ComponentCreature, double>();
        readonly Dictionary<ComponentCreature, Vector2> rumbo = new Dictionary<ComponentCreature, Vector2>();
        readonly Dictionary<ComponentCreature, double> cambioRumbo = new Dictionary<ComponentCreature, double>();
        readonly System.Random azar = new System.Random();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnProjectDisposed", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
        }

        public override void OnProjectLoaded(Project proyecto)
        {
            creaturas = proyecto.FindSubsystem<SubsystemCreatureSpawn>(true);
            terreno = proyecto.FindSubsystem<SubsystemTerrain>(true);
            tiempo = proyecto.FindSubsystem<SubsystemTime>(true);
            ultimoGolpe.Clear(); rumbo.Clear(); cambioRumbo.Clear();
        }

        public override void OnProjectDisposed()
        {
            creaturas = null; terreno = null; tiempo = null;
            ultimoGolpe.Clear(); rumbo.Clear(); cambioRumbo.Clear();
        }

        // ---------- aparicion natural ----------
        public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsistema,
            List<SubsystemCreatureSpawn.CreatureType> tipos)
        {
            SubsystemTerrain suelo = subsistema.Project.FindSubsystem<SubsystemTerrain>(true);
            tipos.Add(new SubsystemCreatureSpawn.CreatureType(Plantilla, SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = (tipo, punto) =>
                {
                    int celda = suelo.Terrain.GetCellValueFast(punto.X, punto.Y - 1, punto.Z);
                    int contenido = Terrain.ExtractContents(celda);
                    if (contenido == 0 || contenido == WaterBlock.Index) return 0f;
                    return BlocksManager.Blocks[contenido].IsCollidable ? PesoAparicion : 0f;
                },
                SpawnFunction = (tipo, punto) => subsistema.SpawnCreatures(tipo, Plantilla, punto, 1).Count
            });
        }

        // ---------- vuelo ----------
        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || terreno == null || tiempo == null) return;
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;

            foreach (ComponentCreature gigante in new List<ComponentCreature>(creaturas.Creatures))
            {
                if (gigante.DisplayName != Nombre) continue;

                ComponentBody cuerpo = gigante.ComponentBody;
                ComponentHealth salud = gigante.ComponentHealth;
                if (cuerpo == null || salud == null) continue;

                if (salud.Health <= 0f)
                {
                    cuerpo.IsGravityEnabled = true; // al morir cae
                    continue;
                }
                cuerpo.IsGravityEnabled = false;

                Vector3 pos = cuerpo.Position;
                int suelo = terreno.Terrain.GetTopHeight(Terrain.ToCell(pos.X), Terrain.ToCell(pos.Z)) + 1;

                ComponentChaseBehavior persecucion = gigante.Entity.FindComponent<ComponentChaseBehavior>();
                ComponentCreature objetivo = persecucion != null ? persecucion.Target : null;
                bool hayObjetivo = objetivo != null && objetivo.ComponentBody != null
                    && objetivo.ComponentHealth != null && objetivo.ComponentHealth.Health > 0f;

                float alturaDeseada;
                Vector2 direccion = Vector2.Zero;
                float velocidad = 0f;

                if (hayObjetivo)
                {
                    Vector3 posObj = objetivo.ComponentBody.Position;
                    Vector2 hacia = new Vector2(posObj.X - pos.X, posObj.Z - pos.Z);
                    float distHoriz = hacia.Length();
                    if (distHoriz > 0.01f) direccion = hacia / distHoriz;

                    if (distHoriz > 7f) alturaDeseada = Math.Max(suelo + 3f, posObj.Y + 3f);
                    else alturaDeseada = posObj.Y + 0.6f; // pica hacia el objetivo
                    velocidad = distHoriz > 1.2f ? VelocidadPersecucion : 0f;

                    // golpe cuando esta cerca
                    float distTotal = Vector3.Distance(pos + new Vector3(0f, cuerpo.BoxSize.Y * 0.5f, 0f),
                        posObj + new Vector3(0f, objetivo.ComponentBody.BoxSize.Y * 0.5f, 0f));
                    double ahora = tiempo.GameTime;
                    double ultimo;
                    if (!ultimoGolpe.TryGetValue(gigante, out ultimo)) ultimo = -100;
                    if (distTotal <= DistanciaAtaque && ahora - ultimo >= PausaAtaque)
                    {
                        ComponentMiner minero = gigante.Entity.FindComponent<ComponentMiner>();
                        if (minero != null)
                        {
                            Vector3 punto = posObj + new Vector3(0f, objetivo.ComponentBody.BoxSize.Y * 0.5f, 0f);
                            Vector3 dir = Vector3.Normalize(punto - pos);
                            minero.Hit(objetivo.ComponentBody, punto, dir);
                            ultimoGolpe[gigante] = ahora;
                        }
                    }
                }
                else
                {
                    alturaDeseada = suelo + AlturaCrucero;
                    double ahora = tiempo.GameTime;
                    double siguiente;
                    if (!cambioRumbo.TryGetValue(gigante, out siguiente) || ahora >= siguiente || !rumbo.ContainsKey(gigante))
                    {
                        double ang = azar.NextDouble() * Math.PI * 2.0;
                        rumbo[gigante] = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                        cambioRumbo[gigante] = ahora + 4.0 + azar.NextDouble() * 6.0;
                    }
                    direccion = rumbo[gigante];
                    velocidad = VelocidadPatrulla;
                }

                alturaDeseada = Math.Max(alturaDeseada, suelo + 0.3f);

                // velocidad vertical suave hacia la altura deseada
                float vy = (alturaDeseada - pos.Y) * 1.5f;
                if (vy > VelocidadVertical) vy = VelocidadVertical;
                if (vy < -VelocidadVertical) vy = -VelocidadVertical;

                Vector3 v = cuerpo.Velocity;
                float k = Math.Min(1f, 4f * dt);
                v.Y += (vy - v.Y) * k;
                v.X += (direccion.X * velocidad - v.X) * k;
                v.Z += (direccion.Y * velocidad - v.Z) * k;
                cuerpo.Velocity = v;

                // girar hacia donde se mueve
                if (velocidad > 0.2f)
                {
                    float objetivoYaw = (float)Math.Atan2(-direccion.X, -direccion.Y);
                    Vector3 f = cuerpo.Matrix.Forward;
                    float yawActual = (float)Math.Atan2(-f.X, -f.Z);
                    float dif = objetivoYaw - yawActual;
                    while (dif > Math.PI) dif -= (float)(2 * Math.PI);
                    while (dif < -Math.PI) dif += (float)(2 * Math.PI);
                    float nuevo = yawActual + dif * Math.Min(1f, 6f * dt);
                    cuerpo.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, nuevo);
                }
            }
        }
    }
}
