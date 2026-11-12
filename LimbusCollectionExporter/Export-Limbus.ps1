[CmdletBinding()]
param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'outputs'),[switch]$RefreshDiscovery)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$process=Get-Process LimbusCompany -ErrorAction Stop | Select-Object -First 1
if(-not ('LimbusRead' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'LimbusRead.cs')}
$reader=[LimbusRead]::new($process.Id)
$cachePath=Join-Path $PSScriptRoot 'limbus-runtime-cache.json'
$stamp=$process.StartTime.ToUniversalTime().ToString('o')
function Read-Map($folder,$pattern){$map=@{};Get-ChildItem -LiteralPath $folder -Filter $pattern -File|ForEach-Object {$doc=Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8|ConvertFrom-Json;foreach($item in $doc.dataList){$map[[int]$item.id]=$item}};return $map}
function Decode-Int([long]$address){return $reader.Int($address) -bxor $reader.Int($address+4)}
function Format-Day([int]$date){if($date -eq 0){return ''};return [datetime]::ParseExact([string]$date,'yyyyMMdd',[Globalization.CultureInfo]::InvariantCulture).ToString('dd/MM/yyyy')}
function Format-Season([int]$season){if($season -ge 9101 -and $season -le 9199){return 'Walpurgis Night '+($season-9100)};if($season -eq 8000){return 'Collaboration - Limited'};return "Season $season"}
function Get-WikiUrl([string]$Name,[string]$Sinner,[bool]$UniqueFacade=$false){
    $title=($Name -replace '[\[\]]','') -replace '\s*::\s*','::'
    $title=($title.Trim()+' '+$Sinner.Trim()) -replace '\s+【','【' -replace '】\s+','】'
    $title=($title -replace '\s+','_').Normalize([Text.NormalizationForm]::FormC)
    $slug=[Uri]::EscapeDataString($title).Replace('%3A',':')
    if($UniqueFacade){return 'https://limbuscompany.wiki.gg/wiki/List_of_Unique_Fa%C3%A7ades#'+$slug}
    return 'https://limbuscompany.wiki.gg/wiki/'+$slug
}
function ConvertTo-WikiCsvRow($Row){
    $values=[ordered]@{}
    foreach($property in $Row.PSObject.Properties){
        $values[$property.Name]=$property.Value
    }
    $uniqueFacade=($Row.Kind -eq 'Façade') -or ($null -ne $Row.PSObject.Properties['FacadeID'])
    $url=Get-WikiUrl $Row.Name $Row.Sinner $uniqueFacade
    $label=([string]$Row.Name).Replace('"','""')
    $values['Name']='=HYPERLINK("'+$url+'","'+$label+'")'
    return [pscustomobject]$values
}
function ConvertTo-CollectionCsvRow($Row,[int]$SheetRow){
    if($SheetRow -lt 2){throw 'Collection data starts at spreadsheet row 2.'}
    $linked=ConvertTo-WikiCsvRow $Row
    $linked.'1'='=SWITCH(B{r},"Yi Sang",1,"Faust",2,"Don Quixote",3,"Ryōshū",4,"Meursault",5,"Hong Lu",6,"Heathcliff",7,"Ishmael",8,"Rodion",9,"Dante",10,"Sinclair",11,"Outis",12,"Gregor",13,"?")'.Replace('{r}',[string]$SheetRow)
    $linked.Percentage='=IF(OR(F{r}="No",AND(C{r}="E.G.O",G{r}="Yes")),0%,IF(COUNTIFS(C:C,"E.G.O",F:F,"Yes",G:G,"Yes")=COUNTIFS(C:C,"E.G.O",F:F,"Yes"),IF(D{r}=3,3%,IF(D{r}=2,13%,IF(D{r}=1,84%,IF(C{r}="E.G.O",0%,0)))),IF(D{r}=3,2.9%,IF(D{r}=2,12.8%,IF(D{r}=1,83%,IF(C{r}="E.G.O",1.3%,0))))))/IF(OR(D{r}=1,D{r}=2,D{r}=3),MAX(1,COUNTIFS(D:D,D{r},F:F,"Yes")),IF(C{r}="E.G.O",MAX(1,COUNTIFS(C:C,"E.G.O",F:F,"Yes",G:G,"No")),1))'.Replace('{r}',[string]$SheetRow)
    $order='=A{r}*100+IFERROR(MATCH(D{r},{1,2,3,"ZAYIN","TETH","HE","WAW","ALEPH"},0),999)'.Replace('{r}',[string]$SheetRow)
    $linked|Add-Member -NotePropertyName Order -NotePropertyValue $order
    return $linked
}
function Is-Class([long]$p,[string]$name){return $p -gt 0 -and $reader.Name($p) -eq $name}
try {
    $cache=$null
    if((Test-Path -LiteralPath $cachePath) -and -not $RefreshDiscovery){$cache=Get-Content -LiteralPath $cachePath -Raw|ConvertFrom-Json;if($cache.ProcessId -ne $process.Id -or $cache.StartTime -ne $stamp){$cache=$null}}
    if(-not $cache){
        Write-Host 'Discovering game models using read-only memory access; this can take a few minutes.'
        $classPath=Join-Path $PSScriptRoot '.limbus-classes.tmp'
        $fieldPath=Join-Path $PSScriptRoot '.limbus-fields.tmp'
        $objectPath=Join-Path $PSScriptRoot '.limbus-objects.tmp'
        $reader.Discover($classPath)
        $reader.Fields($classPath,$fieldPath)
        $reader.Objects($classPath,$objectPath)
        $objects=@(Get-Content -LiteralPath $objectPath|ForEach-Object {$v=$_ -split ',';[pscustomobject]@{Type=$v[0];Address=$v[1]}})
        $cache=[pscustomobject]@{ProcessId=$process.Id;StartTime=$stamp;Classes=[IO.File]::ReadAllLines($classPath);Objects=$objects}
        $cache|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $cachePath -Encoding UTF8
        Remove-Item -LiteralPath $classPath,$fieldPath,$objectPath
    }
    $classes=@{}
    foreach($entry in $cache.Classes){
        $line=if($entry -is [string]){$entry}else{[string]$entry.value}
        $parts=$line -split ','
        if($parts.Count -ne 2){throw 'Invalid runtime class cache. Retry with -RefreshDiscovery.'}
        $classes[$parts[0]]=[Convert]::ToInt64($parts[1],16)
    }
    function Get-StaticLayout([string]$ClassName,[string[]]$Fields){
        if(-not $classes.ContainsKey($ClassName)){throw "Game class not loaded: $ClassName. Retry with -RefreshDiscovery."}
        $layout=@{}
        foreach($field in $Fields){$layout[$field]=$reader.Offset($classes[$ClassName],$field)}
        return $layout
    }
    $identityLayout=Get-StaticLayout 'PersonalityStaticData' @('id','characterId','rank','season','updatedDate')
    $egoLayout=Get-StaticLayout 'EgoStaticData' @('id','characterId','_egoType','season','updatedDate')
    $accountLayouts=@{
        Personality=@{_id=16;_level=32;_gacksung=52;_classInfo=96}
        Ego=@{_id=16;_gacksung=32;_classInfo=64}
        UserSkinData=@{_ownedSkinIds=24}
        UserDataManager=@{'<IsInited>k__BackingField'=304;_personalities=32;_skins=40;_egos=56}
    }
    foreach($type in $accountLayouts.Keys){
        foreach($field in $accountLayouts[$type].Keys){
            if(-not $classes.ContainsKey($type) -or $reader.Offset($classes[$type],$field) -ne $accountLayouts[$type][$field]){
                throw "Account data layout changed: $type.$field. Export stopped before reading account values."
            }
        }
    }
    $manager=0L
    foreach($obj in $cache.Objects|Where-Object Type -eq 'UserDataManager'){
        $p=[Convert]::ToInt64($obj.Address,16)
        if((Is-Class $p 'UserDataManager') -and $reader.Byte($p+304) -eq 1 -and (Is-Class ($reader.Ptr($p+40)) 'UserSkinData') -and (Is-Class ($reader.Ptr($p+56)) 'UserEgoListData')){$manager=$p;break}
    }
    if(-not $manager){throw 'No initialized account model found. Log in and retry with -RefreshDiscovery.'}
    $personalityList=$reader.Ptr($reader.Ptr($manager+32)+24)
    $egoList=$reader.Ptr($reader.Ptr($manager+56)+24)
    $skins=$reader.Ptr($manager+40)
    if(-not (Is-Class $personalityList 'PersonalityList') -or -not (Is-Class $egoList 'EgoList')){throw 'Account collections changed; refusing to export uncertain data.'}
    $ownedIdentities=@{}
    foreach($p in $reader.List($reader.Ptr($personalityList+16))){
        if(-not (Is-Class $p 'Personality')){throw 'Invalid identity collection member'}
        $id=Decode-Int ($p+16);$ownedIdentities[$id]=[pscustomobject]@{Level=(Decode-Int ($p+32));Uptie=(Decode-Int ($p+52));Static=$reader.Ptr($p+96)}
    }
    $ownedEgos=@{}
    foreach($entry in $reader.Dict($reader.Ptr($egoList+16))){
        $v=$entry -split ',';$id=[int]$v[0];$p=[Convert]::ToInt64($v[1],16)
        if(-not (Is-Class $p 'Ego') -or (Decode-Int ($p+16)) -ne $id){throw 'Invalid E.G.O collection member'}
        $ownedEgos[$id]=[pscustomobject]@{Threadspin=(Decode-Int ($p+32));Static=$reader.Ptr($p+64)}
    }
    $facades=@{};foreach($id in $reader.Set($reader.Ptr($skins+24))){$facades[$id]=$true}
    $gameFolder=Split-Path -Parent $process.Path
    $localize=Join-Path $gameFolder 'LimbusCompany_Data\Assets\Resources_moved\Localize\en'
    $identityNames=Read-Map $localize 'EN_Personalities*.json'
    $egoNames=Read-Map $localize 'EN_Egos*.json'
    $sinnerNames=Read-Map $localize 'EN_Characters.json'
    $identityCatalog=@{};$egoCatalog=@{}
    foreach($obj in $cache.Objects){
        $p=[Convert]::ToInt64($obj.Address,16)
        if($obj.Type -eq 'PersonalityStaticData' -and (Is-Class $p 'PersonalityStaticData')){
            $id=$reader.Int($p+$identityLayout.id);$sinner=$reader.Int($p+$identityLayout.characterId);$date=$reader.Int($p+$identityLayout.updatedDate)
            if($id -ge 10101 -and $id -le 11299 -and $sinner -ge 1 -and $sinner -le 12 -and $date -ge 20230227 -and $identityNames.ContainsKey($id)){$identityCatalog[$id]=$p}
        }elseif($obj.Type -eq 'EgoStaticData' -and (Is-Class $p 'EgoStaticData')){
            $id=$reader.Int($p+$egoLayout.id);$sinner=$reader.Int($p+$egoLayout.characterId);$date=$reader.Int($p+$egoLayout.updatedDate)
            if($id -ge 20101 -and $id -le 21299 -and $sinner -ge 1 -and $sinner -le 12 -and $date -ge 20230227 -and $egoNames.ContainsKey($id)){$egoCatalog[$id]=$p}
        }
    }
    foreach($id in $ownedIdentities.Keys){$identityCatalog[$id]=$ownedIdentities[$id].Static}
    foreach($id in $ownedEgos.Keys){$egoCatalog[$id]=$ownedEgos[$id].Static}
    $standard=0L
    foreach($obj in $cache.Objects|Where-Object Type -eq 'GachaStaticData'){
        $p=[Convert]::ToInt64($obj.Address,16)
        if((Is-Class $p 'GachaStaticData') -and $reader.Int($p+16) -eq 1 -and (Is-Class ($reader.Ptr($p+72)) 'GachaBannerInfo')){$standard=$p;break}
    }
    if(-not $standard){throw 'Standard banner data is not loaded. Open Extraction once, then retry with -RefreshDiscovery.'}
    $groups=@($reader.List($reader.Ptr($standard+80)))
    $standardEgos=@();foreach($g in $groups){if($reader.Utf16($reader.Ptr($g+48)) -eq 'EGO'){$standardEgos+=@($reader.IntList($reader.Ptr($g+64)))}}
    $remainingEgos=@($standardEgos|Where-Object {-not $ownedEgos.ContainsKey([int]$_)})
    $allEgoOwned=$standardEgos.Count -gt 0 -and $remainingEgos.Count -eq 0
    $pool=@{};$rates=@{}
    foreach($g in $groups){
        $kind=$reader.Utf16($reader.Ptr($g+48));$ids=@($reader.IntList($reader.Ptr($g+64)))
        $probability=if($allEgoOwned){[double]$reader.Float($g+40)}else{[double]$reader.Float($g+32)}
        $denominator=if($kind -eq 'EGO'){$remainingEgos.Count}else{$ids.Count}
        foreach($id in $ids){$pool[[int]$id]=$true;$rates[[int]$id]=if($denominator -gt 0 -and ($kind -ne 'EGO' -or -not $ownedEgos.ContainsKey([int]$id))){$probability*100/$denominator}else{0.0}}
    }
    if($groups.Count -lt 4 -or $pool.Count -lt 100){throw 'Incomplete standard extraction table'}
    $rows=[Collections.Generic.List[object]]::new()
    $detail=[Collections.Generic.List[object]]::new()
    $rowIds=@{}
    foreach($kind in 'Identity','E.G.O'){
        $catalog=if($kind -eq 'Identity'){$identityCatalog}else{$egoCatalog}
        foreach($id in $catalog.Keys|Sort-Object){
            $p=[long]$catalog[$id];$owned=if($kind -eq 'Identity'){$ownedIdentities.ContainsKey([int]$id)}else{$ownedEgos.ContainsKey([int]$id)}
            if($kind -eq 'Identity'){
                $sinner=$reader.Int($p+$identityLayout.characterId);$rarity=$reader.Int($p+$identityLayout.rank);$name=$identityNames[[int]$id].title -replace '\s+',' ';$season=$reader.Int($p+$identityLayout.season);$date=$reader.Int($p+$identityLayout.updatedDate)
                $up=if($owned){$ownedIdentities[[int]$id].Uptie}else{''};$level=if($owned){$ownedIdentities[[int]$id].Level}else{''};$facade=if($facades.ContainsKey([int]$id)){'Yes'}else{'No'}
            }else{
                $sinner=$reader.Int($p+$egoLayout.characterId);$risk=$reader.Int($p+$egoLayout._egoType);if($risk -lt 0 -or $risk -gt 4){throw "Unknown E.G.O risk $risk"};$rarity=@('ZAYIN','TETH','HE','WAW','ALEPH')[$risk];$name=$egoNames[[int]$id].name;$season=$reader.Int($p+$egoLayout.season);$date=$reader.Int($p+$egoLayout.updatedDate)
                $up=if($owned){$ownedEgos[[int]$id].Threadspin}else{''};$level='';$facade=''
            }
            if(-not $sinnerNames.ContainsKey($sinner)){throw "Unknown sinner $sinner for $kind $id; refusing to export uncertain data."}
            $percentage=if($rates.ContainsKey([int]$id)){$rates[[int]$id]}else{0.0}
            $row=[pscustomobject][ordered]@{'1'=$sinner;Sinner=$sinnerNames[$sinner].name;Kind=$kind;Rarity=$rarity;Name=$name;'In Standard?'=$(if($pool.ContainsKey([int]$id)){'Yes'}else{'No'});'Owned?'=$(if($owned){'Yes'}else{'No'});Uptie=$up;Level=$level;Facade=$facade;Season=(Format-Season $season);Day=(Format-Day $date);Percentage=($percentage.ToString('F4',[Globalization.CultureInfo]::InvariantCulture)+'%')}
            $rowIds[$row.Sinner+'|'+$row.Kind+'|'+$row.Name]=[int]$id;$rows.Add($row);$detail.Add([pscustomobject]@{ID=[int]$id;Kind=$kind;SinnerID=$sinner;Sinner=$row.Sinner;Name=$name;Owned=$owned;Tier=$up;Level=$level;Facade=$facade;Rarity=$rarity;SeasonID=$season;ReleaseDate=$row.Day;InStandard=$pool.ContainsKey([int]$id);Percentage=$percentage})
        }
    }
    # Standalone facades have their own collection rows, including locked catalog entries.
    $facadeMetadata=@{}
    $metadataPath=Join-Path $PSScriptRoot 'limbus-facade-metadata.json'
    if(Test-Path -LiteralPath $metadataPath){
        $metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8|ConvertFrom-Json
        foreach($property in $metadata.PSObject.Properties){$facadeMetadata[[int]$property.Name]=$property.Value}
    }
    $standalone=@{}
    foreach($id in $facades.Keys){if(-not $identityCatalog.ContainsKey([int]$id)){$standalone[[int]$id]=$true}}
    foreach($id in $facadeMetadata.Keys){$standalone[[int]$id]=$true}
    foreach($obj in $cache.Objects|Where-Object Type -eq 'SkinStaticData'){
        $p=[Convert]::ToInt64($obj.Address,16)
        if(Is-Class $p 'SkinStaticData'){
            $id=$reader.Int($p+16);$character=$reader.Int($p+20)
            if($id -ge 40000 -and $character -ge 1 -and $character -le 12 -and $identityNames.ContainsKey($id)){$standalone[$id]=$true}
        }
    }
    foreach($id in $standalone.Keys|Sort-Object){
        $local=$identityNames[[int]$id];if(-not $local){throw "Missing standalone facade name: $id"}
        $meta=$facadeMetadata[[int]$id]
        $sinner=if($meta){[int]$meta.SinnerID}else{[int](@($sinnerNames.Keys|Where-Object {$sinnerNames[$_].name -eq $local.name})|Select-Object -First 1)}
        if(-not $sinnerNames.ContainsKey($sinner)){throw "Unknown standalone facade sinner: $id"}
        $owned=$facades.ContainsKey([int]$id);$yes=if($owned){'Yes'}else{'No'}
        $name=$local.title -replace '\s+',' ';$season=if($meta){Format-Season ([int]$meta.SeasonID)}else{''};$day=if($meta){$meta.Day}else{''}
        $row=[pscustomobject][ordered]@{'1'=$sinner;Sinner=$sinnerNames[$sinner].name;Kind='Façade';Rarity='Façade';Name=$name;'In Standard?'='No';'Owned?'=$yes;Uptie='';Level='';Facade=$yes;Season=$season;Day=$day;Percentage='0.0000%'}
        $rowIds[$row.Sinner+'|'+$row.Kind+'|'+$row.Name]=[int]$id;$rows.Add($row)
        $detail.Add([pscustomobject]@{ID=[int]$id;Kind='Façade';SinnerID=$sinner;Sinner=$row.Sinner;Name=$name;Owned=$owned;Tier='';Level='';Facade=$yes;Rarity='Façade';SeasonID=$(if($meta){[int]$meta.SeasonID}else{$null});ReleaseDate=$day;InStandard=$false;Percentage=0.0})
    }
    $ordered=@($rows|Sort-Object {$_.'1'},{if($_.Kind -eq 'Identity'){0}elseif($_.Kind -eq 'E.G.O'){1}else{2}},{$rowIds[$_.Sinner+'|'+$_.Kind+'|'+$_.Name]})
    New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
    $csv=Join-Path $OutputDirectory 'limbus-collection.csv'
    @(for($i=0;$i -lt $ordered.Count;$i++){ConvertTo-CollectionCsvRow $ordered[$i] ($i+2)})|Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
    $detail|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'limbus-collection.json') -Encoding UTF8
    $facadeRows=@(foreach($id in $facades.Keys|Sort-Object){$local=$identityNames[[int]$id];[pscustomobject]@{FacadeID=$id;Sinner=$local.name;Name=($local.title -replace '\s+',' ');Unlocked='Yes'}})
    @(foreach($row in $facadeRows){ConvertTo-WikiCsvRow $row})|Export-Csv -LiteralPath (Join-Path $OutputDirectory 'limbus-facades.csv') -NoTypeInformation -Encoding UTF8
    $snapshot=[pscustomobject]@{CapturedAtUTC=[datetime]::UtcNow.ToString('o');ProcessId=$process.Id;OwnedIdentities=$ownedIdentities.Count;OwnedEgos=$ownedEgos.Count;UnlockedFacades=$facades.Count;CSVRows=$ordered.Count;StandardBannerID=1;StandardEgoPool=$standardEgos.Count;UnownedStandardEgos=$remainingEgos.Count;AllStandardEgosOwned=$allEgoOwned;Rates='Single extraction; live standard-banner pool; account-adjusted for owned E.G.O';DaySource='Game static-data updatedDate field';Access='Read-only OpenProcess, VirtualQueryEx, ReadProcessMemory; no writes or injection'}
    $snapshot|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'limbus-export-info.json') -Encoding UTF8
    Write-Host "Exported $($ownedIdentities.Count) owned identities, $($ownedEgos.Count) owned E.G.O, and $($facades.Count) facade unlocks to $csv"
} finally {$reader.Close()}


