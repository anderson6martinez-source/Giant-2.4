// Barra de vida de Kik's Mod (reconstruida desde el IL de Kik'sMod.dll)
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentHealthBar : Component, IDrawable
    {
        private SubsystemModelsRenderer m_subsystemModelsRenderer;
        private ComponentCreature m_componentCreature;
        private ComponentHealth m_componentHealth;
        private ComponentBody m_componentBody;

        public float MaxDisplayDistance = 10f; // distancia maxima para verla

        private static int[] m_drawOrders = new int[1];
        public int[] DrawOrders => m_drawOrders;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);
            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
            m_componentBody = Entity.FindComponent<ComponentBody>(true);
        }

        public void Draw(Camera camera, int drawOrder)
        {
            if (m_componentHealth == null || m_componentHealth.Health <= 0f) return;

            BoundingBox bb = m_componentBody.BoundingBox;
            Vector3 center = (bb.Min + bb.Max) * 0.5f;
            if (Vector3.Distance(camera.ViewPosition, center) > MaxDisplayDistance) return;

            float hp = m_componentHealth.Health * m_componentHealth.AttackResilience;
            float frac = MathUtils.Saturate(m_componentHealth.Health);

            float height = bb.Max.Y - bb.Min.Y;
            float textOffset = height * 0.3f;
            float barOffset = height * 0.2f;

            Vector3 textPos = new Vector3(center.X, bb.Max.Y + textOffset, center.Z);
            Vector3 barPos = new Vector3(center.X, bb.Max.Y + barOffset, center.Z);

            // no dibujar si esta detras de la camara
            if (Vector3.Dot(camera.ViewDirection, textPos - camera.ViewPosition) <= 0f ||
                Vector3.Dot(camera.ViewDirection, barPos - camera.ViewPosition) <= 0f) return;

            Vector3 right = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY));
            Vector3 down = -Vector3.UnitY;
            float s = 0.005f;

            // Texto: "Nombre 12 HP"
            string text = string.Format("{0} {1:0} HP", m_componentCreature.DisplayName, hp);
            BitmapFont font = ContentManager.Get<BitmapFont>("Fonts/Pericles", null);
            FontBatch3D fontBatch = m_subsystemModelsRenderer.PrimitivesRenderer.FontBatch(
                font, 1, DepthStencilState.None, RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend, SamplerState.LinearClamp);
            fontBatch.QueueText(text, textPos, right * s, down * s, Color.White, TextAnchor.Center);

            // Barra: 120 de ancho, 5 de alto
            Vector3 halfWidth = right * (120f * s * 0.5f);
            Vector3 thick = -down * (5f * s);
            Vector3 p1 = barPos - halfWidth;
            Vector3 p2 = barPos + halfWidth;
            Vector3 p3 = p1 + thick;
            Vector3 p4 = p2 + thick;

            FlatBatch3D flat = m_subsystemModelsRenderer.PrimitivesRenderer.FlatBatch(
                0, null, null, null);

            // parte llena (rojo -> verde segun vida)
            flat.QueueQuad(p1, p3, Vector3.Lerp(p3, p4, frac), Vector3.Lerp(p1, p2, frac),
                Color.Lerp(Color.Red, Color.Green, frac));

            // parte vacia (negro semitransparente)
            if (frac < 1f)
                flat.QueueQuad(Vector3.Lerp(p1, p2, frac), Vector3.Lerp(p3, p4, frac), p4, p2,
                    new Color(0, 0, 0, 180));

            fontBatch.Flush(camera.ViewProjectionMatrix, true);
            flat.Flush(camera.ViewProjectionMatrix, true);
        }
    }
}
