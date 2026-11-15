using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;

namespace GiganteCode
{
    // Registra el spawn natural de los tres gigantes:
    // GigantePequeno, GigantePequenoCanibal y GigantePequenoMuerto.
    // Esta clase REEMPLAZA al GiganteCode.dll original (ya registra los tres).
    public class GiganteSpawnLoader : ModLoader
    {
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

            // nombre de plantilla, peso de spawn, min y max de criaturas por grupo
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequeno", 0.6f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoCanibal", 0.4f, 1, 1);
            AddGiant(subsystemCreatureSpawn, subsystemTerrain, creatureTypes, random,
                "GigantePequenoMuerto", 0.5f, 1, 2);
        }

        private static void AddGiant(
            SubsystemCreatureSpawn subsystemCreatureSpawn,
            SubsystemTerrain subsystemTerrain,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes,
            System.Random random,
            string templateName, float weight, int minCount, int maxCount)
        {
            SubsystemCreatureSpawn.CreatureType type =
                new SubsystemCreatureSpawn.CreatureType(
                    templateName, SpawnLocationType.Surface, true, false);

            // Adecuado si hay suelo solido debajo, aire donde aparece (2 bloques de alto) y no hay agua
            type.SpawnSuitabilityFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int below = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
                int here = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y, point.Z));
                int above = Terrain.ExtractContents(
                    subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y + 1, point.Z));

                if (below == WaterBlock.Index || here == WaterBlock.Index || above == WaterBlock.Index)
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
                return weight;
            };

            type.SpawnFunction = (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) =>
            {
                int count = random.Next(minCount, maxCount + 1);
                return subsystemCreatureSpawn.SpawnCreatures(creatureType, templateName, point, count).Count;
            };

            creatureTypes.Add(type);
        }
    }
}
