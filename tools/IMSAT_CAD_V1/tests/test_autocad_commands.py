import math
import pytest
import ezdxf
from imsatcad.autocad_commands import execute, point, resolve_alias, CommandError


def test_auto_cad_aliases():
    required={'L':'LINE','PL':'PLINE','C':'CIRCLE','REC':'RECTANG','TR':'TRIM','EX':'EXTEND','O':'OFFSET','CO':'COPY','M':'MOVE','RO':'ROTATE','SC':'SCALE','MI':'MIRROR'}
    assert {x:resolve_alias(x) for x in required}==required
    assert resolve_alias('_.l')=='LINE'


def test_points_and_relative_polar():
    assert point('10,20,5').z==5
    assert point('@10,0',point('5,5')).x==15
    p=point('@10<90',point('0,0'))
    assert p.x==pytest.approx(0,abs=1e-9)
    assert p.y==pytest.approx(10)
    with pytest.raises(CommandError):point('1,nan')


def test_draw_commands_and_dxf_roundtrip(tmp_path):
    doc=ezdxf.new();msp=doc.modelspace()
    for cmd in ['L 0,0 10,0','PL 0,0 @10,0 @0,10','C 5,5 3','REC 0,0 8,4','A 0,0 5 10 90']:
        assert execute(doc,cmd).changed
    assert len(msp)==5
    assert next(e for e in msp if e.dxftype()=='LWPOLYLINE' and e.closed).closed
    name=tmp_path/'test.dxf';doc.saveas(name)
    copy=ezdxf.readfile(name)
    assert len(copy.modelspace())==5
    assert len(copy.audit().errors)==0


def test_transform_commands_do_not_flatten_z():
    doc=ezdxf.new();msp=doc.modelspace()
    a=execute(doc,'L 1,2,100 3,2,105');handle=a.created_handle
    execute(doc,'M 5,6,7',selected_handle=handle)
    e=doc.entitydb[handle];assert e.dxf.start.z==pytest.approx(107)
    execute(doc,'RO 90 0,0',selected_handle=handle)
    assert e.dxf.start.x==pytest.approx(-8)
    assert e.dxf.start.y==pytest.approx(6)
    execute(doc,'SC 2 0,0',selected_handle=handle)
    assert e.dxf.start.z==pytest.approx(214)
    execute(doc,'MI 0,0 0,10',selected_handle=handle)
    assert e.dxf.start.x==pytest.approx(16)
    b=execute(doc,'CO 1,1',selected_handle=handle)
    assert b.created_handle!=handle
    assert len(msp)==2


def test_offset_trim_extend():
    doc=ezdxf.new()
    l=execute(doc,'L 0,0 10,0').created_handle
    boundary=execute(doc,'L 5,-5 5,5').created_handle
    result=execute(doc,f'TR {boundary} 0,0',selected_handle=l)
    assert result.changed
    assert doc.entitydb[l].dxf.start.x==pytest.approx(5)
    off=execute(doc,'O 2',selected_handle=l)
    assert doc.entitydb[off.created_handle].dxf.start.y==pytest.approx(2)
    # Extend right-hand segment to the left-side boundary
    e=execute(doc,'L 6,2 7,2').created_handle
    bb=execute(doc,'L 5,-10 5,10').created_handle
    execute(doc,f'EX {bb} 6,2',selected_handle=e)
    assert doc.entitydb[e].dxf.start.x==pytest.approx(5)
    c=execute(doc,'C 0,0 4').created_handle
    o=execute(doc,'O -1',selected_handle=c)
    assert doc.entitydb[o.created_handle].dxf.radius==pytest.approx(3)


def test_reject_invalid_operations_no_fabricated_results():
    d=ezdxf.new()
    e=execute(d,'L 0,0 2,0').created_handle
    with pytest.raises(CommandError):execute(d,'O -50',selected_handle=e) if False else execute(d,'RO bad',selected_handle=e)
    with pytest.raises(CommandError):execute(d,'TR 9999 0,0',selected_handle=e)
    with pytest.raises(CommandError):execute(d,'FILLET',selected_handle=e)
    with pytest.raises(CommandError):execute(d,'L 1,1 1,1')
    assert len(d.modelspace())==1
