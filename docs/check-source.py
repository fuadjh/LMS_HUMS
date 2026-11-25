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
domain = src / 'ReusableAccess.Domain/Academics'
app = sorted((src / 'ReusableAccess.Application/Academics').glob('*Requests.cs'))
infra = src / 'ReusableAccess.Infrastructure'
mig = infra / 'Persistence/Migrations'
v1 = (mig / '20261121203400_AddUniversityLms.cs').read_text()
v2 = (mig / '20261125090000_AddLmsStudio.cs').read_text()
model = (infra / 'Academics/AcademicModel.cs').read_text()
frozen1 = (mig / 'AcademicSchemaV1.cs').read_text()
frozen2 = (mig / 'AcademicStudioSchemaV2.cs').read_text()
snapshot = (mig / 'ApplicationDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
read_classes = lambda name: set(re.findall(r'public sealed class (\w+)', (domain / name).read_text()))
read_tables = lambda text: set(re.findall(r'CreateTable\(\s*name: "(\w+)", schema: "lms"', text))
read_models = lambda text: set(re.findall(r'b.ToTable\("(\w+)", "lms"\);', text))
assert read_classes('AcademicEntities.cs') == read_tables(v1) == read_models(frozen1)
assert read_classes('StudioEntities.cs') == read_tables(v2) == read_models(frozen2) == {'OfferingScheduleDraft', 'TemplateVersionMedia'}
assert read_models(model) == read_tables(v1) | read_tables(v2)
fks = re.findall(r'AddForeignKey\([^;]+;', v1 + v2)
assert fks and all('ReferentialAction.NoAction' in x for x in fks)
assert len(fks) == model.count('OnDelete(DeleteBehavior.NoAction)') == (frozen1 + frozen2).count('OnDelete(DeleteBehavior.NoAction)')
assert 'HasFilter("[EndedUtc] IS NULL")' in model and 'unique: true, filter: "[EndedUtc] IS NULL"' in v1
assert 'AcademicStudioSchemaV2.Build(modelBuilder)' in snapshot and 'AcademicSchemaV1.Build(model)' in frozen2
assert all(op not in v1 + v2 for op in ('DropTable', 'DropColumn', 'DeleteData'))
print(f'PASS: {len(read_models(model))} tables in V1/V2, {len(fks)} NO ACTION FKs; static snapshot wiring only')

api = '\n'.join((src / f'ReusableAccess.Api/Academics/{name}Endpoints.cs').read_text() for name in ('Lms', 'Studio'))
registered = re.findall(r'r.Map(Get|Post|Put)\("(/[^"]+)"', api)
assert len(registered) == len(set(registered))
assert all('WithName(' in chunk for chunk in re.split(r'\br.Map(Get|Post|Put)\(', api)[1:] if chunk not in ('Get', 'Post', 'Put'))
assert 'RequireAuthorization("AccessApi")' in api and 'r.MapLmsStudio();' in api
assert 'WithMetadata(new RequestSizeLimitAttribute(UploadLimits.FileBytes))' in api
handler_types = set(re.findall(r'public sealed record (\w+(?:Query|Command))\(', '\n'.join(p.read_text() for p in app)))
endpoint_types = set(re.findall(r'new (\w+(?:Query|Command))\(', api))
assert handler_types == endpoint_types, handler_types.symmetric_difference(endpoint_types)
assert len(re.findall(r'WithName\("([^"]+)"\)', api)) == len(set(re.findall(r'WithName\("([^"]+)"\)', api)))
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
