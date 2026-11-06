# DG_XMLMERGE — append File Layout output to an existing XML file

`DG_XMLMERGE:XmlFileMerger` reads an existing XML file, such as a BI Publisher
transcript data file written by a delivered File Layout. It appends extra
sections rendered by your own XML File Layout from a Rowset, then saves the
result to the same path under the same file name.

See [`Example_AppendTranscript.ppl`](Example_AppendTranscript.ppl) for the full flow.

## Flow

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
* XML File Layouts put `class="R"` on record elements. These attributes are
  removed by default (`StripLayoutAttributes = True`).
* The new elements are added as the **last children** of the parent node.
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
| `RecordTag` | rowset `DBRecordName` | Element that marks one row in the layout output |

Like `DG_SERIALIZE`, this hasn't been compiled against a live PeopleTools
environment yet. It uses `XmlNode.CopyNode`, `XmlNode.RemoveAttribute`,
`XmlDoc.GetElementsByTagName`, and `@("FileLayout." | &name)` with
`SetFileLayout`. If `@` doesn't resolve a FileLayout reference on your
release, hard-code `FileLayout.YOUR_LAYOUT` in `AppendRowset`.
