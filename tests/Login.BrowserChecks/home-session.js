// Uses synthetic fixture accounts. Home is intentionally an exit from the workspace.
async page => {
    const base = 'http://localhost:5197';
    const results = [];
    const check = (ok,label) => { if (!ok) throw new Error(label); results.push(label); };
    await page.context().clearCookies();
    const login = async (user,destination) => {
        await page.goto(base + '/Auth/Login');
        await page.locator('#Email').fill(`${user}@example.test`);
        await page.locator('#Password').fill('Preview-Only!4729');
        await page.locator('#RememberMe').check();
        await page.getByRole('button',{name:'Sign in',exact:true}).click();
        await page.waitForURL(base + destination);
    };
    const loginVisibleFromHome = async () => {
        await page.locator('.zt-nav').waitFor();
        await page.locator('a[href="/Auth/Login"]:visible').first().click();
        await page.locator('#Email').waitFor();
        check(new URL(page.url()).pathname === '/Auth/Login', 'Home Login opens a fresh sign-in form');
    };
    const roles = [['officer','/SourcingOfficer'],['admin','/Admin'],['requester','/Department/Dashboard'],
        ['supplier','/Suppliers'],['technical','/EvaluationCommittee'],['financial','/EvaluationCommittee'],
        ['manager','/SourcingManager'],['top','/TopCommittee']];
    for (const [user,destination] of roles) {
        await login(user,destination);
        const internal = await page.request.get(base + destination);
        check(internal.ok() && !internal.url().includes('/Auth/Login'), `${user}: internal navigation preserves session`);
        const home = page.getByRole('button',{name:'Home',exact:true});
        check(await home.evaluate(el => el.form.method === 'post' && !!el.form.querySelector('input[name=__RequestVerificationToken]')), `${user}: Home uses protected logout POST`);
        await home.click();
        await page.waitForURL(base + '/');
        await page.locator('.zt-nav').waitFor();
        check(!(await page.context().cookies()).some(c => c.name.startsWith('.AspNetCore.Identity.Application')), `${user}: Home clears persistent authentication cookie`);
        const protectedPage = await page.request.get(base + destination);
        check(protectedPage.url().includes('/Auth/Login'), `${user}: protected page now requires authentication`);
        await loginVisibleFromHome();
    }
    await login('officer','/SourcingOfficer');
    const initial = await page.request.get(base + '/');
    check((await initial.text()).includes('data-home-exit'), 'Direct Home GET presents a logout form');
    const stillSignedIn = await page.request.get(base + '/SourcingOfficer');
    check(!stillSignedIn.url().includes('/Auth/Login'), 'Passive GET does not mutate authentication');
    const noToken = await page.request.post(base + '/Auth/Logout',{form:{}});
    check(noToken.status() === 400, 'Exit remains protected against missing CSRF token');
    await page.goto(base + '/Home/Index');
    await page.locator('.zt-nav').waitFor();
    check((await page.request.get(base + '/SourcingOfficer')).url().includes('/Auth/Login'), 'Typing the Home URL also ends session through POST');
    await loginVisibleFromHome();
    // Reproduce Home -> Login -> Workspace -> browser Back to Home.
    await page.goto(base + '/');
    await page.locator('a[href="/Auth/Login"]:visible').first().click();
    await page.locator('#Email').fill('officer@example.test');
    await page.locator('#Password').fill('Preview-Only!4729');
    await page.getByRole('button',{name:'Sign in',exact:true}).click();
    await page.waitForURL(base + '/SourcingOfficer');
    const exitResponse = page.waitForResponse(r => r.url() === base + '/Auth/Logout' && r.request().method() === 'POST');
    await page.goBack();
    await exitResponse;
    await page.waitForURL(base + '/');
    await page.locator('.zt-nav').waitFor();
    check((await page.request.get(base + '/SourcingOfficer')).url().includes('/Auth/Login'), 'Back to Home ends the session too');
    await loginVisibleFromHome();
    // Back must not revive an old authenticated workspace after explicit Home exit.
    await login('officer','/SourcingOfficer');
    await page.getByRole('button',{name:'Home',exact:true}).click();
    await page.waitForURL(base + '/');
    await page.goBack();
    await page.waitForURL(/\/Auth\/Login/);
    check(await page.locator('#Email').isVisible(), 'Back cannot reopen a logged-out workspace');
    return {checks:results.length,passed:results};
}
