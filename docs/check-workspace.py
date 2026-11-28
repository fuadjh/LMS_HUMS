"""Offline release gate for source layout, schema links and role wiring. Not a C#/Razor compiler or SQL Server run."""
import re
import sys
from collections import defaultdict
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile

root = Path(__file__).resolve().parents[1]
src = root / 'src'
infra = src / 'ReusableAccess.Infrastructure'
migrations = infra / 'Persistence/Migrations'
workspace = (migrations / '20261126093000_SeparateOfferingWorkspace.cs').read_text()
frozen = (migrations / 'AcademicLearningSchemaV4.cs').read_text()

def fold_chains(text):
    return re.sub(r'\n\s+(?=\.)', '', text)

def live(name):
    return (infra / f'Academics/{name}.cs').read_text()

runtime = live('AcademicModel') + live('OfferingWorkspaceModel')
current = None
indexes = defaultdict(dict)
links = defaultdict(set)
for line in fold_chains(runtime).splitlines():
    start = re.search(r'model.Entity<(\w+)>\(', line)
    if start:
        current = start[1]
    if 'b.HasIndex(' in line:
        fields = tuple(re.findall(r'x\.(\w+)', line))
        assert fields, line
        indexes[current][fields] = ('.IsUnique()' in line, re.findall(r'HasFilter\("([^"]+)"\)', line))
    relationship = re.search(r'b.HasOne<(\w+)>.*?HasForeignKey\(x => (.*?)\)(?:\.HasPrincipalKey\(x => .*?\))?\.OnDelete\(DeleteBehavior.NoAction\)', line)
    if relationship:
        links[current].add((relationship[1], tuple(re.findall(r'x\.(\w+)', relationship[2]))))

snapshot_indexes = defaultdict(dict)
snapshot_links = defaultdict(set)
for line in fold_chains(frozen).splitlines():
    start = re.search(r'model.Entity\("ReusableAccess.Domain.Academics\.(\w+)"', line)
    if start:
        current = start[1]
    if 'b.HasIndex(' in line:
        fields = tuple(re.findall(r'"([^"]+)"', line.split(')', 1)[0]))
        snapshot_indexes[current][fields] = ('.IsUnique()' in line, re.findall(r'HasFilter\("([^"]+)"\)', line))
    relationship = re.search(r'b.HasOne\("[\w.]+\.(\w+)", null\)\.WithMany\(\)\.HasForeignKey\((.*?)\).*?OnDelete\(DeleteBehavior.NoAction\)', line)
    if relationship:
        snapshot_links[current].add((relationship[1], tuple(re.findall(r'"(\w+)"', relationship[2]))))
assert indexes == snapshot_indexes, {k for k in indexes.keys() | snapshot_indexes.keys() if indexes.get(k) != snapshot_indexes.get(k)}
assert links == snapshot_links, {k for k in links.keys() | snapshot_links.keys() if links.get(k) != snapshot_links.get(k)}
assert ('LessonSession', ('SessionId',)) not in links['Lesson']
assert ('Offering', ('OfferingId',)) in links['Assessment']
assert all(fk[0] not in ('Chapter', 'Lesson') for fk in links['Assessment'])
assert ('Submission', ('SubmissionId',)) in links['AssignmentCalendarReceipt']
assert re.search(r'PK_AssignmentCalendarReceipt", x => x.SubmissionId', workspace)
assert 'IX_Submission_AssessmentId_EnrollmentId_RequestId' in workspace
assert workspace.index('SET OfferingId = c.OfferingId') < workspace.index('AlterColumn<Guid>("OfferingId"')
assert all(f'{verb} lms.{table}' not in workspace for verb in ('UPDATE', 'DELETE FROM') for table in ('Submission', 'Enrollment', 'AssessmentGrade', 'FinalGrade'))
print(f'PASS: {sum(len(x) for x in indexes.values())} explicit indexes and {sum(len(x) for x in links.values())} FK links match the named frozen model; legacy backfill precedes constraints')

for feature in ('LmsOfferings', 'LmsEnrollments', 'LmsGrading', 'LmsCalendar', 'StudioMaterializer', 'StudioScheduling'):
    assert 'LessonSession' not in live(feature) and '.SessionId' not in live(feature), feature
assert 'new Assessment' not in live('StudioMaterializer')
assert 'CanReadLesson(now, info.Chapter, info.Lesson)' in live('LmsEnrollments')
assert 'LmsEnrollments.PublishedLesson(' in live('LmsFiles')
assert 's.ScoreLimit ?? i.Assessment.MaxScore' in live('LmsGrading')
assert 'AssignmentCalendarReceipt' not in live('LmsMaintenance')
assert all('data.Db' not in live(name) and 'data.Finish(' not in live(name) for name in ('AssignmentQueries',))
views = src / 'ReusableAccess.Web/Components'
offering = (views / 'Lms/LmsOffering.razor').read_text()
assert re.findall(r'\("(\w+)", "[^"]+"\)', offering.split('Sections =', 1)[1].split(';', 1)[0]) == ['content', 'online', 'assignments', 'discussion']
assert 'LmsAssignmentWorkspace' in offering and 'LmsCourseDiscussion' in offering
assert all((views / f'Layout/Lms{name}Layout.razor').is_file() for name in ('Admin', 'Instructor', 'Student'))
assert 'LmsLayoutRoutes.For(routeData.PageType, routeData.RouteValues)' in (views / 'Routes.razor').read_text('utf-8-sig')
assert sum('<AppDataTable ' in p.read_text('utf-8-sig') for p in (views/'Lms').glob('*.razor')) >= 4
print('PASS: content, assessment, calendar and file gates; four sections; three role layouts; shared table use')

if len(sys.argv) > 1:
    with ZipFile(sys.argv[1]) as archive:
        assert archive.testzip() is None
        assert b'b306a67f6b96d60693151fd1830d480e1cfd0fa4' in archive.comment, 'Unexpected source baseline'
        paths = {e.filename.split('/', 1)[1]: sha256(archive.read(e)).digest() for e in archive.infolist() if not e.is_dir()}
        history = [name for name in paths if re.search(r'/Migrations/\d{14}_', name)]
        assert len(history) == 24 and all(sha256((root/name).read_bytes()).digest() == paths[name] for name in history)
        frozen_history = [name for name in paths if re.search(r'/Migrations/Academic.*Schema', name)]
        assert len(frozen_history) == 3 and all(sha256((root/name).read_bytes()).digest() == paths[name] for name in frozen_history)
        assets = [name for name in paths if Path(name).suffix in ('.woff2', '.xlsx')]
        assert assets and all(sha256((root/name).read_bytes()).digest() == paths[name] for name in assets)
        print(f'PASS: {len(history)} historical migration files (including designers), {len(assets)} original font and Excel files byte-identical to the uploaded baseline')
