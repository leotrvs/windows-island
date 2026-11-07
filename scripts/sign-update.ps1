# Writes update.json and its signature next to the exe.
# The key is shared with Claude Usage Taskbar; the "app" field keeps each app from accepting the other's manifest.
param(
    [Parameter(Mandatory)][string]$ExePath,
    [Parameter(Mandatory)][string]$Version,
    [string]$KeyPath = (Join-Path $HOME '.claude-usage-taskbar-signing\update-signing-key.xml')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $KeyPath)) { throw "Signing key not found at $KeyPath. See docs/RELEASING.md." }

$directory = Split-Path (Resolve-Path $ExePath)
$hash = (Get-FileHash $ExePath -Algorithm SHA256).Hash
$manifestPath = Join-Path $directory 'update.json'
$manifest = "{`n  `"app`": `"WindowsIsland`",`n  `"version`": `"$Version`",`n  `"sha256`": `"$hash`"`n}`n"
[IO.File]::WriteAllText($manifestPath, $manifest, (New-Object Text.UTF8Encoding $false))

$rsa = [System.Security.Cryptography.RSA]::Create()
$rsa.FromXmlString([IO.File]::ReadAllText($KeyPath))
$signature = $rsa.SignData([IO.File]::ReadAllBytes($manifestPath),
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
[IO.File]::WriteAllBytes("$manifestPath.sig", $signature)
"SHA-256: $hash"
