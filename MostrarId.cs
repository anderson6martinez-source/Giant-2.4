// MostrarId: ayuda temporal. Al cambiar el objeto que llevas en la mano, muestra en pantalla su nombre de clase y su ID.
// Uso: compila, abre el creativo, agarra el garrote de madera (o cualquier objeto) y mira el mensaje.
// El numero "id" es el que va en el .xdb (Slot0 > Contents). Cuando ya tengas el ID, borra este archivo.
// Archivo nuevo: sube este .cs junto a los demas; se compila todo junto en GiganteCode.dll.
using System.Collections.Generic;
using Engine;
using GameEntitySystem;

namespace Game
{
    public class MostrarIdLoader : ModLoader
    {
        SubsystemPlayers jugadores;
        SubsystemTime tiempo;
        double proximaRevision;
        readonly Dictionary<ComponentPlayer, int> ultimo = new Dictionary<ComponentPlayer, int>();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnProjectDisposed", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
        }

        public override void OnProjectLoaded(Project proyecto)
        {
            jugadores = proyecto.FindSubsystem<SubsystemPlayers>(true);
            tiempo = proyecto.FindSubsystem<SubsystemTime>(true);
            proximaRevision = 0;
            ultimo.Clear();
        }

        public override void OnProjectDisposed()
        {
            jugadores = null; tiempo = null;
            ultimo.Clear();
        }

        public override void SubsystemUpdate(float dt)
        {
            if (jugadores == null || tiempo == null) return;
            if (tiempo.GameTime < proximaRevision) return;
            proximaRevision = tiempo.GameTime + 0.5;

            foreach (ComponentPlayer jugador in jugadores.ComponentPlayers)
            {
                if (jugador.ComponentMiner == null) continue;
                int valor = jugador.ComponentMiner.ActiveBlockValue;
                int anterior;
                if (ultimo.TryGetValue(jugador, out anterior) && anterior == valor) continue;
                ultimo[jugador] = valor;
                if (valor == 0) continue;

                int id = Terrain.ExtractContents(valor);
                string clase = BlocksManager.Blocks[id].GetType().Name;
                string texto = "Objeto en mano: " + clase + "  |  id = " + id;
                Log.Information("MostrarId: " + texto);
                if (jugador.ComponentGui != null)
                    jugador.ComponentGui.DisplaySmallMessage(texto, Color.White, false, false);
            }
        }
    }
}
