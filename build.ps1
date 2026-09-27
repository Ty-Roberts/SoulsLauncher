[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $root 'build'
$executable = Join-Path $output 'Souls Launcher.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found. Install .NET Framework 4.8 developer tools.'
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output 'assets') -Force | Out-Null

& (Join-Path $root 'make-icon.ps1')

& $compiler /nologo /target:winexe "/out:$executable" "/win32icon:$root\assets\app-icon.ico" `
    "/reference:$wpf\PresentationFramework.dll" `
    "/reference:$wpf\PresentationCore.dll" `
    "/reference:$wpf\WindowsBase.dll" `
    "/reference:$framework\System.Xaml.dll" `
    "/reference:$framework\System.Windows.Forms.dll" `
    "/reference:$framework\System.Web.Extensions.dll" `
    (Join-Path $root 'SoulsLauncher.cs')

if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }

Copy-Item -LiteralPath (Join-Path $root 'assets\souls-panorama.png') -Destination (Join-Path $output 'assets\souls-panorama.png') -Force
Copy-Item -LiteralPath (Join-Path $root 'assets\elden-panorama.png') -Destination (Join-Path $output 'assets\elden-panorama.png') -Force
Copy-Item -LiteralPath (Join-Path $root 'assets\app-icon.png') -Destination (Join-Path $output 'assets\app-icon.png') -Force
Copy-Item -LiteralPath (Join-Path $root 'assets\app-icon.ico') -Destination (Join-Path $output 'assets\app-icon.ico') -Force

$test = Start-Process -FilePath $executable -ArgumentList '/smoketest' -Wait -PassThru -WindowStyle Hidden
if ($test.ExitCode -ne 0) { throw "Smoke test failed with exit code $($test.ExitCode)." }

Write-Host "Build succeeded: $executable"
