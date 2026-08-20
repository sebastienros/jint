#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Regenerates Jint/WebApi/Encoding/EncodingTables.Data.cs from the Encoding Standard's own data files.

.DESCRIPTION
  Reads the two files vendored next to this script - indexes.json (the index tables) and encodings.json
  (the names-and-labels table) - and emits one C# file carrying both: the label table TextDecoder resolves
  a label through, and the ushort[128] index table each legacy single-byte encoding decodes through.

  The build never runs this script. Its output is committed, and regenerating is a manual step taken when
  the vendored data is refreshed; README.md next to this script describes that refresh, and the generated
  file's header records which commit the data came from and the SHA-256 of the exact bytes it was generated
  from.

  Validation is deliberately loud: every shape the emitted C# relies on - 128 entries per index, no code
  point outside the BMP, no surrogates, U+0000 free to mean "unmapped", unique ASCII-lowercase labels - is
  asserted here rather than assumed, because a silent slip would become a wrong character in someone's
  decoded text.

.PARAMETER OutputPath
  Where to write the generated file. Defaults to Jint/WebApi/Encoding/EncodingTables.Data.cs.

.EXAMPLE
  pwsh tools/whatwg-encoding/generate-encoding-tables.ps1
#>
[CmdletBinding()]
param(
  [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The whatwg/encoding commit indexes.json and encodings.json were vendored from. Update it in the same
# change that replaces those files, never on its own.
$sourceCommit = 'a985b62a9b45c17da3e17a9f0a0b4e30c34c4a8a'

$indexesPath = Join-Path $PSScriptRoot 'indexes.json'
$encodingsPath = Join-Path $PSScriptRoot 'encodings.json'

if (-not $OutputPath) {
  $OutputPath = Join-Path $PSScriptRoot '..\..\Jint\WebApi\Encoding\EncodingTables.Data.cs'
}

$indexes = Get-Content -LiteralPath $indexesPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
$encodings = Get-Content -LiteralPath $encodingsPath -Raw -Encoding utf8 | ConvertFrom-Json

$indexesHash = (Get-FileHash -LiteralPath $indexesPath -Algorithm SHA256).Hash.ToLowerInvariant()
$encodingsHash = (Get-FileHash -LiteralPath $encodingsPath -Algorithm SHA256).Hash.ToLowerInvariant()

# ISO-8859-8 and ISO-8859-8-I are two encodings sharing one index; every other single-byte encoding has an
# index of its own, named after it.
$sharedIndexes = @{ 'iso-8859-8-i' = 'iso-8859-8' }

function Get-Identifier([string] $name) {
  $parts = $name -split '[-_.]' | Where-Object { $_.Length -gt 0 }
  ($parts | ForEach-Object { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) }) -join ''
}

function Get-Kind([string] $name, [string] $heading) {
  switch ($name) {
    'UTF-8' { return 'Utf8' }
    'UTF-16LE' { return 'Utf16Le' }
    'UTF-16BE' { return 'Utf16Be' }
    'replacement' { return 'Replacement' }
    'x-user-defined' { return 'XUserDefined' }
  }

  if ($heading -eq 'Legacy single-byte encodings') { return 'SingleByte' }
  return 'Unsupported'
}

# ---------------------------------------------------------------------------------------------------
# Read the encodings, validating as we go.
# ---------------------------------------------------------------------------------------------------

$allLabels = @{}
$entries = [System.Collections.Generic.List[object]]::new()
$indexOrder = [System.Collections.Generic.List[string]]::new()
$maxLabelLength = 0

foreach ($group in $encodings) {
  foreach ($encoding in $group.encodings) {
    $name = $encoding.name
    $lowerName = $name.ToLowerInvariant()
    $kind = Get-Kind $name $group.heading

    foreach ($label in $encoding.labels) {
      if ($label -ne $label.ToLowerInvariant()) { throw "Label '$label' of $name is not ASCII-lowercase." }
      if ($label -notmatch '^[\x20-\x7e]+$') { throw "Label '$label' of $name is not printable ASCII." }
      if ($allLabels.ContainsKey($label)) { throw "Label '$label' names both $($allLabels[$label]) and $name." }
      $allLabels[$label] = $name
      if ($label.Length -gt $maxLabelLength) { $maxLabelLength = $label.Length }
    }

    # The specification notes that ASCII-lowercasing an encoding's name yields one of its labels, which is
    # what lets the generated table return the name for free.
    if ($encoding.labels -notcontains $lowerName) { throw "The lowercased name '$lowerName' is not one of $name's labels." }

    $indexName = $null
    if ($kind -eq 'SingleByte') {
      $indexName = if ($sharedIndexes.ContainsKey($lowerName)) { $sharedIndexes[$lowerName] } else { $lowerName }
      if (-not $indexes.ContainsKey($indexName)) { throw "No index named '$indexName' for single-byte encoding $name." }
      if (-not $indexOrder.Contains($indexName)) { [void] $indexOrder.Add($indexName) }
    }

    [void] $entries.Add([pscustomobject] @{
        Name      = $lowerName
        Kind      = $kind
        Labels    = @($encoding.labels)
        IndexName = $indexName
      })
  }
}

# ---------------------------------------------------------------------------------------------------
# Read the index tables, validating every entry.
# ---------------------------------------------------------------------------------------------------

$tables = [ordered] @{}
foreach ($indexName in $indexOrder) {
  $values = $indexes[$indexName]
  if ($values.Count -ne 128) { throw "Index '$indexName' has $($values.Count) entries; a single-byte index has 128." }

  $table = New-Object 'int[]' 128
  for ($i = 0; $i -lt 128; $i++) {
    $value = $values[$i]
    if ($null -eq $value) {
      # U+0000 stands for the specification's null. No index maps a byte to it, which is asserted below.
      $table[$i] = 0
      continue
    }

    if ($value -le 0 -or $value -gt 0xFFFF) { throw "Index '$indexName' maps byte 0x$('{0:X2}' -f ($i + 0x80)) to U+$('{0:X4}' -f $value), which the ushort tables cannot hold." }
    if ($value -ge 0xD800 -and $value -le 0xDFFF) { throw "Index '$indexName' maps byte 0x$('{0:X2}' -f ($i + 0x80)) to the surrogate U+$('{0:X4}' -f $value)." }
    $table[$i] = $value
  }

  $tables[$indexName] = $table
}

# ---------------------------------------------------------------------------------------------------
# Emit.
# ---------------------------------------------------------------------------------------------------

$sb = [System.Text.StringBuilder]::new()
[void] $sb.AppendLine('// <auto-generated>')
[void] $sb.AppendLine('//     Generated by tools/whatwg-encoding/generate-encoding-tables.ps1. Do not edit by hand:')
[void] $sb.AppendLine('//     change the script or the data it reads and run it again.')
[void] $sb.AppendLine('//')
[void] $sb.AppendLine('//     Source: indexes.json and encodings.json from https://github.com/whatwg/encoding at commit')
[void] $sb.AppendLine("//     $sourceCommit, vendored verbatim under tools/whatwg-encoding/.")
[void] $sb.AppendLine('//')
[void] $sb.AppendLine("//         sha256(indexes.json)   = $indexesHash")
[void] $sb.AppendLine("//         sha256(encodings.json) = $encodingsHash")
[void] $sb.AppendLine('//')
[void] $sb.AppendLine('//     The build never runs the generator. Regeneration is manual, and tools/whatwg-encoding/README.md')
[void] $sb.AppendLine('//     describes it.')
[void] $sb.AppendLine('//')
[void] $sb.AppendLine('//     Copyright (c) WHATWG (Apple, Google, Mozilla, Microsoft). The Encoding Standard is licensed under')
[void] $sb.AppendLine('//     Creative Commons Attribution 4.0 International, and, in its own words, "to the extent portions of')
[void] $sb.AppendLine('//     it are incorporated into source code, such portions in the source code are licensed under the BSD')
[void] $sb.AppendLine('//     3-Clause License instead" - which is what the tables below are. The full text is vendored as')
[void] $sb.AppendLine('//     tools/whatwg-encoding/whatwg-encoding-LICENSE.txt.')
[void] $sb.AppendLine('// </auto-generated>')
[void] $sb.AppendLine('#if NET8_0_OR_GREATER')
[void] $sb.AppendLine('using Jint.Runtime;')
[void] $sb.AppendLine()
[void] $sb.AppendLine('namespace Jint.WebApi.Encoding;')
[void] $sb.AppendLine()
[void] $sb.AppendLine('/// <summary>')
[void] $sb.AppendLine('/// The Encoding Standard''s tables: the labels that name each encoding')
[void] $sb.AppendLine('/// (https://encoding.spec.whatwg.org/#names-and-labels) and the index each legacy single-byte encoding')
[void] $sb.AppendLine('/// decodes through (https://encoding.spec.whatwg.org/#legacy-single-byte-encodings).')
[void] $sb.AppendLine('/// </summary>')
[void] $sb.AppendLine('/// <remarks>')
[void] $sb.AppendLine('/// <para>')
[void] $sb.AppendLine('/// An index is 128 entries long and covers the bytes 0x80 to 0xFF; the byte''s own value is its code point')
[void] $sb.AppendLine('/// below that. U+0000 stands for the specification''s null, meaning the byte is not mapped at all - no index')
[void] $sb.AppendLine('/// maps a byte to U+0000, which the generator asserts, so the sentinel costs no entry.')
[void] $sb.AppendLine('/// </para>')
[void] $sb.AppendLine('/// <para>')
[void] $sb.AppendLine('/// The tables are <see cref="ReadOnlySpan{T}"/>-valued properties rather than arrays so that the compiler')
[void] $sb.AppendLine('/// can place them in the assembly''s data section: reaching one allocates nothing and initializes nothing,')
[void] $sb.AppendLine('/// however many decoders are built.')
[void] $sb.AppendLine('/// </para>')
[void] $sb.AppendLine('/// </remarks>')
[void] $sb.AppendLine('internal static class EncodingTables')
[void] $sb.AppendLine('{')

$longestLabel = ($allLabels.Keys | Sort-Object -Property Length -Descending | Select-Object -First 1)
[void] $sb.AppendLine("    /// <summary>The length of `"$longestLabel`", the longest label in the table.</summary>")
[void] $sb.AppendLine("    internal const int MaxLabelLength = $maxLabelLength;")
[void] $sb.AppendLine()

# ---- the label table -------------------------------------------------------------------------------

[void] $sb.AppendLine('    /// <summary>')
[void] $sb.AppendLine('    /// The label table of https://encoding.spec.whatwg.org/#names-and-labels. The label must already have')
[void] $sb.AppendLine('    /// been trimmed and ASCII-lowercased, which is what <see cref="EncodingLabels.TryLookup"/> does.')
[void] $sb.AppendLine('    /// </summary>')
[void] $sb.AppendLine('    internal static bool TryMatch(ReadOnlySpan<char> label, out EncodingEntry entry)')
[void] $sb.AppendLine('    {')
[void] $sb.AppendLine('        switch (label)')
[void] $sb.AppendLine('        {')

$first = $true
foreach ($entry in $entries) {
  if (-not $first) { [void] $sb.AppendLine() }
  $first = $false

  foreach ($label in ($entry.Labels | Sort-Object -CaseSensitive)) {
    [void] $sb.AppendLine("            case `"$label`":")
  }

  $index = if ($entry.IndexName) { Get-Identifier $entry.IndexName } else { 'None' }
  [void] $sb.AppendLine("                entry = new EncodingEntry(`"$($entry.Name)`", EncodingKind.$($entry.Kind), SingleByteIndex.$index);")
  [void] $sb.AppendLine('                return true;')
}

[void] $sb.AppendLine()
[void] $sb.AppendLine('            default:')
[void] $sb.AppendLine('                entry = default;')
[void] $sb.AppendLine('                return false;')
[void] $sb.AppendLine('        }')
[void] $sb.AppendLine('    }')
[void] $sb.AppendLine()

# ---- the index lookup ------------------------------------------------------------------------------

[void] $sb.AppendLine('    /// <summary>')
[void] $sb.AppendLine('    /// "Index single-byte" for one encoding, https://encoding.spec.whatwg.org/#index-single-byte.')
[void] $sb.AppendLine('    /// </summary>')
[void] $sb.AppendLine('    internal static ReadOnlySpan<ushort> IndexFor(SingleByteIndex index)')
[void] $sb.AppendLine('    {')
[void] $sb.AppendLine('        switch (index)')
[void] $sb.AppendLine('        {')
foreach ($indexName in $indexOrder) {
  $identifier = Get-Identifier $indexName
  [void] $sb.AppendLine("            case SingleByteIndex.${identifier}: return $identifier;")
}
[void] $sb.AppendLine('        }')
[void] $sb.AppendLine()
[void] $sb.AppendLine('        Throw.ArgumentOutOfRangeException(nameof(index), "There is no single-byte index for this encoding.");')
[void] $sb.AppendLine('        return default;')
[void] $sb.AppendLine('    }')

# ---- the tables ------------------------------------------------------------------------------------

foreach ($indexName in $indexOrder) {
  $identifier = Get-Identifier $indexName
  $table = $tables[$indexName]

  [void] $sb.AppendLine()
  [void] $sb.AppendLine("    /// <summary>https://encoding.spec.whatwg.org/index-$indexName.txt</summary>")
  [void] $sb.AppendLine("    private static ReadOnlySpan<ushort> ${identifier} =>")
  [void] $sb.AppendLine('    [')
  for ($row = 0; $row -lt 16; $row++) {
    $values = @()
    for ($column = 0; $column -lt 8; $column++) {
      $values += '0x{0:X4}' -f $table[($row * 8) + $column]
    }

    [void] $sb.AppendLine('        ' + ($values -join ', ') + ',')
  }

  [void] $sb.AppendLine('    ];')
}

[void] $sb.AppendLine('}')
[void] $sb.AppendLine()

# ---- the index enumeration -------------------------------------------------------------------------

[void] $sb.AppendLine('/// <summary>')
[void] $sb.AppendLine('/// Which "index single-byte" a single-byte encoding decodes through. All but two encodings have one of')
[void] $sb.AppendLine('/// their own: ISO-8859-8 and ISO-8859-8-I share this one index while staying two encodings, because only')
[void] $sb.AppendLine('/// ISO-8859-8 carries a layout direction with it.')
[void] $sb.AppendLine('/// </summary>')
[void] $sb.AppendLine('internal enum SingleByteIndex')
[void] $sb.AppendLine('{')
[void] $sb.AppendLine('    /// <summary>The encoding is not a single-byte one and has no index.</summary>')
[void] $sb.AppendLine('    None,')
foreach ($indexName in $indexOrder) {
  [void] $sb.AppendLine()
  [void] $sb.AppendLine("    /// <summary>index-$indexName.txt</summary>")
  [void] $sb.AppendLine("    $(Get-Identifier $indexName),")
}
[void] $sb.AppendLine('}')
[void] $sb.AppendLine('#endif')

$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) { throw "The output directory '$directory' does not exist." }

[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $OutputPath"
Write-Host "  $($entries.Count) encodings, $($allLabels.Count) labels, $($indexOrder.Count) single-byte indexes"
Write-Host "  longest label '$longestLabel' ($maxLabelLength characters)"
