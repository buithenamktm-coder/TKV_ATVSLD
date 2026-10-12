param([string]$Root)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$dll=Join-Path $Root 'src/MiningVolume.Plugin2023/bin/Release/net48/MiningVolume2023.dll'
if(!(Test-Path $dll)) { $dll=(Get-ChildItem (Join-Path $Root 'src/MiningVolume.Plugin2023/bin/Release') -Filter MiningVolume2023.dll -Recurse | Select-Object -First 1).FullName }
$assembly=[Reflection.Assembly]::LoadFrom($dll)
$theme=$assembly.GetType('MiningVolume2023.UI.UiTheme',$true)
$apply=$theme.GetMethod('ApplyPage',[Reflection.BindingFlags]'Public,Static')
$form=New-Object Windows.Forms.Form
$form.ClientSize=New-Object Drawing.Size(530,270)
$names=@('TẠO TIN HIỆN TRẠNG','TẠO TIN THIẾT KẾ','Nạp layer','Chọn trên CAD','Ẩn TIN','VIEW TIN 3D','Loại điểm','HỦY DỰNG TIN')
$buttons=@()
for($i=0;$i -lt $names.Count;$i++) {
 $b=New-Object Windows.Forms.Button
 $b.Text=$names[$i];$b.Size=New-Object Drawing.Size(238,46)
 $b.Location=New-Object Drawing.Point((12+($i%2)*260),(12+[Math]::Floor($i/2)*64))
 $form.Controls.Add($b);$buttons+=$b
}
# PowerShell may wrap New-Object output in PSObject. Reflection expects
# the underlying WinForms Control inside a real object[] argument vector.
$nativeForm=[System.Windows.Forms.Control]$form.PSObject.BaseObject
$invokeArgs=[object[]]::new(1)
$invokeArgs[0]=$nativeForm
$apply.Invoke($null,$invokeArgs) | Out-Null
$form.CreateControl()
function Luminance($c) {
 $v=@($c.R,$c.G,$c.B) | ForEach-Object { $x=$_/255.0; if($x -le 0.04045){$x/12.92}else{[Math]::Pow(($x+0.055)/1.055,2.4)} }
 return .2126*$v[0]+.7152*$v[1]+.0722*$v[2]
}
$results=@()
foreach($b in $buttons) {
 $fg=Luminance $b.ForeColor;$bg=Luminance $b.BackColor
 $ratio=([Math]::Max($fg,$bg)+.05)/([Math]::Min($fg,$bg)+.05)
 if($ratio -lt 4.5){throw "Button contrast too low: $($b.Text) = $ratio"}
 if($b.UseVisualStyleBackColor){throw 'System theme may replace the verified color'}
 $results+=@{text=$b.Text;contrast=$ratio;foreground=$b.ForeColor.ToArgb();background=$b.BackColor.ToArgb();status='PASS'}
}
$out=Join-Path $Root 'dist';New-Item -ItemType Directory -Force $out | Out-Null
$bitmap=New-Object Drawing.Bitmap(530,270)
$form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,530,270)))
$bitmap.Save((Join-Path $out 'UI_BUTTON_CONTRAST.png'),[Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose();$form.Dispose()
@{scope='Actual WinForms UiTheme, without AutoCAD host';source_sha=$env:SOURCE_SHA;buttons=$results;autocad_runtime='NOT TESTED'} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $out 'UI_BUTTON_CONTRAST.json')
Write-Host 'PASS: actual Windows WinForms buttons, text contrast >=4.5; AutoCAD runtime NOT TESTED'
