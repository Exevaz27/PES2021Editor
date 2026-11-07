@echo off
setlocal
set "ROOT=%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /unsafe /out:"%ROOT%PES2021EditorWeb.exe" /reference:"%ROOT%Microsoft.Web.WebView2.Core.dll" /reference:"%ROOT%Microsoft.Web.WebView2.WinForms.dll" /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "%ROOT%src\Program.cs" "%ROOT%src\WebHostForm.cs" "%ROOT%src\EditorService.cs" "%ROOT%src\logic\DatabaseManager.cs" "%ROOT%src\logic\PesDbModel.cs" "%ROOT%src\logic\AppConfig.cs" "%ROOT%src\logic\SiderLuaGenerator.cs" "%ROOT%src\logic\LeaguePackageManager.cs" "%ROOT%src\logic\PesCrypt.cs" "%ROOT%src\logic\CpkExtractor.cs" "%ROOT%src\logic\SiderIniManager.cs" "%ROOT%src\logic\WesysCompressor.cs" "%ROOT%src\logic\PlayerBitHelper.cs" "%ROOT%src\logic\MinifaceProvider.cs" "%ROOT%src\logic\GameEmblemProvider.cs" "%ROOT%src\logic\ImageProcessor.cs" "%ROOT%src\logic\HealthCheck.cs" "%ROOT%src\logic\SiderBackup.cs" "%ROOT%src\logic\MundoRegistry.cs" "%ROOT%src\logic\PresetEngine.cs" "%ROOT%src\logic\SquadGenerator.cs" "%ROOT%src\logic\NewLifeImporter.cs" "%ROOT%src\logic\CsvImporter.cs" "%ROOT%src\logic\MundoSwitch.cs" "%ROOT%src\logic\ServerMap.cs"
if %ERRORLEVEL% EQU 0 (
    echo [BUILD SUCCESS] PES2021EditorWeb.exe compilado correctamente.
) else (
    echo [BUILD ERROR] Fallo la compilacion.
)
