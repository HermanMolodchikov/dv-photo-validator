# Builds a release: runs all tests, publishes a self-contained Windows x64 build
# (no .NET installation needed on the target PC) and packs it into dist\*.zip.
#   powershell -ExecutionPolicy Bypass -File build\release.ps1 -Version 1.0.0
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet test DvPhotoValidator.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

    $name = "DvPhotoValidator-$Version-win-x64"
    $out = Join-Path $root "dist\$name"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }

    dotnet publish src\DvPhotoValidator.App -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:DebugType=none -o $out
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

    # Import libraries are only needed for native linking, not at run time.
    Get-ChildItem $out -Filter *.lib | Remove-Item

    $docs = New-Item -ItemType Directory -Force (Join-Path $out "docs")
    Copy-Item README.md, LICENSE, THIRD-PARTY.md $out
    Copy-Item docs\user-guide.md, docs\rules-update.md, docs\algorithms.md, docs\report-examples.md $docs

    $zip = Join-Path $root "dist\$name.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
    $size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "Release ready: $zip ($size MB)"
}
finally {
    Pop-Location
}
