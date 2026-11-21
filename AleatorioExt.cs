using System;

namespace Game
{
    // Metodos de ayuda para System.Random, asi los castillos y bosses
    // no dependen de la version de Engine.Random.
    // UniformInt es inclusivo en ambos extremos, igual que en el resto del codigo.
    public static class AleatorioExt
    {
        public static int UniformInt(this System.Random r, int min, int max)
        {
            return r.Next(min, max + 1);
        }

        public static float UniformFloat(this System.Random r, float min, float max)
        {
            return min + (float)r.NextDouble() * (max - min);
        }
    }
}
