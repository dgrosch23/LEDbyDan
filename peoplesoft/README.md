# DG_SERIALIZE — PeopleSoft serialization application package

A PeopleCode application package that serializes and deserializes data between
**JSON**, **XML**, **database data** (Record, Rowset, SQL, tables) and
**application class instances**.

Every conversion goes through one format-neutral tree, `DataNode`. With N
formats you need N adapters instead of N×N converters, and any source can
reach any target:

```
                 JSON text        XML text
                     \              /
                  JsonReader/Writer  XmlReader/Writer
                         \          /
  Record / Rowset --- RecordMapper --- DataNode --- ClassMapper --- app classes
  SQL query / table ------/
```

Requires PeopleTools 8.50 or later. The JSON engine is written in plain
PeopleCode, so it doesn't depend on the delivered `JsonParser` and behaves the
same on every release. XML uses the delivered `XmlDoc` API.

## Package layout

| Class | Purpose |
|---|---|
| `DG_SERIALIZE:Serializer` | Facade: one-line conversions between every format |
| `DG_SERIALIZE:Core:DataNode` | Neutral tree: OBJECT, ARRAY, STRING, NUMBER, BOOLEAN, NULL, DATE, DATETIME, TIME |
| `DG_SERIALIZE:Core:IsoDate` | Locale-independent ISO-8601 date/datetime/time formatting and parsing |
| `DG_SERIALIZE:Core:Serializable` | Interface for classes that control their own format |
| `DG_SERIALIZE:Json:JsonReader` | JSON text → DataNode (strict RFC 8259 parser, precision-preserving) |
| `DG_SERIALIZE:Json:JsonWriter` | DataNode → JSON text (compact or pretty) |
| `DG_SERIALIZE:Xml:XmlReader` | XML → DataNode (restores types exactly from hints, or infers them) |
| `DG_SERIALIZE:Xml:XmlWriter` | DataNode → XML (with or without type hints) |
| `DG_SERIALIZE:Db:RecordMapper` | Record, Rowset (with children), SQL query, INSERT/UPDATE/UPSERT/DELETE |
| `DG_SERIALIZE:Obj:ClassSchema` | Declares how an existing application class maps (no changes to the class needed) |
| `DG_SERIALIZE:Obj:PropertyDescriptor` | One property entry in a schema |
| `DG_SERIALIZE:Obj:ClassMapper` | Application class instance ↔ DataNode |
| `DG_SERIALIZE:Test:*` | Sample classes (`Address`, `Employee`, `Money`) and `SelfTest` |

## Installing in Application Designer

1. **File → New → Application Package**, save it as `DG_SERIALIZE`. If your site
   uses a different customer prefix, rename it and search/replace
   `DG_SERIALIZE` in every file.
2. Right-click the package and choose **Insert Application Class** for
   `Serializer`. Then **Insert Package** for `Core`, `Json`, `Xml`, `Db`, `Obj`
   and `Test`, and insert the classes into each one to match the folder
   layout here.
3. Paste each `.ppl` file into its class's PeopleCode. Save in dependency
   order so the imports resolve:
   `Core:IsoDate` → `Core:DataNode` → `Core:Serializable` → `Json:*` → `Xml:*` →
   `Db:RecordMapper` → `Obj:PropertyDescriptor` → `Obj:ClassSchema` →
   `Obj:ClassMapper` → `Serializer` → `Test:Address` → `Test:Money` →
   `Test:Employee` → `Test:SelfTest`.
4. Run the self test, for example from an App Engine PeopleCode step:

   ```
   import DG_SERIALIZE:Test:SelfTest;
   Local DG_SERIALIZE:Test:SelfTest &t = create DG_SERIALIZE:Test:SelfTest();
   Local string &report = &t.Run();
   MessageBox(0, "", 0, 0, &report);
   ```

   It builds its data in memory on the delivered `PSXLATITEM` record, and its
   only database access is a read of `PSXLATITEM`, so it's safe on any
   environment. Skip `Test` in production if you like; nothing else depends
   on it.

## Usage

```
import DG_SERIALIZE:Serializer;
import DG_SERIALIZE:Core:DataNode;

Local DG_SERIALIZE:Serializer &ser = create DG_SERIALIZE:Serializer();
```

### JSON and XML

```
Local DG_SERIALIZE:Core:DataNode &n = &ser.FromJson(&requestBody);
Local string &name = &n.GetString("name");
Local date &dob = &n.GetDate("birthDate");          /* ISO string -> date */
Local DG_SERIALIZE:Core:DataNode &items = &n.GetMember("items");
For &i = 1 To &items.Count
   ... &items.ValueAt(&i).GetNumber("qty") ...
End-For;

/* build a document */
Local DG_SERIALIZE:Core:DataNode &out = create DG_SERIALIZE:Core:DataNode("OBJECT");
&out.PutString("status", "OK");
&out.PutNumber("count", 3);
&out.PutDate("asOf", %Date);

&ser.JsonOut.Pretty = True;
Local string &json = &ser.ToJson(&out);
Local string &xml  = &ser.ToXml(&out);             /* <root dg_type="object">... */
Local string &same = &ser.XmlToJson(&xml);          /* identical to &json */
```

XML options (`&ser.XmlOut`):

* `RootName` (default `root`), `ItemName` (default `item`), `Pretty`.
* `TypeHints` (default True) writes `dg_type="number|boolean|null|array|object|date|..."`,
  so an XML round trip keeps exact types, including single-item arrays. Set it
  to False for plain XML aimed at external systems.
* Member names that aren't valid XML names become `<entry dg_name="...">`.

Without hints, `XmlReader` infers the structure. Child elements become an
object, repeated element names become an array, and text-only elements
become strings. `&ser.XmlIn.LastRootName` holds the root element name.

### Records and rowsets

```
/* Record -> JSON / XML */
Local Record &rec = CreateRecord(Record.PERSONAL_DATA);
&rec.EMPLID.Value = "KU0001";
&rec.SelectByKey();
Local string &json = &ser.RecordToJson(&rec);

/* JSON -> Record: only the fields present in the JSON are touched */
&ser.JsonToRecord(&json, &rec);

/* Rowset with children -> nested JSON */
Local Rowset &rs = CreateRowset(Record.PARENT_REC, CreateRowset(Record.CHILD_REC));
&rs.Fill("WHERE SETID = :1", &setid);
Local string &json = &ser.RowsetToJson(&rs);
/* [ { "SETID":"SHARE", ..., "CHILD_REC":[ {...}, {...} ] } ] */

/* nested JSON -> Rowset of the same shape */
Local Rowset &target = CreateRowset(Record.PARENT_REC, CreateRowset(Record.CHILD_REC));
&ser.JsonToRowset(&json, &target);
```

`&ser.Records` options:

| Property | Default | Meaning |
|---|---|---|
| `KeyStyle` | `"FIELD"` | Member names: `FIELD` (`EMPL_RCD`), `LOWER` (`empl_rcd`), `CAMEL` (`emplRcd`) |
| `IncludeEmpty` | True | Emit blank char fields and null dates |
| `IncludeChildren` | True | Nest child rowsets under their record name |
| `ClearRowsetFirst` | True | `Flush()` the target rowset before loading |

Reading matches keys loosely: `EMPL_RCD`, `empl_rcd`, `emplRcd` and `EmplRcd`
all populate `EMPL_RCD`, whatever `KeyStyle` is set to.

Field types are honoured. Numbers stay numbers, DATE/DATETIME/TIME fields are
written as ISO-8601 (`2024-05-01`, `2024-05-01T13:45:30`, `13:45:30`), and char
fields are right-trimmed. IMAGE and ATTACHMENT fields are skipped.

### SQL and tables

```
/* SELECT -> JSON. Always pass user input as binds. */
Local string &json = &ser.QueryToJson("PSXLATITEM",
      "WHERE FIELDNAME = :1 AND EFF_STATUS = :2 ORDER BY FIELDVALUE",
      CreateArrayAny("EFF_STATUS", "A"));

/* JSON (one object or an array of them) -> table */
Local number &rows = &ser.JsonToDatabase(&json, "MY_STAGE_TBL", "UPSERT");
```

Save modes are `INSERT`, `UPDATE`, `UPSERT` (SelectByKey, then Update or
Insert) and `DELETE`. A failed row throws an exception that names the key
values. Call these from a context where database updates are allowed, such
as SavePostChange, App Engine or an IB handler. The record name is
validated before it's used in dynamic SQL.

### Application classes

**Existing classes you can't change:** describe them with a `ClassSchema`.
The class needs public properties and, for deserialization, a constructor
with no arguments.

```
import DG_SERIALIZE:Obj:ClassSchema;

Local DG_SERIALIZE:Obj:ClassSchema &addr = create DG_SERIALIZE:Obj:ClassSchema("MY_PKG:Model:Address");
&addr.AddScalar("Street", "STRING");
&addr.WithKey("street");                   /* renames the member just added */
&addr.AddScalar("PostalCode", "STRING");
&addr.WithKey("zip");

Local DG_SERIALIZE:Obj:ClassSchema &emp = create DG_SERIALIZE:Obj:ClassSchema("MY_PKG:Model:Employee");
&emp.AddScalar("EmplId", "STRING");
&emp.WithKey("emplid");
&emp.AddScalar("HireDate", "DATE");
&emp.AddScalar("Salary", "NUMBER");
&emp.AddScalarArray("Skills", "STRING");
&emp.AddObject("HomeAddress", "MY_PKG:Model:Address");
&emp.AddObjectArray("Dependents", "MY_PKG:Model:Person");
&emp.AddRecord("JobRow", "JOB");                  /* a Record-typed property */

&ser.Register(&addr);
&ser.Register(&emp);

Local string &json = &ser.ObjectToJson(&employee, "MY_PKG:Model:Employee");
Local MY_PKG:Model:Employee &copy = &ser.JsonToObject(&json, "MY_PKG:Model:Employee");
```

Scalar types are `STRING`, `NUMBER`, `BOOLEAN`, `DATE`, `DATETIME` and `TIME`.
Property kinds are `AddScalar`, `AddScalarArray`, `AddObject`,
`AddObjectArray`, `AddRecord` and `AddRowset`.

**Your own classes:** implement `DG_SERIALIZE:Core:Serializable` (`ToNode()`
and `FromNode(&node)`) to take full control of the format. Any class without
a registered schema is treated as Serializable, including when it's nested
inside a schema-mapped class. See `Test:Money`, which serializes to
`"1250.5 EUR"`.

## Limitations and notes

* Methods that change an object (`Put*`, `Add*`, `WithKey`, `Register`)
  return nothing, so call each one as its own statement. PeopleCode doesn't
  allow a method that returns a value to be called as a statement, which
  rules out fluent chaining.

* Compiled and tested on PeopleTools 8.62.06, where `Test:SelfTest` passes
  all 78 checks. Rerun it after importing into another environment or
  release.
* **Array properties:** if the class constructor creates the array, the
  mapper empties and refills that array, so typed properties such as
  `property array of MY_PKG:Model:Address Addresses;` work. Create it in
  the constructor, for example
  `%This.Addresses = CreateArrayRept(create MY_PKG:Model:Address(), 0);`.
  If the property is Null, the mapper assigns a new array instead: typed
  for scalars, `array of any` for objects. In that case declare
  object-array properties as `array of any`.
* **A single value where an array is expected** is loaded as a one-item
  array. XML without type hints needs this, because one `<Student>` element
  looks the same as a plain object.
* Time zones in ISO datetimes are ignored (the value is read as local
  time), and fractional seconds are truncated.
* XML attributes other than the `dg_*` hint attributes aren't mapped on
  read.
* `NodeToRowset` fills rows by position into a rowset whose shape (child
  rowsets) you create first. A `ROWSET` class property is rebuilt as a flat
  rowset.
* For component-buffer rowsets, set `Records.ClearRowsetFirst = False` if
  you don't want the scroll flushed before it's loaded.

## PeopleCode rules this code follows

These compiler and runtime behaviors came up while porting the package.
Keep to them when changing the code:

* A method that returns a value can't be called as a standalone statement.
  Assign the result, for example `&textNode = &el.AddText(...)`. That's why
  `Put*`, `Add*`, `WithKey` and `Register` return nothing.
* Don't name a parameter or local variable after a property of the same
  class (case-insensitive). `&kind` clashes with property `Kind` and fails
  with "Duplicate parameter name".
* Don't pass a comparison (`a = b`), `And`/`Or` or `Not` directly as a method
  argument. Depending on the call it either won't compile or evaluates to
  False at runtime. Compute a boolean first, or use an `If`.
* Use `Not` only in `If`/`While` conditions. To assign it, use
  `&ok = (&x = False);`.
* `Array.Join` wraps its result in `(` and `)` unless you pass start and end
  strings: `&arr.Join("", "", "")`.
* `CreateException` substitution values must be strings, so build the whole
  message first and pass no substitutions.
* `XmlNode` has no `ChildNodes` property. Use `ChildNodeCount` and
  `GetChildNode(index)`.
* `Array` has no `Splice` method.
