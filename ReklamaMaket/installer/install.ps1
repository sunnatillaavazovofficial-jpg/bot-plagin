<#
  Reklama Maket - CorelDRAW'ga o'rnatish (xaridor uchun).

  install.bat ni ikki marta bosing. Kompyuterdagi barcha CorelDRAW'lar topiladi va har biriga
  o'z versiyasiga mos plagin qo'yiladi. O'chirish: install.bat -Uninstall
#>
param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$Here = $PSScriptRoot
$AddonName = "ReklamaMaket"

function Find-CorelInstalls {
    $bases = @("$env:ProgramFiles\Corel", "$env:ProgramW6432\Corel") |
        Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    foreach ($base in $bases) {
        Get-ChildItem -Path $base -Filter "CorelDRW.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.DirectoryName "Assemblies\Corel.Interop.VGCore.dll") } |
            ForEach-Object { $_.DirectoryName }
    }
}

# PowerShell uchun bitta tirnoqli matn (yo'lda ' bo'lsa ham ishlaydi)
function Quote([string]$text) {
    return "'" + $text.Replace("'", "''") + "'"
}

function Invoke-Elevated([string]$command) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) {
        Invoke-Expression $command
        return
    }
    # Program Files ga yozish uchun administrator huquqi kerak
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded"
}

function Wait-CorelClosed {
    while (Get-Process -Name "CorelDRW" -ErrorAction SilentlyContinue) {
        Write-Warning "CorelDRAW ochiq. Uni yoping."
        Read-Host "CorelDRAW yopilgach Enter bosing" | Out-Null
    }
}

$found = @(Find-CorelInstalls | Select-Object -Unique)
if ($found.Count -eq 0) {
    throw "CorelDRAW topilmadi (Program Files\Corel ichida)."
}
Wait-CorelClosed

$command = ""
$installed = @()
$missing = @()
foreach ($corel in $found) {
    $target = Join-Path $corel "Addons\$AddonName"
    if ($Uninstall) {
        $command += "if (Test-Path $(Quote $target)) { Remove-Item -Recurse -Force $(Quote $target) }; "
        $installed += $target
        continue
    }

    $major = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $corel "Assemblies\Corel.Interop.VGCore.dll")).Version.Major
    $source = Join-Path $Here "v$major"
    if (-not (Test-Path (Join-Path $source "ReklamaMaket.dll"))) {
        $missing += "$corel (v$major)"
        continue
    }
    $command += "New-Item -ItemType Directory -Force -Path $(Quote $target) | Out-Null; " +
                "Copy-Item -Force -Path $(Quote (Join-Path $source '*')) -Destination $(Quote $target); " +
                "Get-ChildItem -Path $(Quote $target) | Unblock-File; "
    $installed += $target
}

if ($command) {
    Invoke-Elevated $command
}

Write-Host ""
if ($Uninstall) {
    foreach ($target in $installed) {
        Write-Host "Olib tashlandi: $target" -ForegroundColor Green
    }
    return
}

$ok = $installed | Where-Object { Test-Path (Join-Path $_ "ReklamaMaket.dll") }
foreach ($target in $ok) {
    Write-Host "O'rnatildi: $target" -ForegroundColor Green
}
foreach ($item in $missing) {
    Write-Host "Bu CorelDRAW versiyasi uchun plagin yo'q: $item - sotuvchiga murojaat qiling." -ForegroundColor Yellow
}
if (-not $ok) {
    throw "Plagin o'rnatilmadi."
}
Write-Host ""
Write-Host "CorelDRAW'ni oching: menyuda 'Reklama' > 'Reklama Maket'."
Write-Host "Panelda kompyuter kodini nusxalab, kalit oling: https://sunnatilla-designer.uz/"
Write-Host "Menyu ko'rinmasa: CorelDRAW'ni F8 tugmasini bosib turgan holda oching."
