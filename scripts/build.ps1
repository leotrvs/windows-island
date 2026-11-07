# Builds bin\WindowsIsland.exe as one self-contained file that runs without .NET installed.
# Needs the .NET 10 SDK.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\bin'))

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'src\WindowsIsland.csproj'

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true -p:DebugType=none -o $OutDir --nologo
# IncludeAllContentForSelfExtract unpacks the libraries to disk once and maps them from there.
# Without it the compressed libraries are inflated in memory on every start (measured: 128 MB vs 43 MB).
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
"Built $(Join-Path (Resolve-Path $OutDir) 'WindowsIsland.exe')"
