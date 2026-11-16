using Engine;
using Engine.Graphics;
using Game;

namespace GiganteCode
{
    // Lanza de hielo: arma lanzable (no es un bloque colocable), 2 de dano y 50 de durabilidad.
    // Hereda de SpearBlock para conservar el apuntado y el lanzamiento de las lanzas del juego.
    public class LanzaHieloBlock : SpearBlock
    {
        // Indice unico del bloque (distinto del de AlmaGiganteBlock). Si choca con otro mod, cambialo.
        public new const int Index = 701;

        private BlockMesh m_mallaLanza;
        private static Texture2D m_textura;

        // SpearBlock exige (handleTextureSlot, headTextureSlot): los huecos de textura del atlas
        // para el mango y la punta. Aqui no se usan para dibujar (DrawBlock usa su propia textura
        // de hielo), asi que 0, 0 es suficiente.
        public LanzaHieloBlock() : base(0, 0)
        {
            // Nombre con el que se usa en las recetas (.cr)
            CraftingId = "lanzahielo";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Mismo modelo que la lanza normal (Spear.dae), pero con la textura de hielo
            Model modelo = ContentManager.Get<Model>("Models/LanzaHielo");
            Matrix mango = BlockMesh.GetBoneAbsoluteTransform(modelo.FindMesh("Handle").ParentBone);
            Matrix punta = BlockMesh.GetBoneAbsoluteTransform(modelo.FindMesh("Head").ParentBone);
            m_mallaLanza = new BlockMesh();
            m_mallaLanza.AppendModelMeshPart(
                modelo.FindMesh("Handle").MeshParts[0],
                mango * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
            m_mallaLanza.AppendModelMeshPart(
                modelo.FindMesh("Head").MeshParts[0],
                punta * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer, int value, Color color,
            float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            if (m_textura == null)
            {
                m_textura = ContentManager.Get<Texture2D>("Textures/Items/LanzaHielo");
            }
            BlocksManager.DrawMeshBlock(
                primitivesRenderer, m_mallaLanza, m_textura, color, 2f * size, ref matrix, environmentData);
        }

        public override float GetMeleePower(int value)
        {
            return 2f;
        }

        public override float GetProjectilePower(int value)
        {
            return 2f;
        }

        public override int GetDurability(int value)
        {
            return 50;
        }

        public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
        {
            return "Lanza de hielo";
        }

        public override string GetDescription(int value)
        {
            return "Una lanza tallada en hielo eterno. Hace poco dano, pero se puede lanzar.";
        }

        public override string GetCategory(int value)
        {
            return "Weapons";
        }
    }
}
