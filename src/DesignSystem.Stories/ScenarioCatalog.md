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
| `Success` | Happy path (unwrapped stories) | Login, Register, Two-Factor and Recovery Code Sign-In: `NextUrl = "/"`. Forgot Password: `NextUrl = "/Account/ForgotPasswordConfirmation"`; Reset Password: `NextUrl = "/Account/ResetPasswordConfirmation"`. Confirm Email and Resend Email Confirmation: `Value = true`. Confirm Email Change: `NextUrl = "/Account/Login"` |
| `InvalidCredentials` | Login's generic rejection | "Invalid email or password." — deliberately never says which credential failed |
| `LockedOut` | Login and Two-Factor lockout feedback | "This account is locked out. Try again later or reset your password." |
| `NotAllowed` | Login precondition failure | "Sign-in is not allowed for this account." |
| `RegistrationConflict` | Register email-taken | `Email`: "Email 'taken@example.com' is already taken." |
| `RegistrationValidation` | Register and Reset Password password-policy rejection | `Password`: the three complexity messages the `"aaaaaaaa"` fixture provably yields |
| `Fault` | Unmapped failure | Correlation reference `0badc0de-0bad-c0de-0bad-c0de0badc0de` — fixed and obviously synthetic |
| `TwoFactorRequired` | Login success that demands a second factor | `NextUrl = "/Account/LoginWith2fa?challenge=story"` — begun as a session transition the catalog suppresses and records |
| `InvalidTwoFactorCode` | Two-Factor wrong authenticator code | "Invalid authenticator code." — the form recasts any credential failure to its own local text |
| `InvalidRecoveryCode` | Recovery Code Sign-In wrong recovery code | "Invalid recovery code entered." — recast locally the same way |
| `InvalidResetToken` | Reset Password invalid or expired emailed code | "The reset link is invalid or has expired." — recast locally the same way |
| `EmailConfirmationFailed` | Confirm Email / Confirm Email Change rejection | The panel's own "Error confirming your email." / "Error changing email." over the fake's "The confirmation link is invalid or has expired." |

### Train 1 surfaces

The account-lifecycle surfaces drive these scenarios:

| Story | Scenario | Driver |
|---|---|---|
| Login → Two-Factor Required | `TwoFactorRequired` | `FillAndSubmit` |
| Two-Factor → Invalid Code | `InvalidTwoFactorCode` | `FillAndSubmit`, `Email = "000000"` (the code is the form's only text input) |
| Two-Factor → Locked Out | `LockedOut` | `FillAndSubmit`, `Email = "000000"` |
| Recovery Code Sign-In → Invalid Code | `InvalidRecoveryCode` | `FillAndSubmit`, `Email = "0000-0000"` |
| Forgot Password → Fault | `Fault` | `FillAndSubmit` |
| Reset Password → Password Policy | `RegistrationValidation` | `FillAndSubmit` |
| Reset Password → Invalid Token | `InvalidResetToken` | `FillAndSubmit` |
| Confirm Email → Confirmed | `Success` | `ClickOnly` |
| Confirm Email → Failed / Confirm Email Change → Failed | `EmailConfirmationFailed` | `ClickOnly` |
| Resend Email Confirmation → Sent | `Success` | `FillAndSubmit` |

Every "Validation Errors" story is `SubmitOnly` under the ambient `Success`; every "Expired Challenge" and
"Invalid Link" story renders the component without its challenge or link parameters. Forgot Password and
Reset Password succeed by soft navigation, so neither stages a driven success story — their succeeded states
are the confirmation pages ("Email Sent", "Password Reset"); `DrivenStoryNavigationTests` locks both
navigations, and the suppressed transitions of Two-Factor, Recovery Code Sign-In and Confirm Email Change —
the last stages no "Confirmed" story, because its suppressed success renders identically to "Confirm" (the
Logout precedent).

## Reference scenarios

| Scenario | What renders | Canonical shape |
|---|---|---|
| `Success` | Happy path (unwrapped stories) | Every recognized code resolves for real off `Iso3166` — Alpha-2/Alpha-3/Name/Code straight from the generated dataset; `Region` renders only for the United States, the catalog's one hand-fixtured ancestry chain (Americas → Northern America) |
| `Fault` | Unmapped failure | Correlation reference `0badc0de-0bad-c0de-0bad-c0de0badc0de` — the same fixed sentinel the authentication scenarios use |

`CountryLookup`'s validation-errors state needs no scenario at all — `CountryRequestValidator` blocks an empty submit client-side, the same as Login's.

## The playground sentinel

The `Login → Default` story stays interactive: type **`fail@example.com`** into its Email field
(any password) and submit to see the invalid-credentials state live. The sentinel is a garnish —
pinned stories, not magic inputs, are how states are reached in this catalog.

## Driven stories

States that only exist after a submit (`Locked Out`, `Invalid Password`, …) are pinned by
`StoryDriver`, which fills valid-shaped values (password fixture `"aaaaaaaa"`) and submits the form
after first render — the Storybook play-function idiom, with zero changes to shipping components.
Click-to-confirm surfaces (Logout, Confirm Email, Confirm Email Change) are driven by `ClickOnly`, which
clicks the story's first button instead.
