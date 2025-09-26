@echo off
powershell -Command "Start-Process 'dotnet' -ArgumentList 'run --project WootMouseRemap.csproj' -Verb RunAs -WorkingDirectory '%~dp0'"