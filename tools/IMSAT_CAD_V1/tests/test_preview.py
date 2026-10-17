import ezdxf
from app import CadApp, TRANS


def test_bilingual_ui_and_drafting(tmp_path):
    assert {'vi', 'en'} == set(TRANS)
    assert {'title', 'file', 'open', 'save', 'line', 'polyline', 'circle', 'layer'} <= set(TRANS['vi'])
    assert set(TRANS['vi']) == set(TRANS['en'])
    app = CadApp()
    try:
        app.update()
        assert app.icon_img is not None
        app.set_tool('line')
        class Event: pass
        for x, y in [(150, 200), (250, 260)]:
            e = Event(); e.x = x; e.y = y
            app.click(e)
        assert len(app.doc.modelspace()) == 1
        app.undo()
        assert len(app.doc.modelspace()) == 0
        app.redo()
        assert len(app.doc.modelspace()) == 1
        app.lang.set('en')
        app.change_language()
        assert 'Preview' in app.title()
        path = tmp_path / 'bilingual.dxf'
        from imsatcad.dxf_backend import DxfWorkspace
        DxfWorkspace(app.doc).save_as(path)
        assert next(iter(ezdxf.readfile(path).modelspace())).dxftype() == 'LINE'
    finally:
        app.dirty = False
        app.destroy()
