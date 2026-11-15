# Zaytouna Login: growing tree and confirmed-success olive rain

Scope: Login, its dedicated _AuthLayout, _LoginOliveTree partial, auth-login.css
and auth-login.js. Home is untouched. The shared layout loads a small one-use
celebration handler after confirmed sign-in without changing its underlying UI. Uses the
approved palette/fonts. The story panel has no photograph: the tree stands alone on deep olive. No dependencies or schema
changes. Fonts use Google Fonts with system fallbacks.

## Interaction

The olive tree is drawn by auth-login.js from a fixed seed, so it is the same
tree on every visit, and it lives through seven stages: seed, sprout, trunk,
limbs, leaves, blossom, fruit. The stage follows how far the sign-in has come,
read only from field lengths and validity: focusing the form cracks the seed,
the email grows the trunk and limbs, the password opens the leaves, and a
complete form brings the blossom. Clearing a field lets the tree recede. The
camera starts close on the seed and pulls back as the crown fills the frame,
the wood thickens with age, and each twig sways on its own period. No input content is stored by the animation. Mobile
has the same tree, simplified, in the logo footprint. Pointer parallax affects
artwork only; the form never animates.

While the server decides, sap climbs the trunk and the first fruit sets. A
refused sign-in makes the crown shiver and leaves the tree where it was. With
motion paused or reduced the tree still shows the current stage, without
movement.

On genuine successful authentication the tree finishes growing, the olives
ripen to lime and a few fall, and then the workspace opens: about a second for
a typed sign-in, at most 2.6 seconds from a bare seed (a biometric sign-in).
Navigation is in a finally block and the harvest always resolves, so the
decoration cannot block access; paused and reduced-motion users navigate
immediately. A one-use
sessionStorage timestamp lets the destination page show 22 small olives (12 on
mobile) while the workspace is already usable. The decoration does not intercept
input, freezes nothing and removes itself after 2.2 seconds. The flag expires
after 10 seconds and is consumed once, so reloads do not replay it. It contains
no account data and grants no access. Failed/pending attempts never queue it.
Paused and reduced-motion users do not receive the decoration.

## 3D tree

On desktop, where WebGL2 is available, the story panel renders the tree as a lit
3D mesh (wwwroot/js/auth-tree3d.js, no libraries): a fluted trunk, limbs, boughs
and twigs as tapered tubes, about 1,700 leaves in opposite pairs, blossom and
fruit. Every vertex stores when in the tree's life it appears, so the same growth
progress drives it in the vertex shader. The camera pulls back from the seed and
drifts slowly around the tree; the pointer nudges the view. Without WebGL2 the
drawn SVG tree is used instead, and the mobile header always uses it. With motion
paused or reduced the 3D tree renders a still frame of the current stage.

## Passkey prompt

The automatic passkey suggestion in the email box is switched off
(AUTO_PASSKEY_SUGGESTION in wwwroot/biometric/biometric.js) because on Windows it
could open the system "Sign in with your passkey" window unasked. A passkey
prompt appears only after pressing the fingerprint button, and a running
fingerprint or face attempt can be left with "Skip and use my password".

## Authentication integration

The password form progressively enhances the same antiforgery-protected POST.
X-Login-Animation: 1 asks Auth/Login for a JSON result. All existing account,
supplier approval, VPN/network, password, lockout and role-routing checks run
unchanged. Failures return the existing validation messages as JSON; normal form
POSTs without that header retain their original HTML/redirect behaviour.

The client also accepts an existing same-origin HTML redirect instead of showing
a false error when a server uses the original response contract. Unresponsive
requests abort after 20 seconds and release the submit button with a clear error.
Successful navigation runs in a finally block so decoration cannot block access.

The biometric script optionally calls window.stmsLoginSuccess after its existing
server result confirms sign-in. This hook exists only on the dedicated Login;
other biometric pages keep their redirect fallback. Physical fingerprint/camera
success still requires a real device and was not exercised in browser automation.

## Fixed form

The password form fits a 100dvh workspace. Biometric details and camera controls
use an independent native dialog. Long content scrolls only within that dialog.
Escape/backdrop/close restore focus; closing stops active camera tracks. Short
screens use compact spacing. Below 320px viewport height only, page scrolling is
an accessibility fallback to avoid clipping essential fields.

## Verification

- Build: zero errors/warnings. JavaScript syntax checks passed.
- check.js: 45 browser checks, including all eight roles, invalid credentials,
  return URL, five widths, error layout and stylesheet isolation.
- motion.js: 54 browser checks, including ten viewport sizes, fixed form,
  auxiliary dialogs, motion preferences and reduced motion.
- tree-success.js: 39 browser checks, including character-by-character growth,
  actual successful authentication, withholding rain until server response,
  invalid/network failures, mature fruit, mobile tree, reduced motion and a
  JavaScript-disabled form POST.
- gap.js: 9 checks cover immediate navigation, usable workspace, legacy HTML
  redirects, pending requests, timeout feedback and submit recovery. Local observed
  delay from successful response to navigation request: 6ms, not a performance SLA.
- Total: 147 browser checks against isolated WorkspaceSearch fixture data.

The later Home/session policy is documented in home-session.md. Home appearance
is retained; entering it now ends the session through the protected logout POST.

Run the fixture app on localhost:5197 and use playwright-cli run-code with files
under tests/Login.BrowserChecks. Keep the same localhost/HTTPS host used to
register real passkeys. Rebuild/restart the local app to load the changes.
Work remains local until explicitly pushed.
