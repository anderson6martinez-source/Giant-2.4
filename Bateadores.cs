// Bateadores: criaturas que llevan un garrote (bate) de madera fijo en la mano y pelean cuerpo a cuerpo.
// Igual que GiganteLanza.cs agrupa a los lanceros, este archivo agrupa a los bateadores.
//
//   - Solo el Indigena (normal) lleva el garrote (modelo Models/HumanoPequenoGarrote).
//   - NO lanzan nada, no tienen slot y no recogen objetos: el garrote va fijo en el modelo.
//   - El zombi indigena, el esqueleto y el chaman NO son bateadores (el chaman lleva su baculo).
//
// Para agregar otro bateador, pon aqui el Name de su plantilla del .xdb.
// Archivo nuevo: sube este .cs junto a los demas; se compila todo junto en GiganteCode.dll.
using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
    public class BateadoresLoader : ModLoader
    {
        // Name de la plantilla (EntityTemplate) de cada bateador en el .xdb
        static readonly string[] Bateadores = { "Indigena" };

        const string ModeloGarrote = "Models/HumanoPequenoGarrote";
        const double PausaRevision = 1.0; // segundos entre revisiones

        SubsystemCreatureSpawn creaturas;
        SubsystemTime tiempo;
        Model modeloGarrote;
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
            tiempo = proyecto.FindSubsystem<SubsystemTime>(true);
            modeloGarrote = null;
            proximaRevision = 0;
        }

        public override void OnProjectDisposed()
        {
            creaturas = null; tiempo = null; modeloGarrote = null;
        }

        static bool EsBateador(string plantilla)
        {
            return Array.IndexOf(Bateadores, plantilla) >= 0;
        }

        Model ObtenerModelo()
        {
            if (modeloGarrote != null) return modeloGarrote;
            try
            {
                modeloGarrote = ContentManager.Get<Model>(ModeloGarrote);
            }
            catch (Exception e)
            {
                Log.Warning("Bateadores: falta " + ModeloGarrote + " (" + e.Message + ")");
            }
            return modeloGarrote;
        }

        public override void SubsystemUpdate(float dt)
        {
            if (creaturas == null || tiempo == null) return;
            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + PausaRevision;

            foreach (ComponentCreature criatura in creaturas.Creatures)
            {
                if (!EsBateador(criatura.Entity.ValuesDictionary.DatabaseObject.Name)) continue;
                ComponentCreatureModel modelo = criatura.ComponentCreatureModel;
                if (modelo == null) continue;

                // siempre con el garrote en la mano (nunca con lanza)
                Model garrote = ObtenerModelo();
                if (garrote != null && modelo.Model != garrote) modelo.Model = garrote;
            }
        }
    }
}
