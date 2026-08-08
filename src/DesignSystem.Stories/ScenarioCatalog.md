---
$attribute: CustomPage("Scenarios")
---

# Scenarios

Every pinned story in this catalog selects its state through a **scenario** — an ambient value a
story declares with `ScenarioScope` and the backing fake obeys. Nothing here talks to a server;
nothing here accumulates state. A story renders the same way every time you load it.

## Authentication scenarios

| Scenario | What renders | Canonical shape |
|---|---|---|
| `Success` | Happy path (unwrapped stories) | `NextUrl = "/"` / `Succeeded = true` |
| `InvalidCredentials` | Login's generic rejection | "Invalid email or password." — deliberately never says which credential failed |
| `LockedOut` | Login lockout feedback | "This account is locked out. Try again later or reset your password." |
| `NotAllowed` | Login precondition failure | "Sign-in is not allowed for this account." |
| `RegistrationConflict` | Register email-taken | `Email`: "Email 'taken@example.com' is already taken." |
| `RegistrationValidation` | Register password-policy rejection | `Password`: the three complexity messages the `"aaaaaaaa"` fixture provably yields |
| `Fault` | Unmapped failure | Correlation reference `0badc0de-0bad-c0de-0bad-c0de0badc0de` — fixed and obviously synthetic |

## The playground sentinel

The `Login → Default` story stays interactive: type **`fail@example.com`** into its Email field
(any password) and submit to see the invalid-credentials state live. The sentinel is a garnish —
pinned stories, not magic inputs, are how states are reached in this catalog.

## Driven stories

States that only exist after a submit (`Locked Out`, `Invalid Password`, …) are pinned by
`StoryDriver`, which fills valid-shaped values (password fixture `"aaaaaaaa"`) and submits the form
after first render — the Storybook play-function idiom, with zero changes to shipping components.
