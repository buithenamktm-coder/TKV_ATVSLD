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
# Real WinForms event test: changing only the section-station spacing must
# invalidate the old accepted geometry; plot-only Z ticks remain independent.
$sectionType=$assembly.GetType('MiningVolume2023.UI.SectionPage',$true)
$stateType=$assembly.GetType('MiningVolume2023.Services.ProjectState',$true)
$state=$stateType.GetProperty('Current',[Reflection.BindingFlags]'Public,Static').GetValue($null,$null)
$section=[Windows.Forms.Control][Activator]::CreateInstance($sectionType)
$sectionForm=New-Object Windows.Forms.Form
$sectionForm.ClientSize=New-Object Drawing.Size(590,620)
$section.Dock=[Windows.Forms.DockStyle]::Fill
$sectionForm.Controls.Add($section)
$sectionForm.StartPosition=[Windows.Forms.FormStartPosition]::Manual
$sectionForm.Location=New-Object Drawing.Point(-1800,-1800)
$sectionForm.ShowInTaskbar=$false
$sectionForm.Show()
[Windows.Forms.Application]::DoEvents()
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$spacing=[Windows.Forms.NumericUpDown]$sectionType.GetField('_spacing',$flags).GetValue($section)
$zMarks=[Windows.Forms.NumericUpDown]$sectionType.GetField('_axisStep',$flags).GetValue($section)
$mode=[Windows.Forms.ComboBox]$sectionType.GetField('_parameterMode',$flags).GetValue($section)
$status=[Windows.Forms.Label]$sectionType.GetField('_status',$flags).GetValue($section)
$spacing.Value=[decimal]10
if([Math]::Abs([double]$state.SectionSpacing - 10.0) -gt 1e-9) {
  throw "WinForms spacing change did not update the active Project state."
}
if($status.Text -notlike '*XEM TRƯỚC TUYẾN*') {
  throw "Changing station spacing did not warn that existing sections must be regenerated."
}
$zMarks.Value=[decimal]15
if([Math]::Abs([double]$state.SectionAxisElevationInterval - 15.0) -gt 1e-9 -or
   [Math]::Abs([double]$state.SectionSpacing - 10.0) -gt 1e-9) {
  throw "CAD Z-axis tick spacing modified the calculation station spacing."
}
$mode.SelectedItem='TT34/2018/TT-BCT'
if([Math]::Abs([double]$state.SectionSpacing - 20.0) -gt 1e-9 -or
   [Math]::Abs([double]$state.SectionAxisElevationInterval - 20.0) -gt 1e-9) {
  throw "TT34 1:1000 preset failed to restore 20m station / 20m elevation ticks."
}
if([Math]::Abs([double]$state.HorizontalScale - 1000.0) -gt 1e-9 -or
   [Math]::Abs([double]$state.VerticalScale - 1000.0) -gt 1e-9) {
  throw "Section page allowed an old unequal CAD scale."
}
# Custom mode MUST unlock both independent scales; switching to TT34
# must restore both to 1:1000 without changing 3D TIN geometry.
$horizontal=[Windows.Forms.NumericUpDown]$sectionType.GetField('_hScale',$flags).GetValue($section)
$vertical=[Windows.Forms.NumericUpDown]$sectionType.GetField('_vScale',$flags).GetValue($section)
$mode.SelectedItem='Tùy chỉnh'
if(!$horizontal.Enabled -or !$vertical.Enabled) {
  throw "Custom section mode did not unlock horizontal AND vertical scale."
}
$horizontal.Value=[decimal]2000
$vertical.Value=[decimal]500
if([Math]::Abs([double]$state.HorizontalScale-2000.0) -gt 1e-9 -or
   [Math]::Abs([double]$state.VerticalScale-500.0) -gt 1e-9) {
  throw "Custom scales did not flow from actual WinForms controls into ProjectState."
}
$mode.SelectedItem='TT34/2018/TT-BCT'
if($horizontal.Enabled -or $vertical.Enabled -or
   [Math]::Abs([double]$state.HorizontalScale-1000.0) -gt 1e-9 -or
   [Math]::Abs([double]$state.VerticalScale-1000.0) -gt 1e-9) {
  throw "TT34 preset did not lock and restore both 1:1000 scales."
}
# Check the approved TWO separate workflows in REAL WinForms layouts.
# Calculation sections must not show the independent quick-section action.
$nativeSection=[System.Windows.Forms.Control]$section.PSObject.BaseObject
$apply.Invoke($null,[object[]]@($nativeSection)) | Out-Null
[Windows.Forms.Application]::DoEvents()
$preview=[Windows.Forms.Button]($section.Controls.Find('btnPreviewSections',$true)[0])
$draw=[Windows.Forms.Button]($section.Controls.Find('btnDrawSections',$true)[0])
$quickInCalculation=[Windows.Forms.Button]($section.Controls.Find('btnQuickSections',$true)[0])
if($null -eq $preview -or $null -eq $draw) {
  throw 'Required section preview/draw buttons are missing.'
}
# Quick action should not be attached at all to calculation layout.
if(!$preview.Visible -or !$draw.Visible -or ($null -ne $quickInCalculation -and $quickInCalculation.Visible)) {
  throw 'Calculation section must show preview/draw and exclude independent quick-section action.'
}
$gap=$draw.Left-$preview.Right
if($gap -lt 6) {
  throw "Two calculation section buttons are touching/overlapping: gap=$($gap)px"
}
$sectionImage=New-Object Drawing.Bitmap(590,620)
$sectionForm.DrawToBitmap($sectionImage,(New-Object Drawing.Rectangle(0,0,590,620)))
$sectionImage.Save((Join-Path $out 'UI_SECTION_LAYOUT.png'),[Drawing.Imaging.ImageFormat]::Png)
$sectionImage.Dispose()
$sectionForm.Close();$sectionForm.Dispose()

# Quick sections are a separate top-level workflow, not a third calculation button.
$quickPage=[Windows.Forms.Control][Activator]::CreateInstance($sectionType,[object[]]@($true))
$quickForm=New-Object Windows.Forms.Form
$quickForm.ClientSize=New-Object Drawing.Size(590,620)
$quickPage.Dock=[Windows.Forms.DockStyle]::Fill
$quickForm.Controls.Add($quickPage)
$quickForm.StartPosition=[Windows.Forms.FormStartPosition]::Manual
$quickForm.Location=New-Object Drawing.Point(-1800,-1800)
$quickForm.ShowInTaskbar=$false
$quickForm.Show()
$apply.Invoke($null,[object[]]@($quickPage)) | Out-Null
[Windows.Forms.Application]::DoEvents()
$quickAction=[Windows.Forms.Button]($quickPage.Controls.Find('btnQuickSections',$true)[0])
$drawInQuick=[Windows.Forms.Button]($quickPage.Controls.Find('btnDrawSections',$true)[0])
if($null -eq $quickAction -or !$quickAction.Visible -or ($null -ne $drawInQuick -and $drawInQuick.Visible)) {
  throw 'Independent quick-section page must show quick action and hide calculation section draw.'
}
$quickImage=New-Object Drawing.Bitmap(590,620)
$quickForm.DrawToBitmap($quickImage,(New-Object Drawing.Rectangle(0,0,590,620)))
$quickImage.Save((Join-Path $out 'UI_QUICK_SECTION_LAYOUT.png'),[Drawing.Imaging.ImageFormat]::Png)
$quickImage.Dispose()
$quickForm.Close();$quickForm.Dispose()
@{scope='Real WinForms SectionPage and QuickSectionPage; no AutoCAD host';
  change_station='20 to 10m invalidates old geometry';z_axis_ticks='15m without station change';
  tt34_preset='20m';section_axes='custom 2000/500; TT34 1000/1000';
  calculation_buttons='preview and draw; quick hidden';quick_page='quick visible; calculation draw hidden';
  button_gap_px=$gap;status='PASS';source_sha=$env:SOURCE_SHA;autocad_runtime='NOT TESTED'} |
  ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $out 'UI_SECTION_PARAMETERS.json')
# End-to-end WinForms sizing check for the real survey-input panel.
# This catches the exact field failure: global theme forces min button 36px
# while the TableLayoutPanel cell is only 34px, clipping and overlapping rows.
# It also checks that all CAD picker labels fit on one line.
$dataType=$assembly.GetType('MiningVolume2023.UI.DataPage',$true)
$dataPage=[Windows.Forms.Control][Activator]::CreateInstance($dataType)
$dataForm=New-Object Windows.Forms.Form
$dataForm.ClientSize=New-Object Drawing.Size(800,680)
$dataForm.StartPosition=[Windows.Forms.FormStartPosition]::Manual
$dataForm.Location=New-Object Drawing.Point(-1800,-1800)
$dataForm.ShowInTaskbar=$false
$dataPage.Dock=[Windows.Forms.DockStyle]::Fill
$dataForm.Controls.Add($dataPage)
$apply.Invoke($null,[object[]]@($dataPage)) | Out-Null
$dataForm.Show()
[Windows.Forms.Application]::DoEvents()
$sourceFields=[Reflection.BindingFlags]'NonPublic,Instance'
$sourceTable=[Windows.Forms.TableLayoutPanel]$dataType.GetField('_sourceTable',$sourceFields).GetValue($dataPage)
$chooseControls=[Windows.Forms.Control[]]$dataType.GetField('_sourceChoose',$sourceFields).GetValue($dataPage)
$loadControls=[Windows.Forms.Control[]]$dataType.GetField('_sourceLoad',$sourceFields).GetValue($dataPage)
$types=[Windows.Forms.CheckedListBox]$dataType.GetField('_types',$sourceFields).GetValue($dataPage)
$sourceBox=[Windows.Forms.GroupBox]$dataType.GetField('_sourceBox',$sourceFields).GetValue($dataPage)
$dataWidths=@(800,340)
$dataDetails=@()
foreach($clientWidth in $dataWidths) {
  $dataForm.ClientSize=New-Object Drawing.Size($clientWidth,680)
  $dataPage.Size=New-Object Drawing.Size($clientWidth,680)
  $dataPage.PerformLayout()
  [Windows.Forms.Application]::DoEvents()
  $sourceTable.PerformLayout()
  foreach($ctrl in @($chooseControls[0],$chooseControls[1],$loadControls[0],$loadControls[1])) {
    $actual=[Windows.Forms.Control]$ctrl
    if($actual.Height -gt 34) {
      throw "CAD picker overflow: $($actual.Text), height=$($actual.Height), viewport=$clientWidth"
    }
    if($actual.MinimumSize.Height -gt 28) {
      throw "CAD picker minimum height is above compact contract: $($actual.Text)"
    }
    $required=[Windows.Forms.TextRenderer]::MeasureText(
      $actual.Text,$actual.Font,
      (New-Object Drawing.Size(1000,100)),
      [Windows.Forms.TextFormatFlags]::SingleLine).Width + $actual.Padding.Horizontal
    if($required -gt $actual.Width) {
      throw "CAD picker text does not fit at $clientWidth px: $($actual.Text) needs $required, has $($actual.Width)"
    }
    if($actual.Bottom -gt $sourceTable.ClientSize.Height + 2) {
      throw "CAD picker extends below its source table: $($actual.Text)"
    }
  }
  if($types.HorizontalScrollbar) { throw 'TIN source-type list has unwanted horizontal scrolling.' }
  if($types.MultiColumn) {
    $rows=[Math]::Max(1,[Math]::Floor($types.ClientSize.Height / [Math]::Max(1,$types.ItemHeight)))
    $neededColumns=[Math]::Ceiling($types.Items.Count / $rows)
    if($neededColumns -gt 2) {
      throw "TIN types need $neededColumns columns, only 2 fit without horizontal scroll at $clientWidth px."
    }
  }
  $dataDetails+=@{
    palette_width=$clientWidth;source_width=$sourceBox.Width;
    source_height=$sourceBox.Height;source_row_count=$sourceTable.RowCount;
    pick_width=$chooseControls[0].Width;pick_height=$chooseControls[0].Height;
    type_multi_column=$types.MultiColumn;status='PASS'
  }
}
$dataForm.Close();$dataForm.Dispose()
@{scope='Real WinForms DataPage after UiTheme, 800px and 340px widths';
  widths=$dataDetails;source_sha=$env:SOURCE_SHA;autocad_runtime='NOT TESTED'} |
  ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $out 'UI_DATA_SOURCE_BUTTONS.json')

Write-Host 'PASS: separate real Windows calculation section and quick-section action layouts'
Write-Host 'PASS: actual Windows WinForms buttons, text contrast >=4.5; AutoCAD runtime NOT TESTED'
