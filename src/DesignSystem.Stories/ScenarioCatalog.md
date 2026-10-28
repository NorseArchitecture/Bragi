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
| `ExternalProviders` | The sign-in links with an external provider offered | One provider, "Example provider", started at `/Account/PerformExternalLogin?provider=Example&returnUrl=%2F`; a manual Login submit signs in like `Success`, and it applies to no other write |

The sign-in links (`SignInMethodLinks`, on Login and Login With Passkey) answer every scenario, `Fault`
included, in the server's shape for the resolved landing: `/Account/Login?returnUrl=%2F` and
`/Account/LoginWithPasskey?returnUrl=%2F`. Passkey sign-in answers `Success` (the landing), `InvalidCredentials`,
`LockedOut` and `Fault`; its request options fault only under `Fault`.

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

## Account scenarios

The account reads (status, external logins) answer every scenario but `Fault` with the catalog subject, so
a write story still loads its form; each write answers only the scenarios that apply to it.

| Scenario | What renders | Canonical shape |
|---|---|---|
| `Success` | Happy path (unwrapped stories) | A confirmed account with a password, two-factor on, three recovery codes, two passkeys, Google linked and Microsoft available. Writes: `SetPhoneNumber` → `NextUrl = "/Account/Manage?status=phone-updated"`; `ChangePassword` → `"/Account/Manage/ChangePassword?status=password-changed"`; `SetPassword` → `"/Account/Manage/ChangePassword?status=password-set"`; `RemoveExternalLogin` → `"/Account/Manage/ExternalLogins?status=external-login-removed"`; `DeletePersonalData` → `"/"`; `ChangeEmail` → `Value = true` |
| `ExternalOnly` | A passwordless account whose one login is Google | No password, `CanRemove = false`: the Set Password form, Delete Personal Data without a password field, External Logins without a remove button. `SetPassword` and `DeletePersonalData` succeed as under `Success`; `RemoveExternalLogin` answers `Conflict` keyed `LastSignInMethod`: "This is the only way to sign in to your account." |
| `TwoFactorDisabled` / `RecoveryCodesLow` / `NoPasskeys` | Status variants for Train 3's two-factor and passkey pages | Two-factor off with no authenticator and no codes / one code left / no passkeys |
| `IncorrectPassword` | Change Password and Delete Personal Data wrong current password | "Incorrect password." — on `OldPassword` for Change Password, in the summary for Delete Personal Data |
| `PasswordPolicy` | Change Password and Set Password policy rejection | `NewPassword`: "Passwords must have at least one digit ('0'-'9')." |
| `InvalidVerificationCode` | Enable Authenticator wrong code | `Code`: "Verification code is invalid." |
| `MachineRemembered` | Two-Factor Authentication with this browser remembered | The `Success` status with `IsMachineRemembered = true`, so the page offers "Forget this browser"; applies to no write but the forget |
| `Fault` | Unmapped failure | The catalog correlation reference on every write; External Logins faults on its load instead, rendering "Your external logins could not be loaded." with no reference. Delete Personal Data's shred failure keeps its own line, "Your account could not be fully deleted. The failure has been logged for an administrator to finish." The Train 3 pages render their own fallback line instead (below) |

## Identity scenarios

| Scenario | What renders | Canonical shape |
|---|---|---|
| `Success` | Happy path (unwrapped stories) | The catalog subject `designer@example.com` / `+15551234567`; masked, `d***@e***.com` / `***4567`, straight from the PII primitives |
| `Fault` | Unmapped failure | A load fault: the reading page's own "could not be loaded" line (Profile, Email), with no reference; Personal Data's download does nothing. No story pins it yet |

## Session scenarios

| Scenario | What renders | Canonical shape |
|---|---|---|
| `SignedIn` | The default principal | An authenticated `user`-role account, which passes the `Account` policy |
| `Anonymous` | The anonymous-cookie principal | Authenticated, carrying only the anonymous role, which the `Account` policy refuses |

Stories render components directly, so no page's `[Authorize]` is evaluated in the canvas, and no Train 2
story reads the session; the provider keeps `AuthorizeView` and cascaded-state consumers truthful. Pinning
`SessionScenario` through `ScenarioScope` does not refresh the cascaded state, which is read once per circuit:
a story that needs a session pin must first make the provider notify on the pin.

### Train 2 surfaces

The account-core surfaces under `Account/` drive these scenarios. The Manage pages render inside
`ManageLayout`, which nests the catalog's `StoryShellLayout` (declaring `StoryShell`, a bare `@Body`) in place of the host's chrome.

| Story | Scenario | Driver |
|---|---|---|
| Profile → Validation Errors | `Success` | `SubmitOnly`, over a stored number without its country code (`5551234567`) |
| Profile → Saved / Fault | `Success` / `Fault` | `FillAndSubmit`, `Email = "+15551234567"` (the phone rides the `Email` slot) |
| Email → Link Sent / Fault | `Success` / `Fault` | `FillAndSubmit`, `Email = "new@example.com"` |
| Change Password → Changed / Incorrect Password / Password Policy / Fault | `Success` / `IncorrectPassword` / `PasswordPolicy` / `Fault` | `FillAndSubmit` (current `"aaaaaaaa"`, new `"bbbbbbbb"`) |
| Set Password → Default / Set | `ExternalOnly` | — / `FillAndSubmit` |
| Set Password → Password Policy / Fault | `PasswordPolicy` / `Fault` | `FillAndSubmit` |
| External Logins → External Only / Fault | `ExternalOnly` / `Fault` | — (the page's own load; Fault is a load fault, rendering "Your external logins could not be loaded." with no correlation reference) |
| Delete Personal Data → Passwordless | `ExternalOnly` | — |
| Delete Personal Data → Deleted / Incorrect Password / Fault | `Success` / `IncorrectPassword` / `Fault` | `FillAndSubmit` |
| External Logins → Removed | `Success` | `ClickOnly` (the removal is a suppressed transition; the canvas stays as it was) |
| Personal Data → Download | `Success` | `ClickOnly` (saves `PersonalData.json` each time the story loads) |

Every "Validation Errors" story is `SubmitOnly`. Profile, Change Password, Set Password and Delete Personal
Data succeed by a suppressed session transition, so their success stories keep the submitted form; Email
succeeds in place. External Logins → Removed and Personal Data → Download leave the canvas identical to
"Default" — a suppressed removal and a file download — and stage anyway, because the click driver settles
without a mutation (T3-7); `DrivenStoryNavigationTests` holds the recorded transition and the clicked download.

### Train 3 surfaces

The two-factor and passkey pages under `Account/`, and Login With Passkey, drive or pin these scenarios. The
two-factor and passkey fakes (`FakeTwoFactorService`, `FakePasskeyService`) ride `AccountScenario`; the sign-in
links and passkey sign-in (`FakeSignInMethodsService`) ride `AuthenticationScenario`.

| Story | Scenario | Driver |
|---|---|---|
| Two-Factor Authentication → Default / Two-Factor Disabled / Recovery Codes Low / Fault | `Success` / `TwoFactorDisabled` / `RecoveryCodesLow` / `Fault` | — (the page's own status load; Fault is a load fault) |
| Two-Factor Authentication → Forget This Browser | `MachineRemembered` | `ClickOnly` → suppressed transition to `/Account/Manage/TwoFactorAuthentication?status=browser-forgotten` |
| Enable Authenticator → Verified | `Success` | `FillAndSubmit`, `Email = "123456"` (the code is the form's only text input) → suppressed transition to `/Account/Manage/TwoFactorAuthentication?status=authenticator-verified` |
| Enable Authenticator → Invalid Code | `InvalidVerificationCode` | `FillAndSubmit`, `Email = "123456"` |
| Enable Authenticator → Fault | `Fault` | — (a load fault: no form is offered) |
| Recovery Codes Handoff → Default / Revealed | `Success` | — (the bare route; the shared panel with the ten codes under the verified status) |
| Reset Authenticator → Reset / Two-Factor Disabled | `Success` / `TwoFactorDisabled` | `ClickOnly` → suppressed transition to `/Account/Manage/EnableAuthenticator?status=authenticator-reset` |
| Reset Authenticator → Fault | `Fault` | `ClickOnly` |
| Disable 2FA → Disabled | `Success` | `ClickOnly` → suppressed transition to `/Account/Manage/TwoFactorAuthentication?status=two-factor-disabled` |
| Disable 2FA → Not Enabled / Fault | `TwoFactorDisabled` / `Fault` | `ClickOnly` ("Two-factor authentication is not enabled.") |
| Generate Recovery Codes → Generated | `Success` | `ClickOnly` → the ten codes in place, "You have generated new recovery codes." |
| Generate Recovery Codes → Two-Factor Disabled / Fault | `TwoFactorDisabled` / `Fault` | `ClickOnly` ("Cannot generate recovery codes because two-factor authentication is not enabled.") |
| Passkeys → Default / No Passkeys / Fault | `Success` / `NoPasskeys` / `Fault` | none (Add and Delete run live; Add is a real WebAuthn ceremony) |
| Rename Passkey → Default | `Success` | — (the page loads "Work laptop" by its rename id) |
| Rename Passkey → Fault | `Fault` | `FillAndSubmit` over the form, `Email = "Home desktop"` (the name is the form's only text input) |
| Login With Passkey → Default / Fault | `Success` / `Fault` | none (WebAuthn); under `Fault` a click on "Use a passkey" renders the faulted options read before any ceremony |
| Login → External Providers | `ExternalProviders` | — (the provider link is the state) |

The Train 3 pages render their own fallback line for a fault ("Your authenticator key could not be reset.",
"Your recovery codes could not be generated.", …) with no correlation reference; Rename Passkey's form keeps the
reference in its summary. Enable Authenticator under `TwoFactorDisabled` answers the recovery-code handoff hop,
`/Account/Manage/ShowRecoveryCodes?handoff=story-handoff`, since that account holds no codes; no story stages it,
because it renders identically to "Verified". Three states have no story, because the canvas cannot reach them
without navigating out of the story: the handoff page's own reveal and its fault (the handoff rides the query, and
the page strips it by navigating), and a passkey rename's success (a soft navigation, the Forgot Password shape).
`DrivenStoryNavigationTests` holds all three, over the page itself and the fake's own handoff hop.

## The playground sentinel

The `Login → Default` story stays interactive: type **`fail@example.com`** into its Email field
(any password) and submit to see the invalid-credentials state live. The sentinel is a garnish —
pinned stories, not magic inputs, are how states are reached in this catalog.

## Driven stories

States that only exist after a submit (`Locked Out`, `Invalid Password`, …) are pinned by
`StoryDriver`, which fills valid-shaped values (password fixture `"aaaaaaaa"`, and `"bbbbbbbb"` for every
later password field) into every enabled text input and submits the form after first render — the Storybook
play-function idiom, with zero changes to shipping components. A disabled input is a read-only display, so
the driver leaves its value alone.
Click-to-confirm surfaces (Logout, Confirm Email, Confirm Email Change, External Logins, Personal Data and the
Train 3 two-factor pages) are driven by `ClickOnly`, which clicks the story's first button instead. A click need
not change the page (T3-7): a suppressed transition or a saved file leaves the canvas as it was, so the click
settles once its minimum and quiet windows pass, and a bUnit fact holds what the click recorded. A submit still
owes the canvas its result and fails after five seconds without one. A play function that rejects marks the
driver `failed`, with the reason in `data-failure-reason`, instead of tearing down the canvas.
