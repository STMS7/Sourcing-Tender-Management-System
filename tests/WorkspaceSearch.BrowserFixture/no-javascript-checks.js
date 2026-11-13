async page => {
    const context = await page.context().browser().newContext({ javaScriptEnabled: false });
    const target = await context.newPage();
    try {
        await target.goto('http://127.0.0.1:5191/Auth/Login?returnUrl=%2FAdmin%2FUsers');
        await target.getByLabel('Email', { exact: true }).fill('admin@example.test');
        await target.getByLabel('Password', { exact: true }).fill('Preview-Only!4729');
        await target.getByRole('button', { name: 'Login', exact: true }).click();
        await target.waitForURL('http://127.0.0.1:5191/Admin/Users');
        await target.getByLabel('Search name or email', { exact: true }).fill('Osama');
        await target.getByRole('button', { name: 'Search', exact: true }).click();
        await target.waitForURL(/search=Osama/);
        if (await target.locator('#admin-user-results [data-search-item]').count() !== 1)
            throw new Error('Native GET search fallback failed');
        await target.goto('http://127.0.0.1:5191/AdminCatalog/Departments');
        if (await target.locator('form[data-live-search]').isVisible())
            throw new Error('JavaScript-only local controls should remain hidden');
        if (await target.locator('[data-search-item]').count() !== 2)
            throw new Error('Records were lost without JavaScript');
        return { checks: 3, result: 'Native GET search works; local controls hide while records remain visible' };
    } finally { await context.close(); }
}
