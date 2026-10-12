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
# Create an actual visible WinForms layout: an unshown Form.DrawToBitmap
# can return a blank rectangle even though color properties pass.
$form.StartPosition=[Windows.Forms.FormStartPosition]::Manual
$form.Location=New-Object Drawing.Point(-1800,-1800)
$form.ShowInTaskbar=$false
$form.Show()
[Windows.Forms.Application]::DoEvents()
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
# A blank screenshot must fail the probe. Sample an interior point of
# each primary button away from the label and rounded border.
$first=$buttons[0]
$pixel=$bitmap.GetPixel($first.Left+18,$first.Top+8)
$backdrop=$form.BackColor
# Native WinForms/Windows may render a hover tint or gradients in a real
# screenshot. Verify the pixels are visibly distinct from the EMPTY form;
# separately verify color property contrast above.
$diff=[Math]::Abs([int]$pixel.R-[int]$backdrop.R)+[Math]::Abs([int]$pixel.G-[int]$backdrop.G)+[Math]::Abs([int]$pixel.B-[int]$backdrop.B)
if($diff -lt 38) {
 $bitmap.Dispose();$form.Close();$form.Dispose()
 throw "Button screenshot appears blank: expected painted button, actual pixel $pixel, backdrop $backdrop"
}
$bitmap.Save((Join-Path $out 'UI_BUTTON_CONTRAST.png'),[Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose();$form.Close();$form.Dispose()
@{scope='Actual WinForms UiTheme, without AutoCAD host';source_sha=$env:SOURCE_SHA;buttons=$results;autocad_runtime='NOT TESTED'} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $out 'UI_BUTTON_CONTRAST.json')
# Independently render the new Liquid Glass-inspired chrome. It must be
# a real nonblank WinForms surface, not a color token or illustration only.
$glassType=$assembly.GetType('MiningVolume2023.UI.GlassPanel',$true)
$glass=[System.Windows.Forms.Panel][Activator]::CreateInstance($glassType,$true)
$glassForm=New-Object Windows.Forms.Form
$glassForm.ClientSize=New-Object Drawing.Size(530,270)
$glassForm.StartPosition=[Windows.Forms.FormStartPosition]::Manual
$glassForm.Location=New-Object Drawing.Point(-1800,-1800)
$glassForm.ShowInTaskbar=$false
$glass.Dock=[Windows.Forms.DockStyle]::Fill
$glassForm.Controls.Add($glass)
$glassForm.Show()
$glass.Invalidate();$glass.Update()
[Windows.Forms.Application]::DoEvents()
$glassImage=New-Object Drawing.Bitmap(530,270)
$glassForm.DrawToBitmap($glassImage,(New-Object Drawing.Rectangle(0,0,530,270)))
$top=$glassImage.GetPixel(500,35)
$bottom=$glassImage.GetPixel(500,215)
$gradientDelta=[Math]::Abs([int]$top.R-[int]$bottom.R)+[Math]::Abs([int]$top.G-[int]$bottom.G)+[Math]::Abs([int]$top.B-[int]$bottom.B)
if($gradientDelta -lt 9) {
 $glassImage.Dispose();$glassForm.Close();$glassForm.Dispose()
 throw "Liquid Glass chrome did not render the expected depth gradient; top=$top bottom=$bottom"
}
$glassImage.Save((Join-Path $out 'UI_GLASS_CHROME.png'),[Drawing.Imaging.ImageFormat]::Png)
$glassImage.Dispose();$glassForm.Close();$glassForm.Dispose()
@{style='Liquid Glass-inspired, WinForms opaque chrome';glass_gradient_delta=$gradientDelta;source_sha=$env:SOURCE_SHA;autocad_runtime='NOT TESTED'} | ConvertTo-Json -Depth 3 | Set-Content -Encoding UTF8 (Join-Path $out 'UI_GLASS_CHROME.json')
Write-Host 'PASS: actual Windows WinForms buttons, text contrast >=4.5; AutoCAD runtime NOT TESTED'
