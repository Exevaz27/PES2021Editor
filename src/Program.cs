using System;
using System.Windows.Forms;

namespace PesEditorWeb
{
    // Punto de entrada de la app web: una ventana nativa fina (WebView2)
    // con toda la interfaz en HTML/CSS/JS (carpeta www) y la logica PES
    // en C# detras del puente (ver WebHostForm).
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WebHostForm());
        }
    }
}
