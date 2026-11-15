# Home ends the workspace session

User-requested policy: visiting the public Home after signing in ends the current
browser session. Home -> Login must show the sign-in form instead of taking the
person straight back to the workspace. Internal workspace navigation retains the
session. Keep me signed in does not override an explicit Home exit.

Authenticated Home and brand buttons in the shared navigation submit the existing
Auth/Logout POST with an antiforgery token. Anonymous Home links are unchanged.
For a direct Home URL, HomeController returns LeaveWorkspace: a small page that
automatically submits that same protected POST. Without JavaScript, its button
performs the action explicitly. A passive GET never changes authentication.

Home responses are no-store. home-session.js revalidates history snapshots on
pageshow so Back cannot revive an old workspace or bypass the Home exit policy.
Home's appearance is unchanged; its only view change is one script reference.
The authentication cookie is cleared using the existing SignInManager sign-out.
No users, passkeys, saved preferences, role permissions or database data are deleted.

Verification: home-session.js browser checks passed 48 assertions on isolated
fixture accounts. Covers eight roles, persistent cookies, internal navigation,
fresh Home -> Login, direct Home URL, browser Back, protected-route challenges,
and CSRF enforcement. The existing Login checks also cover all eight sign-in roles.

Rebuild/restart the running application to load the new controller and Razor code.
Work remains local unless explicitly pushed.
