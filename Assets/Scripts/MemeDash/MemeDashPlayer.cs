using UnityEngine;
using UnityEngine.InputSystem;

namespace MemeDash
{
    /// <summary>
    /// Cubo del jugador estilo Geometry Dash.
    /// - Avanza solo a velocidad constante.
    /// - ESPACIO / W / flecha arriba / clic: saltar (mantener = salta en cuanto toca suelo).
    /// - Gira en el aire y se endereza al aterrizar.
    /// - Muere al tocar un pincho o al chocar de frente o de cabeza contra un bloque.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class MemeDashPlayer : MonoBehaviour
    {
        const float VelocidadGiro = 400f;     // grados por segundo en el aire
        const float TiempoCoyote = 0.07f;     // margen para saltar justo al salir de un borde
        const float BufferSalto = 0.12f;      // un toque poco antes de aterrizar tambien cuenta
        const float EnfriamientoSalto = 0.1f; // evita saltos dobles por contactos residuales
        const float AlturaMuerte = -6f;

        Rigidbody2D rb;
        Transform visual;
        SpriteRenderer srVisual;
        Sprite cuadrito;

        float velocidad, velSalto, velPad;
        Vector2 inicio;

        // Los callbacks de colision marcan esto despues de cada paso de fisica;
        // FixedUpdate lo consume en el paso siguiente.
        bool contactoSuelo, choque;
        bool mantener;
        float ultimoSuelo = -10f, ultimoPulso = -10f, ultimoSalto = -10f;
        float temporizadorEstela;

        public bool Vivo { get; private set; } = true;
        bool EnSuelo => Time.time - ultimoSuelo < 0.06f;

        /// <summary>
        /// Posiciones X donde saltar solo (modo demo y pruebas). null = control con teclado.
        /// </summary>
        public System.Collections.Generic.List<float> PilotoAutomatico { get; set; }
        int indicePiloto;

        public void Configurar(Sprite skin, Sprite cuadrito, float velocidad, float velSalto,
                               float escalaGravedad, float velPad, Vector2 inicio)
        {
            this.cuadrito = cuadrito;
            this.velocidad = velocidad;
            this.velSalto = velSalto;
            this.velPad = velPad;
            this.inicio = inicio;

            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = escalaGravedad;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var col = GetComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.9f);
            col.sharedMaterial = new PhysicsMaterial2D("MemeDash_SinFriccion") { friction = 0f, bounciness = 0f };

            // El collider no gira; solo gira este hijo visual.
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            srVisual = visual.gameObject.AddComponent<SpriteRenderer>();
            srVisual.sortingOrder = 10;

            CambiarSkin(skin);
            Reiniciar();
        }

        /// <summary>Velocidad horizontal del nivel actual (bloques por segundo).</summary>
        public void CambiarVelocidad(float nueva) => velocidad = nueva;

        public void CambiarSkin(Sprite skin)
        {
            srVisual.sprite = skin;
            Vector2 tam = skin.bounds.size;
            float lado = Mathf.Max(tam.x, tam.y);
            visual.localScale = Vector3.one * (lado > 0f ? 0.95f / lado : 1f);
        }

        public void Reiniciar()
        {
            Vivo = true;
            rb.simulated = true;
            rb.position = inicio;
            transform.position = inicio;
            rb.linearVelocity = Vector2.zero;
            visual.localRotation = Quaternion.identity;
            srVisual.enabled = true;
            contactoSuelo = choque = mantener = false;
            ultimoSuelo = ultimoPulso = ultimoSalto = -10f;
            indicePiloto = 0;
        }

        static bool Jugando()
        {
            var juego = MemeDashGame.Instancia;
            return juego != null && juego.Estado == EstadoJuego.Jugando;
        }

        void Update()
        {
            if (!Vivo) return;
            bool jugando = Jugando();

            // Entrada en Update para no perder pulsaciones entre pasos de fisica.
            if (jugando && PilotoAutomatico == null)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                bool pulsado = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                               || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                mantener = (kb != null && (kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed))
                           || (mouse != null && mouse.leftButton.isPressed);
                if (pulsado) ultimoPulso = Time.time;
            }
            else
            {
                mantener = false;
            }

            // Giro: en el aire rota; en el suelo se endereza al multiplo de 90 mas cercano.
            if (EnSuelo)
            {
                float z = visual.localEulerAngles.z;
                float objetivo = Mathf.Round(z / 90f) * 90f;
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(z, objetivo, 900f * Time.deltaTime));
            }
            else
            {
                visual.Rotate(0f, 0f, -VelocidadGiro * Time.deltaTime);
            }

            // Estela de cuadritos al correr por el suelo.
            if (jugando && EnSuelo)
            {
                temporizadorEstela -= Time.deltaTime;
                if (temporizadorEstela <= 0f)
                {
                    temporizadorEstela = 0.04f;
                    ParticulaSimple.Crear(cuadrito, transform.position + new Vector3(-0.45f, -0.38f, 0f),
                        new Vector2(Random.Range(-2f, -0.5f), Random.Range(0.5f, 2f)),
                        MemeDashGame.Instancia.ColorNivel, 0.35f, Random.Range(0.12f, 0.2f), 6f, 9);
                }
            }
        }

        void FixedUpdate()
        {
            if (!Vivo) return;
            bool jugando = Jugando();

            if (contactoSuelo) ultimoSuelo = Time.time;
            if (jugando && choque)
            {
                Morir();
                return;
            }

            var v = rb.linearVelocity;
            v.x = jugando ? velocidad : 0f;

            if (jugando)
            {
                bool quiere;
                if (PilotoAutomatico != null)
                {
                    // Descarta saltos que ya quedaron atras sin poder hacerse.
                    while (indicePiloto < PilotoAutomatico.Count && rb.position.x > PilotoAutomatico[indicePiloto] + 0.3f)
                        indicePiloto++;
                    quiere = indicePiloto < PilotoAutomatico.Count && rb.position.x >= PilotoAutomatico[indicePiloto] - 0.05f;
                }
                else
                {
                    quiere = mantener || Time.time - ultimoPulso < BufferSalto;
                }

                bool puede = contactoSuelo || (Time.time - ultimoSuelo < TiempoCoyote && v.y <= 0.01f);
                if (quiere && puede && Time.time - ultimoSalto > EnfriamientoSalto)
                {
                    v.y = velSalto;
                    ultimoSalto = Time.time;
                    ultimoPulso = -10f;
                    ultimoSuelo = -10f;
                    if (PilotoAutomatico != null) indicePiloto++;
                    MemeDashGame.Instancia.SonidoSalto();
                }
            }

            rb.linearVelocity = v;

            if (jugando && rb.position.y < AlturaMuerte) Morir();

            contactoSuelo = false;
            choque = false;
        }

        void OnCollisionEnter2D(Collision2D c) => Analizar(c);
        void OnCollisionStay2D(Collision2D c) => Analizar(c);

        // La normal apunta desde el otro collider hacia el jugador:
        //   hacia arriba  -> estamos parados encima (suelo)
        //   hacia la izquierda (pared delante) o hacia abajo (techo) -> choque mortal
        void Analizar(Collision2D c)
        {
            for (int i = 0; i < c.contactCount; i++)
            {
                Vector2 n = c.GetContact(i).normal;
                if (n.y > 0.7f) contactoSuelo = true;
                else if (n.x < -0.7f || n.y < -0.7f) choque = true;
            }
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (!Vivo || !Jugando()) return;
            var obj = otro.GetComponent<ObjetoNivel>();
            if (obj == null) return;

            switch (obj.Tipo)
            {
                case TipoObjeto.Pincho:
                    Morir();
                    break;
                case TipoObjeto.Pad:
                    var v = rb.linearVelocity;
                    v.y = velPad;
                    rb.linearVelocity = v;
                    ultimoSalto = Time.time; // que un salto manual no pise el impulso del pad
                    ultimoSuelo = -10f;
                    MemeDashGame.Instancia.SonidoPad();
                    break;
                case TipoObjeto.Meta:
                    Ganar();
                    break;
            }
        }

        void Morir()
        {
            if (!Vivo) return;
            Vivo = false;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            srVisual.enabled = false;

            Color c = MemeDashGame.Instancia.ColorNivel;
            for (int i = 0; i < 16; i++)
            {
                ParticulaSimple.Crear(cuadrito, transform.position,
                    Random.insideUnitCircle.normalized * Random.Range(3f, 9f),
                    i % 2 == 0 ? c : Color.white,
                    Random.Range(0.5f, 0.9f), Random.Range(0.15f, 0.35f), 12f, 11);
            }
            MemeDashGame.Instancia.JugadorMurio();
        }

        void Ganar()
        {
            Vivo = false;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            for (int i = 0; i < 40; i++)
            {
                ParticulaSimple.Crear(cuadrito, transform.position,
                    new Vector2(Random.Range(-6f, 6f), Random.Range(4f, 12f)),
                    Color.HSVToRGB(Random.value, 0.8f, 1f),
                    Random.Range(0.8f, 1.4f), Random.Range(0.15f, 0.3f), 10f, 11);
            }
            MemeDashGame.Instancia.NivelCompletado();
        }
    }

    /// <summary>Particula ligera: se mueve, cae, gira, se desvanece y se destruye.</summary>
    public class ParticulaSimple : MonoBehaviour
    {
        Vector2 vel;
        float vida, vidaMax, gravedad, giro, escala;
        SpriteRenderer sr;
        Color color;

        public static void Crear(Sprite sprite, Vector3 pos, Vector2 vel, Color color,
                                 float vida, float escala, float gravedad, int orden)
        {
            var go = new GameObject("Particula");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * escala;
            var p = go.AddComponent<ParticulaSimple>();
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = sprite;
            p.sr.sortingOrder = orden;
            p.sr.color = color;
            p.vel = vel;
            p.vida = p.vidaMax = vida;
            p.gravedad = gravedad;
            p.color = color;
            p.escala = escala;
            p.giro = Random.Range(-540f, 540f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            vida -= dt;
            if (vida <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            vel.y -= gravedad * dt;
            transform.position += (Vector3)(vel * dt);
            transform.Rotate(0f, 0f, giro * dt);

            float k = vida / vidaMax;
            var c = color;
            c.a = k;
            sr.color = c;
            transform.localScale = Vector3.one * escala * (0.5f + 0.5f * k);
        }
    }
}
