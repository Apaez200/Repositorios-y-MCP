using System.Text;
using MemeDash;
using UnityEditor;
using UnityEngine;

/// <summary>Herramientas de editor para Meme Dash.</summary>
static class MemeDashMenu
{
    [MenuItem("Meme Dash/Verificar que los niveles se pueden pasar")]
    static void Verificar()
    {
        // Usa los valores del inspector si la escena del juego esta abierta.
        var juego = Object.FindFirstObjectByType<MemeDashGame>();
        var informe = new StringBuilder();
        bool todos = true;

        for (int i = 0; i < NivelMemeDash.Niveles.Length; i++)
        {
            var fisica = juego != null
                ? juego.FisicaDe(i)
                : FisicaSalto.Desde(NivelMemeDash.Niveles[i].Velocidad, 2.2f, 0.235f, 1.45f);
            var r = VerificadorNivel.Resolver(fisica, Time.fixedDeltaTime, i);
            todos &= r.Superable;
            informe.AppendLine($"{i + 1}. {r.Informe}");
        }

        if (todos) Debug.Log("Meme Dash:\n" + informe);
        else Debug.LogWarning("Meme Dash:\n" + informe);
        EditorUtility.DisplayDialog("Meme Dash", informe.ToString(), "OK");
    }
}
