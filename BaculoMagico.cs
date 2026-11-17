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
        public const int Index = 900; // CAMBIAR si ya esta ocupado

        public const int Corazones = 3;
        public const float SaludPorCorazon = 0.1f;
        public const double Recarga = 6.0; // segundos entre usos (0 = sin espera)

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
            return new int[] { Terrain.MakeBlockValue(Index) };
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
        private SubsystemTime m_subsystemTime;
        private double m_proximoUso;

        public override int[] HandledBlocks
        {
            get { return new int[] { BaculoMagicoBlock.Index }; }
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
        }

        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (componentMiner == null || componentMiner.ComponentCreature == null)
                return false;

            ComponentHealth salud = componentMiner.ComponentCreature.ComponentHealth;
            if (salud == null || salud.Health >= 1f)
                return false;
            if (m_subsystemTime.GameTime < m_proximoUso)
                return true;

            salud.Heal(BaculoMagicoBlock.Corazones * BaculoMagicoBlock.SaludPorCorazon);
            m_proximoUso = m_subsystemTime.GameTime + BaculoMagicoBlock.Recarga;
            return true;
        }
    }
}
