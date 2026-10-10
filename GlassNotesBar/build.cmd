@echo off
rem Builds GlassNotesBar.exe with the C# compiler that ships with .NET Framework 4 (no SDK needed).
rem Usage: build.cmd [output folder]   (default: bin)
setlocal
cd /d "%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=bin"
if not exist "%OUT%" mkdir "%OUT%"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32manifest:app.manifest /out:"%OUT%\GlassNotesBar.exe" GlassNotesBar.cs
