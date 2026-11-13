async page => {
    const base = 'http://127.0.0.1:5187';
    const checks = [];
    const check = (ok, label) => { if (!ok) throw new Error(label); checks.push(label); };
    const login = async (target, account, destination) => {
        await target.goto(base + '/Auth/Login?returnUrl=' + encodeURIComponent(destination));
        await target.getByLabel('Email', { exact: true }).fill(account + '@example.test');
        await target.getByLabel('Password', { exact: true }).fill('Preview-Only!4729');
        await target.getByRole('button', { name: 'Login', exact: true }).click();
        await target.waitForURL(url => !url.pathname.includes('/Auth/Login'));
    };
    await login(page, 'officer', '/SourcingOfficer/Tenders');
    const contexts = [];
    try {
        const existing = await page.request.get(base + '/SourcingOfficer/Edit/3');
        if (existing.status() !== 200) {
            await page.goto(base + '/SourcingOfficer/Create?requestId=2');
            check(await page.locator('[name="TechnicalWeight"],[name="FinancialWeight"]').count() === 0, 'Create form has no overall weight fields');
            await page.getByLabel('Title', { exact: true }).fill('Weight ownership test draft');
            await page.getByLabel('Category', { exact: true }).selectOption('1');
            await page.getByLabel('Submission deadline (UTC)', { exact: true }).fill(new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 16));
            await page.getByLabel('Terms and conditions', { exact: true }).fill('Food-safe packaging and agreed delivery.');
            await page.getByRole('button', { name: 'Save draft', exact: true }).click();
            await page.waitForURL('**/SourcingOfficer/Details/*');
        }
        await page.goto(base + '/SourcingOfficer/Edit/3');
        check(await page.locator('[name="TechnicalWeight"],[name="FinancialWeight"]').count() === 0, 'Edit form has no overall weight fields');
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/weight-form-desktop.png', fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Weight-free form fits mobile');
        await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/weight-form-mobile.png', fullPage: true });
        const form = page.locator('form').filter({ has: page.getByRole('button', { name: 'Save draft', exact: true }) });
        const payload = await form.evaluate(element => Object.fromEntries(new FormData(element).entries()));
        payload.TechnicalWeight = '99';
        payload.FinancialWeight = '1';
        const response = await page.request.post(new URL(await form.getAttribute('action'), base).href, { form: payload });
        check(response.url().includes('/SourcingOfficer/Details/3'), 'Forged weight keys do not prevent saving valid officer fields');
        await page.goto(base + '/SourcingOfficer/Details/3');
        check(await page.getByText('Awaiting the technical & financial evaluation committee.', { exact: true }).isVisible(),
            'Default database numbers are not shown as an agreed committee decision');
        await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/weight-pending-mobile.png', fullPage: true });
        await page.goto(base + '/SourcingOfficer/Details/2');
        const handoff = page.getByRole('button', { name: 'Send tender and compliant offers to evaluation committee', exact: true });
        if (await handoff.count()) {
            await handoff.click();
            await page.getByText('Sent to the evaluation committee on', { exact: false }).waitFor();
        }
        for (const [account, technical, financial] of [['technical', '80', '20'], ['financial', '60', '40']]) {
            const context = await page.context().browser().newContext();
            contexts.push(context);
            const evaluator = await context.newPage();
            await login(evaluator, account, '/EvaluationCommittee/Evaluate/2');
            const submit = evaluator.getByRole('button', { name: /^(Submit weights|Update weights)$/ });
            if (await submit.count()) {
                await evaluator.getByLabel('Technical weight (%)', { exact: true }).fill(technical);
                await evaluator.getByLabel('Financial weight (%)', { exact: true }).fill(financial);
                await submit.click();
                await evaluator.waitForLoadState('networkidle');
            }
            check((await evaluator.locator('body').innerText()).includes('Packaging tender ready for evaluation'), account + ': committee page remains functional');
        }
        await page.goto(base + '/SourcingOfficer/Details/2');
        const agreed = page.locator('dd').filter({ hasText: 'agreed by the evaluation committee' });
        check((await agreed.innerText()).includes('70') && (await agreed.innerText()).includes('30'),
            'Officer details show the committee-agreed 70/30 split read-only');
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/weight-agreed-desktop.png', fullPage: true });
        return checks;
    } finally {
        for (const context of contexts) await context.close();
    }
}
