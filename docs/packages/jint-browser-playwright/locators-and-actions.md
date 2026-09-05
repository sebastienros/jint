# Locators and actions

Create a CSS locator or a supported role locator:

```csharp
var save = page.Locator("#save");
var submit = page.GetByRole(AriaRole.Button, new() { Name = "Submit" });
```

Role locators map roles to CSS candidates and apply a first-pass accessible-name filter. Supported name options are `Name`, `NameString`, `NameRegex`, `Exact`, and `IncludeHidden`. This is not Playwright's complete accessibility-name algorithm.

Locators are strict by default: an unindexed locator resolving to multiple elements fails. Use:

```csharp
locator.First
locator.Last
locator.Nth(2)
```

Supported reads:

- `CountAsync`
- `AllTextContentsAsync`
- `TextContentAsync`
- `InputValueAsync`
- `IsVisibleAsync`

Supported waits and actions:

- `WaitForAsync` with attached, detached, visible, or hidden state
- `ClickAsync`
- `FillAsync`
- `PressAsync`

Only the `Timeout` option is supported for locator actions and value reads. `WaitForAsync` also supports `State`. Other non-default options fail explicitly.

```csharp
await page.Locator("#email").FillAsync("ada@example.org");
await page.Locator("#submit").ClickAsync();
```

Actions use `Jint.Browser` trusted input and synthetic geometry. They do not implement Playwright's full actionability, trial, force, position, modifier, or atomic locator-resolution semantics.
