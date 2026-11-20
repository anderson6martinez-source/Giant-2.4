using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // ---------------------------------------------------------------
    // PALO EN FUEGO
    //  - Receta: una fogata + un palo, uno junto al otro (sobra la fogata). Esta en RecetaPaloEnFuego.cr
    //  - Durabilidad 7; mientras lo tienes en la mano gasta 1 punto por segundo.
    //  - Clic normal (el de golpear/picar, el que se usa en el movil): prende el bloque
    //    inflamable al que apuntas o al animal.
    //  - En la mano ilumina alrededor (con un bloque de luz invisible junto a ti).
    //
    // Archivos del mod:
    //   Models/PaloEnFuego.dae
    //   Textures/Items/PaloEnFuego.png
    //
    // Hay que registrar en Blocks.csv (igual que BaculoMagico):
    //   PaloEnFuegoBlock  -> Index 700
    //   PaloLuzBlock      -> Index 702
    // y registrar SubsystemPaloEnFuegoBehavior en el .xdb igual que
    // SubsystemBaculoMagicoBehavior.
    // ---------------------------------------------------------------
    public class PaloEnFuegoBlock : Block
    {
        public const int Index = 700; // CAMBIAR si ya esta ocupado (igual que en Blocks.csv)

        public const int Durabilidad = 7;            // puntos de durabilidad
        public const float SegundosPorPunto = 1f;    // 1 punto por segundo en la mano
        public const float SegundosFuegoAnimal = 8f; // cuanto arde un animal
        public const double Recarga = 0.5;           // segundos entre usos

        // Si el palo se ve girado en el icono o en la mano, cambia esta matriz
        // (por ejemplo Matrix.CreateRotationX(MathUtils.PI / 2f)).
        private static readonly Matrix AjusteModelo = Matrix.Identity;

        private BlockMesh m_standaloneBlockMesh = new BlockMesh();
        private Texture2D m_texture;

        public PaloEnFuegoBlock()
        {
            DefaultDisplayName = "Palo en fuego";
            DefaultDescription = "Un palo ardiendo. Ilumina en la mano y prende bloques inflamables y animales. Dura 7 segundos.";
            DefaultCategory = "Tools";

            Durability = Durabilidad;
            DefaultMeleePower = 1f;
            DefaultMeleeHitProbability = 0.7f;
            DefaultProjectilePower = 1f;
            MaxStacking = 1;
            IsCollidable = false;
            DefaultCreativeData = 0;
        }

        public override string GetCategory(int value)
        {
            return "Tools";
        }

        private static double s_proximoUso;

        // Prende el animal o bloque al que apunta el jugador.
        public static void Encender(ComponentMiner miner, Ray3 ray)
        {
            if (miner == null || miner.ComponentCreature == null) return;

            SubsystemTime tiempo = miner.Project.FindSubsystem<SubsystemTime>(true);
            double ahora = tiempo.GameTime;
            if (s_proximoUso > ahora + Recarga) s_proximoUso = 0; // mundo nuevo: el reloj se reinicio
            if (ahora < s_proximoUso) return;
            s_proximoUso = ahora + Recarga;

            object resultado = miner.Raycast(ray, RaycastMode.Interaction);

            if (resultado is BodyRaycastResult)
            {
                BodyRaycastResult cuerpo = (BodyRaycastResult)resultado;
                if (cuerpo.ComponentBody != null && cuerpo.ComponentBody != miner.ComponentCreature.ComponentBody)
                {
                    ComponentOnFire fuego = cuerpo.ComponentBody.Entity.FindComponent<ComponentOnFire>();
                    if (fuego != null)
                    {
                        fuego.SetOnFire(miner.ComponentCreature, SegundosFuegoAnimal);
                    }
                }
            }
            else if (resultado is TerrainRaycastResult)
            {
                TerrainRaycastResult terreno = (TerrainRaycastResult)resultado;
                EncenderCelda(miner, terreno);
            }
        }

        public static void EncenderCelda(ComponentMiner miner, TerrainRaycastResult terreno)
        {
            SubsystemFireBlockBehavior fuego = miner.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
            CellFace cara = terreno.CellFace;
            Point3 normal = CellFace.FaceToPoint3(cara.Face);

            // Primero el bloque al que apuntas; si no arde, la celda de aire pegada a esa cara.
            if (!fuego.SetCellOnFire(cara.X, cara.Y, cara.Z, 1f))
            {
                fuego.SetCellOnFire(cara.X + normal.X, cara.Y + normal.Y, cara.Z + normal.Z, 1f);
            }
        }

        // El clic derecho no hace nada: nunca se coloca como bloque.
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
        {
            return default(BlockPlacementData);
        }

        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/PaloEnFuego");

            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix bone = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * AjusteModelo;
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_standaloneBlockMesh.AppendModelMeshPart(
                        part, bone, false, false, false, false, Color.White);
                }
            }

            m_texture = ContentManager.Get<Texture2D>("Textures/Items/PaloEnFuego");
            base.Initialize();
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            return new int[] { Terrain.MakeBlockValue(BlockIndex, 0, 0) };
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator,
            TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value,
            Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh,
                m_texture, color, 2f * size, ref matrix, environmentData);
        }
    }

    // Bloque invisible que da luz. Se coloca en el aire junto al jugador mientras
    // sostiene el palo en fuego y se quita cuando lo suelta o se mueve.
    public class PaloLuzBlock : Block
    {
        public const int Index = 702; // CAMBIAR si ya esta ocupado (igual que en Blocks.csv)

        public PaloLuzBlock()
        {
            DefaultDisplayName = "Luz del palo";
            DefaultDescription = "Luz invisible del palo en fuego.";
            DefaultCategory = "Terrain";
            DefaultEmittedLightAmount = 14;
            IsCollidable = false;
            IsTransparent = true;
            DefaultCreativeData = 0;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            return new int[0]; // no aparece en el creativo
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator,
            TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value,
            Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
        }
    }

    public class SubsystemPaloEnFuegoBehavior : SubsystemBlockBehavior, IUpdateable
    {
        private class EstadoJugador
        {
            public float Reloj;
            public bool TieneLuz;
            public Point3 CeldaLuz;
        }

        private SubsystemPlayers m_players;
        private SubsystemTerrain m_terrain;
        private Dictionary<ComponentPlayer, EstadoJugador> m_estados = new Dictionary<ComponentPlayer, EstadoJugador>();
        private List<Point3> m_luzPendiente = new List<Point3>();

        public int UpdateOrder
        {
            get { return 0; }
        }

        public override int[] HandledBlocks
        {
            get { return new int[] { PaloEnFuegoBlock.Index, PaloLuzBlock.Index }; }
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);

            // Luces que se quedaron guardadas en el mundo la ultima vez: se limpian al cargar.
            string guardadas = valuesDictionary.GetValue<string>("LucesPendientes", "");
            if (!string.IsNullOrEmpty(guardadas))
            {
                foreach (string trozo in guardadas.Split(';'))
                {
                    string[] n = trozo.Split(',');
                    int x, y, z;
                    if (n.Length == 3 && int.TryParse(n[0], out x) && int.TryParse(n[1], out y) && int.TryParse(n[2], out z))
                    {
                        m_luzPendiente.Add(new Point3(x, y, z));
                    }
                }
            }

        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            base.Save(valuesDictionary);
            List<string> lista = new List<string>();
            foreach (Point3 p in m_luzPendiente) lista.Add(p.X + "," + p.Y + "," + p.Z);
            foreach (EstadoJugador e in m_estados.Values)
            {
                if (e.TieneLuz) lista.Add(e.CeldaLuz.X + "," + e.CeldaLuz.Y + "," + e.CeldaLuz.Z);
            }
            valuesDictionary.SetValue("LucesPendientes", string.Join(";", lista.ToArray()));
        }

        public void Update(float dt)
        {
            // Limpia luces viejas cuando su zona del mundo ya esta cargada.
            for (int i = m_luzPendiente.Count - 1; i >= 0; i--)
            {
                Point3 p = m_luzPendiente[i];
                if (m_terrain.Terrain.GetChunkAtCell(p.X, p.Z) != null)
                {
                    QuitarLuz(p);
                    m_luzPendiente.RemoveAt(i);
                }
            }

            foreach (ComponentPlayer jugador in m_players.ComponentPlayers)
            {
                EstadoJugador estado;
                if (!m_estados.TryGetValue(jugador, out estado))
                {
                    estado = new EstadoJugador();
                    m_estados[jugador] = estado;
                }

                ComponentMiner miner = jugador.ComponentMiner;
                bool sostiene = miner != null && jugador.ComponentHealth.Health > 0f
                    && Terrain.ExtractContents(miner.ActiveBlockValue) == PaloEnFuegoBlock.Index;

                if (!sostiene)
                {
                    estado.Reloj = 0f;
                    if (estado.TieneLuz)
                    {
                        QuitarLuz(estado.CeldaLuz);
                        estado.TieneLuz = false;
                    }
                    continue;
                }

                // Clic normal (golpear / pulsar en el movil): prende lo que tienes enfrente.
                PlayerInput entrada = jugador.ComponentInput.PlayerInput;
                Ray3? rayo = entrada.Hit ?? entrada.Dig;
                if (rayo.HasValue)
                {
                    PaloEnFuegoBlock.Encender(miner, rayo.Value);
                }

                // Gasta 1 punto por segundo.
                estado.Reloj += dt;
                if (estado.Reloj >= PaloEnFuegoBlock.SegundosPorPunto)
                {
                    estado.Reloj -= PaloEnFuegoBlock.SegundosPorPunto;
                    miner.DamageActiveTool(1);
                }

                // Luz: un bloque invisible en la celda de la cabeza (solo si es aire).
                Vector3 pos = jugador.ComponentBody.Position + new Vector3(0f, 1.5f, 0f);
                Point3 celda = new Point3(Terrain.ToCell(pos.X), Terrain.ToCell(pos.Y), Terrain.ToCell(pos.Z));

                if (estado.TieneLuz && estado.CeldaLuz == celda) continue;

                if (estado.TieneLuz)
                {
                    QuitarLuz(estado.CeldaLuz);
                    estado.TieneLuz = false;
                }

                if (m_terrain.Terrain.GetCellContents(celda.X, celda.Y, celda.Z) == 0)
                {
                    m_terrain.ChangeCell(celda.X, celda.Y, celda.Z, Terrain.MakeBlockValue(PaloLuzBlock.Index, 0, 0));
                    estado.CeldaLuz = celda;
                    estado.TieneLuz = true;
                }
            }
        }

        private void QuitarLuz(Point3 p)
        {
            if (m_terrain.Terrain.GetCellContents(p.X, p.Y, p.Z) == PaloLuzBlock.Index)
            {
                m_terrain.ChangeCell(p.X, p.Y, p.Z, 0);
            }
        }
    }
}
