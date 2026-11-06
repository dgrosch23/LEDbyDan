# DG_XMLMERGE — append File Layout output to an existing XML file

`DG_XMLMERGE:XmlFileMerger` reads an existing XML file, such as a BI Publisher
transcript data file written by a delivered File Layout. It appends extra
sections rendered by your own XML File Layout from a Rowset, then saves the
result to the same path under the same file name.

## Transcript example (CGER sections)

[`Example/AppendTranscript.ppl`](Example/AppendTranscript.ppl) and
[`Example/CgerSectionProvider.ppl`](Example/CgerSectionProvider.ppl) add
`<CGERData>` (with nested `<CGERCourseInfo>`) inside every
`<Start>/<G_SSR_TSRSLT_HDR>`. The new sections go after the header fields and
before `<G_SSR_TSRSLT_SVW1>`:

```
&doc    = &merge.Load(&path);
&merge.RecordTag = "CGERData";
&merged = &merge.InsertSections(&doc, "G_SSR_TSRSLT_HDR", "EMPLID", &cger, "Z_CGER_TSCRPT_FL");
&merge.Save(&merged, &path);
```

`InsertSections` rebuilds the document in order. For each block (one
`G_SSR_TSRSLT_HDR` per student/report), it calls your `SectionProvider` with
the block's `EMPLID` and writes the returned rowset through your layout at the
insert point:

* before the child named `InsertBeforeTag`, if you set it, otherwise
* before the block's first child group (the first child element that has
  child elements of its own), otherwise
* at the end of the block.

The provider returns `Null` when a student has no CGER data, and that
student's block is copied unchanged.

## Installing

Create application package `DG_XMLMERGE` with classes `SectionProvider` and
`XmlFileMerger`, plus a subpackage `Example` with `CgerSectionProvider`. Save
them in that order. You'll probably copy the provider into your own `UW_`
package rather than keep it under `Example`.

## Low-level flow (AppendRowset)

1. `Load(&path)` reads the file with `GetFile` and parses it into an `XmlDoc`.
2. You load a Rowset with your SQL, for example with `Fill` or a `CreateSQL` loop.
3. `AppendRowset(&parentNode, &rs, "MY_LAYOUT")`:
   * writes the rowset with `File.WriteRowset` through your custom layout to a
     work file (`<path>.dgtmp`, deleted afterwards),
   * reads the work file back and parses it,
   * copies each top-level record element, children included, under
     `&parentNode` with `XmlNode.CopyNode`.
4. `Save(&doc, &path)` serializes the document, checks that it parses, and
   overwrites the original. The original file isn't touched before this step.

## Requirements and gotchas

* The custom File Layout's **Format must be XML**. The rowset you pass must
  match its record hierarchy: the same records, the same parent/child levels.
* The tag `AppendRowset` looks for is the rowset's `DBRecordName`. If you
  renamed the **File Record ID** in the layout, set `&merge.RecordTag`.
* The layout's file record names become the tags, so name them `CGERData` and
  `CGERCourseInfo` in your custom layout. Layout output is wrapped in its own
  `<Start>` with an `<?xml?>` declaration. Both are dropped automatically:
  only the `RecordTag` elements and their children are copied.
* Field formats (e.g. `EFFDT` as `09/21/2026`) come from your layout's field
  settings, not from this code.
* XML File Layouts put `class="R"` on record elements. These attributes are
  removed by default (`StripLayoutAttributes = True`).
* `AppendRowset` adds the new elements as the **last children** of the node you pass. Use `InsertSections` to place them mid-block.
* The process (App Server or Process Scheduler) needs write access to the
  directory, because the work file and `.bak` go next to the original.
* **BI Publisher:** to use the new tags in the RTF/XSL template, regenerate
  the data source's sample XML/schema from a merged file.
* Default charset is `UTF8`. Change `Charset` if your files use something else.

## Options

| Property | Default | Meaning |
|---|---|---|
| `Charset` | `UTF8` | Charset for `GetFile` |
| `Pretty` | False | Indent the saved XML |
| `KeepBackup` | False | Write `<path>.bak` before overwriting |
| `StripLayoutAttributes` | True | Remove `class="R"` from appended elements |
| `WorkFile` | `<path>.dgtmp` | Scratch file for `WriteRowset` |
| `RecordTag` | rowset `DBRecordName` | Element that marks one row in the layout output (`CGERData`) |
| `InsertBeforeTag` | blank = first child group | `InsertSections` insert point |
| `BlocksProcessed`, `SectionsAdded` | | Counters set by `InsertSections` |

Like `DG_SERIALIZE`, this hasn't been compiled against a live PeopleTools
environment yet. It uses `XmlNode.CopyNode` (including across two `XmlDoc`s), `XmlNode.RemoveAttribute`,
`XmlDoc.GetElementsByTagName`, and `@("FileLayout." | &name)` with
`SetFileLayout`. If `@` doesn't resolve a FileLayout reference on your
release, hard-code `FileLayout.YOUR_LAYOUT` in `AppendRowset`.
