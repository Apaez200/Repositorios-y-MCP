using UnityEngine;

namespace MemeDash
{
    /// <summary>
    /// Sintetiza la musica y los efectos de sonido por codigo (ondas simples),
    /// para no depender de archivos de audio externos.
    /// </summary>
    public static class AudioProcedural
    {
        const int Frecuencia = 44100;

        enum Onda { Cuadrada, Triangulo, Seno }

        // ------------------------------------------------------------ musica

        /// <summary>Loop de 16 tiempos a 128 BPM: bajo, arpegio, bombo y charles.</summary>
        public static AudioClip Musica()
        {
            const float bpm = 128f;
            const int pulsos = 16;
            float pulso = 60f / bpm;
            var d = new float[Mathf.CeilToInt(pulso * pulsos * Frecuencia)];

            // Progresion La menor - Fa - Do - Sol (4 tiempos cada acorde).
            float[] bajos = { 110f, 87.31f, 130.81f, 98f };
            float[][] acordes =
            {
                new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f },
                new[] { 261.63f, 329.63f, 392f },
                new[] { 196f, 246.94f, 293.66f },
            };

            float corchea = pulso / 2f;
            for (int e = 0; e < pulsos * 2; e++)
            {
                int acorde = (e / 8) % 4;
                float f = bajos[acorde] * (e % 2 == 0 ? 1f : 2f);
                Nota(d, e * corchea, corchea * 0.9f, f, 0.16f, 6f, Onda.Cuadrada);
            }

            float semi = pulso / 4f;
            int[] patron = { 0, 1, 2, 1 };
            for (int s = 0; s < pulsos * 4; s++)
            {
                int acorde = (s / 16) % 4;
                float f = acordes[acorde][patron[s % 4]] * 2f;
                Nota(d, s * semi, semi * 0.8f, f, 0.07f, 10f, Onda.Triangulo);
            }

            var rnd = new System.Random(7);
            for (int b = 0; b < pulsos; b++)
            {
                Bombo(d, b * pulso, 0.5f);
                Charles(d, b * pulso + pulso / 2f, 0.12f, rnd);
            }

            Normalizar(d, 0.55f);
            return Crear("MemeDash_Musica", d);
        }

        // ------------------------------------------------------------ efectos

        public static AudioClip Salto() => Barrido("MemeDash_Salto", 0.07f, 520f, 820f, 0.22f, Onda.Cuadrada);
        public static AudioClip Pad() => Barrido("MemeDash_Pad", 0.22f, 300f, 1300f, 0.45f, Onda.Seno);
        public static AudioClip Blip() => Barrido("MemeDash_Blip", 0.05f, 990f, 990f, 0.2f, Onda.Cuadrada);

        /// <summary>Explosion: ruido filtrado mas un tono que cae.</summary>
        public static AudioClip Muerte()
        {
            int n = (int)(0.5f * Frecuencia);
            var d = new float[n];
            var rnd = new System.Random(3);
            float filtrado = 0f, fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Frecuencia;
                float ruido = (float)rnd.NextDouble() * 2f - 1f;
                filtrado += (ruido - filtrado) * 0.25f;
                float f = Mathf.Lerp(200f, 40f, t / 0.5f);
                fase += f / Frecuencia;
                d[i] = (filtrado * 0.8f + Mathf.Sin(fase * 2f * Mathf.PI) * 0.5f) * Mathf.Exp(-t * 7f) * 0.7f;
            }
            return Crear("MemeDash_Muerte", d);
        }

        /// <summary>Arpegio ascendente de victoria (Do mayor).</summary>
        public static AudioClip Victoria()
        {
            float[] notas = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float paso = 0.11f;
            var d = new float[(int)((paso * 3 + 0.45f) * Frecuencia)];
            for (int i = 0; i < notas.Length; i++)
            {
                float dur = i == notas.Length - 1 ? 0.45f : paso;
                Nota(d, i * paso, dur, notas[i], 0.25f, 4f, Onda.Cuadrada);
            }
            return Crear("MemeDash_Victoria", d);
        }

        // ------------------------------------------------------------ utilidades

        static AudioClip Barrido(string nombre, float dur, float f0, float f1, float vol, Onda onda)
        {
            int n = (int)(dur * Frecuencia);
            var d = new float[n];
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                fase += Mathf.Lerp(f0, f1, k) / Frecuencia;
                d[i] = Muestra(onda, fase) * vol * (1f - k);
            }
            return Crear(nombre, d);
        }

        static void Nota(float[] d, float inicio, float dur, float f, float vol, float decaimiento, Onda onda)
        {
            int i0 = (int)(inicio * Frecuencia);
            int largo = (int)(dur * Frecuencia);
            for (int i = 0; i < largo; i++)
            {
                int k = i0 + i;
                if (k >= d.Length) break;
                float t = i / (float)Frecuencia;
                // Ataque y salida cortos para evitar chasquidos.
                float env = Mathf.Exp(-t * decaimiento) * Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (largo - i) / 200f);
                d[k] += Muestra(onda, f * t) * vol * env;
            }
        }

        static void Bombo(float[] d, float inicio, float vol)
        {
            int i0 = (int)(inicio * Frecuencia);
            int largo = (int)(0.2f * Frecuencia);
            float fase = 0f;
            for (int i = 0; i < largo; i++)
            {
                int k = i0 + i;
                if (k >= d.Length) break;
                float t = i / (float)Frecuencia;
                fase += (45f + 110f * Mathf.Exp(-t * 30f)) / Frecuencia;
                d[k] += Mathf.Sin(fase * 2f * Mathf.PI) * vol * Mathf.Exp(-t * 14f);
            }
        }

        static void Charles(float[] d, float inicio, float vol, System.Random rnd)
        {
            int i0 = (int)(inicio * Frecuencia);
            int largo = (int)(0.04f * Frecuencia);
            for (int i = 0; i < largo; i++)
            {
                int k = i0 + i;
                if (k >= d.Length) break;
                float t = i / (float)Frecuencia;
                d[k] += ((float)rnd.NextDouble() * 2f - 1f) * vol * Mathf.Exp(-t * 90f);
            }
        }

        // fase en ciclos (1 = un periodo completo)
        static float Muestra(Onda onda, float fase)
        {
            float p = Mathf.Repeat(fase, 1f);
            switch (onda)
            {
                case Onda.Cuadrada: return p < 0.5f ? 1f : -1f;
                case Onda.Triangulo: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                default: return Mathf.Sin(p * 2f * Mathf.PI);
            }
        }

        static void Normalizar(float[] d, float pico)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = pico / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static AudioClip Crear(string nombre, float[] datos)
        {
            var clip = AudioClip.Create(nombre, datos.Length, 1, Frecuencia, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }
}
