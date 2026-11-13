# Builds bin\NoteZero.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
# No Visual Studio, .NET SDK or NuGet needed.
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$fw   = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path "$fw\csc.exe")) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$csc  = Join-Path $fw 'csc.exe'
$wpf  = Join-Path $fw 'WPF'
if (-not (Test-Path $csc)) { throw ".NET Framework 4.x compiler not found: $csc" }

$icon = Join-Path $root 'assets\NoteZero.ico'

$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

& $csc /nologo /noconfig /nostdlib- `
    /target:winexe /platform:anycpu /optimize+ /debug- /warn:4 `
    "/out:$out\NoteZero.exe" `
    "/win32manifest:$root\src\app.manifest" `
    "/win32icon:$icon" `
    "/resource:$root\src\Theme.xaml,NoteZero.Theme.xaml" `
    "/r:$fw\System.dll" `
    "/r:$fw\System.Core.dll" `
    "/r:$fw\System.Xml.dll" `
    "/r:$fw\System.Xaml.dll" `
    "/r:$wpf\WindowsBase.dll" `
    "/r:$wpf\PresentationCore.dll" `
    "/r:$wpf\PresentationFramework.dll" `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

Write-Host "Built $out\NoteZero.exe"
