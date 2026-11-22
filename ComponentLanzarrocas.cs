using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    // Criatura "Lanzarrocas": persigue al jugador, mantiene la distancia y le lanza piedras.
    // Antes de lanzar levanta el brazo derecho (Hand2) con la piedra, igual que el jugador.
    public class ComponentLanzarrocas : Component, IUpdateable, IDrawable
    {
        // ===== STATS (edita aqui) =====
        public const float DetectRange = 35f;   // distancia a la que te detecta
        public const float MinRange = 8f;       // mas cerca que esto: se aleja
        public const float MaxRange = 18f;      // mas lejos que esto: se acerca
        public const float ThrowRange = 28f;    // alcance maximo del lanzamiento
        public const float Cooldown = 3f;       // segundos entre lanzamientos
        public const float ThrowSpeed = 22f;    // velocidad de la roca
        public const float WalkSpeedOrder = 0.8f; // 0..1
        public const int RockId = 79;           // StoneChunkBlock (id sacado con MostrarId)

        // ===== ANIMACION DEL BRAZO (grados; 0 = brazo abajo, 180 = vertical arriba) =====
        public const float WindUpTime = 0.9f;   // tarda en levantar el brazo antes de lanzar
        public const float WindUpAngle = 200f;  // brazo arriba y un poco atras
        public const float ThrowTime = 0.22f;   // duracion del latigazo hacia adelante
        public const float ThrowEndAngle = 70f; // donde termina el brazo tras lanzar
        public const float RecoverTime = 0.5f;  // vuelve a la posicion de descanso
        public const float ArmSign = 1f;        // pon -1 si el brazo gira hacia atras en vez de adelante

        // Piedra en la mano
        public const float RockSize = 0.35f;
        private static readonly Vector3 Shoulder = new Vector3(0.1805f, 1.2418f, -0.0033f); // pivote de Hand2
        private const float ArmLength = 0.6f;
        // ==============================

        private ComponentCreature m_creature;
        private ComponentLocomotion m_locomotion;
        private SubsystemPlayers m_players;
        private SubsystemProjectiles m_projectiles;
        private SubsystemAudio m_audio;
        private SubsystemTerrain m_terrain;
        private readonly PrimitivesRenderer3D m_primitives = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData m_environment = new DrawBlockEnvironmentData();
        private readonly Random m_random = new Random();

        private ModelBone m_handBone;
        private float m_timer;
        private float m_wanderTimer;
        private Vector2 m_wanderDir;
        private float m_swing = -1f;       // <0: sin lanzamiento en curso
        private bool m_pendingRelease;     // aun tiene la piedra en la mano
        private float m_armAngle;          // radianes

        public int UpdateOrder => 100; // despues de ComponentHumanModel, para que no pise el brazo
        public int[] DrawOrders => new[] { 110 };

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_creature = Entity.FindComponent<ComponentCreature>(true);
            m_locomotion = Entity.FindComponent<ComponentLocomotion>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_projectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
            m_audio = Project.FindSubsystem<SubsystemAudio>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_environment.SubsystemTerrain = m_terrain;
            m_timer = Cooldown;

            ComponentModel model = m_creature.ComponentCreatureModel;
            if (model != null && model.Model != null) m_handBone = model.Model.FindBone("Hand2", false);
        }

        public void Update(float dt)
        {
            ComponentBody self = m_creature.ComponentBody;
            Vector3 origin = self.Position + new Vector3(0f, self.BoxSize.Y * 0.8f, 0f);

            // Jugador vivo mas cercano
            ComponentPlayer target = null;
            float best = DetectRange * DetectRange;
            foreach (ComponentPlayer p in m_players.ComponentPlayers)
            {
                if (p.ComponentHealth.Health <= 0f) continue;
                float d2 = Vector3.DistanceSquared(p.ComponentBody.Position, self.Position);
                if (d2 < best) { best = d2; target = p; }
            }

            float dist = MathUtils.Sqrt(best);
            Vector3 aim = Vector3.Zero;

            if (target == null)
            {
                Wander(dt);
            }
            else
            {
                aim = target.ComponentBody.Position + new Vector3(0f, target.ComponentBody.BoxSize.Y * 0.6f, 0f);
                Vector2 toTarget = new Vector2(aim.X - self.Position.X, aim.Z - self.Position.Z);

                m_creature.ComponentCreatureModel.LookAtOrder = aim;

                // Movimiento: acercarse / alejarse / quedarse quieto
                Vector2 dir = toTarget.LengthSquared() > 0.0001f ? Vector2.Normalize(toTarget) : Vector2.Zero;
                if (dist > MaxRange) m_locomotion.WalkOrder = dir * WalkSpeedOrder;
                else if (dist < MinRange) m_locomotion.WalkOrder = -dir * WalkSpeedOrder;
                else m_locomotion.WalkOrder = null;

                if (m_locomotion.WalkOrder.HasValue && self.CollisionVelocityChange.XZ.LengthSquared() > 0.001f)
                    m_locomotion.JumpOrder = 1f;
            }

            // --- Lanzamiento con animacion ---
            bool canThrow = target != null && dist <= ThrowRange;
            if (m_swing < 0f)
            {
                m_timer -= dt;
                if (canThrow && m_timer <= 0f)
                {
                    m_swing = 0f;
                    m_pendingRelease = true;
                }
                else if (!canThrow && m_timer < WindUpTime)
                {
                    m_timer = WindUpTime; // sin objetivo no sigue levantando el brazo
                }
            }
            else
            {
                m_swing += dt;
                // Suelta la piedra a mitad del latigazo
                if (m_pendingRelease && m_swing >= ThrowTime * 0.5f)
                {
                    m_pendingRelease = false;
                    if (target != null)
                    {
                        Vector3 shoot = Vector3.Normalize(aim - origin + new Vector3(0f, dist * 0.12f, 0f));
                        int rock = Terrain.MakeBlockValue(RockId, 0, 0);
                        m_projectiles.FireProjectile(rock, origin + shoot * 0.6f, shoot * ThrowSpeed, Vector3.Zero, m_creature);
                        m_audio.PlaySound("Audio/Throw", 1f, -0.3f, origin, 20f, true);
                    }
                }
                if (m_swing >= ThrowTime + RecoverTime)
                {
                    m_swing = -1f;
                    m_timer = Cooldown;
                }
            }

            // Angulo del brazo segun la fase
            float angle = 0f;
            float wind = MathUtils.DegToRad(WindUpAngle);
            float end = MathUtils.DegToRad(ThrowEndAngle);
            if (m_swing >= 0f)
            {
                if (m_swing < ThrowTime) angle = MathUtils.Lerp(wind, end, m_swing / ThrowTime);
                else angle = MathUtils.Lerp(end, 0f, MathUtils.Saturate((m_swing - ThrowTime) / RecoverTime));
            }
            else if (canThrow && m_timer <= WindUpTime)
            {
                float k = MathUtils.Saturate(1f - m_timer / WindUpTime);
                angle = wind * (k * k * (3f - 2f * k)); // suavizado
            }
            m_armAngle = angle * ArmSign;

            // Mueve el hueso del brazo derecho (gira sobre el hombro, eje X)
            if (m_handBone != null)
            {
                m_creature.ComponentCreatureModel.SetBoneTransform(
                    m_handBone.Index, Matrix.CreateRotationX(m_armAngle) * m_handBone.Transform);
            }
        }

        // Dibuja la piedra en la mano; sigue al brazo mientras se levanta
        public void Draw(Camera camera, int drawOrder)
        {
            if (m_creature == null || m_creature.ComponentHealth.Health <= 0f) return;
            if (m_swing >= 0f && !m_pendingRelease) return; // ya la lanzo: mano vacia hasta recuperarse

            // En espacio del cuerpo (Y arriba): el brazo gira alrededor del eje X desde el hombro
            float a = m_armAngle;
            Vector3 hand = Shoulder + new Vector3(0.1f, 0f, 0.05f)
                         + ArmLength * new Vector3(0f, -MathUtils.Cos(a), -MathUtils.Sin(a));

            ComponentBody body = m_creature.ComponentBody;
            Matrix matrix = Matrix.CreateScale(RockSize)
                          * Matrix.CreateTranslation(hand)
                          * body.Matrix;

            Block block = BlocksManager.Blocks[RockId];
            block.DrawBlock(m_primitives, Terrain.MakeBlockValue(RockId, 0, 0), Color.White, 1f, ref matrix, m_environment);
            m_primitives.Flush(camera.ViewProjectionMatrix);
        }

        private void Wander(float dt)
        {
            m_wanderTimer -= dt;
            if (m_wanderTimer <= 0f)
            {
                m_wanderTimer = m_random.Float(3f, 8f);
                m_wanderDir = m_random.Bool(0.4f)
                    ? Vector2.Zero
                    : Vector2.Normalize(new Vector2(m_random.Float(-1f, 1f), m_random.Float(-1f, 1f)));
            }
            m_locomotion.WalkOrder = m_wanderDir == Vector2.Zero ? (Vector2?)null : m_wanderDir * 0.4f;
        }
    }
}
