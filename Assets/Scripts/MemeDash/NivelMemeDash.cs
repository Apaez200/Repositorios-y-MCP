using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MemeDash
{
    public enum TipoObjeto { Pincho, Pad, Meta }

    /// <summary>Marca un objeto del nivel para que el jugador sepa como reaccionar al tocarlo.</summary>
    public class ObjetoNivel : MonoBehaviour
    {
        public TipoObjeto Tipo;

        Vector3 escalaBase;
        float fase;

        void Start()
        {
            escalaBase = transform.localScale;
            fase = transform.position.x;
            enabled = Tipo == TipoObjeto.Pad; // solo los pads se animan
        }

        void Update()
        {
            float k = 1f + 0.12f * Mathf.Sin(Time.time * 8f + fase);
            transform.localScale = new Vector3(escalaBase.x, escalaBase.y * k, escalaBase.z);
        }
    }

    /// <summary>
    /// Construye el nivel a partir de un mapa de texto.
    /// Cada trozo tiene 5 filas (arriba -> abajo); la ultima fila apoya en el suelo (y = 0).
    ///   '#' bloque solido   '^' pincho (mata)   '=' pad de salto   '.' vacio
    /// 1 caracter = 1 bloque = 1 unidad de Unity.
    /// </summary>
    public class NivelMemeDash
    {
        public const int Filas = 5;

        /// <summary>Datos de un nivel: nombre, dificultad, velocidad y sus trozos.</summary>
        public sealed class Definicion
        {
            public string Nombre;
            public string Dificultad;
            public Color ColorDificultad;
            public float Velocidad;     // bloques por segundo
            public float TonoInicial;   // 0-1, color con el que arranca el nivel
            public float TonoMusica;    // pitch de la musica (mas rapido = mas tension)
            public string[][] Trozos;
        }

        /// <summary>
        /// Crea un trozo de 5 filas. Se pasan solo las filas de abajo (de arriba a abajo);
        /// la ultima es la que apoya en el suelo y las que faltan arriba quedan vacias.
        /// </summary>
        static string[] T(params string[] filas)
        {
            var trozo = new string[Filas];
            int desfase = Filas - filas.Length;
            for (int r = 0; r < Filas; r++) trozo[r] = r < desfase ? "" : filas[r - desfase];
            return trozo;
        }

        // Referencia (salto de 2.2 bloques de alto y 0.47 s en el aire):
        //   alcance de un salto = velocidad * 0.47  ->  9: 4.2   10: 4.7   11: 5.2   12: 5.6 bloques
        //   un pad lanza ~4.6 bloques de alto.
        // Todos los niveles se comprueban con VerificadorNivel.
        public static readonly Definicion[] Niveles =
        {
            new Definicion
            {
                Nombre = "Primeros Pasos", Dificultad = "Fácil", ColorDificultad = new Color(0.4f, 0.9f, 1f),
                Velocidad = 9f, TonoInicial = 0.55f, TonoMusica = 0.92f,
                Trozos = new[]
                {
                    T("............^............"),
                    T("............^...........^......"),
                    T("......###.................."),
                    T("..........^^................"),
                    T("......##......##............"),
                    // pad para subir a un muro de 2 bloques
                    T("......######........",
                      "..=...######........"),
                    T("..........^^.....^........"),
                    T("..............."),
                }
            },
            new Definicion
            {
                Nombre = "Saltarín", Dificultad = "Normal", ColorDificultad = new Color(0.5f, 1f, 0.4f),
                Velocidad = 10f, TonoInicial = 0.08f, TonoMusica = 1f,
                Trozos = new[]
                {
                    T("........^.......^....."),
                    T("....^^........^^....."),
                    T("....######....###^......"),
                    // escalera flotante sobre pinchos
                    T("............####........",
                      "........####............",
                      "....####^^^^^^^^........"),
                    // pad para subir a un muro de 3 bloques
                    T(".....########...........",
                      ".....########...........",
                      "..=..########..........."),
                    // plataforma larga con pinchos encima
                    T("......^......^^.........",
                      "...###############......"),
                    // pad sobre un foso de pinchos hacia una plataforma
                    T("......########..........",
                      "",
                      "..=.^^^^^^^^^^.........."),
                    // escalon de 2, torre y pinchos dobles
                    T("..........####..........",
                      "...^..##..####......^^.."),
                    T("............"),
                }
            },
            new Definicion
            {
                Nombre = "Pinchos Locos", Dificultad = "Difícil", ColorDificultad = new Color(1f, 0.7f, 0.2f),
                Velocidad = 11f, TonoInicial = 0.78f, TonoMusica = 1.06f,
                Trozos = new[]
                {
                    T("........^^^..........^^^........"),
                    // islas de bloques entre pinchos
                    T("...###^^###^^###^^###......"),
                    // escalera sobre un foso largo
                    T("............####...........",
                      "........####...............",
                      "....####^^^^^^^^^^^........"),
                    // tunel: aqui NO hay que saltar; el pincho viene justo a la salida
                    T("....########..........",
                      "",
                      "...............^......"),
                    // pad hacia una plataforma; hay que saltar al final para librar el foso
                    T("......######..........",
                      "",
                      "..=.^^^^^^^^^^^^^......"),
                    T("......^^^.......^^.^^........"),
                    T(".............."),
                }
            },
            new Definicion
            {
                Nombre = "Modo Demente", Dificultad = "Demente", ColorDificultad = new Color(1f, 0.3f, 0.35f),
                Velocidad = 12f, TonoInicial = 0.33f, TonoMusica = 1.12f,
                Trozos = new[]
                {
                    T("......^^^.....^^^.....^^^........."),
                    T("...###^^^###^^^###.........."),
                    // escalera con un pincho arriba y foso abajo (se aterriza justo despues del foso)
                    T("............^...........",
                      "..........####..........",
                      "......####..............",
                      "..####^^^^^^^^^^........"),
                    // tunel seguido de pinchos dobles
                    T("....##########............",
                      "",
                      ".................^^........"),
                    // pad hacia una plataforma con un pincho encima, sobre un foso
                    T("..........^.........",
                      "......########......",
                      "",
                      "..=.^^^^^^^^^^^^^^^......"),
                    T("....^^^...^^^...^^^........"),
                    T(".............."),
                }
            },
        };

        public const float InicioDefecto = -6f;  // X inicial del jugador
        public const float DistanciaMeta = 3f;   // bloques libres entre el final del mapa y la meta

        public readonly int Indice;
        public Definicion Datos => Niveles[Indice];
        public float InicioX => InicioDefecto;
        public float MetaX { get; private set; }
        public int Columnas { get; private set; }
        public SpriteRenderer Suelo { get; private set; }

        /// <summary>Bloques y pinchos: se tiñen con el color del nivel cada frame.</summary>
        public readonly List<SpriteRenderer> Tintables = new List<SpriteRenderer>();

        public NivelMemeDash(int indice)
        {
            Indice = Mathf.Clamp(indice, 0, Niveles.Length - 1);
        }

        /// <summary>Une los trozos de un nivel en un mapa de 5 filas del mismo largo.</summary>
        public static string[] Mapa(int indice) => Mapa(indice, out _);

        /// <summary>Igual que Mapa(indice), y ademas devuelve la columna donde empieza cada trozo.</summary>
        public static string[] Mapa(int indice, out List<int> iniciosTrozo)
        {
            var filas = new StringBuilder[Filas];
            for (int r = 0; r < Filas; r++) filas[r] = new StringBuilder();
            iniciosTrozo = new List<int>();

            foreach (var trozo in Niveles[indice].Trozos)
            {
                iniciosTrozo.Add(filas[0].Length);
                if (trozo.Length != Filas)
                {
                    Debug.LogError($"Meme Dash: un trozo del nivel tiene {trozo.Length} filas (deben ser {Filas}).");
                    continue;
                }
                int ancho = 0;
                foreach (var f in trozo) ancho = Mathf.Max(ancho, f.Length);
                for (int r = 0; r < Filas; r++) filas[r].Append(trozo[r].PadRight(ancho, '.'));
            }

            var mapa = new string[Filas];
            for (int r = 0; r < Filas; r++) mapa[r] = filas[r].ToString();
            return mapa;
        }

        public void Construir(Transform raiz, ArteNivel arte)
        {
            var mapa = Mapa(Indice);
            Columnas = mapa[0].Length;
            MetaX = Columnas + DistanciaMeta;

            var bloques = Hijo(raiz, "Bloques");
            var colisiones = Hijo(raiz, "Colisiones");
            var peligros = Hijo(raiz, "Pinchos y pads");

            for (int r = 0; r < Filas; r++)
            {
                int y = Filas - 1 - r;
                string fila = mapa[r];
                int inicioTramo = -1;

                for (int x = 0; x <= Columnas; x++)
                {
                    char c = x < Columnas ? fila[x] : '.';

                    if (c == '#')
                    {
                        if (inicioTramo < 0) inicioTramo = x;
                        var sr = CrearSprite(bloques, "Bloque", arte.Bloque, new Vector2(x + 0.5f, y + 0.5f), 0);
                        Tintables.Add(sr);
                    }
                    else if (inicioTramo >= 0)
                    {
                        // Un solo collider por tramo horizontal: evita que el cubo se atore
                        // en las uniones entre bloques al correr encima.
                        CrearColisionTramo(colisiones, inicioTramo, x - 1, y);
                        inicioTramo = -1;
                    }

                    if (c == '^') CrearPincho(peligros, arte.Pincho, x, y);
                    else if (c == '=') CrearPad(peligros, arte.Pad, x, y);
                }
            }

            CrearSuelo(raiz, arte.Suelo);
            CrearMeta(raiz, arte.Meta);
        }

        static Transform Hijo(Transform padre, string nombre)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            return t;
        }

        static SpriteRenderer CrearSprite(Transform padre, string nombre, Sprite sprite, Vector2 pos, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = orden;
            return sr;
        }

        static void CrearColisionTramo(Transform padre, int x0, int x1, int y)
        {
            var go = new GameObject($"Tramo {x0}-{x1} y{y}");
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector2((x0 + x1 + 1) * 0.5f, y + 0.5f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0 + 1, 1f);
        }

        void CrearPincho(Transform padre, Sprite sprite, int x, int y)
        {
            var sr = CrearSprite(padre, "Pincho", sprite, new Vector2(x + 0.5f, y + 0.5f), 1);
            Tintables.Add(sr);

            // Hitbox mas pequeña que el dibujo, como en Geometry Dash (mas justo para el jugador).
            var col = sr.gameObject.AddComponent<PolygonCollider2D>();
            col.isTrigger = true;
            col.pathCount = 1;
            col.SetPath(0, new[] { new Vector2(-0.2f, -0.45f), new Vector2(0.2f, -0.45f), new Vector2(0f, 0.15f) });

            sr.gameObject.AddComponent<ObjetoNivel>().Tipo = TipoObjeto.Pincho;
        }

        static void CrearPad(Transform padre, Sprite sprite, int x, int y)
        {
            var sr = CrearSprite(padre, "Pad", sprite, new Vector2(x + 0.5f, y), 2);
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 0.4f);
            col.offset = new Vector2(0f, 0.2f);
            sr.gameObject.AddComponent<ObjetoNivel>().Tipo = TipoObjeto.Pad;
        }

        void CrearSuelo(Transform raiz, Sprite sprite)
        {
            float x0 = InicioX - 30f, x1 = MetaX + 40f, largo = x1 - x0;
            var sr = CrearSprite(raiz, "Suelo", sprite, new Vector2((x0 + x1) * 0.5f, 0f), 0);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(largo, 8f);
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(largo, 8f);
            col.offset = new Vector2(0f, -4f);
            Suelo = sr;
        }

        void CrearMeta(Transform raiz, Sprite sprite)
        {
            var sr = CrearSprite(raiz, "Meta", sprite, new Vector2(MetaX, 0f), 2);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(1f, 12f);
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 30f);
            col.offset = new Vector2(0f, 15f);
            sr.gameObject.AddComponent<ObjetoNivel>().Tipo = TipoObjeto.Meta;
        }
    }
}
