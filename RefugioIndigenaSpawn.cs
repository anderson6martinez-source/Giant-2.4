using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Engine;
using Game;

namespace GiganteCode
{
    /// <summary>
    /// Genera refugios dispersos en chunks nuevos. No modifica el spawn normal
    /// de los indigenas ni sus estadisticas.
    /// </summary>
    public class RefugioIndigenaSpawnLoader : ModLoader
    {
        private const float Frecuencia = 0.3f;
        private const string PlantillaIndigena = "Indigena";

        // IDs vanilla usados en Survivalcraft.
        private const int Tablones = 21;
        private const int Tronco = 23;
        private const int Antorcha = 31;

        private static readonly ConcurrentQueue<Point3> RefugiosPendientes =
            new ConcurrentQueue<Point3>();

        private SubsystemCreatureSpawn subsystemCreatureSpawn;
        private SubsystemCreatureSpawn.CreatureType tipoRefugio;
        private readonly Random random = new Random();

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("InitializeCreatureTypes", this);
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnProjectDisposed", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
            ModsManager.RegisterHook("TerrainContentsGenerator24Initialize", this);
        }

        public override void InitializeCreatureTypes(
            SubsystemCreatureSpawn subsystem,
            List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
        {
            // Tipo auxiliar sin spawn natural: solo se invoca al encontrar refugios.
            tipoRefugio = new SubsystemCreatureSpawn.CreatureType(
                PlantillaIndigena, SpawnLocationType.Surface, true, false);
            tipoRefugio.SpawnSuitabilityFunction =
                (SubsystemCreatureSpawn.CreatureType t, Point3 p) => 0f;
            tipoRefugio.SpawnFunction =
                (SubsystemCreatureSpawn.CreatureType t, Point3 p) => 0;
            creatureTypes.Add(tipoRefugio);
        }

        public override void TerrainContentsGenerator24Initialize(
            TerrainContentsGenerator24 generator,
            SubsystemTerrain subsystemTerrain)
        {
            generator.ChunkGenerationStep4.Add(
                new ChunkGenerationStep(1500, chunk => GenerarEnChunk(chunk)));
        }

        private static void GenerarEnChunk(TerrainChunk chunk)
        {
            // Probabilidad determinista de 0.3 por cada chunk generado por primera vez.
            unchecked
            {
                int seed = chunk.Coords.X * 73856093 ^ chunk.Coords.Y * 19349663;
                Random rng = new Random(seed);
                if (rng.NextDouble() >= Frecuencia)
                    return;
            }

            // La construcción queda dentro del chunk para no modificar vecinos.
            const int x0 = 4;
            const int z0 = 4;
            const int ancho = 8;
            const int fondo = 8;

            int minY = 255;
            int maxY = 0;
            int sumaY = 0;

            for (int x = x0; x < x0 + ancho; x++)
            {
                for (int z = z0; z < z0 + fondo; z++)
                {
                    int y = BuscarSuelo(chunk, x, z);
                    if (y < 1)
                        return;

                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                    sumaY += y;
                }
            }

            // Evita colocar el refugio en pendientes fuertes o terreno irregular.
            if (maxY - minY > 2)
                return;

            int suelo = sumaY / (ancho * fondo);
            int mundoX = chunk.Origin.X + x0;
            int mundoZ = chunk.Origin.Y + z0;

            // Piso.
            for (int x = 0; x < ancho; x++)
                for (int z = 0; z < fondo; z++)
                    Poner(chunk, x0 + x, suelo, z0 + z, Tablones);

            // Paredes.
            for (int y = 1; y <= 3; y++)
            {
                for (int x = 0; x < ancho; x++)
                {
                    Poner(chunk, x0 + x, suelo + y, z0, Tablones);
                    Poner(chunk, x0 + x, suelo + y, z0 + fondo - 1, Tablones);
                }

                for (int z = 0; z < fondo; z++)
                {
                    Poner(chunk, x0, suelo + y, z0 + z, Tablones);
                    Poner(chunk, x0 + ancho - 1, suelo + y, z0 + z, Tablones);
                }
            }

            // Postes de tronco en las esquinas.
            int[] esquinas = { x0, x0 + ancho - 1 };
            foreach (int x in esquinas)
            {
                foreach (int z in new int[] { z0, z0 + fondo - 1 })
                {
                    for (int y = 1; y <= 4; y++)
                        Poner(chunk, x, suelo + y, z, Tronco);
                }
            }

            // Techo con alero y parte superior escalonada.
            for (int x = -1; x <= ancho; x++)
                for (int z = -1; z <= fondo; z++)
                    Poner(chunk, x0 + x, suelo + 4, z0 + z, Tablones);

            for (int x = 1; x < ancho - 1; x++)
            {
                Poner(chunk, x0 + x, suelo + 5, z0 + 1, Tablones);
                Poner(chunk, x0 + x, suelo + 5, z0 + fondo - 2, Tablones);
            }

            for (int z = 2; z < fondo - 2; z++)
                Poner(chunk, x0 + ancho / 2, suelo + 5, z0 + z, Tablones);

            // Entrada y espacio interior.
            for (int y = 1; y <= 2; y++)
                Poner(chunk, x0 + ancho / 2, suelo + y, z0, 0);

            for (int x = 1; x < ancho - 1; x++)
            {
                for (int z = 1; z < fondo - 1; z++)
                {
                    for (int y = 1; y <= 3; y++)
                        Poner(chunk, x0 + x, suelo + y, z0 + z, 0);
                }
            }

            // Dos antorchas interiores.
            Poner(chunk, x0 + 2, suelo + 2, z0 + 2, Antorcha);
            Poner(chunk, x0 + ancho - 3, suelo + 2, z0 + 2, Antorcha);

            // Los indígenas se crean después, desde SubsystemUpdate (hilo principal).
            RefugiosPendientes.Enqueue(
                new Point3(mundoX + ancho / 2, suelo + 1, mundoZ + fondo / 2));
        }

        private static int BuscarSuelo(TerrainChunk chunk, int x, int z)
        {
            for (int y = 220; y >= 2; y--)
            {
                int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                if (contents == 0 || contents == WaterBlock.Index)
                    continue;

                if (BlocksManager.Blocks[contents].IsCollidable)
                    return y + 1;
            }

            return -1;
        }

        private static void Poner(TerrainChunk chunk, int x, int y, int z, int contents)
        {
            if (x < 0 || x >= TerrainChunk.Size ||
                z < 0 || z >= TerrainChunk.Size ||
                y < 1 || y >= TerrainChunk.Height)
                return;

            chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(contents));
        }

        public override void OnProjectLoaded(Project project)
        {
            subsystemCreatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(true);
            RefugiosPendientes.Clear();
        }

        public override void OnProjectDisposed()
        {
            subsystemCreatureSpawn = null;
            RefugiosPendientes.Clear();
        }

        public override void SubsystemUpdate(float dt)
        {
            if (subsystemCreatureSpawn == null || tipoRefugio == null)
                return;

            // Procesa como máximo dos refugios por frame.
            for (int i = 0; i < 2; i++)
            {
                Point3 posicion;
                if (!RefugiosPendientes.TryDequeue(out posicion))
                    break;

                int cantidad = random.Next(5, 8);
                subsystemCreatureSpawn.SpawnCreatures(
                    tipoRefugio, PlantillaIndigena, posicion, cantidad);
            }
        }
    }
}
