// Uses the isolated fixture at localhost:5197. No real credentials or data.
async page => {
    const base = 'http://localhost:5197';
    const passed = [];
    const check = (value, label) => { if (!value) throw new Error(label); passed.push(label); };
    await page.context().clearCookies();
    await page.goto(base + '/Auth/Login');
    await page.evaluate(() => localStorage.removeItem('zt.login.motion'));
    await page.reload();
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    const sizes = [[1440,900],[1366,768],[1024,768],[390,844],[375,667],[320,568],[320,480],[844,390],[390,350],[320,320]];
    for (const [width,height] of sizes) {
        await page.setViewportSize({ width,height });
        const bounds = await page.evaluate(() => {
            const form = document.querySelector('.auth-form').getBoundingClientRect();
            return { top:form.top, bottom:form.bottom, scroll:document.scrollingElement.scrollHeight, width:document.scrollingElement.scrollWidth };
        });
        check(bounds.scroll === height && bounds.width <= width, `${width}x${height}: no page scroll`);
        check(bounds.top >= 0 && bounds.bottom <= height, `${width}x${height}: complete password form fits`);
        const start = await page.locator('.auth-form').boundingBox();
        await page.mouse.wheel(0,650);
        const end = await page.locator('.auth-form').boundingBox();
        check(start.y === end.y && await page.evaluate(() => scrollY === 0), `${width}x${height}: wheel does not move form`);
        await page.getByRole('button', { name:'Device help & sign-in options',exact:true }).click();
        check(await page.locator('#auth-options-dialog').evaluate(el => el.open), `${width}x${height}: auxiliary options reachable`);
        await page.keyboard.press('Escape');
    }
    await page.setViewportSize({ width:1440,height:900 });
    const original = await page.locator('.auth-form').boundingBox();
    await page.locator('#Email').focus();
    check(await page.locator('body').getAttribute('data-auth-focus') === 'email', 'Branch responds to email focus');
    await page.locator('#Email').fill('motion@example.test');
    check(await page.locator('.auth-story').evaluate(el => Number(el.style.getPropertyValue('--auth-growth')) > 0), 'Typing drives bounded growth progress');
    await page.locator('#Password').focus();
    check(await page.locator('body').getAttribute('data-auth-focus') === 'password', 'Tree tracks password focus');
    await page.mouse.move(1200,180);
    await page.waitForFunction(() => document.querySelector('.auth-story').style.getPropertyValue('--auth-branch-x') !== '');
    check(await page.locator('.auth-story').evaluate(el => el.style.getPropertyValue('--auth-branch-x') !== '0px'), 'Artwork follows mouse');
    const after = await page.locator('.auth-form').boundingBox();
    check(original.x === after.x && original.y === after.y, 'Interaction never animates the form');
    check(await page.locator('.auth-botanical').getAttribute('aria-hidden') === 'true', 'Decorative artwork excluded from screen readers');
    await page.locator('[data-auth-motion]').click();
    check(await page.locator('body').evaluate(el => el.classList.contains('auth-motion-paused')), 'Manual motion pause works');
    await page.reload();
    check(await page.locator('body').evaluate(el => el.classList.contains('auth-motion-paused')), 'Motion preference persists');
    await page.locator('[data-auth-motion]').click();
    await page.emulateMedia({ reducedMotion:'reduce' });
    check(await page.locator('body').evaluate(el => el.classList.contains('auth-motion-paused')), 'System reduced motion is respected');
    await page.emulateMedia({ reducedMotion:'no-preference' });
    await page.locator('#Email').clear();
    await page.locator('[data-auth-biometric="btnFaceLogin"]').click();
    check(await page.locator('#auth-options-dialog').evaluate(el => el.open), 'Face shortcut opens the existing biometric controls');
    check(await page.locator('#bio-status').isVisible(), 'Biometric feedback stays inside dialog');
    await page.locator('#auth-options-dialog details').evaluate(el => el.open = true);
    const during = await page.locator('.auth-form').boundingBox();
    check(during.y === original.y && await page.evaluate(() => scrollY === 0), 'Device details do not push the form');
    await page.keyboard.press('Escape');
    check(await page.locator('[data-auth-biometric="btnFaceLogin"]').evaluate(el => el === document.activeElement), 'Escape restores shortcut focus');
    await page.setViewportSize({ width:390,height:568 });
    await page.getByRole('button', {name:'Device help & sign-in options',exact:true}).click();
    await page.locator('#auth-options-dialog details').evaluate(el => el.open = true);
    check(await page.locator('#auth-options-dialog').evaluate(el => el.scrollHeight > el.clientHeight && getComputedStyle(el).overflowY === 'auto'), 'Long diagnostics scroll inside the dialog only');
    await page.keyboard.press('Escape');
    await page.setViewportSize({ width:1440,height:900 });
    await page.locator('#Password').focus();
    await page.screenshot({ path:'login-olive-interactive-final.png' });
    return { checks:passed.length, passed };
}
