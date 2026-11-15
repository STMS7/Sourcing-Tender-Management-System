async page => {
    const base = 'http://127.0.0.1:5191';
    const proof = 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/global-voice';
    const checks = [];
    const check = (ok, label) => { if (!ok) throw new Error(label); checks.push(label); };
    // Only recognition events are simulated; pages, authorization and search fetches are real.
    await page.addInitScript(() => {
        window.SpeechRecognition = class {
            constructor() { window.testRecognition = this; }
            start() { this.onstart?.(); }
            stop() { this.onend?.(); }
            abort() { this.onend?.(); }
        };
    });
    async function login(account, destination) {
        await page.context().clearCookies();
        await page.goto(base + '/Auth/Login?returnUrl=' + encodeURIComponent(destination));
        await page.getByLabel('Email', { exact: true }).fill(account + '@example.test');
        await page.getByLabel('Password', { exact: true }).fill('Preview-Only!4729');
        await page.getByRole('button', { name: 'Login', exact: true }).click();
        await page.waitForURL(base + destination);
    }
    const toolbar = () => page.locator('form[data-live-search]');
    const query = () => toolbar().locator('[data-search-input]');
    const status = async () => {
        const id = await toolbar().getAttribute('data-search-status');
        return page.locator('#' + id);
    };
    const resultRows = async () => {
        const id = await toolbar().getAttribute('data-results');
        const selector = await toolbar().getAttribute('data-row-selector') || '[data-search-item]';
        return page.locator('#' + id).locator(selector);
    };
    const waitSettled = async () => {
        const id = await toolbar().getAttribute('data-results');
        await page.waitForFunction(id => document.getElementById(id)?.getAttribute('aria-busy') !== 'true', id);
    };
    async function speak(text, lang) {
        await toolbar().locator('[data-voice-language]').selectOption(lang);
        await toolbar().locator('[data-voice-search]').click();
        check(await page.evaluate(lang => window.testRecognition.lang === lang && window.testRecognition.interimResults, lang),
            'Recognition uses ' + lang + ' at ' + new URL(page.url()).pathname);
        await page.evaluate(text => window.testRecognition.onresult({ results: [[{ transcript: text }]] }), text);
        await toolbar().locator('[data-voice-search]').click();
    }
    async function capture(name) {
        for (const [label, width, height] of [['desktop',1440,1000], ['mobile',390,844]]) {
            await page.setViewportSize({ width, height });
            await page.mouse.move(5, 55);
            await page.waitForTimeout(200); // Settle Bootstrap hover transitions before the evidence capture.
            check(await toolbar().isVisible(), name + ' search controls visible at ' + width);
            check(await toolbar().evaluate(el => el.scrollWidth <= el.clientWidth + 1), name + ' search controls fit at ' + width);
            await page.screenshot({ path: proof + '/' + name + '-' + label + '.png', fullPage: true });
        }
    }
    const surfaces = [
        ['admin', ['/Admin','/Admin/Users','/AdminCatalog/Departments','/AdminCatalog/Categories','/AdminCatalog/Suppliers','/AdminCatalog/Roles','/AdminCatalog/AuditLogs']],
        ['requester', ['/Department/Dashboard','/Department/Index','/Department/Details/1','/Messages','/CycleChat']],
        ['officer', ['/SourcingOfficer','/SourcingOfficer/Requests','/SourcingOfficer/RequestDetails/1','/SourcingOfficer/Tenders','/SourcingOfficer/Details/1','/SourcingOfficer/Offers/1','/SourcingOfficer/Messages']],
        ['supplier', ['/Suppliers','/Suppliers/Invitations','/Suppliers/Invitation/1','/Suppliers/Offers','/Suppliers/OfferDetails/1','/Suppliers/Questions','/Suppliers/Notifications','/CycleChat/Thread/1']],
        ['technical', ['/EvaluationCommittee','/EvaluationCommittee/Evaluate/1']],
        ['financial', ['/EvaluationCommittee','/EvaluationCommittee/Evaluate/1']],
        ['manager', ['/SourcingManager','/SourcingManager/Details/1']],
        ['top', ['/TopCommittee','/TopCommittee/History','/TopCommittee/Statistics','/TopCommittee/Details/4']]
    ];
    for (const [account, paths] of surfaces) {
        await login(account, paths[0]);
        for (const path of paths) {
            const response = await page.goto(base + path);
            check(response.status() === 200 && !page.url().includes('/Auth/'), account + ' page renders ' + path);
            check(await toolbar().count() === 1 && await toolbar().isVisible(), account + ' has one working search surface ' + path);
            check(await toolbar().locator('[data-voice-language] option[value="ar-JO"]').count() === 1 &&
                await toolbar().locator('[data-voice-language] option[value="en-US"]').count() === 1,
                'Both languages available at ' + path);
            check(await page.locator('script[src*="/js/sourcing-search.js"]').count() === 1, 'Shared asset loads once at ' + path);
            const ids = await page.locator('[id]').evaluateAll(nodes => nodes.map(n => n.id));
            check(new Set(ids).size === ids.length, 'Unique element IDs at ' + path);
        }
    }
    // Real live server search must fetch records again rather than filter only the initial result subset.
    for (const [account, path, term, text, shot] of [
        ['admin','/Admin/Users?search=absent-initial','أسامة','Osama','admin'],
        ['requester','/Department/Index?search=absent-initial','حواسيب','Laptops','department'],
        ['officer','/SourcingOfficer/Tenders?search=absent-initial','السكر','Sugar tender','officer'],
        ['top','/TopCommittee/History?search=absent-initial','الطابعات','Printers','top']
    ]) {
        await login(account, path);
        await page.evaluate(() => window.searchDocumentMarker = 'same-page');
        await speak(term, 'ar-JO');
        await page.waitForFunction(text => {
            const form = document.querySelector('form[data-live-search]');
            return document.getElementById(form.dataset.results)?.textContent.includes(text);
        }, text);
        check(await query().inputValue() === term, account + ' Arabic speech fills search field');
        check(await page.evaluate(() => window.searchDocumentMarker === 'same-page'), account + ' voice search stays in the same document');
        await toolbar().locator('[data-search-reset]').click();
        await page.waitForFunction(() => !new URL(location.href).searchParams.has('search'));
        await waitSettled();
        check(!(await query().inputValue()), account + ' clear restores unfiltered database query');
        await capture(shot);
    }
    // Preserve status filters while voice changes only the text query.
    await login('requester', '/Department/Index?status=Submitted');
    await speak('Laptops', 'en-US');
    await page.waitForFunction(() => location.search.includes('search=Laptops'));
    check(new URL(page.url()).searchParams.get('status') === 'Submitted', 'Department voice search preserves the selected request status');
    check(await page.locator('#department-request-results [data-search-item]').count() === 1, 'Department search remains scoped by status');
    // Local lists share the exact same text/voice pipeline and never make data visible across permissions.
    for (const [account, path, arabic, english, shot] of [
        ['admin','/AdminCatalog/Categories','غذائية','Food',null],
        ['supplier','/Suppliers/Invitations','السكر','Sugar','supplier'],
        ['technical','/EvaluationCommittee','السكر','Sugar','evaluation'],
        ['manager','/SourcingManager','المدير','Manager','manager']
    ]) {
        await login(account, path);
        const total = await (await resultRows()).count();
        check(total > 0, account + ' fixture contains searchable records');
        await speak(arabic, 'ar-JO');
        check(await (await resultRows()).filter({ visible: true }).count() > 0, account + ' Arabic speech filters loaded records');
        await speak('no-such-record-voice', 'en-US');
        check(await (await resultRows()).filter({ visible: true }).count() === 0, account + ' voice supports empty results');
        await speak(english, 'en-US');
        check(await (await resultRows()).filter({ visible: true }).count() > 0, account + ' English voice finds loaded records');
        await toolbar().locator('[data-search-reset]').click();
        check(await (await resultRows()).filter({ visible: true }).count() === total, account + ' clear restores loaded rows');
        if (shot) await capture(shot);
    }
    await login('admin', '/AdminCatalog/Departments');
    await query().fill('اَلْإِنْتَاج');
    check(await (await resultRows()).filter({ visible: true }).count() === 1, 'Arabic local search ignores optional vowel marks');
    await toolbar().locator('[data-search-reset]').click();
    const original = await page.locator('form[data-search-item] input[name="name"]').first().inputValue();
    await page.locator('form[data-search-item] input[name="name"]').first().fill('اسم غير محفوظ Unsaved name');
    await query().fill('Unsaved');
    check(await (await resultRows()).filter({ visible: true }).count() === 1, 'Editable catalogs search current input values without submitting edits');
    await query().fill('');
    check(await page.locator('form[data-search-item] input[name="name"]').first().inputValue() === 'اسم غير محفوظ Unsaved name', 'Filtering preserves unsaved form state');
    await page.locator('form[data-search-item] input[name="name"]').first().fill(original);
    await login('officer', '/SourcingOfficer');
    await speak('السكر', 'ar-JO');
    check(await page.locator('[data-task]:visible').count() === 1, 'Arabic voice still filters officer dashboard tasks');
    await speak('Packaging', 'en-US');
    check(await page.locator('[data-task]:visible').count() === 1, 'English voice still filters officer dashboard tasks');
    await login('officer', '/SourcingOfficer/Details/1');
    await query().fill('٥٠');
    check(await (await resultRows()).filter({ visible: true }).count() === 1, 'Arabic digit queries match displayed numeric quantities');
    await login('financial', '/Messages');
    check(await toolbar().isVisible() && (await (await status()).innerText()).includes('0'), 'An empty inbox keeps usable search controls without errors');
    await login('officer', '/SourcingOfficer/Tenders');
    await toolbar().locator('[data-voice-language]').selectOption('en-US');
    await query().fill('Sugar');
    await page.waitForFunction(() => document.querySelector('[data-result-count]')?.dataset.resultCount === '1');
    await page.route('**/SourcingOfficer/Tenders?search=voice-network-error', route => route.abort());
    await query().fill('voice-network-error');
    await page.waitForFunction(() => document.querySelector('#tender-search-status')?.textContent.includes('could not update'));
    check(await page.locator('[data-result-count]').getAttribute('data-result-count') === '1', 'Failed live requests preserve the last good results');
    await page.unroute('**/SourcingOfficer/Tenders?search=voice-network-error');
    await query().fill('Sugar');
    await page.waitForFunction(() => document.querySelector('#tender-results')?.getAttribute('aria-busy') === 'false' &&
        document.querySelector('#tender-search-status')?.textContent.includes('1 result'));
    check(await query().inputValue() === 'Sugar', 'Live search recovers after a failed request');
    // Permission failure and unsupported browsers retain the typed path.
    await login('supplier', '/Suppliers/Offers');
    check(!(await page.locator('body').innerText()).includes('BID-PRIVATE-OTHER'), 'Supplier page never loads another company offer');
    await speak('BID-PRIVATE-OTHER', 'en-US');
    check(await (await resultRows()).filter({ visible: true }).count() === 0, 'Voice cannot reveal another supplier offer');
    await toolbar().locator('[data-search-reset]').click();
    await toolbar().locator('[data-voice-search]').click();
    await page.evaluate(() => { window.testRecognition.onerror({ error: 'not-allowed' }); window.testRecognition.onend(); });
    check((await (await status()).innerText()).includes('permission was denied'), 'Denied permission has an actionable English message');
    await query().fill('BID-VOICE-1');
    check(await (await resultRows()).filter({ visible: true }).count() === 1, 'Typed search works after microphone denial');
    await speak('السكر', 'ar-JO');
    await page.goto(base + '/Suppliers/Invitations');
    check(await toolbar().locator('[data-voice-language]').inputValue() === 'ar-JO', 'Voice-language choice persists across pages');
    await toolbar().locator('[data-voice-search]').click();
    await page.evaluate(() => { window.testRecognition.onerror({ error: 'not-allowed' }); window.testRecognition.onend(); });
    check((await (await status()).innerText()).includes('إذن الميكروفون'), 'Denied permission has an actionable Arabic message');
    await toolbar().locator('[data-voice-search]').click();
    await page.evaluate(() => { window.oldSpeechSession = window.testRecognition; });
    await toolbar().locator('[data-search-reset]').click();
    await page.evaluate(() => window.oldSpeechSession.onresult({ results: [[{ transcript: 'stale transcript' }]] }));
    check(await query().inputValue() === '', 'Late speech events cannot restore a cleared query');
    await page.addInitScript(() => { window.SpeechRecognition = undefined; window.webkitSpeechRecognition = undefined; });
    await page.goto(base + '/Suppliers/Offers');
    check(await toolbar().locator('[data-voice-search]').isDisabled(), 'Unsupported recognition disables only the microphone control');
    await query().fill('VOICE');
    check(await (await resultRows()).filter({ visible: true }).count() === 1, 'Unsupported recognition keeps typed filtering working');
    // Anonymous requests cannot access protected searchable pages.
    await page.context().clearCookies();
    const denied = await page.request.get(base + '/Admin/Users?search=admin');
    check(denied.url().includes('/Auth/Login'), 'Search endpoints retain authentication protection');
    return { checks: checks.length, surfaces: surfaces.reduce((n, row) => n + row[1].length, 0), passed: checks };
}
