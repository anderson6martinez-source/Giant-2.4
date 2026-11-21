using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // Genera el "Castillo del Rey Caido" de forma natural en el mundo
    // y spawnea al boss + secuaces dentro. Se construye una sola vez por castillo.
    public class SubsystemCastilloRey : Subsystem, IUpdateable
    {
        // ---- AJUSTA ESTOS NOMBRES A TUS PLANTILLAS ----
        const string PLANTILLA_BOSS = "ReyGigante";
        const string PLANTILLA_GUARDIA = "GuardiaZombi";
        const string PLANTILLA_LANCERO = "IndigenaZombi";
        // -----------------------------------------------

        const int CELDA = 1024;      // tamano de la cuadricula de busqueda
        const int TAM = 31;          // lado del castillo
        const int DIST_CONSTRUIR = 80;
        const float PROBABILIDAD = 0.6f;
        const int DIST_MIN_SPAWN = 250;

        SubsystemTerrain m_terrain;
        SubsystemPlayers m_players;
        SubsystemGameInfo m_gameInfo;
        SubsystemCreatureSpawn m_spawn;

        HashSet<Point2> m_construidos = new HashSet<Point2>();
        Dictionary<Point2, Point2> m_cache = new Dictionary<Point2, Point2>();
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

            // hash simple y estable de la semilla del mundo
            string s = m_gameInfo.WorldSettings.Seed ?? "";
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            m_seed = h;

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

        // Centro del castillo de una celda (o null si esa celda no tiene)
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
                    Point2 celda = new Point2(cx + dx, cz + dz);
                    Point2 centro;
                    if (!CentroDeCelda(celda, out centro)) continue;
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

        void Construir(Point2 c)
        {
            int mitad = TAM / 2;
            int x0 = c.X - mitad, z0 = c.Y - mitad;
            int x1 = c.X + mitad, z1 = c.Y + mitad;

            // altura base = punto mas alto del centro
            int baseY = m_terrain.Terrain.CalculateTopmostCellHeight(c.X, c.Y);
            if (baseY < 40) baseY = 40;

            int piedra = BrickBlock.Index;
            int suelo = GraniteBlock.Index;

            // 1) aplanar: rellenar hacia abajo y limpiar hacia arriba
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                for (int y = baseY - 6; y < baseY; y++) Poner(x, y, z, suelo);
                Poner(x, baseY, z, suelo);
                for (int y = baseY + 1; y < baseY + 20; y++) Poner(x, y, z, 0);
            }

            // 2) muralla exterior (altura 8)
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                bool borde = x == x0 || x == x1 || z == z0 || z == z1;
                if (!borde) continue;
                bool puerta = z == z0 && Math.Abs(x - c.X) <= 1; // puerta al norte
                for (int y = 1; y <= 8; y++)
                {
                    if (puerta && y <= 4) continue;
                    Poner(x, baseY + y, z, piedra);
                }
                if ((x + z) % 2 == 0) Poner(x, baseY + 9, z, piedra); // almenas
            }

            // 3) cuatro torres en las esquinas (5x5, altura 12)
            int[][] esq = { new[] { x0, z0 }, new[] { x1, z0 }, new[] { x0, z1 }, new[] { x1, z1 } };
            foreach (int[] e in esq)
            {
                for (int x = e[0] - 2; x <= e[0] + 2; x++)
                for (int z = e[1] - 2; z <= e[1] + 2; z++)
                {
                    bool borde = Math.Abs(x - e[0]) == 2 || Math.Abs(z - e[1]) == 2;
                    for (int y = 1; y <= 12; y++)
                        if (borde) Poner(x, baseY + y, z, piedra);
                    if (borde && (x + z) % 2 == 0) Poner(x, baseY + 13, z, piedra);
                }
            }

            // 4) torre del homenaje (sala del trono 13x13, alto 12)
            int k = 6;
            for (int x = c.X - k; x <= c.X + k; x++)
            for (int z = c.Y - k; z <= c.Y + k; z++)
            {
                bool borde = Math.Abs(x - c.X) == k || Math.Abs(z - c.Y) == k;
                bool puerta = z == c.Y - k && Math.Abs(x - c.X) <= 1;
                for (int y = 1; y <= 12; y++)
                {
                    if (borde && !(puerta && y <= 4)) Poner(x, baseY + y, z, piedra);
                }
                if (!borde) Poner(x, baseY + 13, z, piedra); // techo
            }

            // 5) antorchas dentro de la sala
            int[][] torchas = { new[] { c.X - 5, c.Y - 5 }, new[] { c.X + 5, c.Y - 5 },
                                new[] { c.X - 5, c.Y + 5 }, new[] { c.X + 5, c.Y + 5 } };
            foreach (int[] t in torchas)
                m_terrain.ChangeCell(t[0], baseY + 3, t[1], Terrain.MakeBlockValue(TorchBlock.Index, 0, 0));

            // 6) cofre detras del trono
            int cofreX = c.X, cofreZ = c.Y + 5;
            PonerCofre(cofreX, baseY + 1, cofreZ);

            // 7) boss en el centro de la sala
            Spawn(PLANTILLA_BOSS, new Vector3(c.X + 0.5f, baseY + 1.5f, c.Y + 0.5f));

            // 8) guardias en el patio
            System.Random r = new System.Random(m_seed ^ c.X ^ c.Y);
            int n = r.UniformInt(4, 6);
            for (int i = 0; i < n; i++)
            {
                float px = c.X + r.UniformFloat(-13f, 13f);
                float pz = c.Y + r.UniformFloat(-13f, 13f);
                if (Math.Abs(px - c.X) < 8 && Math.Abs(pz - c.Y) < 8) { i--; continue; }
                Spawn(PLANTILLA_GUARDIA, new Vector3(px, baseY + 1.5f, pz));
            }

            // 9) un lancero arriba de cada torre
            foreach (int[] e in esq)
                Spawn(PLANTILLA_LANCERO, new Vector3(e[0] + 0.5f, baseY + 1.5f, e[1] + 0.5f));
        }

        void Spawn(string plantilla, Vector3 pos)
        {
            try { m_spawn.SpawnCreature(plantilla, pos, false); }
            catch (Exception e) { Log.Warning("Castillo: no se pudo spawnear " + plantilla + ": " + e.Message); }
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
                cofre.AddSlotItems(0, Terrain.MakeBlockValue(DiamondChunkBlock.Index), 4);
                cofre.AddSlotItems(1, Terrain.MakeBlockValue(RottenMeatBlock.Index), 10);
                Project.AddEntity(entidad);
            }
            catch (Exception e) { Log.Warning("Castillo: cofre fallo: " + e.Message); }
        }
    }
}
