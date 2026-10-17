// QA artifacts only, not part of the product/runtime. Run with @oai/artifact-tool.
import fs from 'node:fs/promises';
import path from 'node:path';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const [qaPath, reportPath, outputDir] = process.argv.slice(2);
if (!outputDir) throw new Error('Expected qa-data.json, source report directory, output directory');
await fs.mkdir(outputDir,{recursive:true});
const qa=JSON.parse(await fs.readFile(qaPath,'utf8'));
const readLines=async name=>(await fs.readFile(path.join(reportPath,name),'utf8')).trim().split('\n').map(JSON.parse);
const production=await readLines('klmb-production.jsonl');
const oracle=(await readLines('klmb-geos-oracle.jsonl')).filter(x=>x.cell);
const lookup=new Map(production.map(x=>[x.region+':'+x.id,x]));
const source='Source SHA: '+qa.source_sha;
const previews=[];
const sheets=new Map();

function sheet(wb,name,title,headers,rows,widths) {
  const s=wb.worksheets.add(name);s.showGridLines=false;
  if(!sheets.has(wb))sheets.set(wb,[]);sheets.get(wb).push(name);
  const all=s.getRangeByIndexes(0,0,rows.length+7,headers.length);
  all.format.font={name:'Arial',size:12,color:'#000000'};
  all.format.verticalAlignment='center';
  s.getRange('A2').values=[[title]];s.getRange('A2').format.font={name:'Arial',size:14,bold:true};
  s.getRange('A3').values=[[source]];s.getRange('A4').values=[['HOST NOT TESTED — chưa đủ điều kiện phát hành']];
  s.getRange('A4').format.font={name:'Arial',size:12,color:'#000000'};
  s.getRangeByIndexes(5,0,1,headers.length).values=[headers];
  s.getRangeByIndexes(5,0,1,headers.length).format={font:{name:'Arial',size:12,bold:true,color:'#000000'},wrapText:true,rowHeight:44};
  s.getRangeByIndexes(6,0,rows.length,headers.length).values=rows;
  s.getRangeByIndexes(6,0,rows.length,headers.length).format.rowHeight=32;
  s.getRangeByIndexes(5,0,rows.length+1,headers.length).format.borders={preset:'all',style:'thin',color:'#000000'};
  widths.forEach((width,i)=>s.getRangeByIndexes(0,i,rows.length+7,1).format.columnWidthPx=width);
  s.freezePanes.freezeRows(6);
  return s;
}
async function save(wb,name) {
  wb.recalculate();
  const errors=await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#SPILL!',options:{useRegex:true,maxResults:20},maxChars:2000});
  console.log(name,errors.ndjson);
  const meta=await wb.inspect({kind:'sheet',include:'id,name'});
  await fs.writeFile(path.join(outputDir,name+'.inspect.jsonl'),meta.ndjson);
  if(name==='IMSAT_V2_MATH_VALIDATION' && !wb.worksheets.getItem('The tich').getRange('K7:K44').values.flat().every(x=>x==='PASS'))
    throw new Error('Workbook volume residual gate failed');
  for(let i=0;i<sheets.get(wb).length;i++) {
    const s=wb.worksheets.getItemAt(i);
    const image=await wb.render({sheetName:s.name,range:'A1:H16',scale:1.3,format:'png'});
    const file=name+'_'+i+'.png';await fs.writeFile(path.join(outputDir,file),new Uint8Array(await image.arrayBuffer()));previews.push(file);
  }
  await (await SpreadsheetFile.exportXlsx(wb)).save(path.join(outputDir,name+'.xlsx'));
}

const matrix=Workbook.create();
const grouped=[];
for(let year=2012;year<=2026;year++) {
  const group=qa.matrix.filter(x=>x.year===year);
  grouped.push([year,group.map(x=>x.runtime).join(' / '),group.every(x=>x.compile==='COMPILE_PASS')?'COMPILE PASS':'NOT TESTED',
    'NOT TESTED','BLOCKED',group.map(x=>x.api).join(' / '),group[0].issue]);
}
const years=sheet(matrix,'15 doi CAD','IMSAT VOLUME V2 — ma trận tương thích',
  ['AutoCAD','Runtime','Biên dịch','AutoCAD thực','Phát hành','API Autodesk','Vấn đề'],grouped,[85,265,170,170,140,190,100]);
years.getRange('B20:B21').format.wrapText=true;years.getRange('A20:G21').format.rowHeight=52;
const details=qa.matrix.map(x=>[x.year,x.flavor,x.series,x.dll,x.update||'Theo release',x.compile,x.host,x.evidence||'Thiếu adapter / SDK']);
const flavors=sheet(matrix,'Flavor','Flavor và bằng chứng build',
  ['AutoCAD','Flavor','Series','DLL entry','Patch','COMPILE','HOST','CI job'],details,[85,210,85,340,220,150,130,540]);
flavors.getRange('A7:H23').format.wrapText=true;flavors.getRange('A7:H23').format.rowHeight=56;
await save(matrix,'IMSAT_V2_RELEASE_MATRIX');

const math=Workbook.create();
const volRows=oracle.map(o=>{const p=lookup.get(o.region+':'+o.cell);return [o.region,o.cell,p.cut,o.oracle_cut,null,null,p.fill,o.oracle_fill,null,null,null];});
const v=sheet(math,'The tich','KL mb.dxf — đối chứng thể tích từng ô',
 ['Ranh','Ô (handle)','C# đào (m³)','GEOS đào (m³)','Sai số tuyệt đối','Sai số tương đối','C# đắp (m³)','GEOS đắp (m³)','Sai số tuyệt đối','Sai số tương đối','Kết quả'],
 volRows,[80,90,135,135,135,135,135,135,135,135,105]);
v.getRange('A5').values=[['GEOS/NumPy độc lập; chấp nhận sai số thể tích ≤ 0,0001 m³. Không dùng nhãn CAD làm chuẩn.']];
for(let i=0;i<volRows.length;i++) {
 const r=i+7;
 v.getRange('E'+r+':F'+r).formulas=[[`=ABS(C${r}-D${r})`,`=IF(D${r}=0,IF(E${r}=0,0,"n.a."),E${r}/ABS(D${r}))`]];
 v.getRange('I'+r+':K'+r).formulas=[[`=ABS(G${r}-H${r})`,`=IF(H${r}=0,IF(I${r}=0,0,"n.a."),I${r}/ABS(H${r}))`,`=IF(AND(E${r}<=0.0001,I${r}<=0.0001),"PASS","FAIL")`]];
}
v.getRange('C7:J44').setNumberFormat('0.000000E+00');
const areaRows=oracle.map(o=>{const p=lookup.get(o.region+':'+o.cell);return [o.region,o.cell,p.area,o.oracle_area,null,p.covered,o.oracle_covered,null,p.noData];});
const a=sheet(math,'Dien tich','KL mb.dxf — diện tích tính và vùng phủ',
 ['Ranh','Ô (handle)','C# ranh (m²)','GEOS ranh (m²)','Sai số (m²)','C# phủ (m²)','GEOS phủ (m²)','Sai số (m²)','No-data (m²)'],areaRows,[80,90,140,140,140,140,140,140,140]);
a.getRange('A5').values=[['Sai số diện tích ≤ 0,00001 m². Ranh 233B và 2343 được kiểm tra riêng, không cộng hai ranh.']];
for(let i=0;i<areaRows.length;i++){const r=i+7;a.getRange('E'+r).formulas=[[`=ABS(C${r}-D${r})`]];a.getRange('H'+r).formulas=[[`=ABS(F${r}-G${r})`]];}
a.getRange('C7:I44').setNumberFormat('0.000000E+00');
const sq=JSON.parse(await fs.readFile(path.join(reportPath,'sq-aggregate.json'),'utf8')).filter(x=>x.phase==='complete');
const stress=(await readLines('stress-core.jsonl')).filter(x=>x.benchmark==='grid_cells');
const bench=[...sq.map(x=>['TIN SQ '+x.role.toUpperCase(),x.triangles,'Tam giác',x.ms/1000,'Core Linux, không render CAD']),...stress.map(x=>['Lưới giải tích',x.cells,'Ô',x.ms/1000,'Core Linux; đào = 3 × số ô m³'])];
sheet(math,'Hieu nang','Đo hiệu năng lõi — không phải tốc độ AutoCAD',
 ['Bộ thử','Số lượng','Đơn vị','Giây','Phạm vi'],bench,[230,150,120,120,390]);
await save(math,'IMSAT_V2_MATH_VALIDATION');

const issues=Workbook.create();
const audit=await fs.readFile(path.join(reportPath,'../../IMSAT_V2_5_ROUND_AUDIT.md'),'utf8');
const issueRows=audit.split('\n').filter(x=>x.startsWith('| MC-')).map(x=>x.split('|').slice(1,-1).map(s=>s.trim().replaceAll('`','')));
const is=sheet(issues,'Loi va ton tai','IMSAT VOLUME V2 — lỗi đã sửa và còn mở',
 ['Mã','Mức','Nội dung','Trước / sau','Bằng chứng / hành động'],issueRows,[90,65,360,310,560]);
is.getRangeByIndexes(6,2,issueRows.length,3).format.wrapText=true;
is.getRangeByIndexes(6,0,issueRows.length,5).format.rowHeight=60;
await save(issues,'ISSUES_CLOSED_OPEN');
await fs.writeFile(path.join(outputDir,'previews.json'),JSON.stringify(previews,null,2));
