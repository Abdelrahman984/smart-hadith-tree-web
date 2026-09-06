$ErrorActionPreference = "Stop"

$repoBaseUrl = "https://raw.githubusercontent.com/R3GENESI5/Itqan/master"
$dataDir = Join-Path $PSScriptRoot "..\data\itqan"

Write-Host "Creating data directories..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir "rijal") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir "sunni\bukhari") | Out-Null

$filesToDownload = @(
    "app/data/rijal/profiles_companion.json",
    "app/data/rijal/profiles_reliable.json",
    "app/data/rijal/profiles_mostly_reliable.json",
    "app/data/rijal/profiles_weak.json",
    "app/data/rijal/profiles_abandoned.json",
    "app/data/rijal/profiles_fabricator.json",
    "app/data/rijal/profiles_unknown.json",
    "app/data/rijal/by_name.json",
    "src/isnad_kunya_map.json",
    "src/isnad_father_map.json",
    "src/isnad_grandfather_map.json",
    "src/isnad_mother_map.json",
    "src/isnad_uncle_map.json"
)

foreach ($file in $filesToDownload) {
    $url = "$repoBaseUrl/$file"
    $fileName = Split-Path $file -Leaf
    $dest = ""
    if ($file -match "rijal") {
        $dest = Join-Path $dataDir "rijal\$fileName"
    } else {
        $dest = Join-Path $dataDir $fileName
    }
    
    Write-Host "Downloading $fileName..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest
    } catch {
        Write-Warning "Failed to download $fileName"
    }
}

Write-Host "Downloading Bukhari chapters (1-97)..."
for ($i = 1; $i -le 97; $i++) {
    $url = "$repoBaseUrl/app/data/sunni/bukhari/$i.json"
    $dest = Join-Path $dataDir "sunni\bukhari\$i.json"
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -ErrorAction Stop
    } catch {
        # File might not exist, silently ignore
    }
}

Write-Host "Downloading Muslim chapters (1-56)..."
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir "sunni\muslim") | Out-Null
for ($i = 1; $i -le 56; $i++) {
    $url = "$repoBaseUrl/app/data/sunni/muslim/$i.json"
    $dest = Join-Path $dataDir "sunni\muslim\$i.json"
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -ErrorAction Stop
    } catch {
        # File might not exist, silently ignore
    }
}

Write-Host "Downloading Abu Dawud chapters (1-43)..."
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir "sunni\abudawud") | Out-Null
for ($i = 1; $i -le 43; $i++) {
    $url = "$repoBaseUrl/app/data/sunni/abudawud/$i.json"
    $dest = Join-Path $dataDir "sunni\abudawud\$i.json"
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -ErrorAction Stop
    } catch {
        # File might not exist, silently ignore
    }
}

Write-Host "Downloading Tirmidhi chapters (1-50)..."
New-Item -ItemType Directory -Force -Path (Join-Path $dataDir "sunni\tirmidhi") | Out-Null
for ($i = 1; $i -le 50; $i++) {
    $url = "$repoBaseUrl/app/data/sunni/tirmidhi/$i.json"
    $dest = Join-Path $dataDir "sunni\tirmidhi\$i.json"
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -ErrorAction Stop
    } catch {
        # File might not exist, silently ignore
    }
}

Write-Host "Download complete!" -ForegroundColor Green
