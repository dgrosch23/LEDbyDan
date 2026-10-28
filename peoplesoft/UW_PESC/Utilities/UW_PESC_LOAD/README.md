# UW_PESC:Utilities:UW_PESC_LOAD: PESC College Transcript → External Education + Manual Transfer Credit

Loads a deserialized `UW_PESC:CollegeTranscriptType` into Campus Solutions
through two component interfaces:

1. **UW_ACAD_HIST_PERS** (External Education): `EXT_ACAD_DATA`, `EXT_ACAD_SUM`, `EXT_COURSE`, `EXT_DEGREE`
2. **UW_TRNS_CRSE_MNL** (Course Credits - Manual): one **unposted** model per school

The field mapping follows
[`../../../docs/PESC_v1.8_to_ACAD_HIST_PERS_TRNS_CRSE_MNL.md`](../../../docs/PESC_v1.8_to_ACAD_HIST_PERS_TRNS_CRSE_MNL.md).
All CI property names come from the generated templates for your two CIs.

> **Not yet compiled or run.** This was written against the pasted CI
> templates and the UW_PESC classes. It hasn't been through App Designer.
> Expect a round of compile fixes, then work through the
> [first-run checklist](#first-run-checklist).

## Package layout

| Class | Purpose |
|---|---|
| `TranscriptLoader` | Entry point. Resolves each AcademicRecord, then runs both loaders in order |
| `ExtEducationLoader` | UW_ACAD_HIST_PERS: one Get/Save/Cancel per AcademicRecord |
| `TransferCreditLoader` | UW_TRNS_CRSE_MNL: one Get-or-Create/Save/Cancel per AcademicRecord |
| `Crosswalk` | Every PESC → CS code lookup, plus course equivalency |
| `RecordContext` | Resolved values for one AcademicRecord, shared by both loaders |
| `CourseItem` | One course with its term, dates, unit type and skip decision |
| `Config` | Institution, constants, behavior switches |
| `LoadResult` | Info/warning/error log, PSMessages, counts |
| `Check` | `IsSet()` / `IsBlank()`: All()/None() for properties, which the built-ins won't take |

Install in App Designer as subpackage `UW_PESC_LOAD` under `UW_PESC:Utilities`. Save in this order so the
imports resolve: `Check` → `Config` → `LoadResult` → `Crosswalk` → `CourseItem` →
`RecordContext` → `ExtEducationLoader` → `TransferCreditLoader` →
`TranscriptLoader`.

## Calling it

```
import UW_PESC:CollegeTranscriptType;
import UW_PESC:Utilities:UW_PESC_LOAD:Config;
import UW_PESC:Utilities:UW_PESC_LOAD:TranscriptLoader;
import UW_PESC:Utilities:UW_PESC_LOAD:LoadResult;

Local UW_PESC:CollegeTranscriptType &transcript;
/* ... deserialize the XML the way you already do, e.g.
   &transcript = &ser.XmlToObject(&xml, "UW_PESC:CollegeTranscriptType"); */

Local UW_PESC:Utilities:UW_PESC_LOAD:Config &cfg = create UW_PESC:Utilities:UW_PESC_LOAD:Config();
&cfg.DataSource = "EXT";              /* your LS_DATA_SOURCE value, if any */

Local UW_PESC:Utilities:UW_PESC_LOAD:TranscriptLoader &loader = create UW_PESC:Utilities:UW_PESC_LOAD:TranscriptLoader(&cfg);
Local UW_PESC:Utilities:UW_PESC_LOAD:LoadResult &r = &loader.Load(&transcript, &emplid);

Local File &log = GetFile("PESC_LOAD.log", "A", %FilePath_Relative);
&r.WriteTo(&log);
&log.Close();
```

Pass `""` as `&emplid` to use `Person/RecipientAssignedID` when it's a
valid EMPLID. Otherwise run your Search/Match first and pass the result.

Each AcademicRecord gets its own CI saves. A school that fails (missing
crosswalk, save error) is logged and the rest still load. Transfer credit
for a school only runs after its External Education saved and checked out.

## What happens to each AcademicRecord

1. **Resolve.** EXT_ORG_ID from the first of these that maps, in order:
   `AcademicRecord/School`, each `AcademicSession/School` in file order,
   then `TransmissionData/Source/Organization` (the sender) as a last
   resort. The log says which one was used when it wasn't the record's own
   school. It also warns when a session names a different school than the
   one chosen, because those courses still load under the chosen school.
   EXT_CAREER from
   StudentLevel (then CourseCreditLevel, then `Config.DefaultExtCareer`).
   ACAD_CAREER from EXT_CAREER. Transcript date from
   `TransmissionData/CreatedDateTime`. A term (year, EXT_TERM_TYPE,
   EXT_TERM) for every course. The "as of" term is the session that starts
   last.
2. **Duplicate check.** If an `EXT_ACAD_DATA` row already exists for the
   same school, career and transcript date, the record is skipped
   (`Config.SkipDuplicateTranscripts`).
3. **External Education.** EXT_DATA_NBR = MAX + 1. Then one new
   `EXT_ACAD_DATA` row with summaries, one `EXT_COURSE` per course, and one
   `EXT_DEGREE` per award (awards already on file are skipped). The
   component assigns EXT_COURSE_NBR and EXT_DEGREE_NBR. After the save, SQL
   confirms the `EXT_ACAD_DATA` row exists under the expected career and
   data number.
4. **Transfer credit.** One new model: source type E, the school,
   ACAD_PROG/ACAD_PLAN and ADMIT_TERM from the student's current
   `ACAD_PROG` (articulation term), and one `TRNS_CRSE_DTL` per course that
   has an equivalency. Saved unposted for an evaluator.

Course rules:

| Situation | EXT_COURSE | TRNS_CRSE_DTL |
|---|---|---|
| `CourseCreditBasis = TransferCredit` (credit from a third school) | skipped | skipped |
| No subject or course number | skipped | skipped |
| Grade status in `SKIP_GRADE_STATUS` or basis in `SKIP_CREDIT_BASIS` | loaded | skipped |
| No equivalency and no `Config.GenericCrseId` | loaded | skipped (warning: left for the evaluator) |

## Setup: two tables

Create these records in App Designer and build them. To use tables you
already have, change only the SQL in `Crosswalk.Lookup()` and
`Crosswalk.Equivalency()`.

**UW_CGER_PESC_XW** (code crosswalk)

| Field | Type | Key |
|---|---|---|
| UW_CGER_XWALK_TYPE | Char 20 | Y |
| UW_CGER_PESC_VAL | Char 100 | Y |
| UW_CGER_CS_VAL | Char 30 | |

PESC values are matched **upper-cased and trimmed**, so store them that way.

**UW_PESC_EQUIV** (course equivalency, all delivered fields)

| Field | Key |
|---|---|
| EXT_ORG_ID | Y |
| SCHOOL_SUBJECT | Y |
| SCHOOL_CRSE_NBR | Y |
| CRSE_ID | |
| CRSE_OFFER_NBR | |
| UNT_TRNSFR (0 = derive from earned credit) | |

To drive equivalencies from the delivered transfer rules instead, replace
the SQL in `Crosswalk.Equivalency()` with a `PS_TRNSFR_FR` / `PS_TRNSFR_TO`
join.

### Crosswalk types

Leave a type empty and the field it feeds is left to the component, except
where the table says otherwise.

| UW_CGER_XWALK_TYPE | UW_CGER_PESC_VAL (upper case) | UW_CGER_CS_VAL | If missing |
|---|---|---|---|
| `EXT_ORG` | `ATP:1234`, `CEEBACT:…`, `FICE:001234`, `ACT:…`, `IPEDS:…`, `OPEID:…`, `LOCAL:…`, `NAME:<ORG NAME>` | EXT_ORG_ID | **record skipped (error)** |
| `EXT_CAREER` | StudentLevelCode / CourseCreditLevel, e.g. `UNDERGRADUATE` | EXT_CAREER | `Config.DefaultExtCareer` (warning) |
| `ACAD_CAREER` | EXT_CAREER, e.g. `UGRD` | your ACAD_CAREER | `Config.DefaultAcadCareer` |
| `EXT_ACAD_LEVEL` | StudentLevelCode | EXT_ACAD_LEVEL | blank |
| `TRANSCRIPT_TYPE` | DocumentOfficialCode: `OFFICIAL`, `UNOFFICIAL` | TRANSCRIPT_TYPE | blank |
| `EXT_TERM_TYPE` | SessionType: `SEMESTER`, `QUARTER`, `TRIMESTER`, … | EXT_TERM_TYPE | blank |
| `EXT_TERM` | `<SESSIONTYPE>:<MM>` then `*:<MM>`, e.g. `SEMESTER:08`, `*:01` | EXT_TERM | blank |
| `UNT_TYPE` | CourseCreditUnits / GPA CreditUnit: `SEMESTER`, `QUARTER`, … | UNT_TYPE | blank |
| `UNIT_FACTOR` | CourseCreditUnits, e.g. `QUARTER` | multiplier, e.g. `0.6667` | 1 |
| `EXT_SUMM_TYPE` | AcademicSummaryType: `ALL`, `SENDERONLY`, … | EXT_SUMM_TYPE | **summary skipped** |
| `GPA_TYPE` | GPARangeMaximum as text: `4`, `5`, `100` | GPA_TYPE | blank |
| `EXT_CRSE_TYPE` | CourseCreditBasis: `REGULAR`, `ADVANCEDPLACEMENT`, … | EXT_CRSE_TYPE | blank |
| `COURSE_LEVEL` | CourseLevel, then CourseCreditLevel | COURSE_LEVEL | blank |
| `EXT_SUBJECT_AREA` | CourseCIPCode, then subject abbreviation | EXT_SUBJECT_AREA | blank |
| `DEGREE` | `<LEVEL>:<TITLE>`, then `<LEVEL>`, then `<TITLE>`, e.g. `BACHELORS:BS` | DEGREE | **degree skipped (warning)** |
| `DEGREE_STATUS` | `TRUE` (conferred or dated) / `FALSE` | DEGREE_STATUS | blank |
| `HONORS_CATEGORY` | HonorsLevel, then HonorsTitle | HONORS_CATEGORY | blank |
| `FIELD_OF_STUDY` | ProgramCIPCode, then AcademicProgramName | FIELD_OF_STUDY | blank |
| `EXT_SUBJ_AREA` | ProgramCIPCode | EXT_SUBJ_AREA | blank |
| `REPEAT_CODE` | CourseRepeatCode | REPEAT_CODE | blank |
| `SKIP_GRADE_STATUS` | CourseAcademicGradeStatusCode, e.g. `WITHDRAWN` | `Y` | sent to transfer credit |
| `SKIP_CREDIT_BASIS` | CourseCreditBasis | `Y` | sent to transfer credit |

Sample seed rows (replace the right-hand values with your XLATs):

```sql
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_ORG',        'FICE:003895',  '0001234');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_CAREER',     'UNDERGRADUATE','UGRD');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('ACAD_CAREER',    'UGRD',         'UGRD');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_TERM_TYPE',  'SEMESTER',     'SEM');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_TERM',       'SEMESTER:08',  'FALL');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_TERM',       'SEMESTER:01',  'SPR');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_TERM',       '*:06',         'SUM');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('UNT_TYPE',       'SEMESTER',     'SEM');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('UNIT_FACTOR',    'QUARTER',      '0.6667');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_SUMM_TYPE',  'ALL',          'OVRL');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('GPA_TYPE',       '4',            'GPA4');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('DEGREE',         'BACHELORS:BS', 'BS');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('DEGREE_STATUS',  'TRUE',         'C');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('SKIP_GRADE_STATUS','WITHDRAWN',  'Y');
```

## CI gaps (fix before the first run)

The generated templates show some key fields that your CIs don't expose.

1. **UW_TRNS_CRSE_MNL → `TRNS_CRSE_TERM` has no properties at all.**
   `ARTICULATION_TERM` is that level's key, and a model can't be created
   without it. Open the CI, drag `ARTICULATION_TERM` from the component
   (left pane) onto the `TRNS_CRSE_TERM` collection, and save. Until you
   do, every transfer credit load fails with a message naming the
   property. Add `MODEL_STATUS` too if you later want to post from code.
2. **UW_ACAD_HIST_PERS → `EXT_ACAD_DATA` doesn't expose `EXT_CAREER` /
   `EXT_DATA_NBR`.** The loader sets the `ACAD_HISTORY` defaults of the
   same name, which new rows normally copy, and sets the row keys too if
   they exist. After the save it checks with SQL. If the row isn't there,
   you get an error that says to add both fields to the `EXT_ACAD_DATA`
   collection. Adding them now removes the guesswork.
3. **`MODEL_NBR`, `EXT_COURSE_NBR` and `EXT_DEGREE_NBR` aren't exposed.**
   That's expected: the component assigns them. The loader reads the new
   MODEL_NBR back with SQL for the log. Transfer credit rows link to
   external courses by subject, catalog number, term and year
   (`SCHOOL_SUBJECT_0`, `SCHOOL_CRSE_NBR`, `TERM_YEAR`, `EXT_TERM`), not
   by EXT_COURSE_NBR.

## Bugs found in UW_PESC (fix these too)

These are in the `BuildSchema` methods you pasted. The first three
**throw** whenever the element is present in a file, which stops
deserialization before this package ever runs.

| Class | Line in BuildSchema | Problem | Effect |
|---|---|---|---|
| `AcademicSessionType` | `AddObject("StudentLevel", "UW_PESC:SudentLevelType")` | class path typo → `StudentLevelType` | **throws** when a session has StudentLevel |
| `AcademicSummaryFType` | `AddObject("AcademicProgram", "UW_PESC:AcaemicProgramType")` | class path typo → `AcademicProgramType` | **throws** when a record-level AcademicSummary has AcademicProgram |
| `PersonType` | `AddObject("Residency", "UW_PESC:Residency")` | class path → `ResidencyType` | **throws** when Person has Residency |
| `AcademicProgramType` | `AddScalar("ProgarmCIPCode", …)` | property typo → `ProgramCIPCode` | CIP never loads (no FIELD_OF_STUDY / EXT_SUBJ_AREA by CIP) |
| `ResidencyType` | `CountryCode` added twice | second one should be `CountyCode` | CountyCode never loads |
| `AgencyIdentifierType` | `AddScalar("StateProvinceCde", …)` | → `StateProvinceCode` | never loads |
| `HighSchoolSubjectAreaType` | `AddScalar("CreditHourseDeficient", …)` | → `CreditHoursDeficient` | never loads |
| `BirthType` | `BirthCountry` property not in the schema | add `AddScalar("BirthCountry", "STRING")` | never loads |
| `URLType` | first line reads `mport UW_DATA_SOURCE_UTILITIES:Serializer;` | probably a paste slip. If it's real, the class won't compile | — |

`class AcademicSummaryE1Type;`, `class EmailType;`, `class GPAType;` and
`class TestsType;` have a stray semicolon after the class name. If they
already compile, ignore it.

## First-run checklist

1. Fix the CI gaps and the three schema typos that throw.
2. Seed at least `EXT_ORG`, `EXT_CAREER`, `EXT_TERM_TYPE`, `EXT_TERM`,
   `UNT_TYPE`, `EXT_SUMM_TYPE`, `DEGREE` for one test school, plus one or
   two `UW_PESC_EQUIV` rows.
3. Set `Config.DataSource` / `DataMedium` / `TranscriptStatus` /
   grading scheme and basis to real XLAT values, or leave them blank.
4. Run one transcript for a test student. Then check:
   - Education page: one new data row, courses on the right term, degree.
   - `LoadResult`: no errors. Read the PSMSG lines, because component
     warnings show up there.
   - Course Credits - Manual: one unposted model with the right
     articulation term, and incoming courses that match the external
     courses.
5. Run the same file again. It should be skipped as a duplicate.

## Things to decide later

- **InteractiveMode** is off (`Config.InteractiveMode`), so field edits
  and FieldChange run at Save. If a FieldChange overwrites a value this
  package set, try turning it on.
- **Posting** isn't done here, because `MODEL_STATUS` and the post button
  aren't in the CI.
- **`CourseOverrideSchool`** courses are skipped along with every
  `TransferCredit` course. Loading them under the third school's
  EXT_ORG_ID would need one extra org row per override school.
