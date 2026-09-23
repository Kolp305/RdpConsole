# Builds RDP Console using the built-in csc.exe (.NET Framework) --
# no .NET SDK or Visual Studio required.

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    Write-Error "csc.exe (.NET Framework 4.x) not found. Cannot compile."
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir
try {
    if (-not (Test-Path (Join-Path $scriptDir "RdpConsole.ico"))) {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "GenerateIcon.ps1")
    }

    & $csc /nologo /target:winexe /platform:anycpu /codepage:65001 `
        /out:RdpConsole.exe `
        /win32manifest:app.manifest `
        /win32icon:RdpConsole.ico `
        /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Runtime.Serialization.dll,System.Security.dll `
        RdpConsole.cs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: $scriptDir\RdpConsole.exe" -ForegroundColor Green
    } else {
        Write-Error "Build failed (exit code $LASTEXITCODE)."
    }
} finally {
    Pop-Location
}
