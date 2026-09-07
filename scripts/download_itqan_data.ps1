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

$sunniBooks = @(
    "bukhari", "muslim", "abudawud", "tirmidhi", "nasai", "ibnmajah",
    "ahmed", "malik", "darimi", "nawawi40", "qudsi40", "shahwaliullah40",
    "riyad_assalihin", "aladab_almufrad", "bulugh_almaram", "mishkat_almasabih",
    "shamail_muhammadiyah", "musannaf_ibnabi_shaybah"
)

# If git is available, use fast sparse checkout
$hasGit = (Get-Command git -ErrorAction SilentlyContinue) -ne $null

if ($hasGit) {
    Write-Host "Downloading all 18 Sunni Hadith books using git sparse-checkout..." -ForegroundColor Cyan
    $tempGitDir = Join-Path ([System.IO.Path]::GetTempPath()) ("itqan_sparse_" + [System.Guid]::NewGuid().ToString("N"))
    try {
        git clone --depth 1 --filter=blob:none --sparse "https://github.com/R3GENESI5/Itqan.git" $tempGitDir
        git -C $tempGitDir sparse-checkout set app/data/sunni
        
        $sparseSunni = Join-Path $tempGitDir "app\data\sunni"
        if (Test-Path $sparseSunni) {
            $destSunni = Join-Path $dataDir "sunni"
            New-Item -ItemType Directory -Force -Path $destSunni | Out-Null
            Copy-Item -Path "$sparseSunni\*" -Destination $destSunni -Recurse -Force
            Write-Host "All 18 Hadith books synced successfully via git!" -ForegroundColor Green
        }
    } finally {
        if (Test-Path $tempGitDir) {
            Remove-Item -Path $tempGitDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Host "Git not found. Falling back to HTTP download for all books..." -ForegroundColor Yellow
    foreach ($book in $sunniBooks) {
        $bookDir = Join-Path $dataDir "sunni\$book"
        New-Item -ItemType Directory -Force -Path $bookDir | Out-Null
        
        $idxUrl = "$repoBaseUrl/app/data/sunni/$book/index.json"
        $idxDest = Join-Path $bookDir "index.json"
        try {
            Invoke-WebRequest -Uri $idxUrl -OutFile $idxDest -ErrorAction Stop
            $index = Get-Content $idxDest -Raw | ConvertFrom-Json
            Write-Host "Downloading $($book) ($($index.Count) chapters)..."
            foreach ($ch in $index) {
                $chFile = $ch.file
                $chUrl = "$repoBaseUrl/app/data/sunni/$book/$chFile"
                $chDest = Join-Path $bookDir $chFile
                Invoke-WebRequest -Uri $chUrl -OutFile $chDest -ErrorAction SilentlyContinue
            }
        } catch {
            Write-Warning "Could not fetch index for $book"
        }
    }
}

Write-Host "Download complete!" -ForegroundColor Green

