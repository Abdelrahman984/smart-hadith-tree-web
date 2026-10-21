# Phase 6: export every hadith of a database with its chain, as JSON Lines (one file per book).
# Works for both the v2 database (narrators keyed by ItqanId) and the Shamela one (keyed by SourceKey).
# PostgreSQL through psql (since the move off SQL Server; the v2 and early Shamela databases exist only as SQL
# Server backups, and the SQL Server version of this script is at tag pre-postgres). Password from PGPASSWORD.
#   export_db_chains.ps1 -Database smart_hadith_tree -Out data\shamela_rijal\phase6\shamela -KeyBy SourceKey
#   export_db_chains.ps1 -Database smart_hadith_tree -Out out -Psql 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -Port 5433
# A line: {"id","book","number","head" (first 400 chars of MatnArabic),"links":[[step,sheikhKey,studentKey,term,sheikhName,studentName]]}
# Files are numbered in byte order of the book name (COLLATE "C"); SQL Server ordered them by its Arabic collation.
param(
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$Out,
    [ValidateSet('SourceKey', 'ItqanId')][string]$KeyBy = 'SourceKey',
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$User = 'sht',
    [string]$Psql = 'psql'
)
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force $Out | Out-Null
$key = if ($KeyBy -eq 'SourceKey') { { param($a) "$a.""SourceKey""" } } else { { param($a) "$a.""ItqanId""::text" } }
$shKey = & $key 'sh'
$stKey = & $key 'st'

# One output line per hadith: "<book index>`t<json>". The JSON is built by PostgreSQL.
$sql = @"
SELECT dense_rank() OVER (ORDER BY h."BookName" COLLATE "C")::text || E'\t' || json_build_object(
    'id', h."Id", 'book', h."BookName", 'number', h."HadithNumber", 'head', left(h."MatnArabic", 400),
    'links', COALESCE((
        SELECT json_agg(json_build_array(t."StepOrder", $shKey, $stKey, t."TransmissionTerm", sh."FullName", st."FullName")
                        ORDER BY t."StepOrder")
        FROM "Transmissions" t
        JOIN "Narrators" sh ON sh."Id" = t."SheikhId"
        JOIN "Narrators" st ON st."Id" = t."StudentId"
        WHERE t."HadithId" = h."Id"), '[]'::json))::text
FROM "Hadiths" h
ORDER BY h."BookName" COLLATE "C", h."Id"
"@
$sqlFile = [System.IO.Path]::GetTempFileName()
$rawFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding $false))
    $env:PGCLIENTENCODING = 'UTF8'
    & $Psql -X -q -At -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -f $sqlFile -o $rawFile
    if ($LASTEXITCODE -ne 0) { throw "psql failed ($LASTEXITCODE)" }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $reader = New-Object System.IO.StreamReader($rawFile, $utf8)
    $writer = $null; $curBook = $null; $count = 0; $books = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        $tab = $line.IndexOf("`t")
        $book = [int]$line.Substring(0, $tab)
        if ($book -ne $curBook) {
            if ($null -ne $writer) { $writer.Dispose() }
            $writer = New-Object System.IO.StreamWriter((Join-Path $Out ("{0:D2}.jsonl" -f $book)), $false, $utf8)
            $curBook = $book; $books++
        }
        $writer.WriteLine($line.Substring($tab + 1))
        $count++
    }
    if ($null -ne $writer) { $writer.Dispose() }
    $reader.Close()
}
finally { Remove-Item $sqlFile, $rawFile -ErrorAction SilentlyContinue }
"Exported $count hadiths of $books books from $Database to $Out"
