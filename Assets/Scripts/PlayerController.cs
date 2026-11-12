using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mueve al jugador con WASD usando Rigidbody2D (fisica 2D).
/// A/D: movimiento horizontal. W o Espacio: salto (solo tocando suelo).
/// Usa el Input System nuevo, que es el activo en este proyecto.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float velocidad = 6f;
    [SerializeField] private float fuerzaSalto = 7f;

    private Rigidbody2D rb;
    private float entradaHorizontal;
    private bool quiereSaltar;
    private bool enSuelo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true; // evita que el jugador ruede al chocar
    }

    // La entrada se lee en Update para no perder pulsaciones.
    private void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;

        entradaHorizontal = 0f;
        if (teclado.aKey.isPressed) entradaHorizontal -= 1f;
        if (teclado.dKey.isPressed) entradaHorizontal += 1f;

        if (teclado.wKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame)
            quiereSaltar = true;
    }

    // La fisica se aplica en FixedUpdate.
    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(entradaHorizontal * velocidad, rb.linearVelocity.y);

        if (quiereSaltar && enSuelo)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, fuerzaSalto);
            enSuelo = false;
        }
        quiereSaltar = false;
    }

    // Se considera suelo cualquier contacto cuya normal apunte hacia arriba.
    private void OnCollisionStay2D(Collision2D colision)
    {
        foreach (var contacto in colision.contacts)
        {
            if (contacto.normal.y > 0.5f) { enSuelo = true; return; }
        }
    }

    private void OnCollisionExit2D(Collision2D colision)
    {
        enSuelo = false;
    }
}