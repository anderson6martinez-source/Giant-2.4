// Los gigantes lanceros lanzan UNA lanza (de madera) contra su objetivo, una sola vez cada uno.
// Archivo nuevo: sube este .cs junto a los demas; se compila todo junto en GiganteCode.dll.
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
    public class GiganteLanzaLoader : ModLoader
    {
        // Nombres (DisplayName del .xdb) de los gigantes que lanzan lanza.
        static readonly string[] Lanceros = { "Gigante pequeno", "Gigante pequeno canibal" };

        const float DistanciaMin = 5f;    // mas cerca que esto pelea cuerpo a cuerpo
        const float DistanciaMax = 20f;   // mas lejos no lanza
        const float VelocidadLanza = 24f; // metros por segundo
        const string TipoLanza = "WoodenSpearBlock"; // StoneSpearBlock, IronSpearBlock, DiamondSpearBlock...

        SubsystemCreatureSpawn creaturas;
        SubsystemProjectiles proyectiles;
        SubsystemTime tiempo;
        SubsystemTerrain terreno;
        readonly HashSet<ComponentCreature> yaLanzaron = new HashSet<ComponentCreature>();
        double proximaRevision;

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnProjectDisposed", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
        }

        public override void OnProjectLoaded(Project proyecto)
        {
            creaturas = proyecto.FindSubsystem<SubsystemCreatureSpawn>(true);
            proyectiles = proyecto.FindSubsystem<SubsystemProjectiles>(true);
            tiempo = proyecto.FindSubsystem<SubsystemTime>(true);
            terreno = proyecto.FindSubsystem<SubsystemTerrain>(true);
            yaLanzaron.Clear();
            proximaRevision = 0;
        }

        public override void OnProjectDisposed()
        {
            creaturas = null; proyectiles = null; tiempo = null; terreno = null;
            yaLanzaron.Clear();
        }

        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || tiempo == null) return;
            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + 0.4;

            foreach (ComponentCreature gigante in creaturas.Creatures)
            {
                if (Array.IndexOf(Lanceros, gigante.DisplayName) < 0) continue;
                if (yaLanzaron.Contains(gigante)) continue;
                if (gigante.ComponentHealth == null || gigante.ComponentHealth.Health <= 0f) continue;

                ComponentChaseBehavior persecucion = gigante.Entity.FindComponent<ComponentChaseBehavior>();
                ComponentCreature objetivo = persecucion != null ? persecucion.Target : null;
                if (objetivo == null || objetivo.ComponentBody == null) continue;

                Vector3 origen = gigante.ComponentBody.Position + new Vector3(0f, gigante.ComponentBody.BoxSize.Y * 0.8f, 0f);
                Vector3 destino = objetivo.ComponentBody.Position + new Vector3(0f, objetivo.ComponentBody.BoxSize.Y * 0.6f, 0f);
                float distancia = (destino - origen).Length();
                if (distancia < DistanciaMin || distancia > DistanciaMax) continue;

                // sin linea de vista (un bloque en medio): no lanza todavia
                var choque = terreno.Raycast(origen, destino, false, true,
                    (valor, d) => BlocksManager.Blocks[Terrain.ExtractContents(valor)].IsCollidable);
                if (choque.HasValue) continue;

                // apunta un poco mas arriba para compensar la caida de la lanza
                float segundos = distancia / VelocidadLanza;
                Vector3 punto = destino + new Vector3(0f, 0.5f * 10f * segundos * segundos, 0f);
                Vector3 direccion = Vector3.Normalize(punto - origen);

                int valorLanza = Terrain.MakeBlockValue(BlocksManager.FindBlockByTypeName(TipoLanza, true).BlockIndex, 0, 0);
                proyectiles.FireProjectile(valorLanza, origen + direccion * 1.2f, direccion * VelocidadLanza, Vector3.Zero, gigante);

                yaLanzaron.Add(gigante);

                // la lanza ya no esta en la mano: cambia al modelo sin lanza
                try
                {
                    gigante.ComponentCreatureModel.Model = ContentManager.Get<Model>("Models/GigantePequenoSinLanza");
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
