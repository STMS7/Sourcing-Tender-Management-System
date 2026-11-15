// The Login scene is one pre-rendered 3D still that becomes the success film.
// Uses only the isolated synthetic fixture (see docs/workspace-voice-search.md).
async page => {
    const base = 'http://localhost:5197', passed = [];
    const check = (ok, label) => { if (!ok) throw new Error(label); passed.push(label); };
    const account = ['officer@example.test', 'Preview-Only!4729'];
    const signIn = page.getByRole('button', { name: 'Sign in', exact: true });
    const logout = async () => {
        await page.getByRole('button', { name: /^Log\s*out$/i }).click();
        await page.waitForURL(base + '/');
    };
    const fresh = async () => {
        await page.context().clearCookies();
        await page.goto(base + '/Auth/Login');
        await page.evaluate(() => { localStorage.removeItem('zt.login.motion'); sessionStorage.clear(); });
        await page.reload();
    };
    const errors = [];
    page.on('pageerror', error => errors.push(String(error)));
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await fresh();

    // The still render.
    const still = page.locator('.auth-render img');
    check(await still.evaluate(img => img.complete && img.naturalWidth === 960 && img.naturalHeight === 640), 'The still render loads at 960x640');
    check(await page.locator('.auth-render').getAttribute('aria-hidden') === 'true', 'The render is hidden from assistive technology');
    check(await page.locator('.auth-render').evaluate(el => getComputedStyle(el).pointerEvents === 'none'), 'The render never captures input');
    check(await page.locator('canvas, .auth-tree, .auth-tree3d').count() === 0, 'No tree and no real-time canvas on Login');
    check(await page.locator('.auth-story-top, .auth-story-caption, .auth-story-rule, .auth-footer a').count() === 0, 'The scene carries only the journey line and the heading');
    check(!await page.locator('.auth-device-help').isVisible(), 'The extra options link is not shown on a normal screen');
    const slot = await page.locator('.auth-render').boundingBox();
    const panel = await page.locator('.auth-story').boundingBox();
    check(Math.abs(slot.x - panel.x) < 1 && Math.abs(slot.width - panel.width) < 2 && Math.abs(slot.y - panel.y) < 1, 'The render fills the scene panel edge to edge from the top');
    check(await page.locator('.auth-render').evaluate(el => getComputedStyle(el).zIndex === '-1'), 'The render sits behind the words');
    const heading = await page.locator('.auth-story h2').boundingBox();
    check(heading.y > slot.y + slot.height * .58, 'The heading rests where the render has faded into ivory');
    const line = page.locator('.auth-story-label');
    check((await line.textContent()).trim() === 'It starts with one olive.', 'The journey line opens with the olive');
    const form = await page.locator('.auth-form').boundingBox();

    // Nothing reads as success before the server answers.
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    check(await page.locator('.zt-oil-success').count() === 0, 'A complete form shows no success imagery');
    let release;
    const gate = new Promise(resolve => release = resolve);
    await page.route('**/Auth/Login', async route => {
        if (route.request().method() === 'POST') await gate;
        await route.continue();
    });
    await signIn.click();
    await page.waitForTimeout(600);
    check(await page.locator('.zt-oil-success').count() === 0, 'No film while the server is still deciding');
    check(await page.locator('.auth-submit').isDisabled(), 'Pending sign-in blocks a second submit');
    release();

    // Confirmed success: the film plays where the still was, then a clean workspace.
    await page.locator('.zt-oil-success video').waitFor();
    const film = await page.locator('video.zt-oil-film').boundingBox();
    check(Math.abs(film.x - slot.x) < 1 && Math.abs(film.y - slot.y) < 1 && Math.abs(film.width - slot.width) < 1, 'The film plays exactly in the still render frame');
    check(new URL(page.url()).pathname === '/Auth/Login', 'The film is shown on Login');
    check(await page.locator('.auth-render .zt-oil-success.is-inline').count() === 1, 'The film lives inside the render frame, behind the words');
    check((await line.textContent()).trim() === 'It starts with one olive.', 'The journey line does not change before the cut');
    await page.waitForFunction(() => document.querySelector('.zt-oil-success')?.dataset.phase === 'oil');
    check(await page.locator('video.zt-oil-film').evaluate(v => v.videoWidth === 960 && v.duration === 3 && v.muted && !v.paused), 'The silent three-second film reaches the oil shot');
    check((await line.textContent()).trim() === 'And ends as oil.', 'The journey line turns to oil with the film');
    const during = await page.locator('.auth-form').boundingBox();
    check(during.x === form.x && during.y === form.y && during.height === form.height, 'The form stays fixed during the film');
    await page.waitForURL(base + '/SourcingOfficer');
    check(await page.locator('.zt-oil-success, video.zt-oil-film').count() === 0, 'The workspace opens without the film');
    await page.reload();
    check(await page.locator('.zt-oil-success').count() === 0, 'Reloading never replays it');
    await page.unroute('**/Auth/Login');
    await logout();

    // Refused credentials and a network failure.
    await page.goto(base + '/Auth/Login');
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill('Wrong-Password!123');
    await signIn.click();
    await page.locator('.validation-summary-errors').waitFor();
    check(await page.locator('.zt-oil-success').count() === 0, 'Wrong credentials never play the film');
    check(await signIn.isEnabled(), 'A refused attempt can be corrected');
    check(await page.locator('#Email').inputValue() === account[0], 'The email stays in place after a refusal');
    await page.route('**/Auth/Login', route => route.request().method() === 'POST' ? route.abort('failed') : route.continue());
    await page.locator('#Password').fill(account[1]);
    await signIn.click();
    await page.waitForFunction(() => document.querySelector('.auth-validation')?.textContent.includes('Could not complete sign-in'));
    check(await page.locator('.zt-oil-success').count() === 0, 'A network failure never plays the film');
    check(await signIn.isEnabled(), 'A network failure releases the submit button');
    await page.unroute('**/Auth/Login');

    // Film unavailable or refused by the browser: sign-in still lands.
    await fresh();
    await page.route('**/login/cinema/*.mp4', route => route.fulfill({ status: 404, body: '' }));
    await page.reload();
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    await signIn.click();
    await page.waitForURL(base + '/SourcingOfficer', { timeout: 6000 });
    check(true, 'A missing film does not block sign-in');
    await page.unroute('**/login/cinema/*.mp4');
    await logout();
    await page.goto(base + '/Auth/Login');
    await page.evaluate(() => { HTMLMediaElement.prototype.play = () => Promise.reject(new DOMException('blocked', 'NotAllowedError')); });
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    await signIn.click();
    await page.waitForURL(base + '/SourcingOfficer', { timeout: 6000 });
    check(true, 'Rejected autoplay does not block sign-in');
    await logout();

    // Manual pause and reduced motion skip the film and navigate promptly.
    await page.goto(base + '/Auth/Login');
    await page.evaluate(() => localStorage.setItem('zt.login.motion', 'paused'));
    await page.reload();
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    let started = Date.now();
    await signIn.click();
    await page.waitForURL(base + '/SourcingOfficer');
    check(Date.now() - started < 2500, 'Paused motion navigates without the film');
    await logout();
    await page.goto(base + '/Auth/Login');
    await page.evaluate(() => localStorage.removeItem('zt.login.motion'));
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.reload();
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    started = Date.now();
    await signIn.click();
    await page.waitForURL(base + '/SourcingOfficer');
    check(Date.now() - started < 2500, 'Reduced motion navigates without the film');
    await logout();
    await page.emulateMedia({ reducedMotion: 'no-preference' });

    // Mobile: no scene panel, the logo is back, the film gets its own frame.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(base + '/Auth/Login');
    check(!await page.locator('.auth-render').isVisible(), 'Mobile hides the scene panel');
    check(await page.locator('.auth-brand > img').evaluate(img => getComputedStyle(img).opacity === '1' && img.getBoundingClientRect().width > 0), 'Mobile shows the Zaytouna emblem');
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Mobile has no sideways overflow');
    await page.locator('#Email').fill(account[0]);
    await page.locator('#Password').fill(account[1]);
    await signIn.click();
    await page.locator('.zt-oil-success.is-mobile video').waitFor();
    const mobileFilm = await page.locator('video.zt-oil-film').boundingBox();
    check(mobileFilm.x >= 0 && mobileFilm.x + mobileFilm.width <= 390 && mobileFilm.y >= 0 && mobileFilm.y + mobileFilm.height <= 844, 'The mobile film fits inside the viewport');
    await page.waitForURL(base + '/SourcingOfficer');
    await page.setViewportSize({ width: 1440, height: 900 });
    await logout();

    // Without JavaScript the original form post still signs in.
    const plainContext = await page.context().browser().newContext({ javaScriptEnabled: false });
    const plain = await plainContext.newPage();
    await plain.goto(base + '/Auth/Login');
    check(await plain.locator('.auth-render img').isVisible(), 'The still render shows without JavaScript');
    await plain.locator('#Email').fill(account[0]);
    await plain.locator('#Password').fill(account[1]);
    await plain.getByRole('button', { name: 'Sign in', exact: true }).click();
    await plain.waitForURL(base + '/SourcingOfficer');
    check(true, 'The original form post works without JavaScript');
    await plainContext.close();

    check(errors.length === 0, 'No uncaught JavaScript errors: ' + errors.join(' | '));
    return { checks: passed.length, passed };
}
