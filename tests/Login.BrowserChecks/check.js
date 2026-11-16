// Run against the synthetic WorkspaceSearch fixture, never a production database.
async page => {
    const base = 'http://localhost:5197';
    const passed = [];
    const check = (condition, label) => { if (!condition) throw new Error(label); passed.push(label); };
    await page.context().clearCookies();
    await page.goto(base + '/Auth/Login?returnUrl=%2FSourcingOfficer%2FRequests');
    check(await page.locator('#ReturnUrl').inputValue() === '/SourcingOfficer/Requests', 'Return URL preserved');
    check(await page.locator('form[method=post] input[name=__RequestVerificationToken]').count() === 1, 'Antiforgery token preserved');
    check(await page.locator('#btnPasskeyLogin').count() === 1 && await page.locator('#btnFaceLogin').count() === 1, 'Existing biometric controls preserved');
    check(await page.locator('#bio-root').getAttribute('data-return-url') === '/SourcingOfficer/Requests', 'Biometric return URL preserved');
    check(await page.locator('#bioDiagCopy').count() === 1, 'Existing device diagnostic control preserved');
    await page.locator('#Password').fill('Example-Test!');
    await page.getByRole('button', { name: 'Show password', exact: true }).click();
    check(await page.locator('#Password').getAttribute('type') === 'text', 'Password reveal works');
    check(await page.locator('#Password').inputValue() === 'Example-Test!', 'Reveal preserves password');
    await page.getByRole('button', { name: 'Hide password', exact: true }).click();
    check(await page.locator('#Password').getAttribute('type') === 'password', 'Password hides again');
    for (const width of [1440, 1024, 768, 390, 320]) {
        await page.setViewportSize({ width, height: 900 });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px: no horizontal overflow`);
        check(await page.locator('#Email').isVisible() && await page.locator('.auth-submit').isVisible(), `${width}px: form remains visible`);
    }
    await page.setViewportSize({ width: 390, height: 844 });
    check(await page.locator('.auth-story').isHidden(), 'Mobile prioritises form');
    await page.screenshot({ path: 'zaytouna-login-mobile-verified.png', fullPage: true });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.locator('#Password').clear();
    await page.screenshot({ path: 'zaytouna-login-desktop-verified.png' });
    await page.locator('#Email').focus();
    await page.waitForFunction(() => getComputedStyle(document.getElementById('Email')).borderColor === 'rgb(78, 107, 47)');
    check(true, 'Keyboard focus uses approved colour');
    await page.getByRole('button', { name: 'Face recognition', exact: true }).click();
    check(await page.locator('#bio-status').isVisible(), 'Existing biometric email validation still shows');
    await page.keyboard.press('Escape');
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Invalid-Password!2');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.locator('.validation-summary-errors').waitFor();
    check(await page.locator('.validation-summary-errors').isVisible(), 'Invalid credentials show server feedback');
    check(new URL(page.url()).pathname === '/Auth/Login', 'Invalid login remains on login page');
    for (const [width,height] of [[1366,768],[390,844],[375,667],[320,568],[390,480]]) {
        await page.setViewportSize({width,height});
        check(await page.evaluate(() => document.scrollingElement.scrollHeight === innerHeight), `${width}x${height}: validation does not cause page scroll`);
        check(await page.locator('.auth-form').evaluate(el => { const r=el.getBoundingClientRect(); return r.top>=0 && r.bottom<=innerHeight; }), `${width}x${height}: error and form fit together`);
    }
    await page.setViewportSize({width:1440,height:1000});

    const roles = [ ['officer','/SourcingOfficer'], ['admin','/Admin'], ['requester','/Department/Dashboard'],
        ['supplier','/Suppliers'], ['technical','/EvaluationCommittee'], ['financial','/EvaluationCommittee'],
        ['manager','/SourcingManager'], ['top','/TopCommittee'] ];
    for (const [user, destination] of roles) {
        await page.goto(base + '/Auth/Login');
        await page.locator('#Email').fill(`${user}@example.test`);
        await page.locator('#Password').fill('Preview-Only!4729');
        await page.locator('#RememberMe').check();
        await page.getByRole('button', { name: 'Sign in', exact: true }).click();
        await page.waitForURL(base + destination);
        check(new URL(page.url()).pathname === destination, `${user}: signs in to correct dashboard`);
        await page.getByRole('button', { name: /^Log\s*out$/i }).click();
        await page.waitForURL(base + '/');
    }
    await page.goto(base + '/Auth/Login?returnUrl=%2FSourcingOfficer%2FRequests');
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Preview-Only!4729');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.waitForURL(base + '/SourcingOfficer/Requests');
    check(true, 'Deep link resumes after sign-in');
    check(await page.locator('link[href*="auth-login"]').count() === 0, 'Login stylesheet does not affect authenticated pages');
    await page.getByRole('button', { name: /^Log\s*out$/i }).click();
    await page.waitForURL(base + '/');
    check(await page.locator('link[href*="auth-login"]').count() === 0, 'Home does not load Login styles');
    await page.goto(base + '/Auth/Login');
    check(await page.getByRole('link', { name: 'Register your company' }).getAttribute('href') === '/Suppliers/Register', 'Existing supplier registration route retained');
    return { checks: passed.length, passed };
}
