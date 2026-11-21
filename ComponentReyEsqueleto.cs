using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // Boss "Rey Esqueleto" para Survivalcraft 2.4 (normal).
    // Es debil (poca vida y poco dano, se configura en el XML), pero invoca
    // oleadas constantes de esqueletos mientras haya un jugador cerca.
    public class ComponentReyEsqueleto : Component, IUpdateable
    {
        // ---- AJUSTA ESTE NOMBRE A TU PLANTILLA ----
        const string PLANTILLA_ESQUELETO = "Esqueleto";
        // -------------------------------------------

        const float RADIO_DESPERTAR = 30f;
        const float RADIO_CORREA = 35f;
        const float INTERVALO_BASE = 6f;       // segundos entre oleadas
        const float INTERVALO_FASE2 = 3.5f;    // cuando le queda poca vida
        const int ESQUELETOS_POR_OLEADA = 3;
        const int MAX_ESQUELETOS_VIVOS = 20;

        ComponentCreature m_creature;
        ComponentPathfinding m_pathfinding;
        SubsystemPlayers m_players;
        SubsystemCreatureSpawn m_spawn;
        SubsystemGameInfo m_gameInfo;

        Vector3 m_casa;
        bool m_despierto;
        bool m_muerto;
        double m_proximaOleada;
        List<ComponentCreature> m_esqueletos = new List<ComponentCreature>();
        Engine.Random m_random = new Engine.Random();

        public int UpdateOrder { get { return 0; } }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_creature = Entity.FindComponent<ComponentCreature>(true);
            m_pathfinding = Entity.FindComponent<ComponentPathfinding>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_casa = valuesDictionary.GetValue<Vector3>("Casa", Vector3.Zero);
        }

        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            valuesDictionary.SetValue("Casa", m_casa);
        }

        public void Update(float dt)
        {
            if (m_muerto) return;
            if (m_casa == Vector3.Zero) m_casa = m_creature.ComponentBody.Position;

            // muerte: los esqueletos se desmoronan con su rey
            if (m_creature.ComponentHealth.Health <= 0f)
            {
                m_muerto = true;
                foreach (ComponentCreature s in m_esqueletos)
                    if (s != null && s.ComponentHealth.Health > 0f)
                        s.ComponentHealth.Injure(1f, null, true, "El rey esqueleto ha caido");
                m_esqueletos.Clear();
                return;
            }

            Vector3 pos = m_creature.ComponentBody.Position;

            // despertar
            if (!m_despierto)
            {
                foreach (ComponentPlayer p in m_players.ComponentPlayers)
                {
                    if (p.ComponentHealth.Health > 0f &&
                        Vector3.Distance(p.ComponentBody.Position, pos) <= RADIO_DESPERTAR)
                    {
                        m_despierto = true;
                        m_proximaOleada = m_gameInfo.TotalElapsedGameTime + 1.0;
                        break;
                    }
                }
                return;
            }

            // correa al trono
            if (Vector3.Distance(pos, m_casa) > RADIO_CORREA)
                m_pathfinding.SetDestination(m_casa, 1f, 2f, 0, false, true, false, null);

            // oleadas
            if (m_gameInfo.TotalElapsedGameTime >= m_proximaOleada)
            {
                bool fase2 = m_creature.ComponentHealth.Health <= 0.4f;
                m_proximaOleada = m_gameInfo.TotalElapsedGameTime + (fase2 ? INTERVALO_FASE2 : INTERVALO_BASE);

                m_esqueletos.RemoveAll(s => s == null || s.ComponentHealth.Health <= 0f);
                int hueco = MAX_ESQUELETOS_VIVOS - m_esqueletos.Count;
                int n = Math.Min(hueco, fase2 ? ESQUELETOS_POR_OLEADA + 2 : ESQUELETOS_POR_OLEADA);
                for (int i = 0; i < n; i++) Invocar();
            }
        }

        void Invocar()
        {
            Vector3 centro = m_creature.ComponentBody.Position;
            float ang = m_random.UniformFloat(0f, 6.2831855f);
            float dist = m_random.UniformFloat(2.5f, 6f);
            Vector3 p = new Vector3(centro.X + (float)Math.Cos(ang) * dist, centro.Y + 0.5f,
                                    centro.Z + (float)Math.Sin(ang) * dist);
            try
            {
                Entity e = m_spawn.SpawnCreature(PLANTILLA_ESQUELETO, p, false);
                if (e != null)
                {
                    ComponentCreature c = e.FindComponent<ComponentCreature>();
                    if (c != null) m_esqueletos.Add(c);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("ReyEsqueleto: no se pudo invocar: " + ex.Message);
            }
        }
    }
}
