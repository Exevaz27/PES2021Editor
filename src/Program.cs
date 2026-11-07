using System;
using System.Threading;
using System.Windows.Forms;

namespace PesEditorWeb
{
    // Punto de entrada de la app web: una ventana nativa fina (WebView2)
    // con toda la interfaz en HTML/CSS/JS (carpeta www) y la logica PES
    // en C# detras del puente (ver WebHostForm).
    internal static class Program
    {
        private static Mutex _singleInstance;

        [STAThread]
        private static void Main()
        {
            // Instancia unica: el motor WebView2 usa una carpeta de datos compartida
            // (%LocalAppData%\PES2021EditorWeb); una segunda copia abierta a la vez
            // fallaba con 0x800700AA ("recurso en uso") y un mensaje enganoso.
            // Ademas, dos instancias podrian pisarse los .bin de la misma base.
            bool createdNew;
            _singleInstance = new Mutex(true, "PES2021EditorWeb.SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "El editor ya está abierto.\n\n" +
                    "Cerrá la otra ventana antes de abrir una segunda copia: dos instancias a la vez no pueden compartir el motor web ni la misma base.",
                    "PES 2021 EDITOR PRO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WebHostForm());
            GC.KeepAlive(_singleInstance);
        }
    }
}
