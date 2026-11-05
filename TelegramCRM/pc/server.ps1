# TelegramCRM - shu kompyuterni (Windows 10/11) server qilish.
# KOMPYUTER_SERVER.bat bu faylni yuklab oladi va ishga tushiradi. Qayta ishga tushirish xavfsiz:
# sozlamalar (.env) va ma'lumotlar saqlanadi, faqat dastur yangilanadi.
#
# Nima qiladi:
#   1. Docker Desktop bo'lmasa - o'rnatadi (keyin kompyuterni qayta yoqish kerak).
#   2. Dasturni GitHub'dan yuklab, %USERPROFILE%\TelegramCRM-server papkasiga qo'yadi.
#   3. Kerakli sozlamalarni so'raydi (admin paroli, bot tokeni...) va .env ga yozadi.
#   4. Saytni ishga tushiradi. Bepul Cloudflare tunnel https manzil beradi - sayt hamma joydan ochiladi.
#
# Eslatma: bu fayl faqat ASCII belgilardan iborat (Windows PowerShell 5.1 uchun).

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$Repo = 'sunnatillaavazovofficial-jpg/bot-plagin'
$Branch = 'claude/blissful-brown-asw2bf'
$Port = '8181'
$Dir = if ($env:CRM_DIR) { $env:CRM_DIR } else { Join-Path $HOME 'TelegramCRM-server' }
$ProgramDir = if ($env:ProgramFiles) { $env:ProgramFiles } else { 'C:\Program Files' }
$DockerExe = "$ProgramDir\Docker\Docker\Docker Desktop.exe"
$DockerBin = "$ProgramDir\Docker\Docker\resources\bin"
$Utf8 = New-Object System.Text.UTF8Encoding $false

$script:EnvPath = Join-Path $Dir '.env'
$script:EnvLines = New-Object System.Collections.Generic.List[string]
$script:ResetAdmin = $false
$script:NewPassword = ''

function Say([string]$text) { Write-Host ''; Write-Host "==> $text" -ForegroundColor Cyan }
function Note([string]$text) { Write-Host "    $text" }
function Stop-With([string]$text) {
  Write-Host ''
  Write-Host $text -ForegroundColor Red
  exit 1
}
function Quiet([string]$command) {
  cmd /c "$command >nul 2>&1"
  return ($LASTEXITCODE -eq 0)
}

# ---------------------------------------------------------------- .env
function Load-Env {
  $script:EnvLines = New-Object System.Collections.Generic.List[string]
  if (-not (Test-Path -LiteralPath $script:EnvPath)) { return }
  $text = [IO.File]::ReadAllText($script:EnvPath).TrimStart([char]0xFEFF)
  foreach ($line in ($text -split "`r?`n")) { $script:EnvLines.Add($line) }
}

function Get-Var([string]$key) {
  foreach ($line in $script:EnvLines) {
    if ($line.StartsWith("$key=")) { return $line.Substring($key.Length + 1).Trim() }
  }
  return ''
}

function Set-Var([string]$key, [string]$value) {
  for ($i = 0; $i -lt $script:EnvLines.Count; $i++) {
    if ($script:EnvLines[$i].StartsWith("$key=")) {
      $script:EnvLines[$i] = "$key=$value"
      return
    }
  }
  $script:EnvLines.Add("$key=$value")
}

function Save-Env {
  $text = ($script:EnvLines -join "`n").TrimEnd() + "`n"
  [IO.File]::WriteAllText($script:EnvPath, $text, $Utf8)
}

function New-Secret {
  $bytes = New-Object byte[] 32
  [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

# Ish stolidagi TelegramCRM\.env (ORNATISH.bat uchun to'ldirilgan bo'lsa) - bo'sh qatorlarni shundan to'ldiramiz
function Import-DesktopEnv {
  $desktops = @([Environment]::GetFolderPath('Desktop'), (Join-Path $HOME 'Desktop')) | Select-Object -Unique
  $keys = @('ADMIN_LOGIN', 'ADMIN_PASSWORD', 'BOT_TOKEN', 'ADMIN_TG_ID', 'TG_API_ID', 'TG_API_HASH', 'SITE_PRESET', 'TIMEZONE')
  foreach ($desktop in $desktops) {
    if (-not $desktop) { continue }
    foreach ($name in @('.env', '.env.txt')) {
      $path = Join-Path (Join-Path $desktop 'TelegramCRM') $name
      if (-not (Test-Path -LiteralPath $path)) { continue }
      $text = [IO.File]::ReadAllText($path).TrimStart([char]0xFEFF)
      $used = $false
      foreach ($line in ($text -split "`r?`n")) {
        $m = [regex]::Match($line, '^([A-Z_][A-Z0-9_]*)=(.*)$')
        if (-not $m.Success) { continue }
        $key = $m.Groups[1].Value
        $value = $m.Groups[2].Value.Trim()
        if ($keys -notcontains $key -or -not $value -or (Get-Var $key)) { continue }
        Set-Var $key $value
        $used = $true
        if ($key -eq 'ADMIN_PASSWORD') { $script:ResetAdmin = $true }
      }
      if ($used) { Note "Sozlamalar shu fayldan olindi: $path" }
      return
    }
  }
}

function Ask([string]$question, [string]$hint) {
  Write-Host ''
  Write-Host $question -ForegroundColor Yellow
  if ($hint) { Write-Host "   $hint" -ForegroundColor DarkGray }
  return ([string](Read-Host '  ')).Trim()
}

function Ask-Missing {
  if (-not (Get-Var 'ADMIN_PASSWORD')) {
    while ($true) {
      $pass = Ask 'Admin uchun parol o''ylab toping va yozing (kamida 8 belgi, bo''sh joysiz):' 'Saytga shu parol bilan kirasiz. Yozib qo''ying!'
      if ($pass.Length -ge 8 -and $pass -notmatch '[\s"]') { break }
      Write-Host '   Parol kamida 8 belgi bo''lsin, bo''sh joy va " belgisisiz.' -ForegroundColor Red
    }
    Set-Var 'ADMIN_PASSWORD' $pass
    $script:ResetAdmin = $true
    $script:NewPassword = $pass
  }
  if (-not (Get-Var 'BOT_TOKEN')) {
    while ($true) {
      $token = Ask 'Bot tokenini yozing (@BotFather bergan, masalan 123456789:AAH...):' 'Nusxa olib, shu oynada sichqonchaning o''ng tugmasini bossangiz qo''yiladi. Bo''sh qoldirsangiz bot ishlamaydi.'
      if (-not $token -or $token -match '^\d{5,}:[A-Za-z0-9_-]{30,}$') { break }
      Write-Host '   Token noto''g''ri ko''rinadi. Qaytadan yozing.' -ForegroundColor Red
    }
    if ($token) { Set-Var 'BOT_TOKEN' $token }
  }
  if (-not (Get-Var 'ADMIN_TG_ID')) {
    while ($true) {
      $tgId = Ask 'Telegram ID raqamingizni yozing (masalan 6978604490):' 'Bot eslatmalari va kassaga yozish shu ID uchun ishlaydi. Bilmasangiz Enter bosing.'
      if (-not $tgId -or $tgId -match '^\d{5,15}$') { break }
      Write-Host '   Faqat raqamlar bo''lsin.' -ForegroundColor Red
    }
    if ($tgId) { Set-Var 'ADMIN_TG_ID' $tgId }
  }
  if (-not (Get-Var 'TG_API_ID') -or -not (Get-Var 'TG_API_HASH')) {
    $apiId = Ask 'Telegram akkauntlarni ulash uchun API ID (my.telegram.org -> API development tools):' 'Hozir bo''lmasa Enter bosing - keyin shu faylni qayta ishga tushirib qo''shasiz.'
    if ($apiId -match '^\d{3,12}$') {
      while ($true) {
        $apiHash = Ask 'API HASH (32 ta harf-raqam):' ''
        if ($apiHash -match '^[0-9a-fA-F]{32}$') { break }
        Write-Host '   API HASH 32 ta belgidan iborat bo''lishi kerak.' -ForegroundColor Red
      }
      Set-Var 'TG_API_ID' $apiId
      Set-Var 'TG_API_HASH' $apiHash.ToLower()
    }
  }
}

function Set-Defaults {
  if (-not (Get-Var 'SECRET_KEY')) { Set-Var 'SECRET_KEY' (New-Secret) }
  if (-not (Get-Var 'ADMIN_LOGIN')) { Set-Var 'ADMIN_LOGIN' 'admin' }
  if (-not (Get-Var 'TIMEZONE')) { Set-Var 'TIMEZONE' 'Asia/Tashkent' }
  if (-not (Get-Var 'SITE_PRESET')) { Set-Var 'SITE_PRESET' 'media-max' }
  if (-not (Get-Var 'APP_PORT')) { Set-Var 'APP_PORT' $Port }
  Set-Var 'TG_FAKE' '0'
  # Kompyuter rejimi: domen yo'q, https manzilni tunnel beradi (ilova uni o'zi aniqlaydi)
  Set-Var 'DOMAIN' ''
  Set-Var 'WWW_DOMAIN' ''
  Set-Var 'PUBLIC_URL' ''
  Set-Var 'COOKIE_SECURE' '0'
  Set-Var 'COMPOSE_PROFILES' 'tunnel'
  Set-Var 'TUNNEL_METRICS' 'http://tunnel:2000'
}

# ---------------------------------------------------------------- Docker
function Test-Engine { return (Quiet 'docker info') }

function Install-Docker {
  Say 'Docker Desktop o''rnatilmoqda (10-20 daqiqa, internet tezligiga qarab)'
  Note 'Windows ruxsat so''rasa - "Ha" (Yes) ni bosing.'
  $ok = $false
  if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements --override 'install --quiet --accept-license'
    $ok = Test-Path -LiteralPath $DockerExe
  }
  if (-not $ok) {
    $installer = Join-Path $env:TEMP 'DockerDesktopInstaller.exe'
    curl.exe -fL --retry 3 -o $installer 'https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe'
    if ($LASTEXITCODE -eq 0) {
      Start-Process -FilePath $installer -ArgumentList 'install', '--quiet', '--accept-license' -Verb RunAs -Wait
      $ok = Test-Path -LiteralPath $DockerExe
    }
  }
  if (-not $ok) { Stop-With 'Docker o''rnatilmadi. Shu oynani rasmga olib yuboring.' }
  Write-Host ''
  Write-Host 'Docker o''rnatildi.' -ForegroundColor Green
  Write-Host 'Endi kompyuterni QAYTA YOQING (Restart), so''ng KOMPYUTER_SERVER.bat ni yana ishga tushiring.' -ForegroundColor Yellow
  exit 0
}

function Ensure-Docker {
  if ((Test-Path -LiteralPath $DockerBin) -and ($env:Path -notlike "*$DockerBin*")) { $env:Path = "$DockerBin;$env:Path" }
  if (-not (Get-Command docker -ErrorAction SilentlyContinue) -and -not (Test-Path -LiteralPath $DockerExe)) { Install-Docker }
  if (Test-Engine) { return }
  Say 'Docker Desktop ishga tushirilmoqda (1-3 daqiqa)'
  if (Test-Path -LiteralPath $DockerExe) { Start-Process -FilePath $DockerExe }
  for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 4
    if (Test-Engine) { return }
  }
  Stop-With ('Docker ishga tushmadi. "Docker Desktop" dasturini oching, chiqqan savollarga javob bering ' +
    '(Accept / Skip), pastki chap burchakda "Engine running" yozuvi chiqqach shu faylni qayta ishga tushiring. ' +
    'Xato chiqsa - rasmga olib yuboring.')
}

# ---------------------------------------------------------------- dastur
function Get-Code {
  Say 'Dastur yuklab olinmoqda'
  $tmp = Join-Path $env:TEMP 'telegramcrm-src'
  if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
  New-Item -ItemType Directory -Path $tmp | Out-Null
  $zip = Join-Path $tmp 'src.zip'
  curl.exe -fsSL --retry 3 -o $zip "https://codeload.github.com/$Repo/zip/refs/heads/$Branch"
  if ($LASTEXITCODE -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $Dir 'docker-compose.yml')) {
      Note 'Internetdan yuklab bo''lmadi - kompyuterdagi avvalgi nusxa bilan davom etamiz.'
      return
    }
    Stop-With 'Dasturni yuklab bo''lmadi. Internetni tekshirib, qayta urinib ko''ring.'
  }
  Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
  $root = Get-ChildItem -LiteralPath $tmp -Directory | Select-Object -First 1
  $src = Join-Path $root.FullName 'TelegramCRM'
  if (-not (Test-Path -LiteralPath (Join-Path $src 'docker-compose.yml'))) { Stop-With 'Yuklangan arxiv buzilgan. Qayta urinib ko''ring.' }
  New-Item -ItemType Directory -Path $Dir -Force | Out-Null
  robocopy $src $Dir /E /XF .env /NFL /NDL /NJH /NJS /NP | Out-Null
  if ($LASTEXITCODE -ge 8) { Stop-With "Fayllarni ko'chirib bo'lmadi: $Dir" }
  Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
  Note "Papka: $Dir"
}

function Wait-Health {
  for ($i = 0; $i -lt 120; $i++) {
    try {
      $resp = Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 -Uri "http://127.0.0.1:$(Get-Var 'APP_PORT')/api/health"
      if ($resp.StatusCode -eq 200) { return $true }
    } catch { }
    Start-Sleep -Seconds 2
  }
  return $false
}

function Find-TunnelUrl([string]$logs) {
  $found = [regex]::Matches($logs, 'https://[a-z0-9-]+\.trycloudflare\.com')
  if ($found.Count -eq 0) { return '' }
  return $found[$found.Count - 1].Value
}

function Get-TunnelUrl {
  for ($i = 0; $i -lt 40; $i++) {
    $logs = (cmd /c 'docker compose logs --no-color tunnel 2>&1') -join "`n"
    $url = Find-TunnelUrl $logs
    if ($url) { return $url }
    Start-Sleep -Seconds 3
  }
  return ''
}

# Kompyuter qayta yonganda Docker (va sayt) o'zi ishga tushsin; rozetkada bo'lsa uxlab qolmasin
function Set-ServerMode {
  try {
    $startup = [Environment]::GetFolderPath('Startup')
    $link = Join-Path $startup 'Docker Desktop.lnk'
    if ((Test-Path -LiteralPath $DockerExe) -and -not (Test-Path -LiteralPath $link)) {
      $shell = New-Object -ComObject WScript.Shell
      $shortcut = $shell.CreateShortcut($link)
      $shortcut.TargetPath = $DockerExe
      $shortcut.Save()
    }
  } catch { }
  if ((Quiet 'powercfg /change standby-timeout-ac 0') -and (Quiet 'powercfg /change hibernate-timeout-ac 0')) {
    Note 'Kompyuter rozetkaga ulangan paytda uxlab qolmaydigan qilindi (aks holda sayt ham to''xtaydi).'
  }
  try {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $port = Get-Var 'APP_PORT'
    [IO.File]::WriteAllText((Join-Path $desktop 'CRM.url'), "[InternetShortcut]`r`nURL=http://localhost:$port/`r`n")
  } catch { }
}

function Main {
  Write-Host ''
  Write-Host ' TelegramCRM - shu kompyuterni server qilish' -ForegroundColor White
  Ensure-Docker
  Get-Code
  Set-Location -LiteralPath $Dir
  [Environment]::CurrentDirectory = $Dir
  if (-not (Test-Path -LiteralPath $script:EnvPath)) { Copy-Item -LiteralPath (Join-Path $Dir '.env.example') -Destination $script:EnvPath }
  Load-Env
  Import-DesktopEnv
  Ask-Missing
  Set-Defaults
  Save-Env

  Say 'Sayt ishga tushirilmoqda (birinchi marta 5-10 daqiqa)'
  docker compose up -d --build
  if ($LASTEXITCODE -ne 0) { Stop-With 'Docker xato berdi. Shu oynani rasmga olib yuboring.' }
  if (-not (Wait-Health)) { Stop-With 'Sayt ishga tushmadi. Shu oynani rasmga olib yuboring.' }
  if ($script:ResetAdmin) {
    docker compose exec -T app python -m app.cli reset-password (Get-Var 'ADMIN_LOGIN') (Get-Var 'ADMIN_PASSWORD') | Out-Null
  }

  Say 'Internet manzili olinmoqda (Cloudflare)'
  $url = Get-TunnelUrl
  Set-ServerMode

  Say 'Tayyor'
  if ($url) {
    Write-Host "Sayt (hamma joydan):  $url" -ForegroundColor Green
  } else {
    Write-Host 'Internet manzili hali tayyor emas - 1-2 daqiqada botingizga keladi.' -ForegroundColor Yellow
  }
  Write-Host "Shu kompyuterda:      http://localhost:$(Get-Var 'APP_PORT')  (ish stolida CRM yorlig'i)"
  Write-Host "Login:  $(Get-Var 'ADMIN_LOGIN')"
  if ($script:NewPassword) { Write-Host "Parol:  $($script:NewPassword)   <- yozib oling!" -ForegroundColor Yellow }
  else { Write-Host "Parol:  avvalgisi ($script:EnvPath faylida, ADMIN_PASSWORD)" }
  Write-Host ''
  Write-Host 'Muhim:'
  Write-Host ' - Kompyuter qayta yoqilganda internet manzili o''zgaradi. Yangi manzilni bot o''zi yuboradi,'
  Write-Host '   botdagi "CRM" tugmasi ham o''zi yangilanadi. Buning uchun botga bir marta /start yozing.'
  Write-Host ' - Kompyuter o''chsa yoki internet uzilsa, sayt ham ishlamaydi. Qayta yoqilganda Windows''ga kiring -'
  Write-Host '   Docker va sayt o''zi ishga tushadi.'
  if (-not (Get-Var 'TG_API_ID')) {
    Write-Host ' - Telegram akkauntlarni ulash uchun my.telegram.org dan API ID va HASH oling va shu faylni qayta ishga tushiring.'
  }
  if ($url) { Start-Process $url }
  exit 0
}

if ($env:CRM_PS_TEST -ne '1') { Main }
