# PESC College Transcript v1.8 → Campus Solutions mapping

Section-by-section mapping of a PESC College Transcript (v1.8) XML document to
two Campus Solutions components, loaded through Component Interfaces:

1. **ACAD_HIST_PERS**: External Education / Academic History (`EXT_*` records)
2. **TRNS_CRSE_MNL**: Transfer Credit, Course Credits - Manual (`TRNS_CRSE_*` records)

> **Verify before coding.** Record names and the overall hierarchy are standard
> for CS 9.0/9.2. Individual field names can differ by PUM image, bundle level
> and local customization, and the CI's collection names depend on how the CI
> was built. Field names marked † are the most likely to differ. Before you code,
> open each CI in App Designer, expand every collection, and check the field
> names against the tables below.

---

## 0. Load order and the two keys that tie everything together

```
PESC file
  │
  ├─ (1) Search/Match ──────────────► EMPLID              (not a CI load: match first)
  │
  ├─ (2) ACAD_HIST_PERS CI ─────────► EXT_ACAD_DATA / EXT_ACAD_SUM / EXT_DEGREE / EXT_COURSE
  │        Save, then record the EXT_ORG_ID, EXT_CAREER, EXT_DATA_NBR and
  │        each EXT_COURSE_NBR you created
  │
  └─ (3) TRNS_CRSE_MNL CI ──────────► TRNS_CRSE_SCH / TRNS_CRSE_TERM / TRNS_CRSE_DTL
           Each equivalency row points back to an EXT_COURSE_NBR from step 2
```

* **ACAD_HIST_PERS must be saved first.** Manual transfer credit reads its
  incoming courses from external education. In the online page the incoming
  course prompts against `EXT_COURSE`. If the `EXT_COURSE` row doesn't exist,
  the TRNS_CRSE_MNL row has nothing valid to point at.
* **PESC can't give you the equivalent course.** PESC describes the
  *sending* school's course. Your `CRSE_ID` / `CRSE_OFFER_NBR` has to come
  from a crosswalk you maintain: your articulation table, the delivered
  equivalency rules (`TRNS_CRSE_RULE`, the same rules TRNS_CRSE_AUTO uses), or
  a generic elective course.

---

## 1. PESC v1.8 tree (only the parts that map)

```
CollegeTranscript
├── TransmissionData                    → mostly not mapped (dates / official flag only)
├── Student
│   ├── Person                          → Search/Match only (EMPLID)
│   └── AcademicRecord  (1..n)          → one EXT_ORG_ID + EXT_CAREER + EXT_DATA_NBR set each
│       ├── School                      → EXT_ORG_ID
│       ├── StudentLevel                → EXT_CAREER
│       ├── AcademicAward  (0..n)       → EXT_DEGREE (+ degree field of study)
│       │   ├── AcademicAwardProgram    → degree field of study child
│       │   └── AcademicHonors          → degree honors
│       ├── AcademicSession  (0..n)     → term for each EXT_COURSE (not stored on its own)
│       │   ├── AcademicSessionDetail   → EXT_TERM_TYPE / EXT_TERM / TERM_YEAR
│       │   ├── AcademicSummary         → usually not loaded (term GPA)
│       │   └── Course  (0..n)          → EXT_COURSE  ──►  TRNS_CRSE_DTL
│       ├── AcademicSummary  (0..n)     → EXT_ACAD_SUM
│       └── Course  (0..n, no session)  → EXT_COURSE (derive term from dates)
└── (Tests, Health, NoteMessage)        → out of scope for these two CIs
```

---

## 2. ACAD_HIST_PERS (External Education)

### Component hierarchy as the CI sees it

| CI level | Record | Keys (beyond EMPLID) | One row per… |
|---|---|---|---|
| 0 | (person header) | EMPLID | student |
| 1 | `EXT_ACAD_DATA` | EXT_ORG_ID, EXT_CAREER, EXT_DATA_NBR | AcademicRecord (school × career × transcript received) |
| 2 | `EXT_ACAD_SUM` | + EXT_SUMM_TYPE | AcademicRecord/AcademicSummary |
| 1 or 2 | `EXT_DEGREE` | EXT_ORG_ID, EXT_DEGREE_NBR | AcademicAward |
| 2/3 | degree field of study † | + sequence | AcademicAwardProgram |
| 1 or 2 | `EXT_COURSE` | EXT_ORG_ID, EXT_COURSE_NBR | Course (session-level and record-level) |

`EXT_DEGREE` and `EXT_COURSE` are keyed by EXT_ORG_ID plus their own sequence
number. They tie back to the transcript data set by carrying EXT_CAREER and
EXT_DATA_NBR as non-key fields. Populate those on every row so degrees and
courses link to the right `EXT_ACAD_DATA` row.

### 2a. Level 0: the person

| PESC | CS | Notes |
|---|---|---|
| `Student/Person/SchoolAssignedPersonID` (sender's ID) | — | Keep for audit / search-match. Optionally store as an external system ID (EXTERNAL_SYST_ID) |
| `Student/Person/RecipientAssignedID` | EMPLID | Present only if your request carried your ID. Use it if it's there |
| `Person/SSN`, `Birth/BirthDate`, `Name/*`, `Gender`, `Contacts/*` | EMPLID (via Search/Match) | Match input only. Don't push demographics through this CI |

### 2b. Level 1: `EXT_ACAD_DATA` (one per AcademicRecord)

| PESC tag | CS field | Rule |
|---|---|---|
| `AcademicRecord/School/(CEEBACT \| ATP \| FICE \| IPEDS \| OPEID)` | EXT_ORG_ID | Look up the external org by school code. ATP/CEEB and FICE are on the org's school data. OPEID/IPEDS usually need a custom crosswalk. Don't build EXT_ORGs on the fly from `OrganizationName` |
| `AcademicRecord/StudentLevel/StudentLevelCode` (fall back to `AcademicSummary/AcademicSummaryLevel`, then `Course/CourseCreditLevel`) | EXT_CAREER | Crosswalk: `Undergraduate*` → UGRD, `GraduateProfessional`/`Graduate*` → GRAD, `Professional*` → PROF, etc. |
| — (generated) | EXT_DATA_NBR | Next number for EMPLID + EXT_ORG_ID + EXT_CAREER. One per transcript received |
| `TransmissionData/CreatedDateTime` | TRNSCR_DT † | Transcript date (date part) |
| — (system date) | RECEIVED_DT † | Date received |
| — (constant) | TRNSCR_FLAG † = `Y` | Transcript on file |
| `TransmissionData/DocumentOfficialCode` (`Official`/`Unofficial`) | TRNSCR_TYPE † | Crosswalk to your XLAT |
| — (constant) | TRNSCR_STATUS † | e.g., Received / Verified per your XLAT |
| — (constant) | DATA_SOURCE † | Your "PESC/electronic" source value |
| min `AcademicSessionDetail/SessionBeginDate` | FROM_DT † | Attendance from |
| max `AcademicSessionDetail/SessionEndDate` | TO_DT † | Attendance to |
| `StudentLevel/StudentLevelCode` (secondary/class level) | EXT_ACAD_LEVEL † | Optional. Mostly relevant for high school |

### 2c. Level 2: `EXT_ACAD_SUM` (career-level AcademicSummary)

Use **only** `AcademicRecord/AcademicSummary` (the cumulative/career summary).
`AcademicSession/AcademicSummary` holds term GPAs, and External Education has
no term-summary row, so skip it unless you define a separate EXT_SUMM_TYPE for it.

| PESC tag | CS field | Rule |
|---|---|---|
| `AcademicSummary/AcademicSummaryType` (`All`, `SenderOnly`, `TransferOnly`…) | EXT_SUMM_TYPE | Crosswalk. Most schools load only `All` (overall) and `SenderOnly` (institutional) |
| `AcademicSummary/GPA/GradePointAverage` | EXT_GPA † | |
| `GPA/GPARangeMaximum` (+ `GPARangeMinimum`) | GPA_TYPE † | Pick the GPA type that matches the scale (e.g., 4.0) |
| — | CONVERT_GPA † | Leave blank and let component logic convert, or compute it yourself |
| `GPA/CreditHoursAttempted` | UNITS_ATTEMPTED † | |
| `GPA/CreditHoursEarned` | UNITS_COMPLETED † | If the field exists in your release |
| `GPA/TotalQualityPoints`, `GPA/CreditHoursforGPA` | — | Usually not stored. Useful for checking the GPA |
| `Course/CourseCreditUnits` (or session type) | UNT_TYPE † | `Semester` → SEM, `Quarter` → QTR, `ClockHours`… per your XLAT |
| `AcademicSummary/ClassRank` | CLASS_RANK † | Usually only on secondary transcripts |
| `AcademicSummary/ClassSize` | CLASS_SIZE † | |
| `AcademicSummary/AcademicHonors/*` | — | Overall honors usually go on the degree (2d) |

No `GPA` element means no `EXT_ACAD_SUM` row. Don't insert a row of zeros.

### 2d. `EXT_DEGREE` (one per AcademicAward)

| PESC tag | CS field | Rule |
|---|---|---|
| — (generated) | EXT_DEGREE_NBR | Next number for EMPLID + EXT_ORG_ID |
| `AcademicAward/AcademicAwardLevel` + `AcademicAwardTitle` | DEGREE | Crosswalk to `DEGREE_TBL` (level `Bachelor*` + title "BS" → BS) |
| `AcademicAward/AcademicAwardTitle` | DESCR † | Free-text title as sent |
| `AcademicAward/AcademicAwardDate` (fall back to `AcademicCompletionDate`) | DEGREE_DT † | |
| `AcademicAward/AcademicCompletionIndicator` | DEGREE_STATUS † | `true` → Awarded/Completed. `false` → In Progress |
| `AcademicAward/AcademicHonors/HonorsLevel` | HONORS_CATEGORY † | Crosswalk (`CumLaude`, `MagnaCumLaude`, …) |
| (from parent AcademicRecord) | EXT_CAREER, EXT_DATA_NBR | Links the degree to its 2b row |

**Degree field of study** (child of EXT_DEGREE, one per `AcademicAwardProgram`):

| PESC tag | CS field | Rule |
|---|---|---|
| `AcademicAwardProgram/AcademicProgramType` (`Major`/`Minor`/`Concentration`) | field-of-study type † | Crosswalk |
| `AcademicAwardProgram/AcademicProgramCode` (CIP) and/or `AcademicProgramName` | field of study / subject area † | CIP → your external subject area if you keep one. Otherwise store the name in a description field |

### 2e. `EXT_COURSE` (one per Course)

Courses can show up in two places. Load both:
`AcademicRecord/AcademicSession/Course` (the usual place) and `AcademicRecord/Course`
(courses with no session).

| PESC tag | CS field | Rule |
|---|---|---|
| — (generated) | EXT_COURSE_NBR | Next number for EMPLID + EXT_ORG_ID. **Save it. TRNS_CRSE_MNL needs it** |
| `Course/CourseSubjectAbbreviation` | SCHOOL_SUBJECT | |
| `Course/CourseNumber` | SCHOOL_CRSE_NBR | |
| `Course/CourseTitle` | DESCR | Truncate to the field length |
| parent `AcademicSessionDetail/SessionSchoolYear` or year of `SessionDesignator` (`YYYY-MM`) | TERM_YEAR | For sessionless courses, use the year of `CourseBeginDate` |
| parent `AcademicSessionDetail/SessionType` (`Semester`, `Quarter`, `Trimester`, `Summer`…) | EXT_TERM_TYPE | Crosswalk |
| parent `AcademicSessionDetail/SessionName` / month of `SessionDesignator` | EXT_TERM | Crosswalk ("Fall 2023" or `2023-08` → your FALL code) |
| `Course/CourseBeginDate` (fall back to `SessionBeginDate`) | BEGIN_DT † | |
| `Course/CourseEndDate` (fall back to `SessionEndDate`) | END_DT † | |
| `Course/CourseAcademicGrade` | CRSE_GRADE_INPUT | Grade as sent ("A-", "P", "W") |
| `Course/CourseAcademicGradeScaleCode` | — | Use it to pick the grading scheme when converting grades in TRNS_CRSE_MNL |
| `Course/CourseCreditValue` | EXT_UNITS † / UNT_TAKEN † | Attempted units |
| `Course/CourseCreditEarned` | earned-units field † | If your release has one. Otherwise use it to decide whether the course is transferable |
| `Course/CourseCreditUnits` | UNT_TYPE | `Semester`, `Quarter`, … → your XLAT |
| `Course/CourseCreditBasis` (`Regular`, `AdvancedPlacement`, `CreditByExam`, `TransferCredit`…) | EXT_CRSE_TYPE † | Crosswalk. See the next bullet about `TransferCredit` |
| `Course/CourseCreditLevel` | — | Confirms EXT_CAREER when StudentLevel is missing |
| `Course/CourseRepeatCode` | repeat field † | Keep it so you can apply repeat rules in TRNS_CRSE_MNL |
| `Course/CourseQualityPointsEarned` | grade points field † | Optional |
| (from parent AcademicRecord) | EXT_CAREER, EXT_DATA_NBR | Links the course to its 2b row |
| — (constant) | TRANSCRIPT_FLAG † = `Y` | |

Watch out for these courses:

* **`CourseCreditBasis = TransferCredit`** is credit the sending school
  accepted from a *third* school. Either skip it, or, when
  `Course/CourseOverrideSchool` is present, load it under *that* school's
  EXT_ORG_ID. Don't count it as the sender's own work.
* **`CourseAcademicGradeStatusCode`** (withdrawn, incomplete, in progress)
  should feed TRNSFR_STAT in TRNS_CRSE_MNL (reject or skip), not the
  EXT_COURSE load.

---

## 3. TRNS_CRSE_MNL (Course Credits - Manual)

### Component hierarchy as the CI sees it

| CI level | Record | Keys (beyond EMPLID) | Source |
|---|---|---|---|
| 0 | `TRNS_CRSE_SCH` | ACAD_CAREER, INSTITUTION, MODEL_NBR | **Your** career/institution plus a new model number |
| 1 | `TRNS_CRSE_TERM` | + ARTICULATION_TERM | **Your** term (not PESC) |
| 2 | `TRNS_CRSE_DTL` | + TRNSFR_EQVLNCY_GRP, TRNSFR_EQVLNCY_SEQ | One per incoming PESC course (or course group) |

Plan for **one model per AcademicRecord** (school × external career × transcript),
so each model maps cleanly to one `EXT_ACAD_DATA` row.

### 3a. Level 0: `TRNS_CRSE_SCH`

| Source | CS field | Rule |
|---|---|---|
| EMPLID from step 1 | EMPLID | |
| Your admit/program career (`ACAD_PROG` / application) | ACAD_CAREER | **Your** career, not EXT_CAREER |
| Your institution | INSTITUTION | |
| — (generated) | MODEL_NBR | Next number for EMPLID + ACAD_CAREER + INSTITUTION |
| — (constant `E` = External) | TRNSFR_SRC_TYPE † | External organization source |
| = EXT_ORG_ID from 2b | EXT_ORG_ID † (source institution) | |
| = EXT_CAREER / EXT_DATA_NBR from 2b | EXT_CAREER †, EXT_DATA_NBR † | Ties the model to the external data set |

### 3b. Level 1: `TRNS_CRSE_TERM`

| Source | CS field | Rule |
|---|---|---|
| Admit term / current term (business rule) | ARTICULATION_TERM | Not in PESC |
| Student's active program/plan | ACAD_PROG, ACAD_PLAN | Not in PESC. Needed if your equivalency or requirement designation depends on plan |
| — (constant) | MODEL_STATUS † | Load as Not Posted for evaluator review, or post in code (next note) |

**Posting:** online, posting is a pushbutton FieldChange on the term level.
Through a CI you either set that button field (if it's exposed) and `Save()`,
or leave the model unposted and have evaluators post it. Load unposted first
until you trust the grade and equivalency crosswalks.

### 3c. Level 2: `TRNS_CRSE_DTL` (one per incoming course)

| Source (PESC tag or derived) | CS field | Rule |
|---|---|---|
| — (generated) | TRNSFR_EQVLNCY_GRP | 1, 2, 3… per incoming course. Use one group for many-to-one equivalencies |
| — (generated) | TRNSFR_EQVLNCY_SEQ | 1, 2… inside the group |
| EXT_COURSE_NBR saved in step 2 | EXT_COURSE_NBR † | **The link.** Set it first. On many images its FieldChange fills subject, catalog number, term and grade from EXT_COURSE |
| `Course/CourseSubjectAbbreviation` | SCHOOL_SUBJECT † | Only if the EXT_COURSE_NBR FieldChange didn't fill it |
| `Course/CourseNumber` | SCHOOL_CRSE_NBR † | Same as above |
| parent session (same as 2e) | EXT_TERM †, TERM_YEAR † | Same as above |
| `Course/CourseAcademicGrade` | CRSE_GRADE_INPUT | Incoming grade |
| **Your equivalency crosswalk** keyed by EXT_ORG_ID + subject + course number (+ term range) | CRSE_ID, CRSE_OFFER_NBR | Not in PESC. With no match, use your generic elective course (e.g., "XXX 1XX") or skip the row and leave it for the evaluator |
| `Course/CourseCreditValue` (converted with `CourseCreditUnits`) | UNT_TAKEN / UNT_TRNSFR | Convert quarter to semester (×⅔) if your setup doesn't do it. Cap at the equivalent course's units if that's your policy |
| `CourseAcademicGradeScaleCode` → your setup | GRADING_SCHEME, GRADING_BASIS | Usually constants for transfer work (e.g., the transfer grading basis) |
| Your transfer grade table | CRSE_GRADE_OFF | Converted from CRSE_GRADE_INPUT. The component often derives it from scheme/basis plus input grade |
| `Course/CourseRepeatCode` | REPEAT_CODE † | Crosswalk |
| `CourseAcademicGradeStatusCode`, minimum transferable grade, `CourseCreditEarned = 0` | TRNSFR_STAT † | Accepted vs Rejected per your policy |
| Your policy | VALID_ATTEMPT †, RQMNT_DESIGNTN † | Optional |

---

## 4. PESC sections that map to neither CI

| PESC section | Where it goes instead (if anywhere) |
|---|---|
| `TransmissionData` (except CreatedDateTime / DocumentOfficialCode) | Your own log table: DocumentID, RequestTrackingID, Source/Destination |
| `Person` demographics, `Contacts`, `Residency`, `Deceased` | Search/Match only, or the bio/demo CIs if you choose to update them |
| `AcademicSession/AcademicSummary` (term GPA) | Not stored (or a custom EXT_SUMM_TYPE) |
| `AcademicSession/AcademicProgram` (major during a term) | Not stored. Use it for equivalency lookups if they depend on major |
| `Student/Test` (SAT/ACT/AP results printed on the transcript) | Test results component (STDNT_TEST_COMP) |
| `NoteMessage` (any level) | Optional comments: transcript-level → EXT_ACAD_DATA comments if your image has them, otherwise a log table |
| `AcademicRecord/AcademicHonors` (non-degree honors, e.g., Dean's List) | Usually dropped. Honors/awards component if you track them |

---

## 5. Crosswalk tables you'll need

| Crosswalk | From (PESC) | To (CS) |
|---|---|---|
| School | CEEB/ATP, FICE, OPEID, IPEDS | EXT_ORG_ID |
| Career | StudentLevelCode / CourseCreditLevel | EXT_CAREER |
| Summary type | AcademicSummaryType | EXT_SUMM_TYPE |
| Session | SessionType + SessionName / SessionDesignator | EXT_TERM_TYPE, EXT_TERM, TERM_YEAR |
| Units | CourseCreditUnits | UNT_TYPE (+ conversion factor) |
| Degree | AcademicAwardLevel + AcademicAwardTitle | DEGREE |
| Honors | HonorsLevel | HONORS_CATEGORY |
| Course basis | CourseCreditBasis | EXT_CRSE_TYPE |
| Grade | CourseAcademicGradeScaleCode + CourseAcademicGrade | GRADING_SCHEME / CRSE_GRADE_OFF |
| **Equivalency** | EXT_ORG_ID + subject + number (+ term range) | CRSE_ID / CRSE_OFFER_NBR |

When a lookup fails, park the whole AcademicRecord in a suspense/log table.
Don't let the CI save half a transcript.

---

## 6. Handling tags that aren't populated

* **No `AcademicSession`:** courses hang directly off `AcademicRecord`. Derive
  TERM_YEAR, EXT_TERM_TYPE and EXT_TERM from `CourseBeginDate`. If there's no
  date either, use a default "Unknown" term code.
* **No `StudentLevel`:** take EXT_CAREER from `AcademicSummaryLevel` or the
  most common `CourseCreditLevel`.
* **No `AcademicAward`:** no EXT_DEGREE row. That's normal for a student still
  in progress.
* **No `GPA`:** no EXT_ACAD_SUM row.
* **Multiple `AcademicRecord`s** (career change at the same school, or one
  record per school when an aggregator sends a combined transcript): each one
  gets its own EXT_CAREER + EXT_DATA_NBR set and its own TRNS_CRSE_MNL model.
* In code, check each node before you read it (`XmlNode.IsNull`, or
  `FindNode(...)` returning an empty node) instead of assuming the element is
  there. Repeating elements can occur zero times.

## 7. CI coding notes

* Process one **AcademicRecord** per CI `Save()` so a failure only rolls back
  that school/career. Call `Cancel()` between records.
* Generate the sequence numbers (EXT_DATA_NBR, EXT_DEGREE_NBR,
  EXT_COURSE_NBR, MODEL_NBR) yourself with `MAX()+1` inside the same unit of
  work, unless your image's component SaveEdit/SavePreChange assigns them. If
  it does, read them back from the collection after `Save()`.
* Set `InteractiveMode = False` and `GetHistoryItems = True`. Check
  `PSMessages` after every `Save()`. Most crosswalk mistakes show up there as
  prompt-table edit errors (an invalid DEGREE, EXT_TERM or CRSE_ID).
* For parsing, `DG_SERIALIZE:Xml:XmlReader` in this repo can turn the PESC XML
  into a `DataNode` tree. It has three limits. Member names come from
  `NodeName`, so they keep any namespace prefix (`ColTrn:CollegeTranscript`).
  An element that occurs once is a plain member, not a one-item array, so
  `Course`, `AcademicSession` and the other repeating elements need to handle
  both shapes. Attributes are dropped. The delivered `XmlDoc` /
  `FindNodes()` is the simpler choice if you'd rather not deal with those.
