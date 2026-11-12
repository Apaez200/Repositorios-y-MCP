using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MemeDash
{
    public enum EstadoJuego { Menu, Jugando, Muerto, Completado }

    /// <summary>
    /// Punto de entrada de Meme Dash. Al iniciar construye todo por codigo
    /// (nivel, jugador, camara, audio) y dibuja la interfaz con OnGUI.
    /// La escena solo necesita una camara, una Global Light 2D y este componente.
    /// </summary>
    public class MemeDashGame : MonoBehaviour
    {
        [Header("Salto (la velocidad depende de cada nivel)")]
        [SerializeField] float alturaSalto = 2.2f;
        [SerializeField] float tiempoHastaCima = 0.235f;
        [SerializeField] float multiplicadorPad = 1.45f;

        [Header("Partida")]
        [SerializeField] float esperaReinicio = 1f;

        const string ClaveRecord = "MemeDash_Record_";   // + indice del nivel
        const string ClaveSkin = "MemeDash_Skin";
        const string ClaveNivel = "MemeDash_Nivel";

        public static MemeDashGame Instancia { get; private set; }
        public EstadoJuego Estado { get; private set; } = EstadoJuego.Menu;
        public Color ColorNivel { get; private set; } = Color.cyan;
        public int Intentos => intentos;
        public float Progreso => progreso;
        public bool ModoDemo => modoDemo;
        public int NivelActual => nivelActual;
        public VerificadorNivel.Resultado Verificacion => verificaciones?[nivelActual];

        /// <summary>Fisica de un nivel segun los valores del inspector (sirve tambien fuera de Play).</summary>
        public FisicaSalto FisicaDe(int indiceNivel) =>
            FisicaSalto.Desde(NivelMemeDash.Niveles[indiceNivel].Velocidad, alturaSalto, tiempoHastaCima, multiplicadorPad);

        ArteNivel arte;
        NivelMemeDash nivel;
        Transform raizNivel;
        MemeDashPlayer jugador;
        MemeDashCamera camara;
        VerificadorNivel.Resultado[] verificaciones;
        float[] records;
        List<Skin> skins;
        int skinActual, nivelActual, intentos;
        float progreso, tono, marcaTiempo, destello, avisoRecord;
        bool musicaActiva = true, modoDemo;

        AudioSource fuenteMusica, fuenteSfx;
        AudioClip musica, sonidoSalto, sonidoPad, sonidoMuerte, sonidoVictoria, sonidoBlip;

        GUIStyle estilo;

        float Record => records[nivelActual];

        void Awake()
        {
            Instancia = this;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var goCam = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = goCam.AddComponent<Camera>();
                goCam.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;

            arte = ArteProcedural.CrearArteNivel();
            int total = NivelMemeDash.Niveles.Length;

            // Comprueba que todos los niveles se pueden pasar; las soluciones alimentan el modo demo.
            verificaciones = new VerificadorNivel.Resultado[total];
            records = new float[total];
            for (int i = 0; i < total; i++)
            {
                verificaciones[i] = VerificadorNivel.Resolver(FisicaDe(i), Time.fixedDeltaTime, i);
                if (verificaciones[i].Superable) Debug.Log("Meme Dash: " + verificaciones[i].Informe);
                else Debug.LogWarning("Meme Dash: " + verificaciones[i].Informe);
                records[i] = PlayerPrefs.GetFloat(ClaveRecord + i, 0f);
            }

            // Personajes: los dibujados por codigo + imagenes propias en Resources/Skins
            skins = ArteProcedural.CrearSkins();
            skins.AddRange(ArteProcedural.CargarSkinsPersonalizados());
            skinActual = Mathf.Clamp(PlayerPrefs.GetInt(ClaveSkin, 0), 0, skins.Count - 1);
            nivelActual = Mathf.Clamp(PlayerPrefs.GetInt(ClaveNivel, 0), 0, total - 1);

            // Salto definido por altura y tiempo hasta la cima (ver FisicaSalto).
            var fisica = FisicaDe(nivelActual);
            float escalaGravedad = fisica.Gravedad / Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y));

            var goJugador = new GameObject("Jugador") { tag = "Player" };
            jugador = goJugador.AddComponent<MemeDashPlayer>();
            jugador.Configurar(skins[skinActual].Sprite, arte.Cuadrito, fisica.Velocidad, fisica.VelSalto,
                               escalaGravedad, fisica.VelPad, new Vector2(NivelMemeDash.InicioDefecto, 0.46f));

            camara = cam.GetComponent<MemeDashCamera>();
            if (camara == null) camara = cam.gameObject.AddComponent<MemeDashCamera>();
            camara.Configurar(jugador.transform, arte.Fondo, arte.Deco);

            // Audio
            musica = AudioProcedural.Musica();
            sonidoSalto = AudioProcedural.Salto();
            sonidoPad = AudioProcedural.Pad();
            sonidoMuerte = AudioProcedural.Muerte();
            sonidoVictoria = AudioProcedural.Victoria();
            sonidoBlip = AudioProcedural.Blip();

            fuenteMusica = gameObject.AddComponent<AudioSource>();
            fuenteMusica.clip = musica;
            fuenteMusica.loop = true;
            fuenteMusica.playOnAwake = false;
            fuenteMusica.volume = 0.75f;

            fuenteSfx = gameObject.AddComponent<AudioSource>();
            fuenteSfx.playOnAwake = false;

            CargarNivel(nivelActual);
            fuenteMusica.Play();
            Estado = EstadoJuego.Menu;
        }

        /// <summary>Destruye el nivel actual y construye el indicado.</summary>
        void CargarNivel(int indice)
        {
            nivelActual = Mathf.Clamp(indice, 0, NivelMemeDash.Niveles.Length - 1);
            PlayerPrefs.SetInt(ClaveNivel, nivelActual);

            if (raizNivel != null)
            {
                // Se desactiva ya para que sus colliders no afecten al siguiente paso de fisica.
                raizNivel.gameObject.SetActive(false);
                Destroy(raizNivel.gameObject);
            }

            nivel = new NivelMemeDash(nivelActual);
            raizNivel = new GameObject("Nivel " + (nivelActual + 1)).transform;
            nivel.Construir(raizNivel, arte);

            var datos = nivel.Datos;
            jugador.CambiarVelocidad(datos.Velocidad);
            tono = datos.TonoInicial;
            fuenteMusica.pitch = datos.TonoMusica;
            jugador.Reiniciar();
            camara.Colocar();
            AplicarColores();
        }

        void Update()
        {
            if (jugador == null) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // El color del nivel va rotando por el circulo cromatico, como en Geometry Dash.
            tono = Mathf.Repeat(tono + Time.deltaTime * 0.025f, 1f);
            AplicarColores();

            if (kb != null && kb.mKey.wasPressedThisFrame)
            {
                musicaActiva = !musicaActiva;
                fuenteMusica.mute = !musicaActiva;
            }

            bool aceptar = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool escape = kb != null && kb.escapeKey.wasPressedThisFrame;

            switch (Estado)
            {
                case EstadoJuego.Menu:
                    if (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)) CambiarSkin(-1);
                    if (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) CambiarSkin(+1);
                    if (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)) CambiarNivel(-1);
                    if (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)) CambiarNivel(+1);
                    if (kb != null && kb.pKey.wasPressedThisFrame && Verificacion.Superable) Empezar(true);
                    else if (aceptar) Empezar();
                    break;

                case EstadoJuego.Jugando:
                    progreso = Mathf.Clamp01((jugador.transform.position.x - nivel.InicioX) / (nivel.MetaX - nivel.InicioX));
                    if (escape) IrAlMenu();
                    else if (kb != null && kb.rKey.wasPressedThisFrame)
                    {
                        RegistrarRecord();
                        NuevoIntento();
                    }
                    break;

                case EstadoJuego.Muerto:
                    if (escape) IrAlMenu();
                    else if (Time.time - marcaTiempo >= esperaReinicio) NuevoIntento();
                    break;

                case EstadoJuego.Completado:
                    if (escape) IrAlMenu();
                    else if (Time.time - marcaTiempo > 0.6f)
                    {
                        if (kb != null && kb.rKey.wasPressedThisFrame) Empezar(modoDemo);
                        else if (aceptar) SiguienteNivel();
                    }
                    break;
            }

            destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 3f);
            avisoRecord -= Time.deltaTime;
        }

        // ------------------------------------------------------------ flujo de partida

        /// <summary>
        /// Empieza el nivel actual desde el intento 1.
        /// demo = true: el cubo juega solo con la solucion del verificador.
        /// </summary>
        public void Empezar(bool demo = false)
        {
            var v = Verificacion;
            modoDemo = demo && v != null && v.Superable;
            jugador.PilotoAutomatico = modoDemo ? v.Saltos : null;
            intentos = 0;
            NuevoIntento();
        }

        /// <summary>Pasa al siguiente nivel (o repite el ultimo). La demo sigue en modo demo.</summary>
        public void SiguienteNivel()
        {
            bool demo = modoDemo;
            int siguiente = nivelActual + 1;
            if (siguiente < NivelMemeDash.Niveles.Length) CargarNivel(siguiente);
            Empezar(demo);
        }

        void NuevoIntento()
        {
            intentos++;
            progreso = 0f;
            Estado = EstadoJuego.Jugando;
            jugador.Reiniciar();
            camara.Colocar();
            // Como en Geometry Dash, la cancion vuelve a empezar en cada intento.
            fuenteMusica.Stop();
            fuenteMusica.time = 0f;
            fuenteMusica.Play();
        }

        void IrAlMenu()
        {
            RegistrarRecord();
            modoDemo = false;
            jugador.PilotoAutomatico = null;
            Estado = EstadoJuego.Menu;
            jugador.Reiniciar();
            camara.Colocar();
            if (!fuenteMusica.isPlaying) fuenteMusica.Play();
        }

        public void JugadorMurio()
        {
            Estado = EstadoJuego.Muerto;
            marcaTiempo = Time.time;
            RegistrarRecord();
            fuenteMusica.Stop();
            fuenteSfx.PlayOneShot(sonidoMuerte);
            camara.Sacudir(0.3f, 0.35f);
            destello = 0.6f;
        }

        public void NivelCompletado()
        {
            Estado = EstadoJuego.Completado;
            marcaTiempo = Time.time;
            progreso = 1f;
            RegistrarRecord();
            fuenteMusica.Stop();
            fuenteSfx.PlayOneShot(sonidoVictoria);
            camara.Sacudir(0.2f, 0.15f);
        }

        public void SonidoSalto() => fuenteSfx.PlayOneShot(sonidoSalto, 0.6f);
        public void SonidoPad() => fuenteSfx.PlayOneShot(sonidoPad);

        void RegistrarRecord()
        {
            if (modoDemo) return; // la demo no cuenta para el record
            if (progreso <= Record + 0.0001f) return;
            records[nivelActual] = progreso;
            PlayerPrefs.SetFloat(ClaveRecord + nivelActual, progreso);
            PlayerPrefs.Save();
            if (progreso < 1f) avisoRecord = 1.5f;
        }

        void CambiarSkin(int paso)
        {
            skinActual = (skinActual + paso + skins.Count) % skins.Count;
            jugador.CambiarSkin(skins[skinActual].Sprite);
            PlayerPrefs.SetInt(ClaveSkin, skinActual);
            fuenteSfx.PlayOneShot(sonidoBlip);
        }

        void CambiarNivel(int paso)
        {
            int total = NivelMemeDash.Niveles.Length;
            CargarNivel((nivelActual + paso + total) % total);
            fuenteSfx.PlayOneShot(sonidoBlip);
        }

        void AplicarColores()
        {
            ColorNivel = Color.HSVToRGB(tono, 0.6f, 1f);
            foreach (var sr in nivel.Tintables) sr.color = ColorNivel;
            nivel.Suelo.color = Color.HSVToRGB(tono, 0.5f, 0.75f);
            camara.Tintar(Color.HSVToRGB(tono, 0.7f, 0.55f));
        }

        // ------------------------------------------------------------ interfaz

        void OnGUI()
        {
            if (skins == null || nivel == null) return;
            float u = Screen.height / 720f;

            if (destello > 0f) Caja(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, destello * 0.5f));

            switch (Estado)
            {
                case EstadoJuego.Menu:
                    DibujarMenu(u);
                    break;
                case EstadoJuego.Jugando:
                case EstadoJuego.Muerto:
                    DibujarHud(u);
                    break;
                case EstadoJuego.Completado:
                    DibujarHud(u);
                    DibujarCompletado(u);
                    break;
            }
        }

        void DibujarMenu(float u)
        {
            float w = Screen.width, h = Screen.height;
            var datos = nivel.Datos;
            Caja(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.45f));

            Texto(new Rect(0, h * 0.04f, w, 100 * u), "MEME DASH", 88 * u, ColorNivel);
            Texto(new Rect(0, h * 0.04f + 88 * u, w, 36 * u), "Geometry Dash con personajes de internet", 24 * u, Color.white);

            // Personaje
            float t = 160 * u;
            var vista = new Rect((w - t) / 2f, h * 0.27f + Mathf.Sin(Time.time * 4f) * 6f * u, t, t);
            Caja(new Rect(vista.x - 8 * u, vista.y - 8 * u, vista.width + 16 * u, vista.height + 16 * u), new Color(1f, 1f, 1f, 0.15f));
            GUI.DrawTexture(vista, skins[skinActual].Textura, ScaleMode.ScaleToFit);
            Texto(new Rect(vista.x - 160 * u, vista.y, 120 * u, t), "<", 80 * u, Color.white);
            Texto(new Rect(vista.xMax + 40 * u, vista.y, 120 * u, t), ">", 80 * u, Color.white);
            Texto(new Rect(0, vista.yMax + 14 * u, w, 44 * u), skins[skinActual].Nombre, 36 * u, Color.white);
            Texto(new Rect(0, vista.yMax + 52 * u, w, 28 * u),
                $"Personaje {skinActual + 1} de {skins.Count}   ·   A / D para cambiar", 20 * u, new Color(1f, 1f, 1f, 0.8f));

            // Selector de nivel
            float yNivel = h * 0.66f;
            var panel = new Rect(w * 0.25f, yNivel, w * 0.5f, 74 * u);
            Caja(panel, new Color(0f, 0f, 0f, 0.45f));
            Caja(new Rect(panel.x, panel.y, 6 * u, panel.height), datos.ColorDificultad);
            string completado = Record >= 1f ? "   ✔ COMPLETADO" : $"   Récord {Porcentaje(Record)}%";
            Texto(new Rect(panel.x, panel.y + 4 * u, panel.width, 38 * u),
                $"NIVEL {nivelActual + 1}: {datos.Nombre}", 30 * u, Color.white);
            Texto(new Rect(panel.x, panel.y + 40 * u, panel.width, 30 * u),
                $"{datos.Dificultad}{completado}", 22 * u, datos.ColorDificultad);
            Texto(new Rect(panel.x - 70 * u, panel.y, 60 * u, panel.height), "▲", 26 * u, new Color(1f, 1f, 1f, 0.7f));
            Texto(new Rect(panel.xMax + 10 * u, panel.y, 60 * u, panel.height), "▼", 26 * u, new Color(1f, 1f, 1f, 0.7f));
            Texto(new Rect(0, panel.yMax + 4 * u, w, 26 * u),
                $"W / S para elegir nivel  ({NivelMemeDash.Niveles.Length} niveles, de fácil a difícil)", 18 * u, new Color(1f, 1f, 1f, 0.75f));

            float parpadeo = 0.55f + 0.45f * Mathf.Sin(Time.time * 5f);
            Texto(new Rect(0, h * 0.86f, w, 44 * u), "ESPACIO o CLIC para jugar   ·   P para ver la demo", 30 * u, new Color(1f, 1f, 0.6f, parpadeo));
            Texto(new Rect(0, h * 0.93f, w, 28 * u),
                "Saltar: ESPACIO / W / CLIC (mantén para saltar seguido)   ·   R reiniciar   ·   ESC menú   ·   M música",
                17 * u, new Color(1f, 1f, 1f, 0.75f));
        }

        void DibujarHud(float u)
        {
            float w = Screen.width;
            float anchoBarra = w * 0.4f, altoBarra = 16 * u;
            var barra = new Rect((w - anchoBarra) / 2f, 18 * u, anchoBarra, altoBarra);

            Caja(new Rect(barra.x - 3 * u, barra.y - 3 * u, barra.width + 6 * u, barra.height + 6 * u), new Color(0f, 0f, 0f, 0.55f));
            Caja(new Rect(barra.x, barra.y, barra.width * progreso, barra.height), ColorNivel);
            if (Record > 0f) // marca del record en la barra
                Caja(new Rect(barra.x + barra.width * Record - 1.5f * u, barra.y - 4 * u, 3 * u, altoBarra + 8 * u), new Color(1f, 1f, 1f, 0.8f));
            Texto(new Rect(barra.xMax + 12 * u, barra.y - 12 * u, 120 * u, altoBarra + 24 * u), $"{Porcentaje(progreso)}%", 26 * u, Color.white, TextAnchor.MiddleLeft);

            Texto(new Rect(20 * u, 10 * u, 400 * u, 44 * u), $"INTENTO {intentos}", 32 * u, Color.white, TextAnchor.MiddleLeft);
            Texto(new Rect(20 * u, 48 * u, 500 * u, 30 * u), $"Nivel {nivelActual + 1} · {nivel.Datos.Nombre}", 20 * u, nivel.Datos.ColorDificultad, TextAnchor.MiddleLeft);
            Texto(new Rect(w - 320 * u, 10 * u, 300 * u, 40 * u), $"Récord: {Porcentaje(Record)}%", 24 * u, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleRight);
            Texto(new Rect(20 * u, Screen.height - 44 * u, 500 * u, 34 * u), skins[skinActual].Nombre, 22 * u, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);

            if (modoDemo)
                Texto(new Rect(0, 46 * u, w, 40 * u), "MODO DEMO · autopiloto  (ESC para salir)", 24 * u, new Color(1f, 1f, 0.6f, 0.9f));

            if (avisoRecord > 0f)
                Texto(new Rect(0, 80 * u, w, 60 * u), "¡NUEVO RÉCORD!", 46 * u, new Color(1f, 0.9f, 0.3f, Mathf.Clamp01(avisoRecord)));
        }

        void DibujarCompletado(float u)
        {
            float w = Screen.width, h = Screen.height;
            bool hayOtro = nivelActual + 1 < NivelMemeDash.Niveles.Length;
            Caja(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.5f));
            Texto(new Rect(0, h * 0.26f, w, 100 * u), "¡NIVEL COMPLETADO!", 72 * u, ColorNivel);
            Texto(new Rect(0, h * 0.26f + 95 * u, w, 44 * u), $"{nivel.Datos.Nombre}  ·  Intentos: {intentos}", 34 * u, Color.white);
            if (!hayOtro)
                Texto(new Rect(0, h * 0.26f + 145 * u, w, 44 * u), "¡Terminaste todos los niveles!", 32 * u, new Color(1f, 0.9f, 0.3f));
            string siguiente = hayOtro ? $"ESPACIO: nivel {nivelActual + 2} ({NivelMemeDash.Niveles[nivelActual + 1].Nombre})" : "ESPACIO: jugar otra vez";
            Texto(new Rect(0, h * 0.26f + 200 * u, w, 40 * u), $"{siguiente}    ·    R: repetir    ·    ESC: menú", 24 * u, new Color(1f, 1f, 1f, 0.85f));
        }

        static int Porcentaje(float v) => Mathf.FloorToInt(v * 100f + 0.0001f);

        void Texto(Rect r, string texto, float tamaño, Color color, TextAnchor alineacion = TextAnchor.MiddleCenter)
        {
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
                estilo.normal.textColor = Color.white;
            }
            estilo.fontSize = Mathf.Max(8, Mathf.RoundToInt(tamaño));
            estilo.alignment = alineacion;

            var sombra = r;
            sombra.x += tamaño * 0.06f;
            sombra.y += tamaño * 0.06f;
            GUI.color = new Color(0f, 0f, 0f, 0.65f * color.a);
            GUI.Label(sombra, texto, estilo);
            GUI.color = color;
            GUI.Label(r, texto, estilo);
            GUI.color = Color.white;
        }

        static void Caja(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
