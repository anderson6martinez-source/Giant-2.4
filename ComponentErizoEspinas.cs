using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // ---------------------------------------------------------------
    // ERIZO: si el jugador lo golpea cuerpo a cuerpo, las espinas le hacen dano.
    //
    // Dano: la salud va de 0 a 1 (1 corazon = 0.1, igual que el Baculo Magico).
    // Cambia Dano si quieres mas o menos (0.05 = medio corazon).
    //
    // Archivos del mod (ya van dentro de base24.scmod):
    //   Models/Erizo.dae
    //   Textures/Creatures/Erizo.png
    //   GigantePequeno.xdb  (plantilla "Erizo" y componente "ErizoEspinas")
    // ---------------------------------------------------------------
    public class ComponentErizoEspinas : Component
    {
        public const float Dano = 0.1f;        // 1 corazon
        public const float AlcanceEspinas = 4f; // solo pincha si el jugador esta cerca (no con flechas ni desde lejos)

        private ComponentCreature m_creature;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_creature = Entity.FindComponent<ComponentCreature>(true);
            ComponentHealth salud = Entity.FindComponent<ComponentHealth>(true);
            salud.Injured += Pinchar;
        }

        private void Pinchar(Injury injury)
        {
            ComponentPlayer jugador = injury.AttackerPlayer;
            if (jugador == null || jugador.ComponentHealth == null) return;
            if (jugador.ComponentHealth.Health <= 0f) return;
            if (Vector3.Distance(jugador.ComponentBody.Position, m_creature.ComponentBody.Position) > AlcanceEspinas) return;
            jugador.ComponentHealth.Injure(Dano, m_creature, false, "Pinchado por un erizo");
        }
    }
}
