// Run the --preview fixture first. These checks do not connect to a database.
const { chromium } = require('playwright');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');

(async () => {
    const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge' });
    const page = await browser.newPage();
    const failures = [];
    page.on('pageerror', error => failures.push(error.message));
    let checks = 0;
    const check = (ok, label) => { if (!ok) throw new Error(label); checks++; console.log('PASS: ' + label); };
    const proof = path.join(os.tmpdir(), 'stms-sample-image-preview');
    fs.mkdirSync(proof, { recursive: true });
    try {
        for (const width of [1280, 375]) {
            await page.setViewportSize({ width, height: 900 });
            const response = await page.goto('http://127.0.0.1:5198/SamplePreview');
            check(response.ok(), `preview renders at ${width}px`);
            check(await page.locator('[data-pricing-tax]').innerText() === '16.00', '16% item tax shown');
            check(await page.locator('[data-pricing-total]').innerText() === '126.00', 'shipping added after tax');
            await page.getByLabel('Unit price (test quantity 1)').fill('0.10');
            check(await page.locator('[data-pricing-tax]').innerText() === '0.02', 'decimal tax rounding matches server');
            check(await page.locator('[data-pricing-total]').innerText() === '10.12', 'fractional total is exact');
            await page.getByLabel('Unit price (test quantity 1)').fill('-1');
            check(await page.locator('[data-pricing-total]').innerText() === '—', 'invalid price does not show a misleading total');
            await page.getByLabel('Unit price (test quantity 1)').fill('100');
            const input = page.getByLabel('Attach sample photographs');
            check(await input.getAttribute('multiple') !== null, 'multiple images selectable');
            check((await input.getAttribute('accept')).includes('image/png'), 'supported image types communicated');
            await input.focus();
            check(await input.evaluate(element => element === document.activeElement), 'upload control receives keyboard focus');
            const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAACXBIWXMAAAPoAAAD6AG1e1JrAAAAEUlEQVQImWPIqrDLqrBjgFAAID4EgdEOkZwAAAAASUVORK5CYII=', 'base64');
            await input.setInputFiles([{ name: 'one.png', mimeType: 'image/png', buffer: png }, { name: 'two.png', mimeType: 'image/png', buffer: png }]);
            check(await input.evaluate(element => element.files.length) === 2, 'two files selectable');
            await page.locator('img').last().scrollIntoViewIfNeeded();
            await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
            check(await page.getByRole('img').count() === 2, 'gallery images load');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'no horizontal overflow with long Arabic filename');
            await page.screenshot({ path: path.join(proof, `gallery-${width}.png`), fullPage: true });
        }
        await page.goto('http://127.0.0.1:5198/SamplePreview?empty=true');
        check(await page.getByText('No sample images attached.').isVisible(), 'empty gallery explained');
        check(failures.length === 0, 'no browser JavaScript errors');
        console.log(`${checks} browser checks passed. Screenshots: ${proof}`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
