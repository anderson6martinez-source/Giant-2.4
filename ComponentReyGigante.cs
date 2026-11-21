using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // Boss "Rey Gigante Caido" para Survivalcraft 2.4 (normal).
    // Fase 1 (>50% vida): normal, lento.
    // Fase 2 (<=50% vida): mas rapido y cada 15 s invoca secuaces.
    // Duerme hasta que un jugador se acerca, y no se aleja mas de 40 bloques de su trono.
    public class ComponentReyGigante : Component, IUpdateable
    {
        // ---- AJUSTA ESTOS NOMBRES A TUS PLANTILLAS ----
        const string PLANTILLA_GUARDIA = "GuardiaZombi";
        const string PLANTILLA_GIGANTE_MUERTO = "GiganteMuerto";
        // -----------------------------------------------

        const float RADIO_DESPERTAR = 25f;
        const float RADIO_CORREA = 40f;
        const float INTERVALO_INVOCAR = 15f;
        const float VELOCIDAD_FASE2 = 1.6f;   // multiplicador de velocidad
        const int MAX_SECUACES_VIVOS = 8;

        ComponentCreature m_creature;
        ComponentPathfinding m_pathfinding;
        SubsystemPlayers m_players;
        SubsystemCreatureSpawn m_spawn;
        SubsystemTime m_time;
        SubsystemGameInfo m_gameInfo;

        Vector3 m_casa;
        bool m_despierto;
        bool m_fase2;
        bool m_muerto;
        float m_velocidadBase;
        double m_proximaInvocacion;
        List<ComponentCreature> m_secuaces = new List<ComponentCreature>();
        Engine.Random m_random = new Engine.Random();

        public int UpdateOrder { get { return 0; } }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_creature = Entity.FindComponent<ComponentCreature>(true);
            m_pathfinding = Entity.FindComponent<ComponentPathfinding>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

            m_casa = valuesDictionary.GetValue<Vector3>("Casa", Vector3.Zero);
            m_fase2 = valuesDictionary.GetValue<bool>("Fase2", false);
            m_velocidadBase = m_creature.ComponentLocomotion.WalkSpeed;
            if (m_fase2) m_creature.ComponentLocomotion.WalkSpeed = m_velocidadBase * VELOCIDAD_FASE2;
        }

        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            valuesDictionary.SetValue("Casa", m_casa);
            valuesDictionary.SetValue("Fase2", m_fase2);
        }

        public void Update(float dt)
        {
            if (m_muerto) return;

            // la primera vez, guarda donde aparecio (su trono)
            if (m_casa == Vector3.Zero) m_casa = m_creature.ComponentBody.Position;

            // --- muerte: los secuaces caen con el rey ---
            if (m_creature.ComponentHealth.Health <= 0f)
            {
                m_muerto = true;
                foreach (ComponentCreature s in m_secuaces)
                {
                    if (s != null && s.ComponentHealth.Health > 0f)
                        s.ComponentHealth.Injure(1f, null, true, "El rey ha caido");
                }
                m_secuaces.Clear();
                return;
            }

            Vector3 pos = m_creature.ComponentBody.Position;

            // --- dormir / despertar ---
            if (!m_despierto)
            {
                foreach (ComponentPlayer p in m_players.ComponentPlayers)
                {
                    if (p.ComponentHealth.Health > 0f &&
                        Vector3.Distance(p.ComponentBody.Position, pos) <= RADIO_DESPERTAR)
                    {
                        m_despierto = true;
                        m_proximaInvocacion = m_gameInfo.TotalElapsedGameTime + INTERVALO_INVOCAR;
                        break;
                    }
                }
                return;
            }

            // --- correa: vuelve al trono si se aleja demasiado ---
            if (Vector3.Distance(pos, m_casa) > RADIO_CORREA)
            {
                m_pathfinding.SetDestination(m_casa, 1f, 2f, 0, false, true, false, null);
            }

            // --- cambio a fase 2 ---
            float vida = m_creature.ComponentHealth.Health; // 0..1
            if (!m_fase2 && vida <= 0.5f)
            {
                m_fase2 = true;
                m_creature.ComponentLocomotion.WalkSpeed = m_velocidadBase * VELOCIDAD_FASE2;
                m_proximaInvocacion = m_gameInfo.TotalElapsedGameTime + 2.0; // primera oleada rapida
                Invocar(3, PLANTILLA_GUARDIA);
            }

            // --- invocacion periodica en fase 2 ---
            if (m_fase2 && m_gameInfo.TotalElapsedGameTime >= m_proximaInvocacion)
            {
                m_proximaInvocacion = m_gameInfo.TotalElapsedGameTime + INTERVALO_INVOCAR;
                m_secuaces.RemoveAll(s => s == null || s.ComponentHealth.Health <= 0f);
                if (m_secuaces.Count < MAX_SECUACES_VIVOS)
                {
                    Invocar(3, PLANTILLA_GUARDIA);
                    Invocar(1, PLANTILLA_GIGANTE_MUERTO);
                }
            }
        }

        void Invocar(int cantidad, string plantilla)
        {
            Vector3 centro = m_creature.ComponentBody.Position;
            for (int i = 0; i < cantidad; i++)
            {
                float ang = m_random.UniformFloat(0f, 6.2831855f);
                float dist = m_random.UniformFloat(3f, 6f);
                Vector3 p = new Vector3(centro.X + (float)Math.Cos(ang) * dist, centro.Y + 0.5f,
                                        centro.Z + (float)Math.Sin(ang) * dist);
                try
                {
                    Entity e = m_spawn.SpawnCreature(plantilla, p, false);
                    if (e != null)
                    {
                        ComponentCreature c = e.FindComponent<ComponentCreature>();
                        if (c != null) m_secuaces.Add(c);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("ReyGigante: no se pudo invocar " + plantilla + ": " + ex.Message);
                }
            }
        }
    }
}
