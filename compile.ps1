# Etern Audio - Compilacion nativa Windows (WPF / .NET Framework 4.x)
# Uso: .\compile.ps1

param([string]$Configuration = "Release")

Write-Host "========================================"
Write-Host "  Etern Audio - Build nativo Windows"
Write-Host "========================================"

# Locate csc.exe
$cscPaths = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $null
foreach ($p in $cscPaths) { if (Test-Path $p) { $csc = $p; break } }
if (-not $csc) {
    Write-Host "ERROR: No se encontro csc.exe"
    exit 1
}
Write-Host "Usando compilador: $csc"

# GAC Assembly references
$gacMsil = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$gac64   = "C:\Windows\Microsoft.NET\assembly\GAC_64"

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Runtime.Serialization.dll",
    "System.Xaml.dll",
    "System.Windows.Forms.dll",
    "$gac64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll",
    "$gacMsil\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll",
    "$gacMsil\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"
)

$refList = $refs -join ","

# Source files
$sources = @("Models.cs","FileOrganizer.cs","TagEngine.cs","SearchEngine.cs","WpfMainWindow.cs")
foreach ($src in $sources) {
    if (-not (Test-Path $src)) { Write-Host "ERROR: No se encontro $src"; exit 1 }
}

$out = "EternAudio.exe"
$optimize = "/optimize+"

Write-Host "Compilando $($sources.Count) archivos..."

& $csc /target:winexe /out:"$out" $optimize /reference:"$refList" Models.cs FileOrganizer.cs TagEngine.cs SearchEngine.cs WpfMainWindow.cs

if ($LASTEXITCODE -eq 0) {
    $size = [math]::Round((Get-Item $out).Length / 1KB, 1)
    Write-Host ""
    Write-Host "BUILD EXITOSO"
    Write-Host "  Archivo: $out (${size} KB)"
    Write-Host "  Ejecutar: .\EternAudio.exe"
} else {
    Write-Host "BUILD FALLIDO"
    exit 1
}
