# UW_PESC:Utilities:UW_PESC_LOAD: PESC College Transcript → External Education + Manual Transfer Credit

Loads a deserialized `UW_PESC:CollegeTranscriptType` into Campus Solutions
through two component interfaces:

1. **UW_ACAD_HIST_PERS** (External Education): `EXT_ACAD_DATA`, `EXT_ACAD_SUM`, `EXT_COURSE`, `EXT_DEGREE`
2. **UW_TRNS_CRSE_MNL** (Course Credits - Manual, "CCM"): one **unposted** model per school holding the incoming courses

The package only enters transcript data. It never works out equivalencies.
Everything after the data is in (review, equivalencies, posting) stays with
each school's current process.

### Modules: what gets loaded, per institution

| Module | Loads |
|---|---|
| `EXTDATA` | External Education academic data row, with GPA/rank summaries |
| `EXTDEG` | External Education degrees |
| `EXTCRSE` | External Education courses |
| `CCM` | Course Credits - Manual model: the incoming courses, with the equivalent course left blank |

Each institution picks any combination with crosswalk type `MODULES`
(value = INSTITUTION, result = a comma-separated list). For example:

| A school that… | MODULES |
|---|---|
| keeps equivalency rules in PeopleSoft and posts from External Education | `EXTDATA,EXTDEG,EXTCRSE` (also the default with no row) |
| enters equivalencies by hand from a 3rd-party app | `EXTDATA,EXTDEG,EXTCRSE,CCM` |
| wants degrees in External Education and coursework only in CCM | `EXTDATA,EXTDEG,CCM` |
| wants coursework in CCM only | `CCM` |
| is being tested: resolve and log everything, save nothing | `NONE` |

Rules:
- `EXTDEG` and `EXTCRSE` switch `EXTDATA` on, because degree and course
  rows point at an academic data row (career + data number).
- `CCM` runs after External Education, and only if External Education
  saved (when it's selected).
- The duplicate check uses the transcript date when `EXTDATA` is on. For
  `CCM` alone, any existing Manual model for that school counts as a
  duplicate.
- `Config.ModuleList` overrides the table for one run. Unknown names are
  ignored with a warning, and the log's first line shows the modules used.
- A crosswalk value holds 30 characters. All four modules,
  `EXTDATA,EXTDEG,EXTCRSE,CCM`, take 26.

If you later want a setup page with a checkbox per module, build a small
record keyed by INSTITUTION with one Y/N field per module. Then change
only `TranscriptLoader.ResolveModules()` to read it instead of the
crosswalk.

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
| `Crosswalk` | Every PESC → CS code lookup |
| `StudentMatcher` | Finds the EMPLID with Search/Match (HCR_SM_SEARCH) when none is passed |
| `RecordContext` | Resolved values for one AcademicRecord, shared by both loaders |
| `CourseItem` | One course with its term, dates, unit type and skip decision |
| `Config` | Institution, constants, behavior switches |
| `Modules` | Which parts load for an institution (`EXTDATA`, `EXTDEG`, `EXTCRSE`, `CCM`) |
| `LoadResult` | Info/warning/error log, PSMessages, counts |
| `Check` | `IsSet()` / `IsBlank()`: All()/None() for properties, which the built-ins won't take |

Install in App Designer as subpackage `UW_PESC_LOAD` under `UW_PESC:Utilities`. Save in this order so the
imports resolve: `Check` → `Config` → `Modules` → `LoadResult` → `Crosswalk` → `CourseItem` →
`RecordContext` → `ExtEducationLoader` → `TransferCreditLoader` →
`StudentMatcher` → `TranscriptLoader`.

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

Pass `""` as `&emplid` to have the loader find the student. It tries, in
order:
1. `Person/RecipientAssignedID`, if it's an existing EMPLID.
2. Search/Match (`StudentMatcher`, when `Config.UseSearchMatch` is True).

Only exactly one match loads. Several matches, or none, load nothing and
log an error. After the load, `LoadResult.MatchStatus` holds `PASSED` /
`RECIPIENTID` / `MATCH` / `MULTIPLE` / `NONE` / `ERROR`,
`LoadResult.MatchedEmplids` lists the candidates, and `LoadResult.Emplid`
is the EMPLID used. Write those to your suspense table when the status
isn't a match.

Each AcademicRecord gets its own CI saves. A school that fails (missing
crosswalk, save error) is logged and the rest still load.

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
   **Records for the same school and career are merged** and loaded as
   one: one data row, one Manual model. Some senders, Parchment among
   them, split a single transcript into records by summary type. Record 1
   holds the degree and "All" GPA, record 2 the "TransferOnly" courses
   from other schools, and record 3 the "SenderOnly" courses. Records 1
   and 3 are the same school, so they share a data row. The log says when
   a record was merged.
2. **Duplicate check.** If an `EXT_ACAD_DATA` row already exists for the
   same school, career and transcript date, the record is skipped
   (`Config.SkipDuplicateTranscripts`).
3. **External Education (`EXTDATA`, plus `EXTDEG` / `EXTCRSE`).**
   EXT_DATA_NBR = MAX + 1. Then one new `EXT_ACAD_DATA` row with
   summaries, one `EXT_DEGREE` per award (awards already on file are
   skipped) and one `EXT_COURSE` per course. The
   component assigns EXT_COURSE_NBR and EXT_DEGREE_NBR. After the save, SQL
   confirms the `EXT_ACAD_DATA` row exists under the expected career and
   data number.
4. **Course Credits - Manual (`CCM`).** One new model: source
   type E, the school, ACAD_PROG/ACAD_PLAN and ADMIT_TERM from the
   student's current `ACAD_PROG` (articulation term), and one
   `TRNS_CRSE_DTL` per course with the incoming subject, number, term,
   title, units taken and grade. The equivalent course (`CRSE_ID`) is left
   blank for staff to enter. Saved unposted.

Course rules:

| Situation | EXT_COURSE | TRNS_CRSE_DTL |
|---|---|---|
| `CourseCreditBasis = TransferCredit` (credit from a third school) | skipped | skipped |
| No subject or course number | skipped | skipped |
| Grade status in `SKIP_GRADE_STATUS` or basis in `SKIP_CREDIT_BASIS` | loaded | skipped |

## Setup: the crosswalk table

Create this record in App Designer and build it. To use a different
table, change only the SQL in `Crosswalk.Lookup()`.

**UW_CGER_PESC_XW** (code crosswalk)

| Field | Type | Key |
|---|---|---|
| UW_CGER_XWALK_TYPE | Char 20 | Y |
| UW_CGER_PESC_VAL | Char 100 | Y |
| UW_CGER_CS_VAL | Char 30 | |

PESC values are matched **upper-cased and trimmed**, so store them that way.

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
| `EXT_SUMM_TYPE` | AcademicSummaryType: `ALL`, `SENDERONLY`, … | EXT_SUMM_TYPE | **summary skipped** |
| `GPA_TYPE` | GPARangeMaximum as text: `4`, `5`, `100` | GPA_TYPE | blank |
| `EXT_CRSE_TYPE` | CourseCreditBasis: `REGULAR`, `ADVANCEDPLACEMENT`, … | EXT_CRSE_TYPE (**required**) | `Config.DefaultExtCrseType` |
| `CRSE_GRADE` | `<SCALE>:<GRADE>` then `<GRADE>`, e.g. `T:A*`, `D:S/A` | CRSE_GRADE_INPUT, valid for the grading scheme/basis | grade as sent, upper-cased |
| `GRADING_BASIS` | CourseAcademicGradeScaleCode, e.g. `G`, `D`, `T` | GRADING_BASIS for EXT_COURSE | `Config.ExtGradingBasis` |
| `SKIP_RECORD` | AcademicSummaryType, e.g. `TRANSFERONLY` | `Y` | record loaded |
| `COURSE_LEVEL` | CourseLevel, then CourseCreditLevel, e.g. `UNDERGRADUATE` | COURSE_LEVEL (**required** on EXT_COURSE) | `Config.DefaultCourseLevel` |
| `EXT_SUBJECT_AREA` | CourseCIPCode, then subject abbreviation | EXT_SUBJECT_AREA | blank |
| `DEGREE` | `<LEVEL>:<TITLE>`, then `<LEVEL>`, then `<TITLE>`. PESC levels are numeric codes, e.g. `2.3:ASSOCIATE OF GENERAL STUDIES`, `2.3`, `3.1` | DEGREE | **degree skipped (warning)** |
| `DEGREE_STATUS` | `TRUE` (conferred or dated) / `FALSE` | DEGREE_STATUS | blank |
| `HONORS_CATEGORY` | HonorsLevel, then HonorsTitle | HONORS_CATEGORY | blank |
| `FIELD_OF_STUDY` | ProgramCIPCode, then AcademicProgramName | FIELD_OF_STUDY | blank |
| `EXT_SUBJ_AREA` | ProgramCIPCode | EXT_SUBJ_AREA | blank |
| `REPEAT_CODE` | CourseRepeatCode | REPEAT_CODE | blank |
| `SKIP_GRADE_STATUS` | CourseAcademicGradeStatusCode, e.g. `WITHDRAWN` | `Y` | sent to transfer credit |
| `SKIP_CREDIT_BASIS` | CourseCreditBasis | `Y` | sent to transfer credit |
| `MODULES` | INSTITUTION, e.g. `UWRVF` | module list, e.g. `EXTDATA,EXTDEG,CCM` | `EXTDATA,EXTDEG,EXTCRSE` |

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
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_SUMM_TYPE',  'ALL',          'OVRL');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('GPA_TYPE',       '4',            'GPA4');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('DEGREE',         '2.3',          'AGS');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('EXT_CRSE_TYPE',  'REGULAR',      'REG');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('CRSE_GRADE',     'T:A*',         'A');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('GRADING_BASIS',  'D',            'SUS');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('DEGREE_STATUS',  'TRUE',         'C');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('SKIP_GRADE_STATUS','WITHDRAWN',  'Y');
INSERT INTO PS_UW_CGER_PESC_XW VALUES ('MODULES',        'UWRVF',        'EXTDATA,EXTDEG,CCM');
```

## Finding valid values

"Invalid value" on a field means the value the loader set isn't in that
field's prompt table. These queries list what is valid (add your own
SETID/effective-date filters):

```sql
-- EXT_CRSE_TYPE (Config.DefaultExtCrseType, crosswalk EXT_CRSE_TYPE)
SELECT FIELDVALUE, XLATLONGNAME FROM PSXLATITEM WHERE FIELDNAME = 'EXT_CRSE_TYPE' AND EFF_STATUS = 'A';

-- grading schemes and bases (Config.ExtGradingScheme / ExtGradingBasis, crosswalk GRADING_BASIS)
SELECT DISTINCT SETID, GRADING_SCHEME, GRADING_BASIS FROM PS_GRADE_TBL ORDER BY 1, 2, 3;

-- grades valid for one scheme + basis (crosswalk CRSE_GRADE results)
SELECT CRSE_GRADE_INPUT, DESCR FROM PS_GRADE_TBL
 WHERE SETID = :setid AND GRADING_SCHEME = :scheme AND GRADING_BASIS = :basis
   AND EFFDT = (SELECT MAX(EFFDT) FROM PS_GRADE_TBL G2 WHERE G2.SETID = PS_GRADE_TBL.SETID
                AND G2.GRADING_SCHEME = PS_GRADE_TBL.GRADING_SCHEME AND G2.EFFDT <= SYSDATE);
```

The quickest check is online. Key one external course by hand on the
Education page, then copy the course type, grading scheme, basis and
grade staff choose into Config and the crosswalk.

## CI gaps (fix before the first run)

The generated templates show some key fields that your CIs don't expose.

1. **UW_TRNS_CRSE_MNL → `TRNS_CRSE_TERM` has no properties at all.**
   `ARTICULATION_TERM` is that level's key, and a model can't be created
   without it. Open the CI, drag `ARTICULATION_TERM` from the component
   (left pane) onto the `TRNS_CRSE_TERM` collection, then **clear its
   Read Only flag** in the CI property, because it comes in read-only even
   though the page field is editable. Save. Until you do, every CCM load
   fails with "Property is read-only" or a message naming the property. Add `MODEL_STATUS` too if you later want to post from code.
2. **UW_ACAD_HIST_PERS → `EXT_ACAD_DATA` must expose `EXT_CAREER` and
   `EXT_DATA_NBR`.** The generated CI leaves out these key fields. Add both
   to the collection (done in UWRVF's CI). The loader sets them on the new
   data row and checks with SQL after the save that the row exists under
   those keys. ACAD_HISTORY's own `EXT_DATA_NBR` default is not set,
   because its prompt rejects a data number that isn't saved yet.
3. **`MODEL_NBR`, `EXT_COURSE_NBR` and `EXT_DEGREE_NBR` aren't exposed.**
   That's expected: the component assigns them. The loader reads the new
   MODEL_NBR back with SQL for the log. Transfer credit rows link to
   external courses by subject, catalog number, term and year
   (`SCHOOL_SUBJECT_0`, `SCHOOL_CRSE_NBR`, `TERM_YEAR`, `EXT_TERM`), not
   by EXT_COURSE_NBR.
4. **Rows with no equivalent course: confirmed.** Course Credits - Manual
   saves detail rows with `CRSE_ID` blank (tested at UWRVF), so
   `Config.GenericCrseId` can stay blank.
5. **Does CCM need the courses in External Education?** The incoming course
   fields on Course Credits - Manual prompt against the student's external
   courses (`EXT_CRS_TRNS_VW` in the CI). For a school without `EXTCRSE`,
   check online that you can type an incoming course that isn't in External
   Education and save. If the component rejects it, that school needs
   `EXTCRSE` as well.

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
   `UNT_TYPE`, `EXT_SUMM_TYPE`, `DEGREE` for one test school, plus a
   `MODULES` row for each institution that doesn't use the default.
3. Set `Config.DataSource` / `DataMedium` / `TranscriptStatus` /
   `TranscriptFlag` to real XLAT values, or leave them blank. Set **`Config.ExtGradingScheme`,
   `ExtGradingBasis`, `DefaultExtCrseType` and `DefaultCourseLevel`** to what staff choose when
   they key an external course online. Every grade has to exist in that
   scheme and basis, or EXT_COURSE fails with "Invalid value" on
   CRSE_GRADE_INPUT.
4. Run one transcript for a test student. Then check:
   - Education page: one new data row, courses on the right term, degree.
   - `LoadResult`: no errors. Read the PSMSG lines, because component
     warnings show up there.
   - With `CCM`: Course Credits - Manual has one unposted model with the
     right articulation term and the incoming courses, with no equivalent
     courses. Without `CCM`, no model was created.
   - Without `EXTDEG` or `EXTCRSE`: no degrees or courses were added to
     External Education.
   - With `NONE`: the log has one "Dry run" line per AcademicRecord, and
     nothing was saved.
5. Run the same file again. It should be skipped as a duplicate.

## Search/Match setup

`StudentMatcher` drives the delivered Search/Match CI, `HCR_SM_SEARCH`:

- **Security:** add `HCR_SM_SEARCH` to the run user's permission list
  with **Full Access** on `Get` and `Cancel`.
- **Config:** `SearchType` (SM_TYPE for a Person search, `P`; check
  `PSXLATITEM` for FIELDNAME `SM_TYPE`), `SearchParmCode`
  (`UW_CGER_TSCRIPT`) and `SearchResultCode` (`UW_CGER_SM_RSLT`).
- **Search fields filled:** `FIRST_NAME`, `LAST_NAME`, `BIRTHDATE` (from
  Person/Birth/BirthDate) and `POSTAL` (the first PostalCode under
  Person/Contacts, cut to 5 characters). Any other field in the parameter
  is left blank and logged.
- **How it runs:** after `Get()`, the loader fills the parameter's
  search-field rows (`HCR_SM_SRCHFLDV`) and sets the Search button
  (`SM_SEARCH_BTN`). It then reads the matches from `HCR_SM_PER_VW`. The
  CI runs in interactive mode, because it has no Save and the search runs
  in the button's FieldChange.

## Commits and rollback

CI saves don't commit on their own. Everything commits when the run
ends. If any save fails, PeopleSoft rolls back all work that isn't
committed yet. That includes the data row from the same school's first
save, and schools loaded earlier in the run. The log checks the database
after a failure and says whether the data row survived.

Set `Config.CommitEachRecord = True` to call `CommitWork()` after each
school's External Education, and again after its CCM model. A failure then
only loses that one step: a failed CCM model no longer takes the school's
External Education with it. Use it when running from
App Engine. Online, `CommitWork()` is only allowed in some PeopleCode
events.

## Things to decide later

- **InteractiveMode** is off (`Config.InteractiveMode`), so field edits
  and FieldChange run at Save. If a FieldChange overwrites a value this
  package set, try turning it on.
- **Posting** stays with each school's current process. The package never
  posts, and `MODEL_STATUS` and the post button aren't in the CI anyway.
- **`CourseOverrideSchool`** courses are skipped along with every
  `TransferCredit` course. Loading them under the third school's
  EXT_ORG_ID would need one extra org row per override school.
