# Builds bin\WindowsIsland.exe as a single file. Needs the .NET 10 SDK.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\bin'))

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'src\WindowsIsland.csproj'

dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $OutDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
"Built $(Join-Path (Resolve-Path $OutDir) 'WindowsIsland.exe')"
