async page => {
    const base = 'http://127.0.0.1:5187';
    const results = [];
    const check = (ok, message) => { if (!ok) throw new Error(message); results.push(message); };
    const login = async (target, email) => {
        await target.goto(base + '/Messages');
        if (target.url().includes('/Identity/Account/Login')) {
            await target.getByRole('textbox', { name: 'Email', exact: true }).fill(email);
            await target.getByRole('textbox', { name: 'Password', exact: true }).fill('Preview-Only!4729');
            await target.getByRole('button', { name: 'Log in', exact: true }).click();
            await target.waitForURL(url => !url.pathname.includes('/Identity/Account/Login'));
        }
    };
    await login(page, 'officer@example.test');
    const contexts = [];
    try {
        for (const fixture of [
            { group: 'RequestingDepartment', context: 'request:1', user: 'requester', subject: 'Confirm food ingredient grade' },
            { group: 'Supplier', context: 'bid:1', user: 'supplier-user', subject: 'Confirm delivery and certificate' },
            { group: 'EvaluationCommittee', context: 'tender:1', user: 'technical', subject: 'Technical report clarification' }
        ]) {
            await page.goto(base + '/SourcingOfficer/Messages?group=' + fixture.group);
            await page.getByLabel('Related transaction', { exact: true }).selectOption(fixture.context);
            await page.getByLabel('Recipient', { exact: true }).selectOption(fixture.user);
            check(await page.getByLabel('Recipient', { exact: true }).locator('option[value="other"]').count() === 0,
                fixture.group + ': unrelated recipients are excluded');
            await page.getByLabel('Subject', { exact: true }).fill(fixture.subject);
            await page.getByLabel('Message', { exact: true }).fill('Please clarify the requirements for this transaction.');
            await page.getByRole('button', { name: 'Send message', exact: true }).click();
            await page.waitForURL(url => url.searchParams.has('conversationId'));
            const id = new URL(page.url()).searchParams.get('conversationId');
            check(await page.getByRole('heading', { name: fixture.subject, exact: true }).isVisible(),
                fixture.group + ': new message is saved and selected');
            const receiverContext = await page.context().browser().newContext();
            contexts.push(receiverContext);
            const receiver = await receiverContext.newPage();
            await login(receiver, fixture.user + '@example.test');
            await receiver.goto(base + '/Messages?conversationId=' + id);
            check(await receiver.getByRole('heading', { name: fixture.subject, exact: true }).isVisible(),
                fixture.group + ': recipient can read the selected conversation');
            await receiver.getByLabel('Your response', { exact: true }).fill('Confirmed. These are the requested details.');
            await receiver.getByRole('button', { name: 'Send response', exact: true }).click();
            await receiver.waitForURL(url => url.searchParams.get('conversationId') === id);
            check(await receiver.getByText('Confirmed. These are the requested details.', { exact: true }).isVisible(),
                fixture.group + ': recipient response is saved');
            await page.reload();
            check(await page.getByText('Confirmed. These are the requested details.', { exact: true }).isVisible(),
                fixture.group + ': officer receives the response in the same conversation');
            if (fixture.group === 'Supplier') {
                await page.setViewportSize({ width: 1440, height: 1000 });
                await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/messages-desktop.png', fullPage: true });
                await page.setViewportSize({ width: 390, height: 844 });
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Officer message layout fits mobile');
                await page.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/messages-mobile.png', fullPage: true });
                await receiver.setViewportSize({ width: 390, height: 844 });
                check(await receiver.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Recipient inbox fits mobile');
                await receiver.screenshot({ path: 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/messages-recipient-mobile.png', fullPage: true });
            }
            await page.getByRole('button', { name: 'Mark resolved', exact: true }).click();
            await page.waitForURL(url => url.searchParams.get('conversationId') === id);
            check(await page.getByRole('button', { name: 'Send response', exact: true }).count() === 0,
                fixture.group + ': resolution removes the reply form');
            await receiver.reload();
            check(await receiver.getByRole('button', { name: 'Send response', exact: true }).count() === 0,
                fixture.group + ': recipient cannot respond after resolution');
        }
        const outsiderContext = await page.context().browser().newContext();
        contexts.push(outsiderContext);
        const outsider = await outsiderContext.newPage();
        await login(outsider, 'other@example.test');
        const denied = await outsider.request.get(base + '/SourcingOfficer/Messages?conversationId=1');
        check(denied.status() === 404, 'An unrelated officer cannot open a conversation by ID');
        await page.goto(base + '/SourcingOfficer/Clarifications');
        check(page.url().includes('/SourcingOfficer/Messages'), 'Legacy clarification links redirect to Messages');
        const noCsrf = await page.request.post(base + '/Messages/Reply', { form: { id: '1', response: 'Forged' } });
        check(noCsrf.status() === 400, 'Shared message replies require antiforgery tokens');
        return results;
    } finally {
        for (const context of contexts) await context.close();
    }
}
