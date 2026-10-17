"""IMSAT CAD V1.0 Preview: standalone bilingual DXF drafting application.

This app is an independent proof-of-concept. It is not QCAD, DWG compatible,
or suitable for engineering sign-off without additional validation.
"""
from __future__ import annotations

import io
import math
import sys
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

import ezdxf
from PIL import Image, ImageTk
from imsatcad.autocad_commands import (
    resolve_alias, execute as execute_cad_command, CommandError, DRAW_COMMANDS, EDIT_COMMANDS
)

ROOT = Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent))
PALETTE = {'bg':'#101d2d','panel':'#182a40','line':'#2b445e','text':'#e8f3fc','muted':'#9db0c2','accent':'#24a0d8','canvas':'#091421'}
TRANS = {
 'vi': {
    'file':'Tệp','new':'Tạo mới','open':'Mở DXF','save':'Lưu','save_as':'Lưu thành','view':'Hiển thị','fit':'Hiện toàn bộ','edit':'Hiệu chỉnh','undo':'Hoàn tác','redo':'Làm lại',
    'language':'Ngôn ngữ','tools':'Công cụ','select':'Chọn','line':'Đường thẳng','polyline':'Đường gấp khúc','circle':'Đường tròn','delete':'Xóa',
    'layers':'Lớp bản vẽ','layer':'Lớp đang vẽ','new_layer':'Thêm lớp','enter_layer':'Tên lớp mới:',
    'about':'Giới thiệu','title':'IMSAT CAD V1.0 — Bản kiểm thử','welcome':'Sẵn sàng. Mở DXF hoặc chọn công cụ vẽ.',
    'unsupported':'Chỉ hỗ trợ DXF trong phiên bản này. DWG chưa được hỗ trợ.',
    'save_confirm':'Bản vẽ có thay đổi. Lưu lại trước khi tiếp tục?',
    'error':'Lỗi','info':'Thông báo','saved':'Đã lưu bản vẽ: ','opened':'Đã mở: ','not_saved':'Có thay đổi chưa lưu',
    'pick_line':'Chọn điểm đầu, sau đó chọn điểm cuối. Esc để hủy.',
    'pick_circle':'Chọn tâm rồi chọn điểm xác định bán kính. Esc để hủy.',
    'pick_poly':'Nhấn để thêm đỉnh. Enter hoặc nhấp đúp để kết thúc. Esc để hủy.',
    'pick_select':'Nhấn vào đối tượng để chọn. Delete để xóa đối tượng đang chọn.',
    'save_as_required':'Hãy dùng Lưu thành để tạo tệp DXF mới. Tệp gốc sẽ không bị ghi đè.',
    'limit':'Bản vẽ có nhiều đối tượng; chỉ hiển thị một phần để tránh treo giao diện.',
    'about_text':'IMSAT CAD V1.0 Preview\nNền tảng DXF 2D độc lập\nViệt / Anh · Win64\nChưa hỗ trợ DWG, chưa có dựng TIN.',
    'entity':'Đối tượng','coord':'Tọa độ','units':'Đơn vị','layer_prompt':'Nhập tên lớp',
    'command':'Dòng lệnh', 'command_hint':'Nhập lệnh AutoCAD: L, PL, C, REC, M, CO, TR…',
    'command_error':'Lệnh chưa hỗ trợ hoặc chưa đúng tham số: ',
    'replace_confirm':'Tệp {name} đã tồn tại. Anh có muốn thay thế không?',
    'no_selection':'Chưa chọn đối tượng.','invalid_layer':'Tên lớp không hợp lệ.', 'new_doc':'Bản vẽ mới',
 },
 'en': {
    'file':'File','new':'New','open':'Open DXF','save':'Save','save_as':'Save As','view':'View','fit':'Zoom Extents','edit':'Edit','undo':'Undo','redo':'Redo',
    'language':'Language','tools':'Tools','select':'Select','line':'Line','polyline':'Polyline','circle':'Circle','delete':'Delete',
    'layers':'Layers','layer':'Current layer','new_layer':'Add layer','enter_layer':'New layer name:',
    'about':'About','title':'IMSAT CAD V1.0 — Preview','welcome':'Ready. Open DXF or choose a drawing tool.',
    'unsupported':'This version supports DXF only. DWG is not yet supported.',
    'save_confirm':'Drawing has unsaved changes. Save before continuing?',
    'error':'Error','info':'Information','saved':'Saved drawing: ','opened':'Opened: ','not_saved':'Unsaved changes',
    'pick_line':'Pick start point, then end point. Esc cancels.',
    'pick_circle':'Pick center then radius point. Esc cancels.',
    'pick_poly':'Click to add vertices. Enter or double-click to finish. Esc cancels.',
    'pick_select':'Click an entity to select. Delete removes the selected entity.',
    'save_as_required':'Use Save As to create a new DXF. Original files are never overwritten.',
    'limit':'Large drawing: preview has been limited to keep the interface responsive.',
    'about_text':'IMSAT CAD V1.0 Preview\nStandalone 2D DXF drafting\nVietnamese / English · Win64\nDWG and TIN triangulation are not supported.',
    'entity':'Entities','coord':'Coordinates','units':'Units','layer_prompt':'Enter layer name',
    'command':'Command line', 'command_hint':'Enter AutoCAD command: L, PL, C, REC, M, CO, TR…',
    'command_error':'Unsupported command or invalid arguments: ',
    'replace_confirm':'File {name} already exists. Replace it?',
    'no_selection':'No selected entity.','invalid_layer':'Invalid layer name.', 'new_doc':'New drawing',
 }
}

class CadApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.lang = tk.StringVar(value='vi')
        self.doc = ezdxf.new('R2013', setup=True)
        self.doc.header['$INSUNITS'] = 6  # metres
        self.doc_path: Path | None = None
        self.dirty = False
        self.tool = 'select'
        self.current_layer = tk.StringVar(value='0')
        self.selection = None
        self.point_start = None
        self.poly_points = []
        self.undo_stack: list[str] = []
        self.redo_stack: list[str] = []
        self.view_scale = 1.0
        self.offset = (0.0, 0.0)
        self.pan_start = None
        self.entity_canvas = {}
        self.geometry_limit = 30000
        self.geometry_shown = 0
        self.configure(background=PALETTE['bg'])
        self.minsize(950, 600)
        self.geometry('1320x810')
        self.report_callback_exception = self.on_tk_error
        self.icon_img = None
        png = ROOT / 'assets' / 'imsat_icon.png'
        if png.exists():
            self.icon_img = ImageTk.PhotoImage(Image.open(png).convert('RGBA').resize((46,46), Image.Resampling.LANCZOS))
            self.iconphoto(True, self.icon_img)
        self.bind('<Control-o>', lambda _: self.open_file())
        self.bind('<Control-s>', lambda _: self.save_file())
        self.bind('<Control-n>', lambda _: self.new_file())
        self.bind('<Control-z>', lambda _: self.undo())
        self.bind('<Control-y>', lambda _: self.redo())
        self.bind('<Delete>', lambda _: self.delete_selected())
        self.bind('<Escape>', lambda _: self.cancel_tool())
        self.bind('<Return>', lambda _: self.finish_polyline())
        self.bind('<Key>', self.type_command_when_canvas_focused)
        self.protocol('WM_DELETE_WINDOW', self.close_app)
        self.style = ttk.Style(self)
        self.style.theme_use('clam')
        self.style.configure('TCombobox', font=('Segoe UI', 10))
        self.build_ui()
        self.render()

    def tr(self, key):
        return TRANS[self.lang.get()][key]

    def build_ui(self):
        for child in self.winfo_children(): child.destroy()
        self.title(self.tr('title'))
        top = tk.Frame(self, bg=PALETTE['panel'], height=69)
        top.pack(fill='x')
        if self.icon_img:
            tk.Label(top, image=self.icon_img, bg=PALETTE['panel']).pack(side='left', padx=(12, 5), pady=6)
        tk.Label(top, text='IMSAT CAD', fg=PALETTE['text'], bg=PALETTE['panel'], font=('Segoe UI', 18, 'bold')).pack(side='left', padx=4)
        tk.Label(top, text='V1.0 PREVIEW', fg=PALETTE['accent'], bg=PALETTE['panel'], font=('Segoe UI', 9, 'bold')).pack(side='left', padx=8)
        self.lang_label = tk.StringVar(value='Tiếng Việt' if self.lang.get()=='vi' else 'English')
        lang_box = ttk.Combobox(top, textvariable=self.lang_label, values=['Tiếng Việt','English'], state='readonly', width=13)
        lang_box.pack(side='right', padx=18)
        lang_box.bind('<<ComboboxSelected>>', self.select_language)
        tk.Label(top, text=self.tr('language'), fg=PALETTE['muted'], bg=PALETTE['panel']).pack(side='right')

        menus = tk.Menu(self)
        fm = tk.Menu(menus, tearoff=False)
        for label, command, accel in [('new',self.new_file,'Ctrl+N'), ('open',self.open_file,'Ctrl+O'), ('save',self.save_file,'Ctrl+S'), ('save_as',self.save_as,'')]:
            fm.add_command(label=self.tr(label), command=command, accelerator=accel)
        menus.add_cascade(label=self.tr('file'), menu=fm)
        em = tk.Menu(menus, tearoff=False)
        for label,command in [('undo',self.undo),('redo',self.redo),('delete',self.delete_selected)]:
            em.add_command(label=self.tr(label),command=command)
        menus.add_cascade(label=self.tr('edit'), menu=em)
        vm = tk.Menu(menus,tearoff=False)
        vm.add_command(label=self.tr('fit'),command=self.zoom_extents)
        menus.add_cascade(label=self.tr('view'),menu=vm)
        menus.add_command(label=self.tr('about'),command=lambda: messagebox.showinfo(self.tr('about'),self.tr('about_text')))
        self.config(menu=menus)

        toolbar = tk.Frame(self, bg=PALETTE['bg'], padx=11, pady=8)
        toolbar.pack(fill='x')
        for name in ['select','line','polyline','circle']:
            tk.Button(toolbar, text=self.tr(name), command=lambda t=name:self.set_tool(t), relief='flat',
                      bg=PALETTE['accent'] if self.tool==name else PALETTE['panel'], fg='white',
                      activebackground=PALETTE['accent'], activeforeground='white', bd=0,
                      font=('Segoe UI',10), padx=15, pady=8).pack(side='left', padx=3)
        tk.Button(toolbar, text=self.tr('fit'), command=self.zoom_extents, bg=PALETTE['panel'],fg='white',relief='flat',padx=14,pady=8).pack(side='left',padx=10)
        tk.Button(toolbar, text=self.tr('new_layer'), command=self.add_layer, bg=PALETTE['panel'],fg='white',relief='flat',padx=10,pady=8).pack(side='right',padx=8)
        self.layer_box = ttk.Combobox(toolbar, textvariable=self.current_layer, values=self.layers(), state='readonly', width=19)
        self.layer_box.pack(side='right', padx=5)
        tk.Label(toolbar,text=self.tr('layer'),fg=PALETTE['muted'],bg=PALETTE['bg']).pack(side='right')

        body = tk.Frame(self,bg=PALETTE['bg'])
        body.pack(fill='both',expand=True,padx=12,pady=(0,9))
        self.canvas = tk.Canvas(body, bg=PALETTE['canvas'], highlightthickness=1, highlightbackground=PALETTE['line'], cursor='crosshair')
        self.canvas.pack(fill='both',expand=True)
        self.canvas.bind('<Configure>',lambda _: self.render())
        self.canvas.bind('<Button-1>',self.click)
        self.canvas.bind('<Double-Button-1>',self.double_click)
        self.canvas.bind('<Button-2>',self.pan_begin)
        self.canvas.bind('<B2-Motion>',self.pan_move)
        self.canvas.bind('<Button-3>',self.pan_begin)
        self.canvas.bind('<B3-Motion>',self.pan_move)
        self.canvas.bind('<MouseWheel>',self.wheel)
        self.canvas.bind('<Button-4>',lambda e:self.scale_about(1.2,e.x,e.y))
        self.canvas.bind('<Button-5>',lambda e:self.scale_about(1/1.2,e.x,e.y))
        self.canvas.bind('<Motion>',self.mouse_move)
        self.canvas.bind('<Button-1>',lambda e:(self.canvas.focus_set(), self.click(e)))
        command_bar = tk.Frame(self, bg=PALETTE['panel'], padx=12, pady=6)
        command_bar.pack(fill='x')
        tk.Label(command_bar, text=self.tr('command') + ' >', fg=PALETTE['accent'],
                 bg=PALETTE['panel'], font=('Consolas', 10, 'bold')).pack(side='left', padx=(0,10))
        self.command_text = tk.StringVar()
        self.command_entry = tk.Entry(command_bar, textvariable=self.command_text,
                                      font=('Consolas', 11), bg=PALETTE['canvas'],
                                      fg=PALETTE['text'], insertbackground='white', bd=0,
                                      relief='flat')
        self.command_entry.pack(side='left', fill='x', expand=True, ipady=5)
        self.command_entry.insert(0, '')
        self.command_entry.bind('<Return>', self.on_command_enter)
        self.command_entry.bind('<Escape>', lambda e: self.canvas.focus_set())
        footer = tk.Frame(self,bg=PALETTE['panel'],height=29)
        footer.pack(fill='x')
        self.message = tk.Label(footer,text=self.tr('welcome'),anchor='w',fg=PALETTE['muted'],bg=PALETTE['panel'],font=('Segoe UI',9))
        self.message.pack(side='left',fill='x',expand=True,padx=12,pady=5)
        self.coord = tk.Label(footer,text='',fg=PALETTE['muted'],bg=PALETTE['panel'],font=('Consolas',9))
        self.coord.pack(side='right',padx=12)
        self.refresh_title()

    def type_command_when_canvas_focused(self, event):
        # Standard CAD practice: typing a command starts the command line.
        # Avoid consuming shortcut keys or keystrokes in other input widgets.
        focused = self.focus_get()
        if focused not in (self.canvas, self):
            return
        if event.state & 0x0004 or event.state & 0x0008:
            return
        if len(event.char)==1 and event.char.isprintable() and not event.char.isspace():
            self.command_entry.focus_set()
            self.command_entry.insert('end', event.char)
            return 'break'

    def on_command_enter(self, event=None):
        line=self.command_text.get().strip()
        self.command_text.set('')
        if not line:
            self.canvas.focus_set()
            return 'break'
        self.execute_command(line)
        self.canvas.focus_set()
        return 'break'

    def execute_command(self, command: str):
        from imsatcad.autocad_commands import Result
        from shlex import split as split_args
        try:
            args=split_args(command)
            if not args:return
            name=resolve_alias(args[0])
            if name in ('UNDO','REDO') and len(args)==1:
                (self.undo if name=='UNDO' else self.redo)();return
            if name=='ZOOM' and (len(args)==1 or args[1].upper() in ('E','EXTENTS','A','ALL')):
                self.zoom_extents();return
            if name=='NEW' and len(args)==1:self.new_file();return
            if name=='OPEN' and len(args)==1:self.open_file();return
            if name in ('SAVEAS','QSAVE') and len(args)==1:
                (self.save_as if name=='SAVEAS' else self.save_file)();return
            if name=='LAYER' and len(args)==1:
                self.add_layer();return
            if name in ('LINE','PLINE','CIRCLE','RECTANG') and len(args)==1:
                mapping={'LINE':'line','PLINE':'polyline','CIRCLE':'circle','RECTANG':'rectangle'}
                self.set_tool(mapping[name]);return
            if name in ('DIST','ID'):
                result=execute_cad_command(self.doc,command,selected_handle=self.selection,
                                           layer=self.current_layer.get())
                self.message.config(text=result.message);return
            if name in DRAW_COMMANDS | EDIT_COMMANDS:
                if not self.snapshot():
                    raise CommandError('Drawing too large for safe undo snapshot')
                old_dirty=self.dirty
                try:
                    result=execute_cad_command(self.doc,command,selected_handle=self.selection,
                                               layer=self.current_layer.get())
                except Exception:
                    # Transaction rollback: never leave a partial edit in modelspace.
                    previous=self.undo_stack.pop()
                    self.doc=ezdxf.read(io.StringIO(previous))
                    self.dirty=old_dirty
                    self.layer_box.config(values=self.layers())
                    self.render();self.refresh_title()
                    raise
                if result.changed:
                    self.mark_modified()
                if result.message:
                    self.message.config(text=result.message)
                else:
                    self.message.config(text='Command: '+result.name)
                return
            raise CommandError('No implementation for '+name)
        except Exception as exc:
            self.message.config(text=self.tr('command_error')+str(exc))

    def layers(self):
        return sorted([x.dxf.name for x in self.doc.layers])

    def select_language(self, event=None):
        self.lang.set('vi' if self.lang_label.get()=='Tiếng Việt' else 'en')
        self.change_language()

    def change_language(self):
        self.build_ui()
        self.render()

    def set_tool(self,tool):
        self.tool=tool
        self.point_start=None
        self.poly_points=[]
        self.build_ui()
        self.render()
        self.set_message('pick_'+tool if tool in ('line','circle','polyline','select') else 'welcome')

    def set_message(self,key):
        self.message.config(text=TRANS[self.lang.get()].get(key,key))

    def refresh_title(self):
        name = self.doc_path.name if self.doc_path else self.tr('new_doc')
        self.title(f"{self.tr('title')} — {name}{' *' if self.dirty else ''}")

    def confirm_leave(self):
        if not self.dirty:return True
        answer=messagebox.askyesnocancel(self.tr('info'),self.tr('save_confirm'))
        if answer is None:return False
        if answer:
            return self.save_file()
        return True

    def new_file(self):
        if not self.confirm_leave():return
        self.doc=ezdxf.new('R2013',setup=True)
        self.doc.header['$INSUNITS']=6
        self.doc_path=None;self.dirty=False;self.selection=None
        self.undo_stack.clear();self.redo_stack.clear()
        self.current_layer.set('0')
        self.build_ui();self.zoom_extents()

    def open_file(self):
        if not self.confirm_leave():return
        path=filedialog.askopenfilename(filetypes=[('DXF files','*.dxf'),('All files','*.*')])
        if not path:return
        if Path(path).suffix.lower()!='.dxf':
            messagebox.showwarning(self.tr('info'),self.tr('unsupported'));return
        try:
            doc=ezdxf.readfile(path)
            audit=doc.audit()
            if audit.errors:raise ValueError(f'DXF has {len(audit.errors)} audit error(s).')
        except Exception as exc:
            messagebox.showerror(self.tr('error'),str(exc));return
        self.doc=doc;self.doc_path=Path(path);self.dirty=False;self.selection=None
        self.undo_stack.clear();self.redo_stack.clear()
        self.current_layer.set('0')
        self.build_ui();self.zoom_extents()
        self.set_message(self.tr('opened')+path)

    def save_file(self):
        # Never alter original user's file: use Save As to create a separate output.
        return self.save_as()

    def save_as(self):
        path=filedialog.asksaveasfilename(defaultextension='.dxf',filetypes=[('DXF files','*.dxf')],initialfile='imsat_export.dxf')
        if not path:return False
        dest=Path(path).resolve()
        if self.doc_path and dest==self.doc_path.resolve():
            messagebox.showwarning(self.tr('info'),self.tr('save_as_required'));return False
        if dest.exists() and not messagebox.askyesno(self.tr('info'),self.tr('replace_confirm').format(name=dest.name)):
            return False
        try:
            from imsatcad.dxf_backend import DxfWorkspace
            ws=DxfWorkspace(self.doc,self.doc_path)
            ws.save_as(dest,overwrite=dest.exists())
        except Exception as exc:
            messagebox.showerror(self.tr('error'),str(exc));return False
        self.doc_path=dest;self.dirty=False;self.refresh_title()
        self.set_message(self.tr('saved')+str(dest))
        return True

    def snapshot(self):
        if len(self.doc.modelspace())>40000:
            self.set_message(self.tr('limit'))
            return False
        buf=io.StringIO()
        self.doc.write(buf)
        self.undo_stack.append(buf.getvalue())
        self.undo_stack=self.undo_stack[-15:]
        self.redo_stack.clear()
        return True

    def restore(self, serialized):
        self.doc=ezdxf.read(io.StringIO(serialized))
        self.dirty=True;self.selection=None
        self.layer_box.config(values=self.layers())
        self.render();self.refresh_title()

    def undo(self):
        if not self.undo_stack:return
        buf=io.StringIO();self.doc.write(buf)
        self.redo_stack.append(buf.getvalue())
        self.restore(self.undo_stack.pop())

    def redo(self):
        if not self.redo_stack:return
        buf=io.StringIO();self.doc.write(buf)
        self.undo_stack.append(buf.getvalue())
        self.restore(self.redo_stack.pop())

    def mark_modified(self):
        self.dirty=True;self.selection=None
        self.refresh_title();self.render()

    def add_layer(self):
        from tkinter.simpledialog import askstring
        name=askstring(self.tr('new_layer'),self.tr('enter_layer'),parent=self)
        if not name:return
        name=name.strip()
        if not name or any(x in name for x in '<>/\\":;?*|='):
            messagebox.showwarning(self.tr('info'),self.tr('invalid_layer'));return
        if name not in self.doc.layers:
            if not self.snapshot():return
            self.doc.layers.add(name)
            self.mark_modified()
        self.current_layer.set(name)
        self.layer_box.config(values=self.layers())

    def world_to_screen(self,p):
        return (p[0]*self.view_scale + self.offset[0], -p[1]*self.view_scale+self.offset[1])

    def screen_to_world(self,x,y):
        s=max(self.view_scale,1e-12)
        return ((x-self.offset[0])/s,(self.offset[1]-y)/s)

    def render(self):
        if not hasattr(self,'canvas'):return
        c=self.canvas;c.delete('all');self.entity_canvas={}
        w=max(c.winfo_width(),10);h=max(c.winfo_height(),10)
        # Grid in display space with stable reference at world origin.
        pitch=10**math.floor(math.log10(max(80/self.view_scale,1e-9)))
        step=max(10,min(2500,pitch*self.view_scale))
        if step>=25:
            ox=self.offset[0]%step;oy=self.offset[1]%step
            for x in range(int(ox),w,int(step)):
                c.create_line(x,0,x,h,fill='#152639')
            for y in range(int(oy),h,int(step)):
                c.create_line(0,y,w,y,fill='#152639')
        x0,y0=self.world_to_screen((0,0))
        if 0<=x0<=w:c.create_line(x0,0,x0,h,fill='#23485b')
        if 0<=y0<=h:c.create_line(0,y0,w,y0,fill='#23485b')
        shown=0
        for e in self.doc.modelspace():
            if shown>=self.geometry_limit:break
            layer=e.dxf.get('layer','0')
            if layer in self.doc.layers and self.doc.layers.get(layer).is_off():continue
            color='#f9b867' if self.selection==e.dxf.handle else ('#55bddc' if layer=='0' else '#aed6e9')
            handle=e.dxf.handle
            try:
                kind=e.dxftype()
                if kind=='LINE':
                    coords=(*self.world_to_screen(e.dxf.start),*self.world_to_screen(e.dxf.end))
                    c.create_line(*coords,fill=color,width=1.3,tags=('ent',handle))
                elif kind=='CIRCLE':
                    cx,cy=self.world_to_screen(e.dxf.center);r=e.dxf.radius*self.view_scale
                    c.create_oval(cx-r,cy-r,cx+r,cy+r,outline=color,tags=('ent',handle))
                elif kind=='ARC':
                    cx,cy=self.world_to_screen(e.dxf.center);r=e.dxf.radius*self.view_scale
                    c.create_arc(cx-r,cy-r,cx+r,cy+r,start=e.dxf.start_angle,extent=(e.dxf.end_angle-e.dxf.start_angle)%360,style='arc',outline=color,tags=('ent',handle))
                elif kind in ('LWPOLYLINE','POLYLINE'):
                    pts=list(e.get_points('xy') if kind=='LWPOLYLINE' else [(v.dxf.location.x,v.dxf.location.y) for v in e.vertices])
                    if len(pts)<2:continue
                    vertices=[]
                    for pt in pts:vertices.extend(self.world_to_screen(pt))
                    c.create_line(*vertices,fill=color,width=1.1,tags=('ent',handle))
                    if e.is_closed:c.create_line(*vertices[-2:],*vertices[:2],fill=color,tags=('ent',handle))
                elif kind=='3DFACE':
                    pts=[e.dxf.vtx0,e.dxf.vtx1,e.dxf.vtx2]
                    data=[]
                    for p in pts:data.extend(self.world_to_screen(p))
                    c.create_polygon(*data,fill='',outline='#367386',tags=('ent',handle))
                elif kind=='POINT':
                    x,y=self.world_to_screen(e.dxf.location)
                    c.create_oval(x-2,y-2,x+2,y+2,fill=color,outline='',tags=('ent',handle))
                elif kind in ('TEXT','MTEXT'):
                    p=e.dxf.get('insert',(0,0));x,y=self.world_to_screen(p)
                    label=e.dxf.text if kind=='TEXT' else e.plain_text()
                    c.create_text(x,y,text=label[:70],fill=color,anchor='sw',font=('Arial',9),tags=('ent',handle))
                else:continue
                shown+=1
            except (AttributeError, TypeError, ValueError, OverflowError):
                continue
        self.geometry_shown=shown
        if shown>=self.geometry_limit:self.set_message('limit')
        if self.poly_points:
            data=[]
            for pt in self.poly_points:data.extend(self.world_to_screen(pt))
            if len(data)>=4:c.create_line(*data,fill='#f9b867',width=2,dash=(5,3))
        self.set_coords()

    def set_coords(self,xy=None):
        coords=f' | X {xy[0]:.3f}   Y {xy[1]:.3f}' if xy else ''
        self.coord.config(text=f"{self.tr('entity')}: {self.geometry_shown}{coords}   |   {self.tr('units')}: m")

    def mouse_move(self,event):
        self.set_coords(self.screen_to_world(event.x,event.y))

    def click(self,event):
        world=self.screen_to_world(event.x,event.y)
        if self.tool=='select':
            ids=self.canvas.find_overlapping(event.x-5,event.y-5,event.x+5,event.y+5)
            handles=[self.canvas.gettags(i) for i in ids]
            self.selection=next((t[1] for t in reversed(handles) if len(t)>1 and t[0]=='ent'),None)
            self.render();return
        if self.tool=='polyline':
            self.poly_points.append(world)
            self.render();return
        if self.point_start is None:
            self.point_start=world
            self.set_message('pick_'+self.tool)
            return
        if not self.snapshot():return
        p=self.point_start
        msp=self.doc.modelspace()
        if self.tool=='rectangle' and abs(p[0]-world[0])>1e-12 and abs(p[1]-world[1])>1e-12:
            msp.add_lwpolyline([(p[0],p[1]),(world[0],p[1]),world,(p[0],world[1])],close=True,dxfattribs={'layer':self.current_layer.get()})
        elif self.tool=='line' and math.dist(p,world)>1e-12:
            msp.add_line(p,world,dxfattribs={'layer':self.current_layer.get()})
        elif self.tool=='circle' and math.dist(p,world)>1e-12:
            msp.add_circle(p,math.dist(p,world),dxfattribs={'layer':self.current_layer.get()})
        else:
            self.undo_stack.pop();return
        self.point_start=None;self.mark_modified()

    def double_click(self,event):
        if self.tool=='polyline':
            # Tk sends the first click of a double-click as normal click.
            self.finish_polyline()

    def finish_polyline(self):
        if self.tool!='polyline' or len(self.poly_points)<2:
            self.poly_points=[];self.render();return
        if not self.snapshot():return
        self.doc.modelspace().add_lwpolyline(self.poly_points,dxfattribs={'layer':self.current_layer.get()})
        self.poly_points=[];self.mark_modified()

    def cancel_tool(self):
        self.point_start=None;self.poly_points=[]
        self.set_tool('select')

    def delete_selected(self):
        if not self.selection:
            self.set_message('no_selection');return
        ent=self.doc.entitydb.get(self.selection)
        if ent is None:return
        if not self.snapshot():return
        self.doc.modelspace().delete_entity(ent)
        self.mark_modified()

    def wheel(self,event):
        self.scale_about(1.2 if event.delta>0 else (1/1.2),event.x,event.y)

    def scale_about(self,factor,x,y):
        world=self.screen_to_world(x,y)
        self.view_scale=max(1e-7,min(self.view_scale*factor,1e7))
        self.offset=(x-world[0]*self.view_scale,y+world[1]*self.view_scale)
        self.render()

    def pan_begin(self,event):self.pan_start=(event.x,event.y)
    def pan_move(self,event):
        if self.pan_start is None:return
        dx=event.x-self.pan_start[0];dy=event.y-self.pan_start[1]
        self.offset=(self.offset[0]+dx,self.offset[1]+dy)
        self.pan_start=(event.x,event.y);self.render()

    def zoom_extents(self):
        xs=[];ys=[]
        for i,e in enumerate(self.doc.modelspace()):
            if i>50000:break
            try:
                t=e.dxftype()
                if t=='LINE':pts=[e.dxf.start,e.dxf.end]
                elif t=='CIRCLE' or t=='ARC':
                    p=e.dxf.center;r=e.dxf.radius;pts=[(p.x-r,p.y-r),(p.x+r,p.y+r)]
                elif t=='LWPOLYLINE':pts=list(e.get_points('xy'))
                elif t=='POLYLINE':pts=[v.dxf.location for v in e.vertices]
                elif t=='3DFACE':pts=[e.dxf.vtx0,e.dxf.vtx1,e.dxf.vtx2]
                elif t=='POINT':pts=[e.dxf.location]
                else:continue
                for p in pts:xs.append(float(p[0]));ys.append(float(p[1]))
            except (AttributeError, ValueError):continue
        w=max(self.canvas.winfo_width(),800);h=max(self.canvas.winfo_height(),520)
        if not xs or not ys:
            self.view_scale=8.0;self.offset=(w/2,h/2)
        else:
            a,b=min(xs),max(xs);c,d=min(ys),max(ys)
            self.view_scale=min((w-90)/max(b-a,1),(h-90)/max(d-c,1))
            self.offset=(w/2-(a+b)/2*self.view_scale,h/2+(c+d)/2*self.view_scale)
        self.render()

    def close_app(self):
        if self.confirm_leave():self.destroy()

    def on_tk_error(self,exc,value,tb):
        import traceback
        traceback.print_exception(exc,value,tb)
        messagebox.showerror(self.tr('error'),str(value))

if __name__=='__main__':
    CadApp().mainloop()
