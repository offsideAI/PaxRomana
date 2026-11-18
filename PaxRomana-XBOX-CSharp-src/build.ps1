# Builds the signed Xbox (x64) package of Pax Romana: Tertia Roma, native C# edition.
# Run on Windows, in PowerShell, from this folder:
#
#     powershell -ExecutionPolicy Bypass -File .\build.ps1
#
# Needs Visual Studio 2022 with the "Universal Windows Platform development" workload. Nothing else: no Node.js,
# no content pipeline. Output: .\package\PaxRomanaNative_<version>_x64_Test\  (the .msix plus Dependencies\x64)
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

# 3. Package.
$out = Join-Path $PSScriptRoot 'package'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

& $msbuild 'xbox\PaxRomanaNative\PaxRomanaNative.csproj' /restore /m /v:minimal `
    /p:Configuration=Release /p:Platform=x64 `
    /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly `
    "/p:AppxPackageDir=$out\" `
    /p:AppxPackageSigningEnabled=true "/p:PackageCertificateKeyFile=$pfx" "/p:PackageCertificatePassword=$pfxPassword" /p:PackageCertificateThumbprint=
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)" }

Write-Host ''
Write-Host 'Package files:'
Get-ChildItem $out -Recurse -Include *.appx, *.msix | ForEach-Object { Write-Host "  $($_.FullName)" }
