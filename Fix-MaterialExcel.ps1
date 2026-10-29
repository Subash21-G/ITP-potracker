
$ErrorActionPreference = "Stop"

$controller = "Controllers\Api\MaterialsApiController.cs"
$dto = "Contracts\Api\MaterialListItemDto.cs"

if (!(Test-Path $controller) -or !(Test-Path $dto)) {
    throw "Project files not found. Check current folder."
}

# Read original files
$c = [IO.File]::ReadAllText((Resolve-Path $controller))
$d = [IO.File]::ReadAllText((Resolve-Path $dto))

function Replace-ExactOnce {
    param([string]$Text, [string]$Old, [string]$New)

    $count = ([regex]::Matches($Text, [regex]::Escape($Old))).Count
    if ($count -ne 1) {
        throw "Expected one occurrence, found $count : $Old"
    }

    return $Text.Replace($Old, $New)
}

# Add DTO property
$d = Replace-ExactOnce $d `
    'public string Description { get; init; } = string.Empty;' `
    ('public string Description { get; init; } = string.Empty;' +
    "`r`n        public string FullDescription { get; init; } = string.Empty;")

# Map original database description
$projectionPattern = '(?m)^(\s*)Description\s*=\s*\r?\n\s*material\.ShortDescription'
if (([regex]::Matches($c, $projectionPattern)).Count -ne 1) {
    throw "Material description projection not found."
}
$c = [regex]::Replace(
    $c,
    $projectionPattern,
    '${1}FullDescription = material.BriefDescription ?? string.Empty,' +
    "`r`n" + '${1}Description =' + "`r`n" + '${1}    material.ShortDescription',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
)

# Preserve description when grouping duplicate materials
$c = Replace-ExactOnce $c `
    'Description = first.Description, Quantity =' `
    'Description = first.Description, FullDescription = first.FullDescription, Quantity ='

# Add export column at the end (U)
$c = Replace-ExactOnce $c `
    '"Stock Status",' + "`r`n" + '                "Drawing Number"' `
    ('"Stock Status",' + "`r`n" +
    '                "Drawing Number",' + "`r`n" +
    '                "Full Description"')

# Write new Excel cell
$pattern = '(worksheet\.Cell\(row,\s*20\)\.Value\s*=\s*item\.DrawingNo;)'
if (([regex]::Matches($c, $pattern)).Count -ne 1) {
    throw "Excel Drawing Number cell not found."
}
$c = [regex]::Replace(
    $c,
    $pattern,
    '${1}' + "`r`n" +
    '                worksheet.Cell(row, 21).Value = item.FullDescription;'
)

# Extend existing title and header formatting
foreach ($range in @("A1:T1","A2:T2","A3:T3","A2:T3","A5:T5")) {
    $c = $c.Replace('"' + $range + '"', '"' + $range.Replace(':T', ':U') + '"')
}

# Format full-description column
$oldCall = @'
            WriteMaterialExcel(
                worksheet,
                items);
'@
$newCall = @'
            WriteMaterialExcel(
                worksheet,
                items);

            worksheet.Column(21).Width = 65;
            if (items.Count > 0)
            {
                worksheet.Range(6, 21, items.Count + 5, 21)
                    .Style.Alignment.WrapText = true;
            }
'@
$pattern = 'WriteMaterialExcel\(\s*worksheet,\s*items\s*\);'

if (([regex]::Matches($c, $pattern)).Count -ne 1) {
    throw "WriteMaterialExcel call not found or duplicated."
}

$c = [regex]::Replace(
    $c,
    $pattern,
    [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        $m.Value + "`r`n" +
        '            worksheet.Column(21).Width = 65;' + "`r`n" +
        '            if (items.Count > 0)' + "`r`n" +
        '            {' + "`r`n" +
        '                worksheet.Range(6, 21, items.Count + 5, 21)' + "`r`n" +
        '                    .Style.Alignment.WrapText = true;' + "`r`n" +
        '            }'
    }
)

# Save only after all checks pass
$backup = "MaterialExcelBackup_" + (Get-Date -Format "yyyyMMdd_HHmmss")
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $controller $backup
Copy-Item $dto $backup

$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Resolve-Path $controller), $c, $utf8)
[IO.File]::WriteAllText((Resolve-Path $dto), $d, $utf8)

Write-Host "Files updated successfully" -ForegroundColor Green
Write-Host "Backup: $backup" -ForegroundColor Yellow

dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "BUILD PASSED" -ForegroundColor Green
} else {
    Write-Host "BUILD FAILED - Check errors above" -ForegroundColor Red
}
