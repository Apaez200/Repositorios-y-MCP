using UnityEngine;

namespace MemeDash
{
    /// <summary>
    /// Camara que sigue al jugador en X (con el cubo a la izquierda de la pantalla),
    /// sube suavemente si el jugador sube, y dibuja un fondo con degradado y parallax.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MemeDashCamera : MonoBehaviour
    {
        [SerializeField] float desplazamientoX = 5f;
        [SerializeField] float alturaBase = 3f;
        [SerializeField] float suavizadoY = 0.2f;

        const float PeriodoDeco = 8f;  // tamaño del mosaico de decoracion (unidades)
        const float Parallax = 0.3f;   // 0 = pegado al mundo, 1 = pegado a la camara

        Camera cam;
        Transform objetivo;
        SpriteRenderer fondo, deco;
        Vector3 posBase;
        float velY, tiempoSacudida, duracionSacudida, fuerzaSacudida;

        public void Configurar(Transform objetivo, Sprite spriteFondo, Sprite spriteDeco)
        {
            cam = GetComponent<Camera>();
            this.objetivo = objetivo;

            fondo = new GameObject("Fondo").AddComponent<SpriteRenderer>();
            fondo.sprite = spriteFondo;
            fondo.sortingOrder = -100;
            fondo.transform.SetParent(transform, false);
            fondo.transform.localPosition = new Vector3(0f, 0f, 20f);

            deco = new GameObject("Fondo Parallax").AddComponent<SpriteRenderer>();
            deco.sprite = spriteDeco;
            deco.drawMode = SpriteDrawMode.Tiled;
            deco.sortingOrder = -90;
        }

        /// <summary>Coloca la camara de golpe en el jugador (al reiniciar un intento).</summary>
        public void Colocar()
        {
            if (objetivo == null) return;
            velY = 0f;
            posBase = new Vector3(objetivo.position.x + desplazamientoX, alturaBase, -10f);
            transform.position = posBase;
            ActualizarFondo();
        }

        public void Sacudir(float duracion, float fuerza)
        {
            duracionSacudida = tiempoSacudida = duracion;
            fuerzaSacudida = fuerza;
        }

        public void Tintar(Color colorFondo)
        {
            if (fondo != null) fondo.color = colorFondo;
        }

        void LateUpdate()
        {
            if (objetivo == null) return;

            float objetivoY = Mathf.Max(alturaBase, objetivo.position.y + 0.5f);
            posBase.x = objetivo.position.x + desplazamientoX;
            posBase.y = Mathf.SmoothDamp(posBase.y, objetivoY, ref velY, suavizadoY);
            posBase.z = -10f;

            Vector3 sacudida = Vector3.zero;
            if (tiempoSacudida > 0f)
            {
                tiempoSacudida -= Time.deltaTime;
                sacudida = (Vector3)(Random.insideUnitCircle * fuerzaSacudida * (tiempoSacudida / duracionSacudida));
            }

            transform.position = posBase + sacudida;
            ActualizarFondo();
        }

        void ActualizarFondo()
        {
            if (cam == null || fondo == null) return;
            float alto = cam.orthographicSize * 2f;
            float ancho = alto * cam.aspect;

            // El degradado cubre toda la vista (va pegado a la camara).
            Vector2 tam = fondo.sprite.bounds.size;
            fondo.transform.localScale = new Vector3(ancho * 1.3f / tam.x, alto * 1.3f / tam.y, 1f);

            // La decoracion se mueve a una fraccion de la camara. Al avanzar un periodo
            // completo se recoloca exactamente un mosaico, asi que no se nota el salto.
            Vector3 c = transform.position;
            float ox = Mathf.Repeat(c.x * (1f - Parallax), PeriodoDeco);
            float oy = Mathf.Repeat(c.y * (1f - Parallax), PeriodoDeco);
            deco.size = new Vector2(ancho + PeriodoDeco * 2f, alto + PeriodoDeco * 2f);
            deco.transform.position = new Vector3(c.x - ox + PeriodoDeco * 0.5f, c.y - oy + PeriodoDeco * 0.5f, 5f);
        }
    }
}
