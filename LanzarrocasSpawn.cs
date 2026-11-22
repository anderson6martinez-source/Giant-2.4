using System.Collections.Generic;
using Engine;
using Game;

namespace GiganteCode
{
    // Aparicion natural de Lanzarrocas: en superficie y en cuevas.
    // Usa el mismo sistema que los demas bichos del mod (InitializeCreatureTypes), asi que
    // NO hay que registrar ningun subsistema en el .xdb. Sube este .cs junto a los demas.
    // La plantilla "Lanzarrocas" ya esta en GigantePequeno.xdb (dentro de base24.scmod).
    public class LanzarrocasLoader : ModLoader
    {
        private const string Plantilla = "Lanzarrocas";
        private const float PesoSuperficie = 0.35f;   // frecuencia en superficie
        private const float PesoCueva = 0.5f;         // frecuencia en cuevas
        private const int GrupoMin = 1, GrupoMax = 2;

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
        }

        public override void InitializeCreatureTypes(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            SubsystemTerrain subsystemTerrain =
                subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
            System.Random random = new System.Random();

            Agregar(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                Plantilla + " (superficie)", SpawnLocationType.Surface, PesoSuperficie);
            Agregar(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                Plantilla + " (cueva)", SpawnLocationType.Cave, PesoCueva);
        }

        private static void Agregar(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            SubsystemTerrain subsystemTerrain,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes,
            System.Random random,
            string nombre, SpawnLocationType lugar, float peso)
        {
            SubsystemCreatureSpawn.CreatureType type =
                new SubsystemCreatureSpawn.CreatureType(nombre, lugar, true, false);

            // suelo solido debajo, 2 bloques de aire, sin agua ni lava
            type.SpawnSuitabilityFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                Terrain terrain = subsystemTerrain.Terrain;
                int below = Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                int here = Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y, point.Z));
                int above = Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y + 1, point.Z));

                if (below == WaterBlock.Index || here == WaterBlock.Index || above == WaterBlock.Index
                    || below == MagmaBlock.Index)
                {
                    return 0f;
                }
                if (!BlocksManager.Blocks[below].IsCollidable)
                {
                    return 0f;
                }
                if (BlocksManager.Blocks[here].IsCollidable || BlocksManager.Blocks[above].IsCollidable)
                {
                    return 0f;
                }
                return peso;
            };

            type.SpawnFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int cantidad = random.Next(GrupoMin, GrupoMax + 1);
                return subsystemCreatureSpawn.SpawnCreatures(creatureType, Plantilla, point, cantidad).Count;
            };

            creatureTypes.Add(type);
        }
    }
}
