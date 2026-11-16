async page => {
    const base = 'http://localhost:5197';
    const checks = [];
    const check = (ok,name) => { if (!ok) throw new Error(name); checks.push(name); };
    await page.context().clearCookies();
    await page.goto(base + '/Auth/Login');
    await page.evaluate(() => { localStorage.removeItem('zt.login.motion'); sessionStorage.clear(); });
    await page.reload();
    await page.emulateMedia({reducedMotion:'no-preference'});
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Preview-Only!4729');
    let confirmedAt, navigationAt;
    const onResponse = response => {
        if (response.url().endsWith('/Auth/Login') && response.request().method() === 'POST' && response.ok()) confirmedAt = Date.now();
    };
    const onRequest = request => {
        if (request.isNavigationRequest() && request.url() === base + '/SourcingOfficer') navigationAt = Date.now();
    };
    page.on('response',onResponse); page.on('request',onRequest);
    await page.getByRole('button',{name:'Sign in',exact:true}).click();
    await page.waitForURL(base + '/SourcingOfficer');
    page.off('response',onResponse); page.off('request',onRequest);
    // The success film plays on Login after the server confirms, then the workspace opens clean.
    // 3.5 s is the hard ceiling in auth-login.js; allow a little for the navigation itself.
    check(confirmedAt && navigationAt && navigationAt >= confirmedAt && navigationAt - confirmedAt < 4200, 'Workspace opens within the bounded success transition');
    check(await page.locator('.zt-oil-success, .zt-signin-olive, .zt-signin-rain').count() === 0, 'Workspace opens without any leftover celebration');
    check(!await page.locator('body').evaluate(el => el.inert), 'Workspace is usable on arrival');
    await page.getByRole('button', { name: /^Log\s*out$/i }).click();
    await page.waitForURL(base + '/');
    await page.goto(base + '/Auth/Login');
    // Exercise a server returning its original HTML redirect, with genuine auth.
    await page.route('**/Auth/Login',route => {
        const headers = {...route.request().headers()};
        delete headers['x-login-animation'];
        return route.continue({headers});
    });
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Preview-Only!4729');
    await page.getByRole('button',{name:'Sign in',exact:true}).click();
    await page.waitForURL(base + '/SourcingOfficer');
    check(true, 'Original HTML redirect does not leave user stuck on Login');
    await page.unroute('**/Auth/Login');
    await page.getByRole('button', { name: /^Log\s*out$/i }).click();
    await page.waitForURL(base + '/');
    await page.goto(base + '/Auth/Login');
    // A request that never resolves must release the form at its deadline.
    await page.clock.install();
    await page.evaluate(() => {
        const original = window.fetch;
        window.fetch = (url,options) => options?.headers?.['X-Login-Animation'] ? new Promise((resolve,reject) => {
            options.signal.addEventListener('abort',() => reject(new DOMException('Timeout','AbortError')));
        }) : original(url,options);
    });
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Preview-Only!4729');
    await page.getByRole('button',{name:'Sign in',exact:true}).click();
    check(await page.locator('.auth-submit').isDisabled(), 'Pending request is protected from duplicate submit');
    await page.clock.fastForward(21000);
    await page.locator('.validation-summary-errors').waitFor();
    check(await page.locator('.auth-validation').textContent().then(t => t.includes('took too long')), 'Unresponsive request shows a clear timeout');
    check(await page.getByRole('button',{name:'Sign in',exact:true}).isEnabled(), 'Timeout restores submit button');
    check(await page.locator('.auth-form').getAttribute('aria-busy') === null, 'Timeout clears busy state');
    check(!await page.locator('.auth-shell').evaluate(el => el.inert), 'Timeout never freezes the form');
    return {checks:checks.length,navigationDelayMs:navigationAt-confirmedAt,passed:checks};
}
