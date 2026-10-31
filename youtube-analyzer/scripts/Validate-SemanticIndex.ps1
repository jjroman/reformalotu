#requires -Version 7
<#
.SYNOPSIS
Valida los semantic-index.json de promptVersion 3 (task 0001).

.PARAMETER Path
Carpeta del corpus (contiene una subcarpeta por vídeo) o un semantic-index.json concreto.

.PARAMETER VideoId
Opcional. Limita la validación a estos ids de vídeo.

.EXAMPLE
.\scripts\Validate-SemanticIndex.ps1 -Path .\output -VideoId '--iD5mqyYFo','_3VHKFOT2Vw'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [string[]]$VideoId
)

$ErrorActionPreference = 'Stop'

$files = if (Test-Path -LiteralPath $Path -PathType Leaf) {
    Get-Item -LiteralPath $Path
}
else {
    Get-ChildItem -LiteralPath $Path -Directory |
        Where-Object { -not $VideoId -or $VideoId -contains $_.Name } |
        ForEach-Object { Join-Path $_.FullName 'semantic-index.json' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Get-Item
}

function Get-Prop($obj, [string]$name) {
    $p = $obj.PSObject.Properties | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
    if ($p) { $p.Value }
}

$totalErrors = 0
$validated = 0

foreach ($file in $files) {
    $errors = [System.Collections.Generic.List[string]]::new()
    $index = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    $inputs = Get-Prop $index 'Inputs'
    $tasks = @(Get-Prop $index 'Tasks')

    if ((Get-Prop $index 'PromptVersion') -ne 3) { $errors.Add('promptVersion distinto de 3') }

    $summary = [string](Get-Prop $index 'Summary')
    $words = ($summary -split '\s+' | Where-Object { $_ }).Count
    if ($words -lt 60 -or $words -gt 180) { $errors.Add("resumen con $words palabras (esperado 60-180)") }

    $ids = @{}
    $leads = 0
    $labor = @(Get-Prop $inputs 'Labor')
    foreach ($group in @(@('Materials', 'm'), @('Equipment', 'e'), @('Labor', 'l'))) {
        $items = @(Get-Prop $inputs $group[0])
        for ($i = 0; $i -lt $items.Count; $i++) {
            $item = $items[$i]
            $id = [string](Get-Prop $item 'Id')
            if ($id -ne "$($group[1])$($i + 1)") { $errors.Add("id '$id' no canónico en $($group[0])[$i]") }
            $ids[$id] = $true
            $conf = Get-Prop $item 'Confidence'
            if ($conf -lt 1 -or $conf -gt 10) { $errors.Add("confidence fuera de 1-10 en $id") }
            if ($group[1] -eq 'm') {
                $q = Get-Prop $item 'Quantity'
                if ($null -ne $q -and ($q -le 0 -or [string]::IsNullOrWhiteSpace((Get-Prop $item 'Unit')))) {
                    $errors.Add("cantidad inválida o sin unidad en $id")
                }
            }
            if ($group[1] -eq 'l' -and (Get-Prop $item 'Level') -eq 'lead') { $leads++ }
        }
    }
    if ($labor.Count -gt 0 -and $leads -ne 1) { $errors.Add("hay $leads responsables (esperado 1)") }
    if ($tasks.Count -gt 0 -and $labor.Count -eq 0) { $errors.Add('hay tareas pero ninguna mano de obra') }

    for ($i = 0; $i -lt $tasks.Count; $i++) {
        $t = $tasks[$i]
        if ((Get-Prop $t 'Order') -ne $i + 1) { $errors.Add("order incorrecto en tarea $($i + 1)") }
        if ([string]::IsNullOrWhiteSpace((Get-Prop $t 'Title'))) { $errors.Add("tarea $($i + 1) sin título") }
        $conf = Get-Prop $t 'Confidence'
        if ($conf -lt 1 -or $conf -gt 10) { $errors.Add("confidence fuera de 1-10 en tarea $($i + 1)") }
        foreach ($field in 'MaterialIds', 'EquipmentIds', 'LaborIds') {
            foreach ($ref in @(Get-Prop $t $field)) {
                if ($ref -and -not $ids.ContainsKey([string]$ref)) { $errors.Add("tarea $($i + 1) referencia id inexistente '$ref'") }
            }
        }
        $minutes = Get-Prop $t 'EstimatedMinutes'
        if ($null -ne $minutes -and $minutes -le 0) { $errors.Add("minutos no positivos en tarea $($i + 1)") }
    }

    $validated++
    if ($errors.Count -eq 0) {
        Write-Host "OK    $($file.Directory.Name) (tareas: $($tasks.Count), palabras: $words)"
    }
    else {
        $totalErrors += $errors.Count
        Write-Host "FALLO $($file.Directory.Name)" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host "      - $_" -ForegroundColor Red }
    }
}

Write-Host "Validados: $validated. Errores: $totalErrors."
if ($totalErrors -gt 0 -or $validated -eq 0) { exit 1 }
