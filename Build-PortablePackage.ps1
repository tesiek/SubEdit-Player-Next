#requires -Version 5.1
<#
.SYNOPSIS
Creates a reviewed portable package from a verified Release output.
.DESCRIPTION
For final packaging, pass the reviewed public checkout as PublicSourceRoot and
its independently selected full commit ID as ExpectedPublicCommit. The commit
can be local and unpublished. Build Release from that clean checkout. In the
approved runtime manifest, record BuildProvenance with Commit and State="CLEAN"
based on the build audit (same Git HEAD and clean status before and after the
build, no build metadata overrides). Review these facts with the Files hashes.
Use the .NET 10 SDK to read assembly metadata when checking provenance.
Candidate and ValidateOnly also work without provenance, including from staging.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReleaseOutput,
    [Parameter(Mandatory=$true)][string]$VerifiedOutputManifest,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$ZipTimestamp,
    [switch]$ValidateOnly,
    [switch]$Candidate,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,31}$')][string]$CandidateLabel,
    [switch]$ApproveReleaseWithUnverifiedFFmpegSource,
    [string]$PublicSourceRoot,
    [string]$ExpectedPublicCommit
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Get-PackageZipName([string]$ProductVersion, [bool]$IsCandidate, [string]$Label) {
    $match = [regex]::Match($ProductVersion, '^(?<version>(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(?:\+[0-9A-Za-z.-]+)?$')
    if (-not $match.Success) { throw "Unexpected product version: $ProductVersion" }
    $version = $match.Groups['version'].Value
    if ($IsCandidate) {
        $suffix = if ($Label) { '-candidate-' + $Label } else { '-candidate' }
        return 'SubEdit-Player-Next-' + $version + '-portable-x64' + $suffix + '.zip'
    }
    return 'SubEdit-Player-Next-' + $version + '.zip'
}
function Get-PublicRevisionProblem {
    if (-not $PublicSourceRoot -or $ExpectedPublicCommit -notmatch '\A(?:[0-9a-f]{40}|[0-9a-f]{64})\z') {
        return 'Provide PublicSourceRoot and the full ExpectedPublicCommit of the reviewed public checkout.'
    }
    $attestation = $review.PSObject.Properties['BuildProvenance']
    if (-not $attestation -or $attestation.Value -isnot [pscustomobject]) {
        return 'The approved runtime manifest must attest a clean build of ExpectedPublicCommit.'
    }
    $commit = $attestation.Value.PSObject.Properties['Commit']
    $state = $attestation.Value.PSObject.Properties['State']
    if (-not $commit -or $commit.Value -isnot [string] -or $commit.Value -ne $ExpectedPublicCommit -or
        -not $state -or $state.Value -isnot [string] -or $state.Value -cne 'CLEAN') {
        return 'The approved runtime manifest must attest a clean build of ExpectedPublicCommit.'
    }

    $checkout = (Resolve-Path -LiteralPath $PublicSourceRoot).ProviderPath
    $gitArgs = @('--no-optional-locks', '-C', $checkout)
    $root = & git @gitArgs rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($root) -ne [IO.Path]::GetFullPath($checkout)) {
        return 'PublicSourceRoot must be the root of the reviewed Git checkout.'
    }
    $head = & git @gitArgs rev-parse --verify 'HEAD^{commit}' 2>$null
    if ($LASTEXITCODE -ne 0 -or $head -ne $ExpectedPublicCommit) {
        return 'The public checkout HEAD differs from ExpectedPublicCommit.'
    }
    $buildInfo = $head + ':src/SubEdit.Player.WinForms/ApplicationBuildInfo.targets'
    & git @gitArgs cat-file -e $buildInfo 2>$null
    if ($LASTEXITCODE -ne 0) { return 'PublicSourceRoot must use the public source layout.' }
    $status = & git @gitArgs status --porcelain=v1 --untracked-files=all --ignore-submodules=none 2>$null
    if ($LASTEXITCODE -ne 0 -or $status) {
        return 'The public checkout is dirty or its status could not be verified.'
    }

    $reader = Join-Path $sourceRoot 'Read-PortableBuildMetadata.cs'
    try { $metadataJson = & dotnet run --file $reader -- $applicationDll 2>$null }
    catch { $metadataJson = $null }
    if ($LASTEXITCODE -ne 0 -or -not $metadataJson) {
        return 'Could not read unambiguous assembly build metadata with the .NET 10 SDK.'
    }
    $metadata = $metadataJson | ConvertFrom-Json
    if ($metadata.SHA256 -ne $approvedHashes['SubEdit.Player.Next.dll'] -or
        $metadata.BuildCommit -ne $ExpectedPublicCommit -or $metadata.BuildDirtyState -cne 'CLEAN' -or
        $metadata.ProductVersion -notmatch '\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z' -or
        $metadata.InformationalVersion -ne "$($metadata.ProductVersion)+$ExpectedPublicCommit.clean" -or
        $metadata.PeProductVersion -cne $metadata.InformationalVersion -or
        $productVersion -cne $metadata.InformationalVersion) {
        return 'Assembly metadata does not match the approved DLL, clean revision and product version.'
    }
}

$sourceRoot=$PSScriptRoot
$runtime=[IO.Path]::GetFullPath($ReleaseOutput).TrimEnd('\')
$output=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
$date=[DateTime]::MinValue
if(-not [DateTime]::TryParseExact($ZipTimestamp,"yyyy-MM-dd'T'HH:mm:ss",[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::None,[ref]$date) -or
   $date.Year -lt 1981 -or $date.Year -gt 2107 -or $date.Second % 2 -ne 0){
    throw 'ZipTimestamp must be a fixed yyyy-MM-ddTHH:mm:ss value, year 1981-2107, with an even second (ZIP precision).'
}
# ZIP has no timezone field. Use these explicit UTC wall-clock components unchanged.
$entryTimestamp=[DateTimeOffset]::new($date.Year,$date.Month,$date.Day,$date.Hour,$date.Minute,$date.Second,[TimeSpan]::Zero)
if($CandidateLabel -and -not $Candidate){throw 'CandidateLabel requires Candidate.'}
if($Candidate -and $ApproveReleaseWithUnverifiedFFmpegSource){throw 'Owner release approval applies only to final packages; do not combine it with Candidate.'}
if($output -eq $runtime -or $output.StartsWith($runtime+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside the runtime input tree.'}
$noticePath=Join-Path $sourceRoot 'THIRD-PARTY-NOTICES.txt'
$notices=[IO.File]::ReadAllText($noticePath)
$sourceVerified=$notices -match '(?m)^FFMPEG SOURCE GATE: PASS\r?$'
$distributionReady=$notices -match '(?m)^PUBLIC DISTRIBUTION READY: true\r?$'
$review=Get-Content -LiteralPath $VerifiedOutputManifest -Raw | ConvertFrom-Json
if($review.Status -isnot [string] -or $review.Status -ne 'PASS'){throw 'Release output has not passed its audit.'}
if($review.Files -isnot [array] -or $review.Files.Count -eq 0){throw 'The runtime review must contain a nonempty Files array.'}
$applicationDll=Join-Path $runtime 'SubEdit.Player.Next.dll'
$productVersion=(Get-Item -LiteralPath $applicationDll).VersionInfo.ProductVersion
$zipName=Get-PackageZipName $productVersion $Candidate.IsPresent $CandidateLabel
$ffmpegComplianceReady=$sourceVerified -and $distributionReady
$ownerReleaseApproved=$ApproveReleaseWithUnverifiedFFmpegSource.IsPresent
if($ownerReleaseApproved){
    Write-Warning ('EXPLICIT OWNER RELEASE APPROVAL: packaging is authorized while FFmpeg source/distribution verification may remain incomplete. This does not confirm GPL compliance. FFmpegComplianceReady={0}.' -f $ffmpegComplianceReady)
}
if(Test-Path -LiteralPath $output){throw 'Use a new empty package directory; existing artifacts will not be overwritten.'}
$roots=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($name in @('SubEdit.Player.Next.exe','SubEdit.Player.Next.dll','SubEdit.Player.Next.deps.json','SubEdit.Player.Next.runtimeconfig.json')){[void]$roots.Add($name)}
$deps=Get-Content -LiteralPath (Join-Path $runtime 'SubEdit.Player.Next.deps.json') -Raw | ConvertFrom-Json
foreach($target in $deps.targets.PSObject.Properties){foreach($library in $target.Value.PSObject.Properties){
    $entry=$library.Value.PSObject.Properties['runtime']
    if($entry){foreach($asset in $entry.Value.PSObject.Properties){if($asset.Name.EndsWith('.dll')){[void]$roots.Add([IO.Path]::GetFileName($asset.Name))}}}
}}
$help=@('SubEditHelp.html','SkrotyKlawiszowe.html','faq_edycja.html')
$native=@('avcodec-63.dll','avfilter-12.dll','avformat-63.dll','avutil-61.dll','swresample-7.dll','swscale-10.dll')
$selected=[Collections.Generic.List[string]]::new()
$runtimeFiles=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($file in Get-ChildItem -LiteralPath $runtime -Recurse -File -Force){
    $relative=$file.FullName.Substring($runtime.Length+1).Replace('\','/')
    if($runtimeFiles.ContainsKey($relative)){throw "Ambiguous runtime path: $relative"}
    $runtimeFiles.Add($relative,$file.FullName)
    $allow=($relative -notmatch '/' -and $roots.Contains($relative)) -or
      ($relative -match '^FFmpeg/([^/]+)$' -and $native -contains $Matches[1]) -or
      ($relative -in @('runtimes/win-x64/native/Scintilla.dll','runtimes/win-x64/native/Lexilla.dll')) -or
      ($relative -match '^doc/([^/]+)$' -and $help -contains $Matches[1])
    if(-not $allow){continue}
    if($relative -match '(?i)(^|/)(src|tests|bin|obj|\.cache|\.git|images)(/|$)|\.pdb$|SubEditHelp-Editor\.html$'){throw "Forbidden package entry: $relative"}
    $selected.Add($relative)
}
foreach($required in @($roots)+@($native|ForEach-Object {'FFmpeg/'+$_})+@($help|ForEach-Object {'doc/'+$_})+@('runtimes/win-x64/native/Scintilla.dll','runtimes/win-x64/native/Lexilla.dll')){
    if(-not $selected.Contains($required)){throw "Missing required runtime entry: $required"}
}
# The input review can also cover files excluded from the ZIP. Verify any such entries,
# but require coverage only for selected runtime files, before adding packaging metadata.
$approvedHashes=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($item in $review.Files){
    if($item.Path -isnot [string] -or $item.SHA256 -isnot [string] -or $item.SHA256 -notmatch '\A[0-9a-fA-F]{64}\z'){
        throw 'Each runtime review entry must have a path and a valid SHA-256.'
    }
    # Match enumerated relative paths so aliases (such as dot segments or rooted paths) cannot approve a file.
    $relative=$item.Path.Replace('\','/')
    if(-not $runtimeFiles.ContainsKey($relative)){throw "Invalid or missing runtime manifest path: $($item.Path)"}
    if($approvedHashes.ContainsKey($relative)){throw "Duplicate runtime manifest path: $($item.Path)"}
    $approvedHashes.Add($relative,$item.SHA256)
}
foreach($relative in $selected){
    if(-not $approvedHashes.ContainsKey($relative)){throw "Runtime file is missing from the approved manifest: $relative"}
}
foreach($relative in $approvedHashes.Keys){
    if((Get-FileHash -LiteralPath $runtimeFiles[$relative] -Algorithm SHA256).Hash -ne $approvedHashes[$relative]){
        throw "Runtime output changed after review: $relative"
    }
}

try { $revisionProblem = Get-PublicRevisionProblem }
catch { $revisionProblem = "Could not verify public build provenance: $($_.Exception.Message)" }
$publicRevisionReady = -not $revisionProblem
$finalPackageAllowed = $publicRevisionReady -and ($ffmpegComplianceReady -or $ownerReleaseApproved)
if (-not $Candidate -and -not $ValidateOnly) {
    if (-not $publicRevisionReady) { throw "Final package blocked: $revisionProblem" }
    if (-not $finalPackageAllowed) {
        throw 'Final package blocked: verified FFmpeg source/distribution or explicit owner release approval is required.'
    }
}

# Refuse private developer paths before creating a redistributable payload.
foreach($relative in $selected){
    $bytes=[IO.File]::ReadAllBytes((Join-Path $runtime $relative))
    $ascii=[Text.Encoding]::ASCII.GetString($bytes)
    $unicode=[Text.Encoding]::Unicode.GetString($bytes)
    $privatePattern='(?i)[A-Z]:[\\/](?:Users|AI)[\\/]|/(?:home|Users)/|[\\/]\.cache[\\/]'
    if($ascii -match $privatePattern -or $unicode -match $privatePattern){throw "Private build metadata in runtime input: $relative. Rebuild Release without debug metadata."}
}
$zip=Join-Path $output $zipName
if($ValidateOnly){
    $previewEntries=@(foreach($relative in $selected){
        $file=Join-Path $runtime $relative
        $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if($hash -ne $approvedHashes[$relative]){throw "Runtime output changed after review: $relative"}
        [pscustomobject]@{Path=$relative;Bytes=(Get-Item -LiteralPath $file).Length;SHA256=$hash}
    })
    foreach($name in @('LICENSE','THIRD-PARTY-NOTICES.txt')){
        $file=Join-Path $sourceRoot $name
        $previewEntries+= [pscustomobject]@{Path=$name;Bytes=(Get-Item -LiteralPath $file).Length;SHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
    }
    if($Candidate){
        $marker="Internal audit candidate. Not a final release. ProductVersion=$productVersion. FFmpeg Corresponding Source and all release blockers must be resolved before distribution.`n"
        $markerBytes=[Text.UTF8Encoding]::new($false).GetBytes($marker)
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$markerHash=[BitConverter]::ToString($sha.ComputeHash($markerBytes)).Replace('-','')}finally{$sha.Dispose()}
        $previewEntries+= [pscustomobject]@{Path='CANDIDATE-NOT-FOR-DISTRIBUTION.txt';Bytes=$markerBytes.Length;SHA256=$markerHash}
    }
    $byPreviewPath=@{}
    foreach($entry in $previewEntries){$byPreviewPath[$entry.Path]=$entry}
    $previewPaths=[string[]]@($previewEntries | ForEach-Object {$_.Path})
    [Array]::Sort($previewPaths,[StringComparer]::Ordinal)
    [pscustomobject]@{Status='VALIDATED / NO FILES WRITTEN';Zip=$zip;ZipName=$zipName;Files=$previewEntries.Count;ProductVersion=$productVersion;FinalPackageAllowed=$finalPackageAllowed;PublicRevisionReady=$publicRevisionReady;PublicRevisionProblem=$revisionProblem;FFmpegComplianceReady=$ffmpegComplianceReady;FFmpegSourceVerification=if($sourceVerified){'VERIFIED'}else{'UNVERIFIED'};OwnerReleaseApproval=if($ownerReleaseApproved){'EXPLICIT'}else{'NOT PROVIDED'};Manifest=@($previewPaths | ForEach-Object {$byPreviewPath[$_]})}
    return
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$package=Join-Path $output 'package-staging'
New-Item -ItemType Directory -Path $package | Out-Null
foreach($relative in $selected){
    $destination=Join-Path $package $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $runtime $relative) -Destination $destination
}
foreach($name in @('LICENSE','THIRD-PARTY-NOTICES.txt')){Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $package}
if($Candidate){[IO.File]::WriteAllText((Join-Path $package 'CANDIDATE-NOT-FOR-DISTRIBUTION.txt'),"Internal audit candidate. Not a final release. ProductVersion=$productVersion. FFmpeg Corresponding Source and all release blockers must be resolved before distribution.`n",[Text.UTF8Encoding]::new($false))}
$entries=@(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object{[pscustomobject]@{Path=$_.FullName.Substring($package.Length+1).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$byPath=@{}
foreach($entry in $entries){$byPath[$entry.Path]=$entry}
foreach($relative in $selected){
    if(-not $byPath.ContainsKey($relative) -or $byPath[$relative].SHA256 -ne $approvedHashes[$relative]){
        throw "Staged runtime file differs from the approved manifest: $relative"
    }
}
$sortedPaths=[string[]]@($entries | ForEach-Object {$_.Path})
[Array]::Sort($sortedPaths,[StringComparer]::Ordinal)
$manifest=@($sortedPaths | ForEach-Object {$byPath[$_]})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'package-manifest.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($entry in $manifest){
    $created=$archive.CreateEntry($entry.Path,[IO.Compression.CompressionLevel]::Optimal)
    $created.LastWriteTime=$entryTimestamp
    $inputStream=[IO.File]::OpenRead((Join-Path $package $entry.Path))
    $outputStream=$created.Open()
    try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
}}finally{$archive.Dispose()}
$result=[ordered]@{Status=if($Candidate){'CANDIDATE / NOT FINAL / NOT FOR DISTRIBUTION'}else{'PACKAGED / PUBLICATION REQUIRES SEPARATE APPROVAL'};Zip=$zip;SHA256=(Get-FileHash -LiteralPath $zip).Hash;Bytes=(Get-Item -LiteralPath $zip).Length;Files=$manifest.Count;ProductVersion=$productVersion;ZipTimestamp=$ZipTimestamp;ZipTimestampConvention='Fixed UTC wall-clock components; ZIP has no timezone field';FFmpegComplianceReady=$ffmpegComplianceReady;FFmpegSourceVerification=if($sourceVerified){'VERIFIED'}else{'UNVERIFIED'};OwnerReleaseApproval=if($ownerReleaseApproved){'EXPLICIT'}else{'NOT PROVIDED'}}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'package-result.json') -Encoding utf8
$result | ConvertTo-Json -Depth 4
