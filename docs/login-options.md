# Login biometric actions

The Login page is the single place to choose fingerprint or face recognition.
Fingerprint starts the existing system/device flow directly. Only a failure opens
fingerprint help, with retry and password fallback; face selection is absent there.
Face recognition validates the existing Login email field first, then opens its
camera surface without repeating the fingerprint option or another email field.
The existing hidden faceEmail handoff preserves the biometric module contract.

Closing the modal cancels an active attempt through the existing skip control and
stops camera tracks. Suppression prevents a late cancellation message from reopening
it. Reduced motion, normal password submission and safe role redirects are retained.
No biometric server endpoint or verification rule was changed.

17 browser assertions passed on the isolated localhost:5197 fixture. Tests cover
no modal during a pending fingerprint request, method-specific help after a
simulated service failure, missing-email focus, face setup failure, mobile fit,
cancellation without reopening, and genuine password authentication/role redirect.
Hardware verification was not exercised by these tests.
