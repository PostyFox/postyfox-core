# Embedded field-descriptor schemas

Field-descriptor schemas are normally string literals — connector config in `ServiceDefinitionSeeder`,
per-submission options on the `ConnectorDescriptor` in `ServiceCollectionExtensions`. A schema lands
here instead when its option lists are too large to keep readable inline. Files matching
`*.schema.json` are embedded resources (see the `.csproj`); `EmbeddedSchema.Load` reads and compacts
them, so the pretty-printed form here is purely for review.

The format is the same field-descriptor JSON used everywhere else — an object keyed by field name.
Two conventions matter here:

- **`options`** — a flat, order-preserving list of `{ value, label, group? }`. The client renders a
  `<select>`, grouping consecutive entries that share a `group` into an `<optgroup>`;
  `ConfigSchemaValidator` rejects any value not in the list. `placeholder` labels the blank "not set"
  choice.
- **Keys starting with `$`** are metadata, not fields. Both the validator and the client skip them,
  which is what makes `$comment` below safe.

## furaffinity-post-options.schema.json

These describe an individual **submission**, not the account, so they are the FurAffinity connector
descriptor's `PostOptionsSchema` rather than its config schema: the compose form renders them once per
selected FurAffinity target, and the chosen values are stored on the `PostTarget` and applied over the
connector's config at delivery. (They were connector settings before — see `DeliverTargetHandler`
`EffectiveConfig`, which strips values left behind by that arrangement.)

`Category` (`cat`), `Theme` (`atype`), `Species` and `Gender` are numeric IDs chosen from fixed lists
on FurAffinity's own submission form, so they can be mirrored verbatim. `FolderIds` cannot — gallery
folders belong to an individual account — so it stays a free-text field.

Leaving a field unset is valid: the Node connector then applies FurAffinity's own defaults (`cat=1`,
`atype=1`, `species=1`, `gender=0`), which is why those values must stay selectable.

These lists change only when FurAffinity changes its form. To refresh one, log in, open
<https://www.furaffinity.net/submit/finalize/>, and run the matching snippet in the browser console
(adapted from [PostyBirb](https://github.com/mvdicarlo/postybirb), which mirrors the same lists):

```js
// name: 'cat' for Category, 'atype' for Theme, 'species' for Species, 'gender' for Gender
const name = 'cat';
JSON.stringify(
  [...document.querySelector(`select[name='${name}']`).options].map((o) =>
    o.parentNode?.label
      ? { value: o.value, label: o.label, group: o.parentNode.label }
      : { value: o.value, label: o.label },
  ),
);
```

Paste the result over that field's `options` array. Values are strings, and order is preserved as-is
from the form. Removing an ID that posts already reference will make a "post again" of one of those
fail validation — check `ConfigSchemaValidator`'s option check before dropping entries.
