// Los gigantes lanceros llevan UN objeto (inventario de un solo slot).
// Empiezan con una lanza de madera, la lanzan contra su objetivo y despues pueden
// recoger del suelo la lanza (o cualquier objeto a su alcance) para llevarla y volver a lanzarla.
// El "Gigante pequeno muerto" (zombie) NO esta en la lista de lanceros: no tiene slot, ni recoge, ni lanza.
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
        // Nombres (DisplayName del .xdb) de los gigantes con slot. El zombie NO va aqui.
        static readonly string[] Lanceros = { "Gigante pequeno", "Gigante pequeno canibal", "Indigena", "Esqueleto" };

        // Humanos pequenos lanceros (usan el modelo del humano con lanza y sin lanza)
        static bool EsHumanoLancero(string nombre)
        {
            return nombre == "Indigena" || nombre == "Esqueleto";
        }

        static bool EsLancero(string nombre)
        {
            return Array.IndexOf(Lanceros, nombre) >= 0;
        }

        const float DistanciaMin = 5f;    // mas cerca que esto pelea cuerpo a cuerpo
        const float DistanciaMax = 20f;   // mas lejos no lanza
        const float VelocidadLanza = 24f; // metros por segundo
        const string TipoLanza = "WoodenSpearBlock"; // StoneSpearBlock, IronSpearBlock, DiamondSpearBlock...

        const float RadioRecoger = 2.0f;          // alcance (en bloques) para agarrar del suelo
        const double PausaTrasLanzar = 2.5;       // segundos sin recoger despues de lanzar
        const bool RecogerCualquierCosa = true;   // false = solo recogen lanzas
        const bool LanzarCualquierCosa = true;    // false = solo lanzan lanzas (lo demas solo lo cargan)

        SubsystemCreatureSpawn creaturas;
        SubsystemProjectiles proyectiles;
        SubsystemPickables objetos;
        SubsystemTime tiempo;
        SubsystemTerrain terreno;

        // el slot: valor del bloque que lleva cada gigante (0 = vacio)
        readonly Dictionary<ComponentCreature, int> slot = new Dictionary<ComponentCreature, int>();
        readonly Dictionary<ComponentCreature, double> noRecogerHasta = new Dictionary<ComponentCreature, double>();
        double proximaRevision;
        double proximaLimpieza;

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
            objetos = proyecto.FindSubsystem<SubsystemPickables>(true);
            tiempo = proyecto.FindSubsystem<SubsystemTime>(true);
            terreno = proyecto.FindSubsystem<SubsystemTerrain>(true);
            slot.Clear();
            noRecogerHasta.Clear();
            proximaRevision = 0;
            proximaLimpieza = 0;
        }

        public override void OnProjectDisposed()
        {
            creaturas = null; proyectiles = null; objetos = null; tiempo = null; terreno = null;
            slot.Clear();
            noRecogerHasta.Clear();
        }

        static int ValorLanza()
        {
            return Terrain.MakeBlockValue(BlocksManager.FindBlockByTypeName(TipoLanza, true).BlockIndex, 0, 0);
        }

        // Todos los gigantes lanceros empiezan con una lanza de madera.
        static int LanzaInicial(string nombre)
        {
            return ValorLanza();
        }

        // cualquier lanza del juego (bloques que heredan de SpearBlock)
        static bool EsLanza(int valor)
        {
            if (valor == 0) return false;
            Block bloque = BlocksManager.Blocks[Terrain.ExtractContents(valor)];
            return bloque is SpearBlock || bloque.GetType().Name.EndsWith("SpearBlock");
        }

        // modelo con lanza en la mano solo si lleva una lanza; si no, modelo sin lanza
        static void ActualizarModelo(ComponentCreature gigante, int valor)
        {
            try
            {
                bool conLanza = EsLanza(valor);
                string modelo = EsHumanoLancero(gigante.DisplayName)
                    ? (conLanza ? "Models/IndigenaLanza" : "Models/HumanoPequeno")
                    : (conLanza ? "Models/GigantePequeno" : "Models/GigantePequenoSinLanza");
                gigante.ComponentCreatureModel.Model = ContentManager.Get<Model>(modelo);
            }
            catch (Exception)
            {
            }
        }

        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || tiempo == null) return;
            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + 0.4;

            foreach (ComponentCreature gigante in creaturas.Creatures)
            {
                // solo los lanceros tienen slot (el gigante muerto queda fuera aqui)
                if (!EsLancero(gigante.DisplayName)) continue;
                if (gigante.ComponentBody == null || gigante.ComponentHealth == null) continue;

                // gigante nuevo: nace con su lanza en el slot (madera; el de hielo, la lanza de hielo)
                if (!slot.ContainsKey(gigante)) slot[gigante] = LanzaInicial(gigante.DisplayName);

                // si muere, suelta lo que lleva
                if (gigante.ComponentHealth.Health <= 0f)
                {
                    int llevado = slot[gigante];
                    if (llevado != 0)
                    {
                        objetos.AddPickable(llevado, 1, gigante.ComponentBody.Position + new Vector3(0f, 0.5f, 0f), null, null);
                        slot[gigante] = 0;
                    }
                    continue;
                }

                // 1) slot vacio: intenta recoger algo del suelo a su alcance
                if (slot[gigante] == 0)
                {
                    noRecogerHasta.TryGetValue(gigante, out double hasta);
                    if (tiempo.GameTime >= hasta) IntentarRecoger(gigante);
                }

                // 2) slot lleno: lanza contra su objetivo
                int valor = slot[gigante];
                if (valor == 0) continue;
                if (!LanzarCualquierCosa && !EsLanza(valor)) continue;

                ComponentChaseBehavior persecucion = gigante.Entity.FindComponent<ComponentChaseBehavior>();
                ComponentCreature objetivo = persecucion != null ? persecucion.Target : null;
                if (objetivo == null || objetivo.ComponentBody == null) continue;

                Vector3 origen = gigante.ComponentBody.Position + new Vector3(0f, gigante.ComponentBody.BoxSize.Y * 0.8f, 0f);
                Vector3 destino = objetivo.ComponentBody.Position + new Vector3(0f, objetivo.ComponentBody.BoxSize.Y * 0.6f, 0f);
                float distancia = (destino - origen).Length();
                if (distancia < DistanciaMin || distancia > DistanciaMax) continue;

                // sin linea de vista (un bloque en medio): no lanza todavia
                var choque = terreno.Raycast(origen, destino, false, true,
                    (v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if (choque.HasValue) continue;

                // apunta un poco mas arriba para compensar la caida del proyectil
                float segundos = distancia / VelocidadLanza;
                Vector3 punto = destino + new Vector3(0f, 0.5f * 10f * segundos * segundos, 0f);
                Vector3 direccion = Vector3.Normalize(punto - origen);

                proyectiles.FireProjectile(valor, origen + direccion * 1.2f, direccion * VelocidadLanza, Vector3.Zero, gigante);

                slot[gigante] = 0;
                noRecogerHasta[gigante] = tiempo.GameTime + PausaTrasLanzar;
                ActualizarModelo(gigante, 0);
            }

            // limpieza de gigantes que ya no existen (cada ~10 s)
            if (tiempo.GameTime >= proximaLimpieza)
            {
                proximaLimpieza = tiempo.GameTime + 10.0;
                var vivos = new HashSet<ComponentCreature>(creaturas.Creatures);
                var viejos = new List<ComponentCreature>();
                foreach (ComponentCreature c in slot.Keys)
                {
                    if (!vivos.Contains(c)) viejos.Add(c);
                }
                foreach (ComponentCreature c in viejos)
                {
                    slot.Remove(c);
                    noRecogerHasta.Remove(c);
                }
            }
        }

        void IntentarRecoger(ComponentCreature gigante)
        {
            Vector3 centro = gigante.ComponentBody.Position + new Vector3(0f, gigante.ComponentBody.BoxSize.Y * 0.4f, 0f);
            float radio2 = RadioRecoger * RadioRecoger;

            foreach (Pickable p in objetos.Pickables)
            {
                if (p.ToRemove || p.Count <= 0) continue;
                if (Vector3.DistanceSquared(p.Position, centro) > radio2) continue;
                if (!RecogerCualquierCosa && !EsLanza(p.Value)) continue;

                // toma UNA unidad (el slot es de un solo objeto)
                slot[gigante] = p.Value;
                p.Count--;
                if (p.Count <= 0) p.ToRemove = true;

                ActualizarModelo(gigante, slot[gigante]);
                return;
            }
        }
    }
}
