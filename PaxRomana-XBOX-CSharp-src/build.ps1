# Builds the signed Xbox (x64) package of Pax Romana: Tertia Roma, native C# edition.
# Run on Windows, in PowerShell, from this folder:
#
#     powershell -ExecutionPolicy Bypass -File .\build.ps1
#
# Needs Visual Studio 2022 with the "Universal Windows Platform development" workload. Nothing else: no Node.js,
# no content pipeline. Output: .\package\PaxRomanaNative_<version>_x64_Test\  (the .msix plus Dependencies\x64)
# The project builds without .NET Native (see the comment in the project file), so the package carries the game's
# managed assemblies and depends on the .NET Core runtime packages in Dependencies\x64.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# 1. Find MSBuild: already on PATH (Developer PowerShell), or located through vswhere.
$msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue).Source
if (-not $msbuild) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $msbuild) { throw 'MSBuild not found. Install Visual Studio 2022 with the Universal Windows Platform development workload.' }
Write-Host "MSBuild: $msbuild"

# 2. Self-signed signing certificate, created once and reused. Its subject must match Publisher in Package.appxmanifest.
$pfx = Join-Path $PSScriptRoot 'paxromana-sideload.pfx'
$pfxPassword = 'paxromana-sideload'
if (-not (Test-Path $pfx)) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=PaxRomana' -KeyUsage DigitalSignature -FriendlyName 'Pax Romana sideload' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $pfxPassword -Force -AsPlainText) | Out-Null
    Write-Host "Created signing certificate $pfx"
}

# 3. Give this build its own version number. The console treats a package with the same name and version as already
#    installed, so without this a new build can be accepted and yet leave the old one in place. On GitHub the third
#    part is the run number; on a PC it is the number of hours since the start of 2026. The manifest in the source
#    tree is put back afterwards.
$manifest = Join-Path $PSScriptRoot 'xbox\PaxRomanaNative\Package.appxmanifest'
$original = [System.IO.File]::ReadAllText($manifest)
$build = if ($env:GITHUB_RUN_NUMBER) { [int]$env:GITHUB_RUN_NUMBER } else { [int]([datetime]::UtcNow - [datetime]'2026-01-01').TotalHours }
$version = "1.0.$build.0"
$stamped = [regex]::Replace($original, '(<Identity\b[^>]*\bVersion=")[^"]*', ('${1}' + $version))
if ($stamped -eq $original) { throw 'Could not find the Identity Version in Package.appxmanifest.' }
[System.IO.File]::WriteAllText($manifest, $stamped, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Package version: $version"

# 4. Package.
$out = Join-Path $PSScriptRoot 'package'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

try {
& $msbuild 'xbox\PaxRomanaNative\PaxRomanaNative.csproj' /restore /m /v:minimal `
    /p:Configuration=Release /p:Platform=x64 `
    /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly `
    "/p:AppxPackageDir=$out\" `
    /p:AppxPackageSigningEnabled=true "/p:PackageCertificateKeyFile=$pfx" "/p:PackageCertificatePassword=$pfxPassword" /p:PackageCertificateThumbprint=
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)" }
} finally {
    [System.IO.File]::WriteAllText($manifest, $original, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host ''
Write-Host 'Package files:'
Get-ChildItem $out -Recurse -Include *.appx, *.msix | ForEach-Object { Write-Host "  $($_.FullName)" }
