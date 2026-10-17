import pytest
from app import CadApp


def test_aliases_execute_real_geometry_and_undo():
    app = CadApp()
    try:
        app.update()
        app.execute_command('L 0,0 10,0')
        assert len(app.doc.modelspace()) == 1
        h = next(iter(app.doc.modelspace())).dxf.handle
        app.selection = h
        app.execute_command('M 5,7')
        assert app.doc.entitydb[h].dxf.start.x == pytest.approx(5)
        assert app.doc.entitydb[h].dxf.start.y == pytest.approx(7)
        app.undo()
        assert app.doc.entitydb[h].dxf.start.x == pytest.approx(0)
        app.redo()
        assert app.doc.entitydb[h].dxf.start.x == pytest.approx(5)
        app.execute_command('REC 0,0 10,10')
        assert any(e.dxftype() == 'LWPOLYLINE' and e.closed for e in app.doc.modelspace())
        before = len(app.doc.modelspace())
        app.execute_command('FILLET')
        assert before == len(app.doc.modelspace())
        assert 'FILLET' in app.message.cget('text')
        app.lang.set('en');app.change_language()
        assert 'Command line' in [x.cget('text')[:-2] for x in app.command_entry.master.winfo_children() if x.winfo_class() == 'Label']
    finally:
        app.dirty=False
        app.destroy()
