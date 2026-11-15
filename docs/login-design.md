# Current direction: one 3D render, no tree (supersedes everything below)

The growing olive tree has been removed at the owner's request: the real-time
WebGL tree (auth-tree3d.js), its SVG fallback and mobile mini tree, the growth
controller, the bark and soil textures, and their styles and checks are gone.
Login no longer has a canvas and no part of the scene reacts to typing, so the
form's content, including password length, cannot show in the artwork.

The scene panel (left, 56:44) is filled edge to edge by a single pre-rendered 3D
still: the olive close-up, `wwwroot/login/cinema/olive-still-v2.webp`, 960 x 640,
the first frame of the success film. It is a plain `<img>` in `.auth-render`,
behind the words, warmed toward the ivory palette with a CSS filter and faded
into the panel below so the copy stays on a clean ground. It is hidden from
assistive technology, takes no pointer events, and shows without JavaScript.

The small line above the heading names the journey: "It starts with one olive."

Only after the server confirms authentication, the three-second film
(`olive-to-oil-v2.mp4`: olive close-up, a cut to a metal press lip, an oil drop
forming and falling) plays inside that same frame, so the picture comes alive
behind the words and the Pause control stays visible. On the cut to the press
the line turns to "And ends as oil." Then the workspace opens with no leftover
effect. Paused or reduced motion skips the film. A missing film, rejected
autoplay or slow load cannot block navigation: 3.3 s media guard, 3.5 s overall
guard. On mobile the panel is hidden, the Zaytouna emblem is back in the header,
and the film uses a viewport-fitting overlay. See
[rendering and asset notes](login-cinema.md).

Unchanged: password and biometric sign-in, antiforgery, validation, return URLs,
role redirects, the "Skip and use my password" control, and the switched-off
automatic passkey prompt.

Verification (15 November 2026, published build against the isolated synthetic
fixture, real sign-ins): `render.js` 37, `motion.js` 54, `check.js` 45, `gap.js` 9,
145 checks in total, all passing. Covered: still render, no success imagery
before the server answers, film in the still's frame, fixed form, clean
workspace, wrong credentials, network failure, missing film, rejected autoplay,
manual pause, reduced motion, mobile, and sign-in without JavaScript.

The removed tree work is preserved in the Git ref
`refs/backup/login-tree-20261115` and in commits 78f9bce and 42eb233.

---

Everything below is history. It describes the tree and earlier success
animations that no longer exist.

# Current direction: rendered macro film (supersedes the studies below)

The existing seedling/tree grows as the user fills the fixed Login form. Only after
the server confirms authentication, a silent three-second product film shows an
olive close-up, then cuts to oil forming and falling from a metal press lip.
The dashboard opens afterward without any effect. There is no basket, olive rain,
or SVG olive-to-drop morph in this success sequence.

The film is a locally authored Blender Cycles 3D render, not camera footage or a
fluid simulation. See [rendering and asset notes](login-cinema.md). Desktop places
the film in the artwork panel; mobile uses a viewport-fitting overlay. Neither
moves the form. Reduced motion and manual Pause skip the film. Playback failure
also permits navigation, with a bounded wait as a final safeguard.

Current verification: 43 assertions passed in the isolated Login growth and
confirmed-success browser checks. Additional actual sign-ins passed in portrait,
landscape, missing-video and autoplay-blocked cases. The original 3D tree also
initializes with no WebGL error.

---

The following entries are historical design studies. The current direction above
supersedes their conflicting success-animation descriptions.

# Current direction: from olive tree to golden oil

The plant still grows while the form is filled. After confirmed authentication, one olive on the tree gleams and becomes a small golden oil drop on Login, then the role dashboard opens. No full-screen olive rain or basket is used. This supersedes earlier celebration studies below.

# Current direction: no basket

The basket idea has been cancelled. Success shows only varied falling olives on Login, followed by a clean dashboard. Basket/pile code, styles and image preloading are removed. Earlier basket notes below are historical.

# Zaytouna Login: growing tree and confirmed-success olive rain

Current basket: a transparent photorealistic product-style image, preloaded locally
and composited as back/front layers so olives land behind its rim. The SVG basket
below describes the earlier version and is now an image-load fallback only.


Current success effect: varied olive drops converge on a wicker basket at the
bottom of Login. Every olive remains as part of the collected pile until navigation.
The dashboard stays clean; no cross-page celebration flag is used.


Current opening: opaque brown soil, fine fissures on typing, then a two-leaf
seedling. Underground roots remain hidden throughout. The earlier exposed-root
studies below are historical and are superseded by this opening.


Current behavior: confirmed-success olive rain is **Login-only**. It plays after
server authentication, then navigation opens a clean dashboard. There is no
cross-page celebration flag. This supersedes the earlier destination-page notes.


Scope: Login, its dedicated _AuthLayout, _LoginOliveTree partial, auth-login.css
and auth-login.js. Home is untouched. The shared layout loads a small one-use
celebration handler after confirmed sign-in without changing its underlying UI. Uses the
approved palette/fonts. The story panel has no photograph: the tree grows in an open, sunlit field within the approved ivory/olive palette. No dependencies or schema
changes. Fonts use Google Fonts with system fallbacks.

## Interaction

The olive tree is drawn by auth-login.js from a fixed seed, so it is the same
tree on every visit, and it lives through seven stages: roots, sprout, trunk,
limbs, leaves, blossom, fruit. The stage follows how far the sign-in has come,
read only from field lengths and validity: the page opens on roots spreading under the soil, focusing the form brings the
shoot above ground,
the email grows the trunk and limbs, any password opens the leaves and blossom in one
fixed step (its length is never shown), and a complete form sets green olives
on the twigs. Clearing a field lets the tree recede. The
camera starts close on the roots and pulls back as the crown fills the frame,
the wood thickens with age, and each twig sways on its own period. No input content is stored by the animation. Mobile
has the same tree, simplified, in the logo footprint. Pointer parallax affects
artwork only; the form never animates.

While the server decides, sap climbs the trunk. The green olives are the last
stage of growth; decorative shedding starts at maturity; only the full-screen rain means confirmed sign-in. A refused sign-in makes the tree draw back a
little and return, with no shake; the message carries the reason. With motion
paused or reduced the tree still shows the current stage, without movement.

On genuine successful authentication the tree finishes growing, the olives
ripen and a few fall, and then the workspace opens, in under a second (1.3
seconds is the hard ceiling). Navigation is in a finally block and the harvest
always resolves, so the decoration cannot block access; paused and
reduced-motion users navigate immediately. A one-use
sessionStorage timestamp lets the destination page show 22 small olives (12 on
mobile) while the workspace is already usable. The decoration does not intercept
input, freezes nothing and removes itself after 4.2 seconds. The flag expires
after 10 seconds and is consumed once, so reloads do not replay it. It contains
no account data and grants no access. Failed/pending attempts never queue it.
Paused and reduced-motion users do not receive the decoration.

## Layout and studio

Adapted from the Orange Auth Studio V3 kit, keeping the Zaytouna palette, type
and olive tree: the scene sits left and the form right at 56:44 (the form is
still first in the document), and the scene is an open field with photographed soil, distance haze, scattered limestone and cast tree shadows. The foreground fades into ivory below the tree to keep the copy readable. The camera is
directed rather than a free orbit: it opens level with the soil, on the roots, then rises and
swings to three-quarters as the tree rises. Not taken from the kit: the orange
palette, the SIM, lens and fibre objects, and the "coming soon" fingerprint,
because fingerprint sign-in already works here.

## 3D tree

On desktop, where WebGL2 is available, the story panel renders the tree as a lit
3D mesh (wwwroot/js/auth-tree3d.js, no libraries): a fluted, short trunk, a low spreading crown, multiple generations of curved shoots, paired silver-green leaves, blossom and fruit. Every vertex stores when in the tree's life it appears, so the same growth
progress drives it in the vertex shader. The terrain extends beyond the camera, with no circular rim or container. It is translucent only in the early roots stage, then becomes solid as the shoot emerges. The camera pulls back from the roots and
drifts slowly around the tree; the pointer nudges the view. Without WebGL2 the
drawn SVG tree is used instead, and the mobile header always uses it. With motion
paused or reduced the 3D tree renders a still frame of the current stage.

### Materials and shadows

Bark and soil are photographed materials from Poly Haven (polyhaven.com), licence
CC0, 1k JPEG, stored in wwwroot/login/tree (about 4 MB, loaded after the page):

- bark-color.jpg, bark-normal.jpg: "Bark Brown 02"
- soil-color.jpg, soil-normal.jpg: "Brown Mud Dry"

The normal maps give the bark and soil relief under the light. The tree casts
real shadows on itself and on the soil through a 2048 px depth map rendered
from the sun each frame. Leaves have a waxy sheen and let light through.
Until the images arrive, or if they fail, the drawn bark and soil are used.

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

## Open-field refinement (15 November 2026)

The old hemisphere of soil and the separate floating floor-shadow pass have been
removed. A gently uneven height field extends into atmospheric haze; its surface
uses the existing CC0 soil color/normal photographs, with irregular vegetation
patches and small limestone fragments. The crown is wider and lower, with smaller
leaves on additional lateral shoots, greyer bark, and more deeply fluted wood.
Roots split into fine branchlets rather than seven thick spokes. Ground becomes
opaque gradually, burying the roots as the tree matures. No generated bitmap,
external model dependency, authentication or Home change is part of this refinement.
This remains a procedural WebGL model, not a photograph or a scanned tree.

A lost WebGL context now reveals the existing SVG fallback without reloading the
page or erasing either form field. The pause control freezes the actual 3D frame;
reduced motion and the lightweight mobile fallback remain in place.

Validation: 54 existing responsive/motion assertions across ten viewport sizes,
plus 17 field checks (growth, password-length privacy, fixed form, WebGL rendering,
actual pause, reduced motion, mobile and context-loss fallback) passed against the
local running app. No credentials were submitted during these visual checks.
The motion check now waits for eased growth before asserting its progress value.
Password/biometric success handling is unchanged; it was not re-exercised in this
visual-only pass. Desktop and mobile screenshots were inspected.

## Sharpness and material pass (15 November 2026)

Desktop foliage now renders at 1.75x CSS resolution on ordinary 1x displays and
up to 2x on high-DPI displays. A four-million-pixel supersampling budget limits
extra GPU work on large monitors; it never deliberately renders below native CSS
resolution (apart from the hardware maximum renderbuffer dimension). Texture
anisotropy is up to 16x where supported. The same local photographs are used;
there are no new network dependencies or generated image assets.

The trunk has more radial geometry and a recessed, twisting groove. Leaf blades
have smooth cupped geometry instead of a sharp centre fold; fruit has a smoother
silhouette. Lighting now decodes surface sRGB colors into linear light and uses a
filmic highlight roll-off before display encoding. Silver undersides, vein detail,
softer sampled cast shadows and restrained fruit/leaf highlights reduce the hard,
plastic appearance. This is still the procedural model, not a scan of a real tree.

Validation: the 54 responsive/motion checks and 19 field checks passed, including
supersampling, large-display pixel budget, pause, context loss and unchanged form
geometry. A separate DPR 2 preview rendered 1610 x 1800 pixels for the 805 x 900 CSS
panel without WebGL errors. The local 1440 x 900 preview observed about 144 animation
frames/second (95th-percentile frame interval 7 ms); this is evidence for this
machine only, not a device-independent performance guarantee. No credentials were
submitted. Authentication, shared layouts, Home and other cycles were not changed.

## Soil, olive foliage and root detail (15 November 2026)

The field now uses bare brown mineral soil throughout: vegetation patches were
removed, the photograph is earth-toned, surface normal relief strengthened, and
soil clods/grit made smaller and kept clear of the root study. Olive leaf blades
are more elliptical, with varied outward/hanging orientations, darker olive-green
upper surfaces and muted silver undersides. Fruit remains attached to the shoots.

Root detail was rebuilt as nine asymmetrical structural roots, lateral branches,
fine roots and feeder roots. Cubic curves replace straight spokes, with smooth
taper, finer radial geometry, a connected buried root collar and dedicated woody
root coloring. Root geometry has its own deterministic seed and no longer receives
the crown widening transform. The opening camera is closer and looks farther down
so the branches can be inspected; the earth becomes opaque during growth, leaving
the tree rooted in the field. The growth controller and authentication are unchanged.

Validation: 54 responsive/motion assertions and 19 field assertions passed. The
field check waits for the asynchronous reduced-motion media event before asserting
its effect. Actual root and mature-tree screenshots were reviewed. There were no
WebGL errors, submitted credentials, backend changes or changes to Home.

## Slower growth and two distinct olive falls (15 November 2026)

Growth retargets the current visible value, with a 900 ms easing constant and a
maximum upward rate of .12 progress units/second. It does not queue one animation
per keystroke. Email growth now uses a bounded curve with further increments beyond
fourteen characters; password presence still supplies a single fixed target,
independent of its length. A normal typing sequence now lets the tree develop
through the form instead of jumping to a nearly complete tree on the first
password character. CSS progress is recorded to three decimal places for inspection.

Green olives emerge at staggered positions/times on shoots, then a scattered subset
falls once after maturity. This is decorative and does NOT mean credentials are
correct. It is implemented both in WebGL and SVG, including miniature mobile fruit.
The SVG fallback remains intact if WebGL loses its context. Clearing inputs resets
the maturity cycle; quiet/reduced-motion mode skips the decorative falls, and pausing
freezes the natural-fall clock. Most olives stay on the canopy.

After actual server-confirmed authentication, the destination page receives a
single rain of 22 olives on desktop / 12 on mobile. Olives are now 23–29 CSS pixels
on desktop (17–23 mobile), fall over 3.1 seconds with staggered delays, have stronger
opacity, never intercept input, and remove themselves at 4.2 seconds. Reload does
not replay the effect. The existing sign-in harvest remains bounded at 1.3 seconds;
quiet/reduced-motion users proceed directly. No controller, role or authentication
policy changed.

Validation: 54 responsive/motion checks, 19 field checks, 22 real-authentication /
failure / reduced-motion / no-JavaScript checks, and 14 slower-growth/shedding/rain
checks passed. Authentication runs used only the isolated synthetic SQL fixture.
Build passed with one pre-existing duplicate-using warning in BiometricController.
The tests use the current `Log out` label and distinguish decorative tree shedding
from the authenticated full-screen effect. An actual browser video records typing,
the mature tree and successful sign-in rain, using a synthetic supplier account.

## Correction: success rain belongs on Login (15 November 2026)

The celebration helper is now an explicit `stmsPlayLoginCelebration()` promise,
called only by the genuine-success handler on the auth page. The existing tree
harvest and the rain run together. Rain finishes at 3.7 seconds, with an independent
3.9-second navigation deadline in `finally`; reduced-motion and paused users
navigate immediately. A successful biometric flow closes its options dialog first,
using the existing close handler to release camera tracks and expose the page.

The helper refuses non-Login pages, has no automatic playback and discards the
legacy sessionStorage flag. Both celebration stylesheet and script were removed
from the signed-in shared layout. No page in the workspace starts or continues
this animation. Motion preference changes remove the overlay and resolve its wait.
No authentication or authorization policy changed.

The real-login tests now assert that the rain is visibly on `/Auth/Login` after
server confirmation and absent from the destination/reload. Invalid credentials,
network failure, reduced motion and the original JavaScript-free POST are checked
against the synthetic fixture, not a user's database.

Validation of the correction: 23 real-authentication checks and 16 growth/rain checks passed; the build passed with the same pre-existing duplicate-using warning. Actual Login rain and a clean dashboard were verified in the browser.

## Soil cracks and two-leaf seedling opening (15 November 2026)

The user chose a seedling emerging from lightly splitting soil. The opening is now
opaque mineral soil, not an x-ray of exposed roots. Underground wood is clipped
from both visible and shadow passes; SVG roots are hidden too. Local antialiased
fissures and a small height-field heave develop with the first typed characters,
then settle under the growing trunk. Terrain vertices are concentrated near the
shoot to resolve the displacement without dense geometry across the whole field.

A slender stem emerges with two cotyledons at its tip. The early trunk has a short
hold in its growth schedule, so it does not shoot above the seed leaves before
becoming a young tree. The camera starts closer and pulls back after emergence;
young wood thickens progressively. The two seed leaves fade only as the tree gains
branches. Body stages begin `soil`, then `sprout`, `trunk`, `limbs`, etc. Focus alone
leaves the soil intact; typing initiates emergence. The existing final olive stages,
Login-only success celebration and authentication are unchanged.

Validation: 20 field/seedling assertions and 54 responsive/motion assertions passed,
with no WebGL or JavaScript errors. Soil-only and two-leaf frames were visually
reviewed, and a browser recording shows the emergence while typing. No credentials
were submitted and no backend, Home or shared-layout changes were made in this pass.

## Staggered harvest into a basket (15 November 2026)

The Login-only success effect now gives each olive a deterministic, distinct delay
(0–850 ms), duration (1.45–2.2 s), rotation and curved route from the top of the page
to the basket. The endpoint is measured from the actual responsive basket, rather
than sending fruit below the viewport. Arrival order determines the pile's layers.
Each falling olive is replaced at exactly its endpoint by a settled olive, with a
small landing motion; the front wicker panel masks the lower part of the pile.

The basket is native SVG with a handle, mouth, woven body and ground shadow. It sits
under the artwork on desktop and at bottom centre on mobile, with smaller sizing
for short windows. All 22 desktop / 12 mobile olives collect before the 3.7-second
cleanup, leaving a visible settled basket before the existing 3.9-second redirect
deadline. Resize or a quiet/reduced-motion change cancels the effect cleanly, cancels
pending Web Animations and resolves the wait. No input interception, layout changes,
backend modifications or new dependencies were introduced.

Validation: the 19 growth/real-login assertions passed, including varied delays and
speeds, all 22 arrivals, pile bounds and a clean destination. Actual supplier sign-in
also passed at 390x844 and 844x390, with all olives and the basket inside the viewport.
Reduced-motion supplier sign-in skipped the effect with no JavaScript errors. Tests
used only the isolated synthetic SQL fixture. A browser recording captures actual
server-confirmed sign-in, collection and the clean dashboard.

## Photorealistic wicker basket replacement (15 November 2026)

Generated a new transparent, detailed willow-basket image with the built-in imagegen
tool. The source is a generated product-style image, not a photographed physical
prop or 3D scan. The web asset is a 768x512 alpha WebP (133,666 bytes), with provenance
and checksum in `docs/assets/harvest-basket-v2.md`. It is stored inside the project's
wwwroot; the original generated PNG is preserved locally.

The basket's back and clipped front share the same image and framing, preserving
realistic weave, handle, interior occlusion and contact shadow. A nine-column pile
is fitted inside the new opening; the front rim masks the lower fruit. Desktop
width is 236 px, portrait mobile 196 px, and short windows 150 px. The image preloads
on Login, and its availability never adds a wait to authentication. A missing image
uses the SVG fallback with its original seven-column landing geometry.

Validation: all 22 growth/authentication assertions passed, including image readiness,
source resolution, front occlusion, varied falls, complete collection and clean
navigation. A separate 390x844 supplier sign-in collected all 12 olives within the
basket and viewport. An intentionally blocked image request used the fallback and
still signed in successfully. Browser screenshots and a real-authentication video
were reviewed. Only the isolated synthetic SQL fixture was used. No backend, Home,
role or authentication-policy changes were made.

## One olive becomes golden oil (15 November 2026)

Success now finishes tree growth, stops decorative shedding and selects a single
olive from the actual tree. The WebGL renderer projects a real olive's instance
into CSS coordinates and hides that instance during its transfer to the overlay.
SVG/mobile fallback uses the bounds of an existing fruit and hides it with a class.
The olive enlarges slightly, catches one glint and cross-morphs into a small golden
oil droplet using native SVG/CSS. A restrained warm highlight accompanies the change.
Desktop keeps the moment beside the canopy; mobile moves it to the quiet space
under the brand. The password form never moves.

Only the server-confirmed success callback starts the story. It lasts 1.9 seconds
after bounded tree completion, with an independent 3.5-second overall navigation
deadline. Finally always navigates; quiet/reduced-motion users skip the effect.
Resize or motion preference changes remove the overlay and resolve its wait.
There are no new assets, external services, packages, or authentication-policy changes.

Validation: 19 growth/success checks, 20 field/WebGL checks and 23 real-authentication,
failure, reduced-motion and JavaScript-free checks passed. Portrait (390x844) and
landscape (844x390) supplier sign-ins showed a visible oil drop from the tree, within
the viewport, and a clean destination. The tests used only the synthetic SQL fixture.
A browser video records actual typing, plant growth, confirmed sign-in and the
olive-to-oil transformation. The golden phase was visually inspected on desktop
and mobile. Normal browser animation timing may vary with device performance.
