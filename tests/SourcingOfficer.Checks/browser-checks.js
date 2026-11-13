async page => {
    const output = 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review';
    const base = 'http://127.0.0.1:5187';
    const results = [];
    const check = (value, message) => {
        if (!value) throw new Error(message);
        results.push(message);
    };
    const capture = async (name, width, height) => {
        await page.setViewportSize({ width, height });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), name + ': no page overflow');
        await page.screenshot({ path: output + '/' + name + '.png', fullPage: true });
    };
    for (const route of ['', '/Requests', '/RequestDetails/2', '/Tenders', '/Details/1', '/Offers/1', '/Clarifications', '/Create?requestId=2']) {
        const response = await page.goto(base + '/SourcingOfficer' + route);
        check(response.status() === 200, route + ': HTTP 200');
        check(await page.locator('h1').count() === 1, route + ': one main heading');
    }
    await page.goto(base + '/SourcingOfficer');
    await capture('overview-desktop', 1440, 1000);
    await page.getByRole('searchbox', { name: 'Search tasks' }).fill('does not exist');
    check(await page.locator('#no-tasks').isVisible(), 'Task filtering shows an empty state');
    await page.getByRole('searchbox', { name: 'Search tasks' }).fill('sugar');
    check(await page.locator('[data-task]:visible').count() === 1, 'Task filtering finds the requested task');
    await capture('overview-mobile', 390, 844);
    await page.goto(base + '/SourcingOfficer/Create?requestId=2');
    await capture('form-mobile', 390, 844);
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByLabel('Title', { exact: true }).fill('Food packaging procurement');
    await page.getByLabel('Category', { exact: true }).selectOption({ label: 'Food ingredients' });
    await page.getByLabel('Submission deadline (UTC)', { exact: true }).fill(new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 16));
    await page.getByLabel('Terms and conditions', { exact: true }).fill('Food-safe packaging; delivery in sealed cartons.');
    await page.getByLabel('Financial weight (%)', { exact: true }).fill('30');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    check(await page.getByText('Technical and financial weights must total 100%.', { exact: true }).count() > 0, 'Server rejects invalid weight totals');
    await page.getByLabel('Financial weight (%)', { exact: true }).fill('40');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await page.waitForURL('**/SourcingOfficer/Details/*');
    check(await page.getByRole('heading', { name: 'Food packaging procurement', exact: true }).isVisible(), 'Draft saves through the authenticated HTML form');
    const draftUrl = page.url();
    const draftId = draftUrl.split('/').pop();
    await page.goto(base + '/SourcingOfficer/Edit/' + draftId);
    check(await page.getByRole('heading', { name: 'Edit tender draft', exact: true }).isVisible(), 'Edit draft route renders');
    await page.getByLabel('Description', { exact: true }).fill('Packaging for food production.');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await page.waitForURL(draftUrl);
    check(await page.getByText('Packaging for food production.', { exact: true }).isVisible(), 'Draft edits persist through the authenticated form');
    for (const [name, mandatory] of [['Optional sample report', false], ['Mandatory safety certificate', true]]) {
        await page.getByText('Add document requirement', { exact: true }).click();
        await page.getByLabel('Document type', { exact: true }).fill(name);
        await page.getByLabel('Mandatory', { exact: true }).setChecked(mandatory);
        await page.getByRole('button', { name: 'Add requirement', exact: true }).click();
        check((await page.locator('li').filter({ hasText: name }).innerText()).includes(mandatory ? 'Mandatory' : 'Optional'),
            name + ': saved with the correct mandatory flag');
    }
    await capture('draft-desktop', 1440, 1000);
    await capture('draft-mobile', 390, 844);
    await page.goto(base + '/SourcingOfficer/Offers/1');
    await capture('comparison-desktop', 1440, 1000);
    await capture('comparison-mobile', 390, 844);
    await page.goto(base + '/SourcingOfficer/Clarifications?tenderId=1');
    await capture('clarifications-mobile', 390, 844);
    const missingCsrf = await page.request.post(base + '/SourcingOfficer/Close', { form: { id: '1' } });
    check(missingCsrf.status() === 400, 'POST without an antiforgery token is rejected');
    const anonymous = await page.context().browser().newContext();
    const anonymousPage = await anonymous.newPage();
    await anonymousPage.goto(base + '/SourcingOfficer');
    check(anonymousPage.url().includes('/Identity/Account/Login'), 'Anonymous access redirects to login');
    await anonymous.close();
    return { results, screenshotDirectory: output };
}
