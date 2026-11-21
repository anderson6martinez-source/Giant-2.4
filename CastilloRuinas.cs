using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // Genera el "Castillo en Ruinas" del Rey Esqueleto.
    // Misma idea que el castillo del Rey Gigante, pero con muros rotos,
    // torres derrumbadas y escombros. Usa otra "sal" para no coincidir con el otro castillo.
    public class SubsystemCastilloRuinas : Subsystem, IUpdateable
    {
        // ---- AJUSTA ESTOS NOMBRES A TUS PLANTILLAS ----
        const string PLANTILLA_BOSS = "ReyEsqueleto";
        const string PLANTILLA_ESQUELETO = "Esqueleto";
        // -----------------------------------------------

        const int CELDA = 1024;
        const int TAM = 31;
        const int DIST_CONSTRUIR = 80;
        const float PROBABILIDAD = 0.5f;
        const int DIST_MIN_SPAWN = 300;
        const int SAL = 91823;   // distinta a la del otro castillo

        SubsystemTerrain m_terrain;
        SubsystemPlayers m_players;
        SubsystemGameInfo m_gameInfo;
        SubsystemCreatureSpawn m_spawn;

        HashSet<Point2> m_construidos = new HashSet<Point2>();
        float m_timer;
        int m_seed;

        public int UpdateOrder { get { return 0; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_spawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);

            string s = m_gameInfo.WorldSettings.Seed ?? "";
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            m_seed = h ^ SAL;

            string guardado = valuesDictionary.GetValue<string>("Construidos", "");
            foreach (string par in guardado.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = par.Split(',');
                m_construidos.Add(new Point2(int.Parse(p[0]), int.Parse(p[1])));
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            base.Save(valuesDictionary);
            List<string> partes = new List<string>();
            foreach (Point2 p in m_construidos) partes.Add(p.X + "," + p.Y);
            valuesDictionary.SetValue("Construidos", string.Join(";", partes.ToArray()));
        }

        bool CentroDeCelda(Point2 celda, out Point2 centro)
        {
            centro = Point2.Zero;
            System.Random r = new System.Random(m_seed ^ (celda.X * 73856093) ^ (celda.Y * 19349663));
            if (r.UniformFloat(0f, 1f) > PROBABILIDAD) return false;
            centro = new Point2(
                celda.X * CELDA + r.UniformInt(150, CELDA - 150),
                celda.Y * CELDA + r.UniformInt(150, CELDA - 150));
            if (Math.Abs(centro.X) < DIST_MIN_SPAWN && Math.Abs(centro.Y) < DIST_MIN_SPAWN) return false;
            return true;
        }

        public void Update(float dt)
        {
            m_timer += dt;
            if (m_timer < 2f) return;
            m_timer = 0f;

            foreach (ComponentPlayer jugador in m_players.ComponentPlayers)
            {
                Vector3 pos = jugador.ComponentBody.Position;
                int cx = (int)Math.Floor(pos.X / CELDA);
                int cz = (int)Math.Floor(pos.Z / CELDA);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    Point2 centro;
                    if (!CentroDeCelda(new Point2(cx + dx, cz + dz), out centro)) continue;
                    if (m_construidos.Contains(centro)) continue;
                    float d = Vector2.Distance(new Vector2(pos.X, pos.Z), new Vector2(centro.X, centro.Y));
                    if (d > DIST_CONSTRUIR) continue;
                    if (!ChunksCargados(centro)) continue;
                    Construir(centro);
                    m_construidos.Add(centro);
                }
            }
        }

        bool ChunksCargados(Point2 c)
        {
            int m = TAM / 2 + 2;
            int[] xs = { c.X - m, c.X + m };
            int[] zs = { c.Y - m, c.Y + m };
            foreach (int x in xs)
            foreach (int z in zs)
            {
                TerrainChunk ch = m_terrain.Terrain.GetChunkAtCell(x, z);
                if (ch == null || ch.State < TerrainChunkState.InvalidLight) return false;
            }
            return true;
        }

        void Poner(int x, int y, int z, int indice)
        {
            m_terrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(indice));
        }

        // Pone un bloque de muro con probabilidad que baja con la altura:
        // abajo casi siempre hay muro, arriba casi todo esta derrumbado.
        void Muro(System.Random r, int x, int baseY, int z, int altoMax, float integridad)
        {
            for (int y = 1; y <= altoMax; y++)
            {
                float p = integridad * (1f - 0.8f * y / altoMax);
                if (r.UniformFloat(0f, 1f) < p) Poner(x, baseY + y, z, BrickBlock.Index);
            }
        }

        void Construir(Point2 c)
        {
            System.Random r = new System.Random(m_seed ^ c.X ^ (c.Y * 31));
            int mitad = TAM / 2;
            int x0 = c.X - mitad, z0 = c.Y - mitad;
            int x1 = c.X + mitad, z1 = c.Y + mitad;

            int baseY = m_terrain.Terrain.CalculateTopmostCellHeight(c.X, c.Y);
            if (baseY < 40) baseY = 40;

            int suelo = GraniteBlock.Index;

            // 1) aplanar
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                for (int y = baseY - 6; y <= baseY; y++) Poner(x, y, z, suelo);
                for (int y = baseY + 1; y < baseY + 20; y++) Poner(x, y, z, 0);
            }

            // 2) muralla rota (altura 8, 70% de integridad abajo)
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                bool borde = x == x0 || x == x1 || z == z0 || z == z1;
                if (!borde) continue;
                bool brecha = r.UniformFloat(0f, 1f) < 0.12f; // huecos totales en la muralla
                if (brecha) continue;
                Muro(r, x, baseY, z, 8, 0.85f);
            }

            // 3) torres: dos casi derrumbadas, dos a medias
            int[][] esq = { new[] { x0, z0 }, new[] { x1, z0 }, new[] { x0, z1 }, new[] { x1, z1 } };
            for (int i = 0; i < esq.Length; i++)
            {
                int alto = (i % 2 == 0) ? r.UniformInt(3, 5) : r.UniformInt(7, 10);
                for (int x = esq[i][0] - 2; x <= esq[i][0] + 2; x++)
                for (int z = esq[i][1] - 2; z <= esq[i][1] + 2; z++)
                {
                    bool borde = Math.Abs(x - esq[i][0]) == 2 || Math.Abs(z - esq[i][1]) == 2;
                    if (borde) Muro(r, x, baseY, z, alto, 0.9f);
                }
            }

            // 4) sala del trono en ruinas (13x13): muros bajos, sin techo, con agujeros
            int k = 6;
            for (int x = c.X - k; x <= c.X + k; x++)
            for (int z = c.Y - k; z <= c.Y + k; z++)
            {
                bool borde = Math.Abs(x - c.X) == k || Math.Abs(z - c.Y) == k;
                bool puerta = z == c.Y - k && Math.Abs(x - c.X) <= 1;
                if (borde && !puerta) Muro(r, x, baseY, z, 9, 0.85f);
            }

            // 5) escombros por todo el patio
            int escombros = r.UniformInt(25, 45);
            for (int i = 0; i < escombros; i++)
            {
                int ex = r.UniformInt(x0 + 1, x1 - 1);
                int ez = r.UniformInt(z0 + 1, z1 - 1);
                Poner(ex, baseY + 1, ez, BrickBlock.Index);
                if (r.UniformFloat(0f, 1f) < 0.3f) Poner(ex, baseY + 2, ez, BrickBlock.Index);
            }

            // 6) antorchas viejas (solo algunas)
            int[][] torchas = { new[] { c.X - 5, c.Y - 5 }, new[] { c.X + 5, c.Y + 5 } };
            foreach (int[] t in torchas)
                m_terrain.ChangeCell(t[0], baseY + 2, t[1], Terrain.MakeBlockValue(TorchBlock.Index, 0, 0));

            // 7) cofre del tesoro (en la sala)
            PonerCofre(c.X, baseY + 1, c.Y + 5);

            // 8) el rey en el centro
            Spawn(PLANTILLA_BOSS, new Vector3(c.X + 0.5f, baseY + 1.5f, c.Y + 0.5f));

            // 9) esqueletos dispersos por las ruinas
            int n = r.UniformInt(6, 9);
            for (int i = 0; i < n; i++)
            {
                float px = c.X + r.UniformFloat(-13f, 13f);
                float pz = c.Y + r.UniformFloat(-13f, 13f);
                Spawn(PLANTILLA_ESQUELETO, new Vector3(px, baseY + 1.5f, pz));
            }
        }

        void Spawn(string plantilla, Vector3 pos)
        {
            try { m_spawn.SpawnCreature(plantilla, pos, false); }
            catch (Exception e) { Log.Warning("Ruinas: no se pudo spawnear " + plantilla + ": " + e.Message); }
        }

        void PonerCofre(int x, int y, int z)
        {
            try
            {
                Poner(x, y, z, ChestBlock.Index);
                Entity entidad = DatabaseManager.CreateEntity(Project, "Chest", true);
                entidad.FindComponent<ComponentBlockEntity>(true).Coordinates = new Point3(x, y, z);
                ComponentChest cofre = entidad.FindComponent<ComponentChest>(true);
                // ajusta los items a tu gusto: (slot, valorBloque, cantidad)
                cofre.AddSlotItems(0, Terrain.MakeBlockValue(BoneBlock.Index), 15);
                cofre.AddSlotItems(1, Terrain.MakeBlockValue(DiamondChunkBlock.Index), 2);
                Project.AddEntity(entidad);
            }
            catch (Exception e) { Log.Warning("Ruinas: cofre fallo: " + e.Message); }
        }
    }
}
