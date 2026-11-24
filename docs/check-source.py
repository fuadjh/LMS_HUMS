"""Mechanical source checks only. This does not parse/compile C# or Razor or run SQL."""
import json
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
projects = [ROOT / p.attrib['Path'] for p in ET.parse(ROOT / 'ReusableAccess.slnx').iter('Project')]
assert len(projects) == 11 and len(projects) == len(set(projects))
refs = 0
for p in projects:
    assert p.is_file(), p
    for q in ET.parse(p).iter('ProjectReference'):
        refs += 1
        assert (p.parent / q.attrib['Include'].replace('\\', '/')).resolve().is_file(), (p, q.attrib)
print(f'PASS: Solution XML: {len(projects)} projects; {refs} project references resolve')
for p in (ROOT / 'docs').glob('*.json'):
    json.loads(p.read_text(encoding='utf-8'))

src = ROOT / 'src'
domain = src / 'ReusableAccess.Domain/Academics/AcademicEntities.cs'
app = list((src / 'ReusableAccess.Application/Academics').glob('*Requests.cs'))
infra = src / 'ReusableAccess.Infrastructure'
mig = infra / 'Persistence/Migrations'
up = (mig / '20261121203400_AddUniversityLms.cs').read_text()
model = (infra / 'Academics/AcademicModel.cs').read_text()
snapshot = (mig / 'AcademicSchemaV1.cs').read_text()
classes = set(re.findall(r'public sealed class (\w+)', domain.read_text()))
tables_up = set(re.findall(r'CreateTable\(\s*name: "(\w+)", schema: "lms"', up))
tables_model = set(re.findall(r'b.ToTable\("(\w+)", "lms"\);', model))
assert classes == tables_model == tables_up == set(re.findall(r'b.ToTable\("(\w+)", "lms"\);', snapshot))
fks = re.findall(r'AddForeignKey\(.+\);', up)
assert fks and all('ReferentialAction.NoAction' in x for x in fks)
assert len(fks) == model.count('OnDelete(DeleteBehavior.NoAction)') == snapshot.count('OnDelete(DeleteBehavior.NoAction)')
assert 'HasFilter("[EndedUtc] IS NULL")' in model and 'unique: true, filter: "[EndedUtc] IS NULL"' in up
assert 'DropTable' not in up and 'DropColumn' not in up and 'DeleteData' not in up
print(f'PASS: migration/model cross-check: {len(tables_up)} LMS tables, {len(fks)} NO ACTION foreign keys; active-record filtered index')

api = (src / 'ReusableAccess.Api/Academics/LmsEndpoints.cs').read_text()
registered = re.findall(r'r.Map(Get|Post|Put)\("(/[^"]+)"', api)
assert len(registered) == len(set(registered))
assert all('WithName(' in chunk for chunk in re.split(r'\br.Map(Get|Post|Put)\(', api)[1:] if chunk not in ('Get', 'Post', 'Put'))
assert 'RequireAuthorization("AccessApi")' in api
assert 'WithMetadata(new RequestSizeLimitAttribute(UploadLimits.FileBytes))' in api
handler_types = set(re.findall(r'public sealed record (\w+(?:Query|Command))\(', '\n'.join(p.read_text() for p in app)))
endpoint_types = set(re.findall(r'new (\w+(?:Query|Command))\(', api))
assert handler_types == endpoint_types, handler_types.symmetric_difference(endpoint_types)
compat = (src / 'ReusableAccess.Api/Completion/CompletionApiExtensions.cs').read_text('utf-8-sig')
assert compat.count('"/api/console/writable-scopes"') == 1 and 'new GetWritableScopesQuery(Mode(c), Scope(c))' in compat
print(f'PASS: {len(registered)} named LMS endpoints, {len(endpoint_types)} matching CQRS request types; writable-scopes wiring')

ns = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
for profile, tail in [('student', ['student_number']), ('teacher', ['classification', 'cv_url'])]:
    with zipfile.ZipFile(src / f'ReusableAccess.Web/wwwroot/lms-templates/{profile}.xlsx') as w:
        assert w.testzip() is None and not any('vba' in n.lower() for n in w.namelist())
        sheet = ET.fromstring(w.read('xl/worksheets/sheet1.xml'))
        rows = sheet.findall('.//s:row', ns)
        assert len(rows) == 501 and not sheet.findall('.//s:f', ns)
        values = [x.text for x in rows[0].findall('.//s:t', ns)]
        assert values == ['national_id', 'first_name', 'last_name', 'organization_code', 'email', 'mobile', *tail]
        assert rows[1].find('s:c', ns).get('s') is not None
        assert not any(r.findall('.//s:v', ns) or r.findall('.//s:t', ns) for r in rows[1:])
print('PASS: 2 XLSX templates; no data/formula/macro; expected headers and 500 styled blank rows')

if len(sys.argv) == 2:
    with zipfile.ZipFile(sys.argv[1]) as base:
        assert base.testzip() is None and base.comment == b'd66194d9dcc62b1edeac5a8a8e8544c34099518c'
        original = [i for i in base.infolist() if not i.is_dir()]
        assert len(original) == 378
        migrations = 0
        missing = []
        changed = []
        for row in original:
            relative = row.filename.split('/', 1)[1]
            candidate = ROOT / relative
            if not candidate.is_file():
                missing.append(relative)
                continue
            content = base.read(row)
            if re.search(r'/Migrations/\d{14}_', relative):
                assert content == candidate.read_bytes(), relative
                if not relative.endswith('Designer.cs'):
                    migrations += 1
            if content != candidate.read_bytes():
                changed.append(relative)
        assert not missing and migrations == 9, missing
        print(f'PASS: no original tracked file omitted; {migrations} original migrations unchanged; {len(changed)} original files modified')
