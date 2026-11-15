using Engine;
using Engine.Graphics;
using Game;

namespace GiganteCode
{
    // Arma "Aniquilador de gigantes".
    // Poder base: 4 (contra cualquier animal). Contra los gigantes sube a 20
    // (ver OnMinerHit en GiganteSpawn.cs).
    public class AniquiladorGigantesBlock : FlatBlock
    {
        // Indice unico del bloque (distinto del de AlmaGiganteBlock)
        public const int Index = 701;

        public const float PoderNormal = 4f;
        public const float PoderContraGigantes = 20f;

        private static Texture2D m_texture;

        public AniquiladorGigantesBlock()
        {
            DefaultTextureSlot = 0;
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer, int value, Color color,
            float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            if (m_texture == null)
            {
                m_texture = ContentManager.Get<Texture2D>("Textures/Items/AniquiladorGigantes");
            }
            BlocksManager.DrawFlatBlock(
                primitivesRenderer, value, size, ref matrix, m_texture, color, false, environmentData);
        }

        public override float GetMeleePower(int value)
        {
            return PoderNormal;
        }

        public override float GetMeleeHitProbability(int value)
        {
            return 0.95f;
        }

        public override int GetDurability(int value)
        {
            return 400;
        }

        public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
        {
            return "Aniquilador de gigantes";
        }

        public override string GetDescription(int value)
        {
            return "Un arma forjada para matar gigantes. Hace poco dano a los animales, pero devastador contra los gigantes.";
        }

        public override string GetCategory(int value)
        {
            return "Tools";
        }
    }
}
