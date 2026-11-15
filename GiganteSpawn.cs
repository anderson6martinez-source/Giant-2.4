// Aparicion natural del "Gigante pequeno" en todos los biomas.
// Escrito para la API de mods de Survivalcraft (ModLoader). No se ha podido compilar
// aqui: si el compilador da errores, pegame el registro de GitHub y lo corrijo.
using System;
using System.Collections.Generic;
using Engine;

namespace Game
{
    public class GiganteSpawnLoader : ModLoader
    {
        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
        }

        public override void InitializeCreatureTypes(SubsystemCreatureSpawn subsystemCreatureSpawn,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            SubsystemTerrain subsystemTerrain = subsystemCreatureSpawn.Project.FindSubsystem<SubsystemTerrain>(true);
            Random azar = new Random();

            creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType(
                "GigantePequeno", SpawnLocationType.Surface, true, false)
            {
                // Aparece en cualquier bioma: basta con que el suelo sea solido y no sea agua.
                SpawnSuitabilityFunction = (SubsystemCreatureSpawn.CreatureType tipo, Point3 punto) =>
                {
                    int celda = subsystemTerrain.Terrain.GetCellValueFast(punto.X, punto.Y - 1, punto.Z);
                    int contenido = Terrain.ExtractContents(celda);
                    if (contenido == 0 || contenido == WaterBlock.Index) return 0f;
                    return BlocksManager.Blocks[contenido].IsCollidable ? 0.7f : 0f;
                },
                SpawnFunction = (SubsystemCreatureSpawn.CreatureType tipo, Point3 punto) =>
                    subsystemCreatureSpawn.SpawnCreatures(tipo, "GigantePequeno", punto, 1).Count
            });
        }
    }
}
