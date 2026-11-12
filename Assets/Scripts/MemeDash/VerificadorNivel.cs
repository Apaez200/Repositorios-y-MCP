using System.Collections.Generic;
using UnityEngine;

namespace MemeDash
{
    /// <summary>Parametros del salto, derivados de la altura y el tiempo hasta la cima.</summary>
    public struct FisicaSalto
    {
        public float Velocidad, Gravedad, VelSalto, VelPad;

        public static FisicaSalto Desde(float velocidad, float alturaSalto, float tiempoHastaCima, float multiplicadorPad)
        {
            // h = g*t^2/2  ->  g = 2h/t^2   y   v0 = g*t
            float g = 2f * alturaSalto / (tiempoHastaCima * tiempoHastaCima);
            float v0 = g * tiempoHastaCima;
            return new FisicaSalto { Velocidad = velocidad, Gravedad = g, VelSalto = v0, VelPad = v0 * multiplicadorPad };
        }
    }

    /// <summary>
    /// Comprueba que el nivel se puede completar.
    /// Simula el cubo paso a paso con la misma fisica que el juego (misma gravedad,
    /// velocidad, hitboxes y paso fijo) y busca una secuencia de saltos que llegue a la meta.
    /// Las posiciones X de esos saltos sirven como autopiloto para el modo demo.
    /// </summary>
    public static class VerificadorNivel
    {
        public sealed class Resultado
        {
            public bool Superable;
            public readonly List<float> Saltos = new List<float>(); // X del jugador al saltar
            public float XMaxima;
            public int TrozoBloqueado = -1;   // 0-based; -1 si es superable
            public int EstadosExplorados;
            public string Informe;
        }

        public static Resultado Resolver(FisicaSalto fisica, float dt, int indiceNivel)
        {
            var sim = new Simulador(fisica, dt, indiceNivel);
            return sim.Resolver();
        }

        struct Caja
        {
            public float X0, Y0, X1, Y1;
            public Caja(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public bool Toca(Caja o) => X1 > o.X0 + 1e-4f && X0 < o.X1 - 1e-4f && Y1 > o.Y0 + 1e-4f && Y0 < o.Y1 - 1e-4f;
        }

        struct Estado
        {
            public float X, Y, Vy, DesdeSalto;
            public bool EnSuelo, EnPad;
            public int Paso;
        }

        enum Fin { Sigue, Muere, Gana }

        sealed class Simulador
        {
            const float Medio = 0.45f;          // mitad del collider del jugador (0.9 x 0.9)
            const float MargenPincho = 0.03f;   // exige librar los pinchos con un poco de holgura
            const float Enfriamiento = 0.1f;    // igual que en MemeDashPlayer
            const int LimiteEstados = 2000000;

            readonly FisicaSalto f;
            readonly float dt, metaX;
            readonly string nombreNivel;
            readonly List<int> inicios;
            readonly List<Caja> solidos = new List<Caja>();
            readonly List<Caja> pads = new List<Caja>();
            readonly List<Vector2[]> pinchos = new List<Vector2[]>();
            readonly HashSet<(int, int, int, bool, bool, int)> visitados = new HashSet<(int, int, int, bool, bool, int)>();
            readonly List<float> saltosInvertidos = new List<float>();
            int explorados;
            float xMaxima;

            public Simulador(FisicaSalto fisica, float dt, int indiceNivel)
            {
                f = fisica;
                this.dt = dt;
                nombreNivel = NivelMemeDash.Niveles[indiceNivel].Nombre;
                var mapa = NivelMemeDash.Mapa(indiceNivel, out inicios);
                int columnas = mapa[0].Length;
                metaX = columnas + NivelMemeDash.DistanciaMeta;

                // Mismos colliders que construye NivelMemeDash.
                solidos.Add(new Caja(-1000f, -8f, 1000f, 0f)); // suelo
                for (int r = 0; r < NivelMemeDash.Filas; r++)
                {
                    int y = NivelMemeDash.Filas - 1 - r;
                    int inicioTramo = -1;
                    for (int x = 0; x <= columnas; x++)
                    {
                        char c = x < columnas ? mapa[r][x] : '.';
                        if (c == '#') { if (inicioTramo < 0) inicioTramo = x; }
                        else if (inicioTramo >= 0)
                        {
                            solidos.Add(new Caja(inicioTramo, y, x, y + 1));
                            inicioTramo = -1;
                        }
                        if (c == '^')
                            pinchos.Add(new[] { new Vector2(x + 0.3f, y + 0.05f), new Vector2(x + 0.7f, y + 0.05f), new Vector2(x + 0.5f, y + 0.65f) });
                        else if (c == '=')
                            pads.Add(new Caja(x + 0.1f, y, x + 0.9f, y + 0.4f));
                    }
                }
            }

            public Resultado Resolver()
            {
                var inicio = new Estado { X = NivelMemeDash.InicioDefecto, Y = 0.46f, DesdeSalto = 10f };
                bool ok = Buscar(inicio);

                var r = new Resultado { Superable = ok, XMaxima = xMaxima, EstadosExplorados = explorados };
                if (ok)
                {
                    for (int i = saltosInvertidos.Count - 1; i >= 0; i--) r.Saltos.Add(saltosInvertidos[i]);
                    r.Informe = $"'{nombreNivel}' SI se puede pasar ({r.Saltos.Count} saltos, {inicios.Count} trozos, {explorados} estados simulados).";
                }
                else
                {
                    int col = Mathf.FloorToInt(xMaxima);
                    for (int i = 0; i < inicios.Count; i++) if (col >= inicios[i]) r.TrozoBloqueado = i;
                    r.Informe = $"'{nombreNivel}' NO se puede pasar: se atora en el trozo {r.TrozoBloqueado + 1} (x = {xMaxima:F1}).";
                }
                return r;
            }

            // Busqueda en profundidad. En cada paso con el cubo en el suelo hay dos opciones:
            // no saltar o saltar. Los estados repetidos se descartan.
            bool Buscar(Estado s)
            {
                if (++explorados > LimiteEstados) return false;
                if (s.X > xMaxima) xMaxima = s.X;
                var clave = (s.Paso, Mathf.RoundToInt(s.Y * 1000f), Mathf.RoundToInt(s.Vy * 100f), s.EnSuelo, s.EnPad,
                             Mathf.Min(10, Mathf.RoundToInt(s.DesdeSalto / dt)));
                if (!visitados.Add(clave)) return false;

                bool puedeSaltar = s.EnSuelo && s.DesdeSalto > Enfriamiento;
                for (int opcion = 0; opcion < (puedeSaltar ? 2 : 1); opcion++)
                {
                    bool saltar = opcion == 1;
                    var n = s;
                    var fin = Avanzar(ref n, saltar);
                    if (fin == Fin.Muere) continue;
                    if (fin == Fin.Gana || Buscar(n))
                    {
                        if (saltar) saltosInvertidos.Add(s.X);
                        return true;
                    }
                }
                return false;
            }

            Caja CajaJugador(in Estado s, float extra = 0f) =>
                new Caja(s.X - Medio - extra, s.Y - Medio - extra, s.X + Medio + extra, s.Y + Medio + extra);

            // Un paso de fisica, en el mismo orden que el juego:
            // FixedUpdate fija la velocidad y luego el motor integra (v += g*dt; p += v*dt).
            Fin Avanzar(ref Estado s, bool saltar)
            {
                if (saltar)
                {
                    s.Vy = f.VelSalto;
                    s.DesdeSalto = 0f;
                }
                s.Vy -= f.Gravedad * dt;
                s.Y += s.Vy * dt;
                s.X += f.Velocidad * dt;
                s.DesdeSalto += dt;
                s.Paso++;
                s.EnSuelo = false;

                // Bloques: aterrizar arriba esta bien; de frente o de cabeza es muerte.
                for (int pasada = 0; pasada < 2; pasada++)
                {
                    foreach (var k in solidos)
                    {
                        var j = CajaJugador(s);
                        if (!j.Toca(k)) continue;
                        float penX = j.X1 - k.X0;
                        float penY = s.Vy <= 0f ? k.Y1 - j.Y0 : j.Y1 - k.Y0;
                        if (penY <= penX && s.Vy <= 0f)
                        {
                            s.Y = k.Y1 + Medio;
                            s.Vy = 0f;
                            s.EnSuelo = true;
                        }
                        else return Fin.Muere;
                    }
                }

                var jugador = CajaJugador(s);
                var holgado = CajaJugador(s, MargenPincho);
                foreach (var p in pinchos)
                    if (TrianguloToca(p[0], p[1], p[2], holgado)) return Fin.Muere;

                bool tocaPad = false;
                foreach (var k in pads) if (jugador.Toca(k)) tocaPad = true;
                if (tocaPad && !s.EnPad)
                {
                    s.Vy = f.VelPad;
                    s.DesdeSalto = 0f;
                    s.EnSuelo = false;
                }
                s.EnPad = tocaPad;

                if (jugador.X1 >= metaX - 0.5f) return Fin.Gana;
                if (s.Y < -6f) return Fin.Muere;
                return Fin.Sigue;
            }

            // Teorema del eje separador entre un triangulo y una caja alineada a los ejes.
            static bool TrianguloToca(Vector2 a, Vector2 b, Vector2 c, Caja k)
            {
                if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) <= k.X0 || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) >= k.X1) return false;
                if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) <= k.Y0 || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) >= k.Y1) return false;
                return !Separa(a, b, c, k) && !Separa(b, c, a, k) && !Separa(c, a, b, k);
            }

            static bool Separa(Vector2 p, Vector2 q, Vector2 r, Caja k)
            {
                var n = new Vector2(-(q.y - p.y), q.x - p.x);
                float t1 = Vector2.Dot(n, p), t2 = Vector2.Dot(n, r);
                float tMin = Mathf.Min(t1, t2), tMax = Mathf.Max(t1, t2);
                float b0 = Vector2.Dot(n, new Vector2(k.X0, k.Y0)), b1 = Vector2.Dot(n, new Vector2(k.X1, k.Y0));
                float b2 = Vector2.Dot(n, new Vector2(k.X1, k.Y1)), b3 = Vector2.Dot(n, new Vector2(k.X0, k.Y1));
                float bMin = Mathf.Min(Mathf.Min(b0, b1), Mathf.Min(b2, b3));
                float bMax = Mathf.Max(Mathf.Max(b0, b1), Mathf.Max(b2, b3));
                return bMax <= tMin || bMin >= tMax;
            }
        }
    }
}
