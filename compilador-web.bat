@echo off
rem =====================================================================
rem Recompila la app web completa (anfitrion + servicios + TODA la logica
rem PES reutilizada) con el compilador de .NET Framework de Windows.
rem Las DLL de WebView2 tienen que estar en esta misma carpeta.
rem =====================================================================
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /unsafe /out:PES2021EditorWeb.exe /reference:Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.WinForms.dll,System.Web.Extensions.dll,System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll src\Program.cs src\WebHostForm.cs src\EditorService.cs src\logic\DatabaseManager.cs src\logic\PesDbModel.cs src\logic\AppConfig.cs src\logic\SiderLuaGenerator.cs src\logic\LeaguePackageManager.cs src\logic\PesCrypt.cs src\logic\CpkExtractor.cs src\logic\SiderIniManager.cs src\logic\WesysCompressor.cs src\logic\PlayerBitHelper.cs src\logic\MinifaceProvider.cs src\logic\GameEmblemProvider.cs src\logic\ImageProcessor.cs src\logic\HealthCheck.cs src\logic\SiderBackup.cs src\logic\MundoRegistry.cs src\logic\PresetEngine.cs src\logic\SquadGenerator.cs src\logic\NewLifeImporter.cs src\logic\CsvImporter.cs src\logic\MundoSwitch.cs src\logic\ServerMap.cs
if %errorlevel%==0 (echo OK: PES2021EditorWeb.exe generado) else (echo ERROR al compilar)
pause
