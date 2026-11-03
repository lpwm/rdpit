# Build RdpIt directly with csc.exe against the runtime assemblies,
# avoiding the need for a .NET Framework Targeting Pack (not installed here).
$ErrorActionPreference = "Stop"

$csc = "C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$roslyn = Join-Path $PSScriptRoot ".tools\roslyn\tasks\net472\csc.exe"
if (Test-Path $roslyn) {
    $csc = $roslyn   # modern Roslyn compiler (supports C# 7.3+, dynamic, etc.)
}
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

$root = $PSScriptRoot
$outDir = Join-Path $root "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outExe = Join-Path $outDir "RdpIt.exe"

# Runtime assembly locations (the .NET 4.x GAC-style reference folders).
$fx = "C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319"
$wpfRef = "C:\WINDOWS\Microsoft.NET\assembly\GAC_MSIL"

function Find-Asm($name) {
    # Prefer the runtime folder; fall back to the GAC_MSIL store.
    $p = Join-Path $fx "$name.dll"
    if (Test-Path $p) { return $p }
    $hit = Get-ChildItem (Join-Path $wpfRef $name) -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($hit) { return $hit }
    throw "Could not locate reference assembly: $name"
}

$refs = @(
    (Find-Asm "System"),
    (Find-Asm "System.Core"),
    (Find-Asm "System.Drawing"),
    (Find-Asm "System.Windows.Forms"),
    (Find-Asm "System.Security"),
    (Find-Asm "System.Web.Extensions"),
    (Find-Asm "Microsoft.CSharp")
)

$sources = Get-ChildItem (Join-Path $root "src") -Filter *.cs | Select-Object -ExpandProperty FullName

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }
$srcArgs = $sources | ForEach-Object { "`"$_`"" }

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/langversion:7.3",
    "/win32manifest:`"$(Join-Path $root 'app.manifest')`"",
    "/win32icon:`"$(Join-Path $root 'assets\rdpit.ico')`"",
    "/out:`"$outExe`""
) + $refArgs + $srcArgs

Write-Host "Compiling RdpIt ..." -ForegroundColor Cyan
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit $LASTEXITCODE)" }
Write-Host "Build OK -> $outExe" -ForegroundColor Green
