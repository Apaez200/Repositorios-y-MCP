using System.Collections.Generic;
using UnityEngine;

namespace MemeDash
{
    /// <summary>
    /// Lienzo de pixeles para dibujar sprites por codigo.
    /// El origen (0,0) esta abajo a la izquierda, igual que en Texture2D.
    /// </summary>
    public sealed class Lienzo
    {
        public readonly int Ancho, Alto;
        readonly Color[] pixeles;

        public Lienzo(int ancho, int alto)
        {
            Ancho = ancho;
            Alto = alto;
            pixeles = new Color[ancho * alto]; // transparente
        }

        /// <summary>Pinta un pixel mezclando por alfa.</summary>
        public void Punto(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= Ancho || y >= Alto) return;
            int i = y * Ancho + x;
            if (c.a >= 0.999f) { pixeles[i] = c; return; }

            Color d = pixeles[i];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0f) { pixeles[i] = Color.clear; return; }
            pixeles[i] = new Color(
                (c.r * c.a + d.r * d.a * (1f - c.a)) / a,
                (c.g * c.a + d.g * d.a * (1f - c.a)) / a,
                (c.b * c.a + d.b * d.a * (1f - c.a)) / a,
                a);
        }

        /// <summary>Rectangulo relleno, limites inclusivos.</summary>
        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Punto(x, y, c);
        }

        /// <summary>Contorno rectangular de grosor g.</summary>
        public void Marco(int x0, int y0, int x1, int y1, int g, Color c)
        {
            Rect(x0, y0, x1, y0 + g - 1, c);
            Rect(x0, y1 - g + 1, x1, y1, c);
            Rect(x0, y0 + g, x0 + g - 1, y1 - g, c);
            Rect(x1 - g + 1, y0 + g, x1, y1 - g, c);
        }

        public void Elipse(float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Punto(x, y, c);
                }
        }

        public void Circulo(float cx, float cy, float r, Color c) => Elipse(cx, cy, r, r, c);

        /// <summary>Segmento con extremos redondeados.</summary>
        public void Linea(float x0, float y0, float x1, float y1, float grosor, Color c)
        {
            float r = grosor * 0.5f;
            var a = new Vector2(x0, y0);
            var ab = new Vector2(x1, y1) - a;
            float largo2 = Mathf.Max(ab.sqrMagnitude, 1e-5f);

            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - r), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + r);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - r), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + r);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / largo2);
                    if ((a + ab * t - p).sqrMagnitude <= r * r) Punto(x, y, c);
                }
        }

        public void Triangulo(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            float area = Lado(a, b, c);
            if (Mathf.Abs(area) < 1e-4f) return;

            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Lado(b, c, p), w1 = Lado(c, a, p), w2 = Lado(a, b, p);
                    bool dentro = area > 0f
                        ? (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                        : (w0 <= 0f && w1 <= 0f && w2 <= 0f);
                    if (dentro) Punto(x, y, col);
                }
        }

        static float Lado(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        public Texture2D ATextura(FilterMode filtro = FilterMode.Bilinear)
        {
            var t = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false)
            {
                filterMode = filtro,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixels(pixeles);
            t.Apply(false, true); // sin mipmaps; libera la copia en CPU
            return t;
        }
    }

    /// <summary>Un personaje seleccionable: nombre, textura (para el menu) y sprite (para el juego).</summary>
    public sealed class Skin
    {
        public readonly string Nombre;
        public readonly Texture2D Textura;
        public readonly Sprite Sprite;

        public Skin(string nombre, Texture2D textura, Sprite sprite)
        {
            Nombre = nombre;
            Textura = textura;
            Sprite = sprite;
        }
    }

    /// <summary>Sprites que usa el nivel.</summary>
    public sealed class ArteNivel
    {
        public Sprite Bloque, Pincho, Pad, Suelo, Meta, Cuadrito, Fondo, Deco;
    }

    /// <summary>
    /// Genera todo el arte del juego por codigo, sin depender de imagenes externas.
    /// Las caras son dibujos originales inspirados en memes clasicos.
    /// </summary>
    public static class ArteProcedural
    {
        static readonly Color N = Color.black;
        static readonly Color B = Color.white;
        static readonly Color Rosa = new Color(1f, 0.45f, 0.6f);

        // ------------------------------------------------------------ nivel

        public static ArteNivel CrearArteNivel()
        {
            return new ArteNivel
            {
                Bloque = Bloque(),
                Pincho = Pincho(),
                Pad = Pad(),
                Suelo = Suelo(),
                Meta = Meta(),
                Cuadrito = Cuadrito(),
                Fondo = Fondo(),
                Deco = Deco()
            };
        }

        static Sprite ASprite(Lienzo l, float ppu, Vector2 pivote, FilterMode filtro = FilterMode.Bilinear)
        {
            var t = l.ATextura(filtro);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivote, ppu, 0, SpriteMeshType.FullRect);
        }

        // Bloque: relleno casi negro y borde blanco. El borde toma el color del nivel al teñirlo.
        static Sprite Bloque()
        {
            var l = new Lienzo(64, 64);
            l.Rect(0, 0, 63, 63, B);
            l.Rect(4, 4, 59, 59, new Color(0.06f, 0.06f, 0.1f));
            l.Marco(15, 15, 48, 48, 2, new Color(1f, 1f, 1f, 0.35f));
            return ASprite(l, 64f, new Vector2(0.5f, 0.5f));
        }

        static Sprite Pincho()
        {
            var l = new Lienzo(64, 64);
            l.Triangulo(new Vector2(2, 1), new Vector2(62, 1), new Vector2(32, 63), B);
            l.Triangulo(new Vector2(10, 5), new Vector2(54, 5), new Vector2(32, 51), new Color(0.06f, 0.06f, 0.1f));
            return ASprite(l, 64f, new Vector2(0.5f, 0.5f));
        }

        // Pad de salto amarillo (1 x 0.25 unidades, pivote abajo).
        static Sprite Pad()
        {
            var l = new Lienzo(64, 16);
            l.Elipse(32, 0, 29, 15, new Color(0.85f, 0.6f, 0f));
            l.Elipse(32, 0, 25, 12, new Color(1f, 0.85f, 0.1f));
            l.Elipse(32, 0, 15, 7, new Color(1f, 0.97f, 0.65f));
            return ASprite(l, 64f, new Vector2(0.5f, 0f));
        }

        // Suelo: 1 x 8 unidades (igual al alto del suelo), asi solo se repite en horizontal
        // y la franja brillante aparece una sola vez, arriba.
        static Sprite Suelo()
        {
            const int alto = 512;
            var l = new Lienzo(64, alto);
            for (int y = 0; y < alto; y++)
            {
                float k = Mathf.Lerp(0.06f, 0.32f, Mathf.Pow(y / (alto - 1f), 3f));
                l.Rect(0, y, 63, y, new Color(k, k, k * 1.2f));
            }
            l.Rect(0, alto - 64, 1, alto - 6, new Color(1f, 1f, 1f, 0.12f)); // union entre losetas
            l.Rect(0, alto - 5, 63, alto - 1, B);                           // borde superior
            return ASprite(l, 64f, new Vector2(0.5f, 1f));
        }

        // Meta: tablero de ajedrez, 1 unidad de ancho, en mosaico vertical.
        static Sprite Meta()
        {
            var l = new Lienzo(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    l.Punto(x, y, ((x / 8) + (y / 8)) % 2 == 0 ? B : N);
            return ASprite(l, 16f, new Vector2(0.5f, 0f), FilterMode.Point);
        }

        static Sprite Cuadrito()
        {
            var l = new Lienzo(8, 8);
            l.Rect(0, 0, 7, 7, B);
            return ASprite(l, 8f, new Vector2(0.5f, 0.5f), FilterMode.Point);
        }

        // Degradado vertical blanco -> gris. Se tiñe con el color del nivel.
        static Sprite Fondo()
        {
            var l = new Lienzo(1, 64);
            for (int y = 0; y < 64; y++)
            {
                float k = Mathf.Lerp(0.35f, 1f, y / 63f);
                l.Punto(0, y, new Color(k, k, k));
            }
            return ASprite(l, 1f, new Vector2(0.5f, 0.5f));
        }

        // Cuadros tenues para el fondo con parallax (8 x 8 unidades, en mosaico).
        static Sprite Deco()
        {
            var l = new Lienzo(128, 128);
            var c = new Color(1f, 1f, 1f, 0.09f);
            l.Marco(8, 8, 52, 52, 3, c);
            l.Marco(70, 26, 120, 76, 3, c);
            l.Marco(24, 78, 58, 112, 3, c);
            l.Marco(86, 92, 112, 118, 2, c);
            return ASprite(l, 16f, new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------ personajes

        public static List<Skin> CrearSkins()
        {
            return new List<Skin>
            {
                CrearSkin("Sorprendido", new Color(1f, 0.84f, 0.2f), Sorprendido),
                CrearSkin("Derp", new Color(0.45f, 0.85f, 0.35f), Derp),
                CrearSkin("El Chad", new Color(0.3f, 0.55f, 1f), Chad),
                CrearSkin("Llorón", new Color(0.7f, 0.55f, 1f), Lloron),
                CrearSkin("Gato Gritón", new Color(1f, 0.6f, 0.2f), GatoGriton),
                CrearSkin("Malicioso", new Color(0.95f, 0.3f, 0.3f), Malicioso),
                CrearSkin("Lentes Cool", new Color(1f, 0.45f, 0.75f), LentesCool),
            };
        }

        /// <summary>
        /// Carga imagenes propias desde Assets/Resources/Skins (PNG o JPG).
        /// Cada imagen se convierte en un personaje con el nombre del archivo.
        /// </summary>
        public static List<Skin> CargarSkinsPersonalizados()
        {
            var lista = new List<Skin>();
            foreach (var tex in Resources.LoadAll<Texture2D>("Skins"))
            {
                float ppu = Mathf.Max(tex.width, tex.height); // cabe en 1 unidad
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                lista.Add(new Skin(tex.name, tex, sprite));
            }
            return lista;
        }

        static Skin CrearSkin(string nombre, Color fondo, System.Action<Lienzo, Color> cara)
        {
            var l = new Lienzo(64, 64);
            BaseCubo(l, fondo);
            cara(l, fondo);
            var tex = l.ATextura();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
            return new Skin(nombre, tex, sprite);
        }

        // Cubo con contorno negro, brillo arriba y sombra abajo.
        static void BaseCubo(Lienzo l, Color f)
        {
            l.Rect(0, 0, 63, 63, N);
            l.Rect(4, 4, 59, 59, f);
            l.Rect(4, 50, 59, 59, Color.Lerp(f, B, 0.2f));
            l.Rect(4, 4, 59, 9, Color.Lerp(f, N, 0.2f));
        }

        static void Ojo(Lienzo l, float x, float y, float r, float px, float py, float rp)
        {
            l.Circulo(x, y, r + 1.5f, N);
            l.Circulo(x, y, r, B);
            l.Circulo(px, py, rp, N);
        }

        static void Sorprendido(Lienzo l, Color f)
        {
            l.Linea(13, 52, 26, 56, 3, N);
            l.Linea(38, 56, 51, 52, 3, N);
            Ojo(l, 21, 40, 8, 21, 40, 3.2f);
            Ojo(l, 43, 40, 8, 43, 40, 3.2f);
            l.Elipse(32, 18, 8, 10, N);
            l.Elipse(32, 13, 5, 4, Rosa);
        }

        static void Derp(Lienzo l, Color f)
        {
            Ojo(l, 20, 41, 8.5f, 25, 38, 3.5f);   // ojo grande mirando al centro
            Ojo(l, 45, 44, 5f, 42, 44, 2.5f);     // ojo chico, bizco
            l.Linea(16, 22, 30, 19, 3, N);
            l.Linea(30, 19, 48, 23, 3, N);
            l.Rect(25, 13, 32, 19, N);            // diente
            l.Rect(26, 14, 31, 19, B);
            l.Elipse(40, 15, 5, 6, Rosa);         // lengua de fuera
            l.Linea(40, 11, 40, 18, 1.2f, new Color(0.8f, 0.25f, 0.4f));
        }

        static void Chad(Lienzo l, Color f)
        {
            l.Rect(4, 10, 59, 22, Color.Lerp(f, N, 0.22f)); // mandibula marcada
            var barba = new Color(0f, 0f, 0f, 0.35f);
            for (int x = 12; x <= 52; x += 5)
                for (int y = 11; y <= 20; y += 4)
                    l.Circulo(x + (y % 8 == 3 ? 2 : 0), y, 0.9f, barba);
            l.Linea(13, 48, 28, 51, 4.5f, N);     // cejas gruesas
            l.Linea(36, 51, 51, 48, 4.5f, N);
            l.Elipse(21, 40, 6, 3, B);            // ojos entrecerrados
            l.Elipse(43, 40, 6, 3, B);
            l.Circulo(22, 40, 2, N);
            l.Circulo(44, 40, 2, N);
            l.Linea(14, 43, 28, 43, 2.5f, N);
            l.Linea(36, 43, 50, 43, 2.5f, N);
            l.Linea(32, 37, 29, 29, 2, N);        // nariz
            l.Linea(29, 29, 34, 29, 2, N);
            l.Linea(22, 17, 38, 17, 3, N);        // sonrisa de lado
            l.Linea(38, 17, 45, 22, 3, N);
        }

        static void Lloron(Lienzo l, Color f)
        {
            var lagrima = new Color(0.45f, 0.85f, 1f, 0.9f);
            l.Rect(15, 4, 21, 37, lagrima);       // rios de lagrimas
            l.Rect(43, 4, 49, 37, lagrima);
            l.Linea(14, 45, 23, 40, 3, N);        // ojos apretados > <
            l.Linea(14, 35, 23, 40, 3, N);
            l.Linea(50, 45, 41, 40, 3, N);
            l.Linea(50, 35, 41, 40, 3, N);
            l.Linea(14, 49, 25, 54, 3, N);        // cejas tristes
            l.Linea(39, 54, 50, 49, 3, N);
            l.Elipse(32, 18, 10, 7, N);           // boca abierta llorando
            l.Elipse(32, 14, 6, 3, Rosa);
        }

        static void GatoGriton(Lienzo l, Color f)
        {
            var oreja = Color.Lerp(f, N, 0.45f);
            l.Triangulo(new Vector2(5, 59), new Vector2(22, 59), new Vector2(5, 42), oreja);
            l.Triangulo(new Vector2(8, 56), new Vector2(17, 56), new Vector2(8, 47), Rosa);
            l.Triangulo(new Vector2(58, 59), new Vector2(41, 59), new Vector2(58, 42), oreja);
            l.Triangulo(new Vector2(55, 56), new Vector2(46, 56), new Vector2(55, 47), Rosa);
            Ojo(l, 21, 37, 6.5f, 21, 37, 2f);     // ojos enojados
            Ojo(l, 43, 37, 6.5f, 43, 37, 2f);
            l.Linea(12, 48, 28, 43, 3.5f, N);     // cejas en V
            l.Linea(36, 43, 52, 48, 3.5f, N);
            l.Triangulo(new Vector2(29, 30), new Vector2(35, 30), new Vector2(32, 27), Rosa);
            l.Elipse(32, 16, 8, 9, N);            // boca gritando
            l.Triangulo(new Vector2(26, 24), new Vector2(30, 24), new Vector2(28, 19), B);
            l.Triangulo(new Vector2(34, 24), new Vector2(38, 24), new Vector2(36, 19), B);
            l.Linea(4, 26, 18, 24, 1.5f, N);      // bigotes
            l.Linea(4, 20, 18, 21, 1.5f, N);
            l.Linea(60, 26, 46, 24, 1.5f, N);
            l.Linea(60, 20, 46, 21, 1.5f, N);
        }

        static void Malicioso(Lienzo l, Color f)
        {
            // Sonrisa enorme: media elipse negra con dientes blancos.
            l.Elipse(32, 23, 18, 11, N);
            l.Elipse(32, 23, 15, 8, B);
            l.Rect(10, 23, 54, 34, f);            // recorta la mitad de arriba
            for (int x = 20; x <= 44; x += 6) l.Linea(x, 15, x, 23, 1.4f, N);
            l.Linea(14, 23, 50, 23, 2.5f, N);
            l.Elipse(21, 40, 7, 4.5f, B);         // ojos
            l.Elipse(43, 40, 7, 4.5f, B);
            l.Circulo(23, 39, 2.5f, N);
            l.Circulo(41, 39, 2.5f, N);
            l.Linea(12, 52, 28, 45, 4, N);        // cejas malvadas
            l.Linea(36, 45, 52, 52, 4, N);
        }

        static void LentesCool(Lienzo l, Color f)
        {
            l.Rect(10, 36, 29, 46, N);            // lentes pixelados
            l.Rect(35, 36, 54, 46, N);
            l.Rect(13, 33, 26, 36, N);
            l.Rect(38, 33, 51, 36, N);
            l.Rect(29, 42, 35, 45, N);
            l.Rect(4, 42, 10, 45, N);
            l.Rect(54, 42, 59, 45, N);
            l.Rect(13, 42, 16, 44, B);            // brillos
            l.Rect(38, 42, 41, 44, B);
            l.Linea(24, 20, 36, 18, 3, N);        // sonrisita
            l.Linea(36, 18, 42, 22, 3, N);
        }
    }
}
