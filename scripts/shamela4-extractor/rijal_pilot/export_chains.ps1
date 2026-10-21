# Export the current system's chains (Transmissions) for the given books to UTF-8 JSON.
# -Books holds substrings of Hadiths.BookName (default: al-Bukhari and al-Mustadrak).
# PostgreSQL through psql (since the move off SQL Server; the SQL Server version is at tag pre-postgres).
# The password comes from PGPASSWORD or pgpass.conf, e.g.
#   $env:PGPASSWORD = '...'; .\export_chains.ps1 -Psql 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -Connection 'host=localhost port=5433 dbname=smart_hadith_tree user=sht'
param(
    [string]$Out = "$PSScriptRoot\current_chains.json",
    [string[]]$Books = @('البخاري', 'المستدرك'),
    [string]$Connection = 'host=localhost port=5432 dbname=smart_hadith_tree user=sht',
    [string]$Psql = 'psql'
)
$ErrorActionPreference = 'Stop'

# "h."BookName" LIKE '%..%' OR ..." (quotes doubled). The SQL goes to psql as a UTF-8 file: Arabic in a
# native command's arguments does not survive Windows' code pages.
$bookFilter = ($Books | ForEach-Object { "h.""BookName"" LIKE '%$($_.Replace("'", "''"))%'" }) -join ' OR '
$sql = @"
SELECT COALESCE(json_agg(json_build_object(
    'id', h."Id", 'book', h."BookName", 'number', h."HadithNumber", 'head', left(h."MatnArabic", 400),
    'links', COALESCE((
        SELECT json_agg(json_build_object(
            'step', t."StepOrder", 'term', t."TransmissionTerm",
            'student', st."FullName", 'studentItqan', st."ItqanId",
            'sheikh', sh."FullName", 'sheikhItqan', sh."ItqanId", 'sheikhGrade', sh."ItqanGrade") ORDER BY t."StepOrder")
        FROM "Transmissions" t
        JOIN "Narrators" st ON st."Id" = t."StudentId"
        JOIN "Narrators" sh ON sh."Id" = t."SheikhId"
        WHERE t."HadithId" = h."Id"), '[]'::json))), '[]'::json)
FROM "Hadiths" h
WHERE $bookFilter
"@
$sqlFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding $false))
    $env:PGCLIENTENCODING = 'UTF8'
    & $Psql -X -q -At -v ON_ERROR_STOP=1 -d $Connection -f $sqlFile -o $Out
    if ($LASTEXITCODE -ne 0) { throw "psql failed ($LASTEXITCODE)" }
}
finally { Remove-Item $sqlFile -ErrorAction SilentlyContinue }

$result = [System.IO.File]::ReadAllText($Out, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$links = ($result | ForEach-Object { $_.links.Count } | Measure-Object -Sum).Sum
"hadiths: $($result.Count)  links: $links  -> $Out"
