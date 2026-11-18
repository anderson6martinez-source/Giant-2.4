using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // ---------------------------------------------------------------
    // BACULO MAGICO: al usarlo (clic derecho) cura 3 corazones.
    //
    // Archivos del mod (rutas dentro del .scmod / carpeta de contenido):
    //   Models/BaculoMagico.dae
    //   Textures/Items/BaculoMagico.png   (atlas: madera + corazon)
    //
    // Ajusta aqui si hace falta:
    //   - Index: debe ser un id libre en tu mod.
    //   - SaludPorCorazon: 0.1 = 10 corazones llenan la vida (1.0).
    // ---------------------------------------------------------------
    public class BaculoMagicoBlock : Block
    {
        public const int Index = 200; // CAMBIAR si ya esta ocupado (debe ser el mismo numero que en Blocks.csv)

        public const int Corazones = 3;
        public const float SaludPorCorazon = 0.1f;
        public const double Recarga = 6.0; // segundos entre usos (0 = sin espera)

        // Stats de herramienta
        public const int Durabilidad = 40;          // usos (cada cura gasta 1)
        public const float PoderMelee = 1.5f;       // dano al golpear
        public const float ProbabilidadGolpe = 0.7f;

        // Si el baculo se ve girado en el icono o en la mano, cambia esta matriz
        // (por ejemplo Matrix.CreateRotationX(MathUtils.PI / 2f)).
        private static readonly Matrix AjusteModelo = Matrix.Identity;

        private BlockMesh m_standaloneBlockMesh = new BlockMesh();
        private Texture2D m_texture;

        public BaculoMagicoBlock()
        {
            DefaultDisplayName = "Baculo Magico";
            DefaultDescription = "Un baculo con un corazon flotante. Al usarlo cura 3 corazones.";
            DefaultCategory = "Tools";

            // Es una herramienta: no se coloca, no se apila, tiene durabilidad y dano propio.
            Durability = Durabilidad;
            DefaultMeleePower = PoderMelee;
            DefaultMeleeHitProbability = ProbabilidadGolpe;
            DefaultProjectilePower = 1f; // no es lanzable: solo cura (no tiene comportamiento de apuntar)
            MaxStacking = 1;
            IsCollidable = false;
            DefaultCreativeData = 0; // sin esto el creativo puede no listar el bloque
        }

        public override string GetCategory(int value)
        {
            return "Tools";
        }

        private static double s_proximoUso;

        // Cura al que lo usa. La llaman el comportamiento (clic derecho) y, por si el comportamiento no
        // esta registrado en el .xdb, tambien GetPlacementValue (el juego la llama al intentar "colocarlo").
        public static void Curar(ComponentMiner componentMiner)
        {
            if (componentMiner == null || componentMiner.ComponentCreature == null) return;
            ComponentHealth salud = componentMiner.ComponentCreature.ComponentHealth;
            if (salud == null || salud.Health >= 1f) return;

            SubsystemTime tiempo = componentMiner.Project.FindSubsystem<SubsystemTime>(true);
            double ahora = tiempo.GameTime;
            if (s_proximoUso > ahora + Recarga) s_proximoUso = 0; // mundo nuevo: el reloj se reinicio
            if (ahora < s_proximoUso) return;

            salud.Heal(Corazones * SaludPorCorazon);
            s_proximoUso = ahora + Recarga;
            componentMiner.DamageActiveTool(1); // gasta 1 de durabilidad (de 40)
        }

        // Nunca se coloca como bloque (era lo que creaba el bloque invisible): cura y no devuelve nada
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
        {
            Curar(componentMiner);
            return default(BlockPlacementData);
        }

        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/BaculoMagico");

            // Se recorren todas las mallas y partes del modelo para que no se
            // pierda ninguna pieza (corazon, cana y pieza superior).
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix bone = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * AjusteModelo;
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_standaloneBlockMesh.AppendModelMeshPart(
                        part, bone, false, false, false, false, Color.White);
                }
            }

            m_texture = ContentManager.Get<Texture2D>("Textures/Items/BaculoMagico");
            base.Initialize();
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            // BlockIndex es el indice real que el juego le dio al bloque (no depende de la constante Index)
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

    public class SubsystemBaculoMagicoBehavior : SubsystemBlockBehavior
    {
        public override int[] HandledBlocks
        {
            get { return new int[] { BaculoMagicoBlock.Index }; }
        }

        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            // Siempre true: el uso queda consumido y el juego nunca intenta colocar un bloque.
            BaculoMagicoBlock.Curar(componentMiner);
            return true;
        }
    }
}
