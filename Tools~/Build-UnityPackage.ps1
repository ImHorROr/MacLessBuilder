# Builds Builds~/MacLessBuilder-<version>.unitypackage.
# Copies this package into a throwaway Unity project under Assets/HorrorTools/MacLessBuilder, lets Unity
# import and compile it there (a compile error fails the build), then exports that folder.
# The .meta files travel with the copy, so asset GUIDs stay stable between releases.
param(
    # Defaults to the newest Unity 6 editor installed through Unity Hub.
    [string]$UnityExe
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'package.json') -Raw | ConvertFrom-Json).version

if (-not $UnityExe) {
    $UnityExe = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\6000.*\Editor\Unity.exe' |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $UnityExe -or -not (Test-Path $UnityExe)) { throw 'Unity 6 not found. Pass -UnityExe <path to Unity.exe>.' }

$work = Join-Path ([IO.Path]::GetTempPath()) 'MacLessBuilder-export'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
$target = Join-Path $work 'Assets\HorrorTools\MacLessBuilder'
New-Item -ItemType Directory -Force $target | Out-Null

# Unity never imports ~ or dot folders; the Rust source and CI files ship in the repository instead.
robocopy $root $target /E /NFL /NDL /NJH /NJS /XD 'Helper~' 'Tools~' 'Documentation~' 'Builds~' '.github' '.git' | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }

$outDir = Join-Path $root 'Builds~'
New-Item -ItemType Directory -Force $outDir | Out-Null
$out = Join-Path $outDir "MacLessBuilder-$version.unitypackage"
if (Test-Path $out) { Remove-Item $out -Force }
$log = Join-Path $work 'export.log'

Write-Host "Exporting with $UnityExe ..."
$process = Start-Process -FilePath $UnityExe -Wait -PassThru -NoNewWindow -ArgumentList @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$work`"",
    '-logFile', "`"$log`"",
    '-exportPackage', 'Assets/HorrorTools', "`"$out`"")

$compileErrors = Select-String -Path $log -Pattern 'error CS\d{4}' | Select-Object -First 20
if ($compileErrors) {
    $compileErrors | ForEach-Object { Write-Host $_.Line }
    throw 'The package does not compile in a clean project.'
}
if ($process.ExitCode -ne 0 -or -not (Test-Path $out)) {
    Get-Content $log -Tail 40
    throw "Unity exited with code $($process.ExitCode) and no package was written."
}

Write-Host "Built $out ($([math]::Round((Get-Item $out).Length / 1MB, 1)) MB)"

exit 0
