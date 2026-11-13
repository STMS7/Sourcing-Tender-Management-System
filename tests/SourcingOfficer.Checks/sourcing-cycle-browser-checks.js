async page => {
    const base = 'http://127.0.0.1:5187';
    const checks = [];
    const check = (ok, message) => { if (!ok) throw new Error(message); checks.push(message); };
    const login = async (target, email, destination) => {
        await target.goto(base + '/Auth/Login?returnUrl=' + encodeURIComponent(destination));
        await target.getByLabel('Email', { exact: true }).fill(email);
        await target.getByLabel('Password', { exact: true }).fill('Preview-Only!4729');
        await target.getByRole('button', { name: 'Login', exact: true }).click();
        await target.waitForURL(url => !url.pathname.includes('/Auth/Login'));
    };
    const capture = async (name, width, height) => {
        await page.setViewportSize({ width, height });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name + ': no page overflow');
        await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/' + name + '.png', fullPage: true });
    };
    const contexts = [];
    try {
        await login(page, 'officer@example.test', '/SourcingOfficer/RequestDetails/2');
        await capture('cycle-request-desktop', 1440, 1100);
        await page.getByRole('link', { name: 'Request changes in chat', exact: true }).click();
        check(await page.getByLabel('Related transaction').inputValue() === 'request:2', 'Revision chat retains the source request');
        check(await page.getByLabel('Recipient', { exact: true }).inputValue() === 'requester', 'Request owner is preselected');
        check(await page.getByLabel('Subject', { exact: true }).inputValue() === 'Changes requested for PR-PREVIEW', 'Revision subject is prefilled');
        await page.getByLabel('Message', { exact: true }).fill('Please clarify the bag thickness and revise the specification if needed.');
        await capture('cycle-revision-mobile', 390, 844);
        await page.getByRole('button', { name: 'Send message', exact: true }).click();
        await page.waitForURL(url => url.searchParams.has('conversationId'));
        const conversationId = new URL(page.url()).searchParams.get('conversationId');
        const departmentContext = await page.context().browser().newContext();
        contexts.push(departmentContext);
        const department = await departmentContext.newPage();
        await login(department, 'requester@example.test', '/Messages?conversationId=' + conversationId);
        await department.getByLabel('Your response', { exact: true }).fill('Received. I will review and update the packaging specification.');
        await department.getByRole('button', { name: 'Send response', exact: true }).click();
        await department.getByText('Received. I will review and update the packaging specification.', { exact: true }).waitFor();
        await page.goto(base + '/SourcingOfficer/RequestDetails/2');
        check(await page.getByRole('link', { name: 'Changes requested for PR-PREVIEW', exact: true }).isVisible(), 'The source request links to its department conversation');
        check(await page.getByText('Answered', { exact: true }).isVisible(), 'Department response status reaches the request details');
        check(await page.getByRole('link', { name: 'Create tender draft', exact: true }).isVisible(), 'Chat preserves the approved request state');
        await page.goto(base + '/SourcingOfficer/RequestDetails/1');
        check(await page.getByRole('link', { name: 'Chat with requesting department', exact: true }).isVisible() &&
            await page.getByRole('link', { name: 'Create tender draft', exact: true }).count() === 0,
            'Department chat remains available after conversion without duplicate creation');
        await page.goto(base + '/SourcingOfficer/Tenders');
        await page.getByRole('link', { name: 'Manage Packaging tender ready for evaluation', exact: true }).click();
        const tenderId = page.url().split('/').pop();
        const send = page.getByRole('button', { name: 'Send tender and compliant offers to evaluation committee', exact: true });
        check(await send.isEnabled(), 'Completed compliance and valid committee enable the send action');
        await capture('cycle-handoff-desktop', 1440, 1100);
        await capture('cycle-handoff-mobile', 390, 844);
        await send.click();
        await page.getByText('Tender and compliant offers sent to the technical and financial evaluation committee.', { exact: true }).waitFor();
        check(await page.getByRole('button', { name: 'Send tender and compliant offers to evaluation committee', exact: true }).count() === 0,
            'Sending moves the tender beyond compliance and removes duplicate submission');
        check(await page.getByText('Sent to the evaluation committee on', { exact: false }).isVisible(), 'Successful delivery is recorded on the tender page');
        await capture('cycle-sent-desktop', 1440, 1100);
        for (const account of ['technical', 'financial']) {
            const context = await page.context().browser().newContext();
            contexts.push(context);
            const evaluator = await context.newPage();
            await login(evaluator, account + '@example.test', '/EvaluationCommittee/Evaluate/' + tenderId);
            check(evaluator.url().includes('/EvaluationCommittee/Evaluate/' + tenderId),
                account + ': authenticated account reaches its evaluation page');
            check((await evaluator.locator('body').innerText()).includes('Packaging tender ready for evaluation') &&
                (await evaluator.locator('body').innerText()).includes('Food Supplier'),
                account + ': receives the tender and compliant supplier offer');
        }
        return checks;
    } finally {
        for (const context of contexts) await context.close();
    }
}
