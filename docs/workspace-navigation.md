# Dashboard entry (backend only)

Password and biometric sign-in use Services/WorkspaceNavigation.cs to choose the
existing dashboard for the authenticated role. /Dashboard is an authorised
redirect endpoint, with no new view. Signed-in users visiting Auth/Login return
to their dashboard. Local work links are preserved; external/root/Home return
URLs fall back to the dashboard. Existing role authorisation is unchanged.

All visual/frontend work was cancelled and restored to the Git HEAD views and
styles. The original separate biometric login view and original navigation are
restored. The Dairy public site, enquiry endpoints, related services, middleware
and assets have been removed. No database schema or business workflow was changed.

The confirmed Zaytouna brand reference remains documentation only.
Run tests/WorkspaceNavigation.Checks for the role/return-URL policy checks.
