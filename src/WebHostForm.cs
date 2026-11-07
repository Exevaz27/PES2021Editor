using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PesEditorWeb
{
    // Ventana anfitriona: WebView2 a pantalla completa + puente de mensajes.
    //
    // Protocolo:
    //   JS -> C# : window.chrome.webview.postMessage({ action, _id, ... })
    //   C# -> JS : PostWebMessageAsJson({ _replyTo, type, ok, ... })
    //
    // El host solo resuelve dialogos nativos (carpetas/archivos). Todo lo
    // demas lo atiende EditorService con la logica PES reutilizada.
    public sealed class WebHostForm : Form
    {
        private readonly WebView2 _web;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private readonly EditorService _service = new EditorService();

        public WebHostForm()
        {
            Text = "PES 2021 EDITOR PRO";
            Width = 1360;
            Height = 860;
            MinimumSize = new System.Drawing.Size(1080, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = System.Drawing.Color.FromArgb(11, 15, 25);

            try
            {
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www", "assets", "app.ico");
                if (File.Exists(ico)) Icon = new System.Drawing.Icon(ico);
            }
            catch { }

            _web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_web);
            Load += OnLoad;
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            try
            {
                string uData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PES2021EditorWeb");
                var env = await CoreWebView2Environment.CreateAsync(null, uData);
                await _web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                string msg;
                if (ex.HResult == unchecked((int)0x800700AA))
                {
                    // ERROR_BUSY: la carpeta de datos del motor quedo tomada por otro
                    // proceso (una copia anterior que no cerro del todo).
                    msg = "El motor web está ocupado por otro proceso.\n\n" +
                        "Cerrá todas las ventanas del editor y esperá unos segundos. Si sigue pasando, " +
                        "abrí el Administrador de tareas y finalizá los procesos 'PES2021EditorWeb' o 'msedgewebview2' que hayan quedado colgados.\n\nDetalle: " + ex.Message;
                }
                else
                {
                    msg = "No se pudo iniciar el motor web (WebView2). En Windows 11 viene con Edge; " +
                        "si falta, instalalo desde la web de Microsoft.\n\nDetalle: " + ex.Message;
                }
                MessageBox.Show(msg, "PES 2021 EDITOR PRO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            CoreWebView2 core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.WebMessageReceived += OnWebMessage;

            string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
            core.SetVirtualHostNameToFolderMapping("pesapp.local", www, CoreWebView2HostResourceAccessKind.Allow);
            core.Navigate("https://pesapp.local/index.html?v=per38-20261106");
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            Dictionary<string, object> msg;
            try { msg = _json.Deserialize<Dictionary<string, object>>(args.WebMessageAsJson); }
            catch { return; }
            if (msg == null) return;

            string action = GetString(msg, "action");
            object idObj; msg.TryGetValue("_id", out idObj);

            Dictionary<string, object> reply;
            try
            {
                reply = Dispatch(action, msg) ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                reply = new Dictionary<string, object> { { "ok", false }, { "error", ex.Message } };
            }
            reply["_replyTo"] = idObj;
            reply["type"] = action + "Result";
            if (!reply.ContainsKey("ok")) reply["ok"] = true;
            try { _web.CoreWebView2.PostWebMessageAsJson(_json.Serialize(reply)); } catch { }
        }

        private Dictionary<string, object> Dispatch(string action, Dictionary<string, object> msg)
        {
            switch (action)
            {
                case "ping":
                    return new Dictionary<string, object> { { "ok", true }, { "app", "PES 2021 EDITOR PRO" }, { "shell", "beta-1" } };

                case "pickFolder":
                    using (var dlg = new FolderBrowserDialog())
                    {
                        dlg.Description = GetString(msg, "title") ?? "Elegí una carpeta";
                        string initial = GetString(msg, "initial");
                        if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial)) dlg.SelectedPath = initial;
                        DialogResult r = dlg.ShowDialog(this);
                        return new Dictionary<string, object> { { "ok", true }, { "path", r == DialogResult.OK ? dlg.SelectedPath : null } };
                    }

                case "pickFile":
                    using (var dlg = new OpenFileDialog())
                    {
                        dlg.Title = GetString(msg, "title") ?? "Elegí un archivo";
                        dlg.Filter = GetString(msg, "filter") ?? "Todos los archivos (*.*)|*.*";
                        string initial = GetString(msg, "initial");
                        if (!string.IsNullOrEmpty(initial) && File.Exists(initial)) dlg.FileName = initial;
                        DialogResult r = dlg.ShowDialog(this);
                        return new Dictionary<string, object> { { "ok", true }, { "path", r == DialogResult.OK ? dlg.FileName : null } };
                    }

                case "saveFile":
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Title = GetString(msg, "title") ?? "Guardar como";
                        dlg.Filter = GetString(msg, "filter") ?? "Todos los archivos (*.*)|*.*";
                        dlg.FileName = GetString(msg, "defaultName") ?? "";
                        DialogResult r = dlg.ShowDialog(this);
                        return new Dictionary<string, object> { { "ok", true }, { "path", r == DialogResult.OK ? dlg.FileName : null } };
                    }

                // Acciones con archivo elegido por el frontend via dialogos:
                case "exportLeaguePackageFile":
                    return _service.ExportLeaguePackageFile(GetString(msg, "path"), (byte)GetInt(msg, "compId", 0));
                case "importLeaguePackageFile":
                    return _service.ImportLeaguePackageFile(GetString(msg, "path"));
                case "exportTeamsCsvFile":
                    return _service.ExportTeamsCsvFile(GetString(msg, "path"));
                case "importTeamsCsvFile":
                    return _service.ImportTeamsCsvFile(GetString(msg, "path"));
                case "editExportDataFile":
                    return _service.EditExportDataFile(GetString(msg, "path"));
                case "editImportDataFile":
                    return _service.EditImportDataFile(GetString(msg, "path"));

                default:
                    return _service.Handle(action, msg);
            }
        }

        private static string GetString(Dictionary<string, object> msg, string key)
        {
            object v; if (msg.TryGetValue(key, out v) && v != null) return v.ToString();
            return null;
        }

        private static int GetInt(Dictionary<string, object> msg, string key, int def)
        {
            object v; int r;
            if (msg.TryGetValue(key, out v) && v != null && int.TryParse(v.ToString(), out r)) return r;
            return def;
        }
    }
}
