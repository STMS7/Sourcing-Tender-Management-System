async page => {
    const base = 'http://127.0.0.1:5187';
    const results = [];
    const check = (value, message) => {
        if (!value) throw new Error(message);
        results.push(message);
    };
    // Simulate speech events only; search requests still use the real authenticated server.
    await page.addInitScript(() => {
        window.SpeechRecognition = class {
            constructor() { window.testRecognition = this; }
            start() { this.onstart?.(); }
            stop() { this.onend?.(); }
            abort() { this.onend?.(); }
        };
    });
    await page.goto(base + '/SourcingOfficer');
    if (/\/(?:Identity\/Account|Auth)\/Login/.test(page.url())) {
        await page.getByRole('textbox', { name: 'Email', exact: true }).fill('officer@example.test');
        await page.getByRole('textbox', { name: 'Password', exact: true }).fill('Preview-Only!4729');
        await page.getByRole('button', { name: /^(Log in|Login)$/ }).click();
        await page.waitForURL(base + '/SourcingOfficer');
    }
    await page.goto(base + '/SourcingOfficer/Tenders?search=sugar');
    await page.evaluate(() => { window.documentMarker = 'same-document'; });
    const waitCount = count => page.waitForFunction(expected =>
        document.querySelector('[data-result-count]')?.dataset.resultCount === String(expected), count);
    await waitCount(1);
    await page.getByRole('searchbox').fill('');
    await waitCount(2);
    check(await page.evaluate(() => window.documentMarker === 'same-document'), 'Clearing a prefiltered search restores database results without navigation');
    await page.getByRole('searchbox').fill('no matching tender');
    await waitCount(0);
    await page.getByRole('searchbox').fill('sugar');
    await waitCount(1);
    check(await page.getByRole('link', { name: 'Manage Sugar tender' }).count() === 1, 'Typing filters matching tenders live');
    await page.getByRole('link', { name: 'Reset', exact: true }).click();
    await waitCount(2);
    await page.getByLabel('Status', { exact: true }).selectOption('Published');
    await waitCount(0);
    await page.getByLabel('Status', { exact: true }).selectOption('UnderEvaluation');
    await waitCount(1);
    check(true, 'Status filtering applies without submitting the form');
    await page.getByRole('link', { name: 'Reset', exact: true }).click();
    await waitCount(2);
    await page.getByLabel('Voice language').selectOption('ar-JO');
    await page.getByRole('button', { name: 'Search by voice', exact: true }).click();
    check(await page.evaluate(() => window.testRecognition.lang === 'ar-JO' && window.testRecognition.interimResults), 'Selected voice language and interim recognition are configured');
    await page.evaluate(() => window.testRecognition.onresult({ results: [[{ transcript: 'sugar' }]] }));
    await waitCount(1);
    check(await page.getByRole('searchbox').inputValue() === 'sugar', 'Recognized speech fills the input and performs the same live database search');
    await page.getByRole('button', { name: 'Stop listening', exact: true }).click();
    check(await page.getByLabel('Voice language').isEnabled(), 'Stopping recognition restores controls');
    await page.getByRole('button', { name: 'Search by voice', exact: true }).click();
    await page.evaluate(() => { window.testRecognition.onerror({ error: 'not-allowed' }); window.testRecognition.onend(); });
    check((await page.locator('#tender-search-status').innerText()).includes('permission was denied'), 'Denied microphone permission has an actionable message');
    await page.route('**/SourcingOfficer/Tenders?search=failed-search', route => route.abort());
    await page.getByRole('searchbox').fill('failed-search');
    await page.waitForFunction(() => document.querySelector('#tender-search-status').textContent.includes('could not update'));
    check(await page.locator('#tender-results [data-result-count]').getAttribute('data-result-count') === '1', 'Failed requests preserve the previous result set');
    await page.unroute('**/SourcingOfficer/Tenders?search=failed-search');
    await page.getByRole('searchbox').fill('');
    await waitCount(2);
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/live-search-desktop.png', fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Live search controls fit mobile width');
    await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/live-search-mobile.png', fullPage: true });
    await page.goto(base + '/SourcingOfficer/Requests');
    await page.getByRole('searchbox').fill('nothing');
    await page.waitForFunction(() => document.querySelector('#request-search-status').textContent.includes('result(s) found'));
    check(await page.locator('#request-results [data-result-count]').count() === 1, 'Approved request search returns a rendered fragment');
    await page.goto(base + '/SourcingOfficer');
    await page.getByRole('searchbox', { name: 'Search tasks' }).fill('nothing');
    check(await page.locator('#no-tasks').isVisible(), 'Task search filters immediately');
    await page.getByRole('button', { name: 'Search by voice', exact: true }).click();
    await page.evaluate(() => window.testRecognition.onresult({ results: [[{ transcript: 'sugar' }]] }));
    check(await page.locator('[data-task]:visible').count() === 1, 'Voice search uses the same task filter');
    await page.getByRole('button', { name: 'Stop listening', exact: true }).click();
    await page.addInitScript(() => { window.SpeechRecognition = undefined; window.webkitSpeechRecognition = undefined; });
    await page.reload();
    check(await page.getByRole('button', { name: 'Search by voice', exact: true }).isDisabled(), 'Unsupported browsers retain typed search and disable voice');
    await page.getByRole('searchbox', { name: 'Search tasks' }).fill('sugar');
    check(await page.locator('[data-task]:visible').count() === 1, 'Typed search works when speech is unsupported');
    return results;
}
