// Synthetic preview only: start this project's --preview host first. No database is used.
const { chromium } = require('playwright');
const path = require('node:path');
const os = require('node:os');
const fs = require('node:fs');
(async () => {
    const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge' });
    const page = await browser.newPage();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const base = 'http://127.0.0.1:5198/SupplierReports/Index';
    const proof = path.join(os.tmpdir(), 'stms-supplier-report-preview');
    fs.mkdirSync(proof, { recursive: true });
    let checks = 0;
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    const check = (ok, label) => { if (!ok) throw new Error(label); checks++; console.log('PASS: ' + label); };
    try {
        for (const width of [375, 768, 1280, 1920]) {
            for (const theme of ['light', 'dark']) {
                await page.setViewportSize({ width, height: 1000 });
                const response = await page.goto(base);
                check(response.ok(), `report renders ${width}px ${theme}`);
                await page.evaluate(theme => { document.documentElement.dataset.bsTheme = theme; }, theme);
                check(await page.getByRole('heading', { level: 1, name: 'Supplier reports' }).count() === 1, 'one descriptive main heading');
                check(await page.locator('tbody tr').count() === 10, 'only ten suppliers on a page');
                check(await page.getByLabel('Company, registration or email').isVisible(), 'search has a visible label');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'no page overflow including Arabic names');
                check(await page.locator('.btn-primary').evaluate(element => {
                    const style = getComputedStyle(element);
                    const lum = color => { const values = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; }); return .2126 * values[0] + .7152 * values[1] + .0722 * values[2]; };
                    const a = lum(style.color), b = lum(style.backgroundColor);
                    return (Math.max(a,b) + .05) / (Math.min(a,b) + .05) >= 4.5;
                }), 'primary action text contrast meets 4.5:1');
                await page.getByRole('region', { name: 'Supplier results table' }).focus();
                check(await page.evaluate(() => document.activeElement.getAttribute('role') === 'region'), 'scrolling table is keyboard reachable');
                await page.screenshot({ path: path.join(proof, `report-${width}-${theme}.png`), fullPage: true });
            }
        }
        await page.getByRole('link', { name: 'Next', exact: true }).click();
        check(await page.locator('tbody tr').count() === 3, 'second page has remaining three suppliers');
        await page.getByRole('link', { name: 'Previous', exact: true }).click();
        await page.getByLabel('Registration status', { exact: true }).selectOption('Suspended');
        await page.getByRole('button', { name: 'Apply filters' }).click();
        check(await page.locator('tbody tr').count() === 4, 'registration filter applied');
        check((await page.getByRole('link', { name: 'Excel — this page' }).getAttribute('href')).includes('Status=Suspended'), 'download preserves selected filter');
        await page.getByRole('link', { name: 'Clear filters', exact: true }).first().click();
        await page.getByLabel('Document attention', { exact: true }).selectOption('Missing');
        await page.getByRole('button', { name: 'Apply filters' }).click();
        check(await page.locator('tbody tr').count() === 1, 'missing document filter applied');
        await page.getByLabel('Company, registration or email').fill('not-a-real-company');
        await page.getByRole('button', { name: 'Apply filters' }).click();
        check(await page.getByRole('heading', { name: 'No suppliers match these filters' }).isVisible(), 'empty results explained');
        await page.getByRole('link', { name: 'Clear filters', exact: true }).first().click();
        await page.getByLabel('Registered from').fill('2026-12-02');
        await page.getByLabel('Registered to').fill('2026-11-01');
        await page.getByRole('button', { name: 'Apply filters' }).click();
        check(await page.getByRole('alert').getByText('Registered from must be on or before registered to.').isVisible(), 'invalid dates explained');
        await page.goto(base);
        for (const [label, extension] of [['Excel — this page', 'xlsx'], ['PDF — this page', 'pdf']]) {
            const downloadPromise = page.waitForEvent('download');
            await page.getByRole('link', { name: label }).click();
            const download = await downloadPromise;
            await download.saveAs(path.join(proof, `suppliers.${extension}`));
            check(await download.failure() === null, `${extension} downloads successfully`);
            const bytes = fs.readFileSync(path.join(proof, `suppliers.${extension}`));
            check(bytes.subarray(0, extension === 'pdf' ? 5 : 2).toString() === (extension === 'pdf' ? '%PDF-' : 'PK'), `${extension} contains the correct file format`);
        }
        await page.getByRole('link', { name: /^View documents for/ }).first().click();
        check(await page.getByRole('heading', { name: 'Uploaded documents' }).isVisible(), 'supplier document drilldown works');
        check(await page.locator('tbody tr').count() === 10, 'document list is paged');
        check(await page.getByRole('cell', { name: 'Unknown — no expiry date', exact: true }).count() > 0, 'missing expiry is clearly unknown');
        await page.getByRole('link', { name: 'Next', exact: true }).click();
        check(await page.locator('tbody tr').count() === 2, 'document second page works');
        check(errors.length === 0, 'no browser JavaScript errors');
        console.log(`${checks} report browser checks passed. Evidence: ${proof}`);
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
