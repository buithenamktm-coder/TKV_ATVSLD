from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
def read(p):return (ROOT/p).read_text(encoding='utf8')
def contrast(a,b):
 def lum(c):
  v=[x/255 for x in c]; v=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in v]
  return sum(x*w for x,w in zip(v,[.2126,.7152,.0722]))
 a,b=sorted([lum(a),lum(b)]);return (b+.05)/(a+.05)
def test_actual_button_tokens_have_readable_text_and_visible_boundary():
 s=read('src/MiningVolume.Plugin2023/UI/UiTheme.cs')
 def rgb(name):return tuple(map(int,re.search(r'Color '+name+r' = Color.FromArgb\((\d+), (\d+), (\d+)\)',s).groups()))
 assert contrast(rgb('ButtonText'),rgb('ButtonSecondary'))>=4.5
 assert contrast((255,255,255),rgb('Accent'))>=4.5
 assert contrast(rgb('ButtonBorder'),rgb('Canvas'))>=3
 assert contrast(rgb('Danger'),(255,246,246))>=4.5
 assert 'button.UseVisualStyleBackColor = false' in s
 assert 'button.BackColor = ButtonSecondary' in s
 assert 'button.ForeColor = ButtonText' in s

def test_pair_transaction_cannot_delete_both_old_tins_in_failure_handler():
 s=read('src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs')
 pair=s.split('public static void DrawTinPair',1)[1].split('public static void DrawTin(ModelRole',1)[0]
 assert 'InvalidateTin(' not in pair
 assert pair.index('TinCadRenderer.ReplacePair(')<pair.index('AdoptTin(')
 r=read('src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs')
 pair=r.split('public static int[] ReplacePair',1)[1].split('public static int ClearPair(',1)[0]
 assert pair.count('StartTransaction()')==1
 assert pair.count('ReplaceInTransaction(')==2
 assert pair.index('designLayer, design, 3')<pair.index('tr.Commit()')
 assert 'TIN thứ hai lỗi: transaction giữ nguyên cả HT và TK' in read('src/MiningVolume.Plugin2023/Services/SelfTestService.cs')

def test_both_pages_only_build_one_tin_per_action():
 for name in ['DataPage.cs','ModelPage.cs']:
  s=read('src/MiningVolume.Plugin2023/UI/'+name)
  assert '_buildBoth' not in s and '_buildPair' not in s
  assert 'BuildPairTinAsync' not in s and 'BuildBothTinAsync' not in s
 data=read('src/MiningVolume.Plugin2023/UI/DataPage.cs')
 assert 'BuildSingleTinAsync(ModelRole.Existing)' in data
 assert 'BuildSingleTinAsync(ModelRole.Design)' in data
 method=data.split('private async Task BuildSingleTinAsync',1)[1].split('private async Task<SurfaceBuildResult>',1)[0]
 assert method.index('ReferenceEquals(activeDoc,')<method.index('SurfaceWorkflowService.DrawTin(role,build)')
 assert '(build.Tin.Triangles as IDisposable)?.Dispose()' in method
 assert '_buildCts.Dispose()' in method

def test_normalization_cell_lookup_does_not_allocate_strings_per_neighbor():
 s=read('src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs')
 assert 'Dictionary<XYCell, List<int>>' in s
 assert 'new XYCell(ix + dx, iy + dy)' in s
 assert 'Dictionary<string, List<int>>' not in s.split('var grid = new Dictionary<XYCell, List<int>>',1)[1]
