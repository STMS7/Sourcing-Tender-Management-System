async page => {
    const base = 'http://127.0.0.1:5189';
    const proof = 'C:/Users/user/AppData/Local/Temp/stms-sourcing-review/preparation';
    const checks = [];
    const check = (ok, label) => { if (!ok) throw new Error(label); checks.push(label); };
    async function login(account, path) {
        await page.context().clearCookies();
        await page.goto(base + '/Auth/Login?returnUrl=' + encodeURIComponent(path));
        await page.getByLabel('Email', { exact: true }).fill(account + '@example.test');
        await page.getByLabel('Password', { exact: true }).fill('Preview-Only!4729');
        await page.getByRole('button', { name: 'Login', exact: true }).click();
        await page.waitForURL(base + path);
    }
    async function capture(name, width, height) {
        await page.setViewportSize({ width, height });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name + ' fits viewport');
        await page.screenshot({ path: proof + '/' + name + '.png', fullPage: true });
    }
    await login('officer', '/SourcingOfficer/Tenders');
    const available = await page.request.get(base + '/SourcingOfficer/Create?requestId=2');
    if (available.status() === 200) {
        await page.goto(base + '/SourcingOfficer/Create?requestId=2');
    } else {
        // Permit rerunning this check only against its own unpublished synthetic draft.
        await page.goto(base + '/SourcingOfficer/Details/3');
        check(await page.getByRole('heading', { name: 'Browser preparation and category verification', exact: true }).isVisible(), 'Rerun targets its own synthetic draft');
        if (await page.getByText('Reopen draft and reset approval', { exact: true }).count()) {
            await page.getByText('Reopen draft and reset approval', { exact: true }).click();
            await page.getByRole('button', { name: 'Reopen preparation and reset approvals', exact: true }).click();
        }
        for (const name of ['Quality specifications', 'Delivery quality', 'Total cost', 'Food Supplier']) {
            const remove = page.getByRole('button', { name: 'Remove ' + name, exact: true });
            if (await remove.count()) await remove.click();
        }
        await page.getByRole('link', { name: 'Edit draft', exact: true }).click();
    }
    check(await page.locator('[name="TechnicalWeight"],[name="FinancialWeight"]').count() === 0, 'Officer cannot set overall weights');
    await page.getByLabel('Title', { exact: true }).fill('Browser preparation and category verification');
    await page.getByLabel('Submission deadline (UTC)', { exact: true }).fill(new Date(Date.now() + 864000000).toISOString().slice(0,16));
    await page.getByLabel('Terms and conditions', { exact: true }).fill('Food-safe sealed packaging; delivery according to agreed schedule.');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await page.waitForURL(/\/SourcingOfficer\/Details\/\d+/);
    const id = page.url().split('/').pop();
    check(await page.getByRole('button', { name: 'Publish and send invitations', exact: true }).isDisabled(), 'Unapproved draft cannot publish');
    check(await page.getByLabel('Food Supplier', { exact: true }).isVisible(), 'Matching-category supplier is available');
    check(!(await page.locator('body').innerText()).includes('Hardware supplier') &&
        !(await page.locator('body').innerText()).includes('Unmapped supplier'), 'Other and missing categories are excluded');
    check(await page.locator('form[action$="/AddCriterion"],form[action$="/RemoveCriterion"],#criterion-score').count() === 0,
        'Officer has no evaluation criterion or maximum score controls');
    check(await page.locator('#ManagerUserId,#DepartmentUserId,#TechnicalUserId,#FinancialUserId').count() === 0,
        'Officer has no committee assignment controls');
    const sourceToken = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const deniedAssignment = await page.request.post(base + '/SourcingOfficer/AssignEvaluationCommittee', { form: {
        TenderId: id, ManagerUserId: 'manager', DepartmentUserId: 'department', TechnicalUserId: 'technical',
        FinancialUserId: 'financial', __RequestVerificationToken: sourceToken
    }, maxRedirects: 0 });
    check(deniedAssignment.status() === 403 || (deniedAssignment.status() === 302 &&
        (deniedAssignment.headers().location || '').includes('AccessDenied')), 'Officer legacy assignment route is denied');
    const deniedAdmin = await page.request.get(base + '/AdminCommittee/Details/' + id, { maxRedirects: 0 });
    check(deniedAdmin.status() === 403 || (deniedAdmin.status() === 302 &&
        (deniedAdmin.headers().location || '').includes('AccessDenied')), 'Officer cannot open Admin committee assignment');
    await login('admin', '/AdminCommittee');
    check(await page.getByRole('heading', { name: 'Evaluation committees', exact: true }).isVisible(),
        'Admin can find tender committee assignments');
    await capture('admin-list-desktop', 1440, 1000);
    await capture('admin-list-mobile', 390, 844);
    await page.goto(base + '/AdminCommittee/Details/' + id);
    await page.locator('#ManagerUserId').selectOption('manager');
    await page.locator('#DepartmentUserId').selectOption('department');
    await page.locator('#TechnicalUserId').selectOption('technical');
    await page.locator('#FinancialUserId').selectOption('financial');
    await capture('admin-assignment-desktop', 1440, 1000);
    await capture('admin-assignment-mobile', 390, 844);
    await page.getByRole('button', { name: 'Save evaluation committee', exact: true }).click();
    await page.waitForFunction(() => document.body.innerText.includes('Evaluation committee assigned.'));
    await login('officer', '/SourcingOfficer/Details/' + id);
    check((await page.locator('body').innerText()).includes('technical — TechnicalRepresentative'),
        'Admin assignments reach the officer read-only tracking section');
    await page.getByLabel('Food Supplier', { exact: true }).check();
    await page.getByRole('button', { name: 'Add selected suppliers', exact: true }).click();
    const officerToken = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const denied = await page.request.post(base + '/EvaluationCommittee/SubmitWeights', { maxRedirects: 0, form: {
        tenderId: id, technicalWeight: '99', financialWeight: '1', __RequestVerificationToken: officerToken
    } });
    check(denied.status() === 403 || (denied.status() === 302 && (denied.headers().location || '').includes('AccessDenied')),
        'Officer cannot use the committee weight endpoint');
    await page.getByRole('button', { name: 'Lock preparation and request weight approval', exact: true }).click();
    await page.waitForFunction(() => document.body.innerText.includes('Preparation locked.'));
    check((await page.locator('body').innerText()).includes('Preparation locked.'), 'Officer requests approval while still Draft');
    check(await page.getByRole('link', { name: 'Edit draft', exact: true }).count() === 0, 'Locked preparation hides editing');
    await capture('officer-desktop', 1440, 1000);
    await capture('officer-mobile', 390, 844);
    await login('financial', '/EvaluationCommittee/Evaluate/' + id);
    const financialCriterionId = await page.locator('input[name="criterionWeights[0].CriterionId"]').inputValue();
    await login('technical', '/EvaluationCommittee/Evaluate/' + id);
    check(await page.getByRole('heading', { name: 'Pre-publication approval', exact: true }).isVisible(), 'Technical member receives preparation stage');
    check(await page.getByRole('heading', { name: 'Compliant bids', exact: true }).count() === 0, 'Preparation does not expose bid section');
    check((await page.locator('body').innerText()).includes('Quality specifications'), 'Committee can inspect locked criteria');
    await capture('committee-desktop', 1440, 1000);
    await capture('committee-mobile', 390, 844);
    check(await page.locator('[data-criterion-weight]').count() === 2, 'Technical member receives only the two technical criteria');
    const technicalToken = await page.locator('form[action$="/SubmitWeights"] input[name="__RequestVerificationToken"]').inputValue();
    const crossGroup = await page.request.post(base + '/EvaluationCommittee/SubmitWeights', { form: {
        tenderId: id, technicalWeight: '80', financialWeight: '20',
        'criterionWeights[0].CriterionId': financialCriterionId, 'criterionWeights[0].Weight': '100',
        __RequestVerificationToken: technicalToken
    } });
    check((await crossGroup.text()).includes('using only your assigned group'), 'Server rejects cross-group criterion weight forgery');
    await page.locator('[data-criterion-weight]').nth(0).fill('60');
    await page.locator('[data-criterion-weight]').nth(1).fill('40');
    await page.getByLabel('Technical weight (%)', { exact: true }).fill('80');
    await page.getByLabel('Financial weight (%)', { exact: true }).fill('20');
    await page.getByRole('button', { name: 'Approve criteria and submit weights', exact: true }).click();
    await login('officer', '/SourcingOfficer/Details/' + id);
    check(await page.getByRole('button', { name: 'Publish and send invitations', exact: true }).isDisabled(), 'One approval does not enable publication');
    await login('financial', '/EvaluationCommittee/Evaluate/' + id);
    check(await page.locator('[data-criterion-weight]').count() === 1, 'Financial member receives only the financial criterion');
    await page.locator('[data-criterion-weight]').fill('100');
    await page.getByLabel('Technical weight (%)', { exact: true }).fill('60');
    await page.getByLabel('Financial weight (%)', { exact: true }).fill('40');
    await capture('financial-desktop', 1440, 1000);
    await capture('financial-mobile', 390, 844);
    await page.getByRole('button', { name: 'Approve criteria and submit weights', exact: true }).click();
    check(/70(?:\.00)?%/.test(await page.locator('body').innerText()), 'Both approvals produce agreed 70/30 split');
    await login('officer', '/SourcingOfficer/Details/' + id);
    check(await page.locator('[name="Weight"],[name="TechnicalWeight"],[name="FinancialWeight"]').count() === 0,
        'Officer sees committee-assigned criterion and overall weights as read-only');
    check(await page.getByRole('button', { name: 'Publish and send invitations', exact: true }).isEnabled(), 'Both approvals enable publication');
    await page.getByRole('button', { name: 'Publish and send invitations', exact: true }).click();
    check((await page.locator('body').innerText()).includes('Published'), 'Officer publishes approved tender');
    await login('technical', '/EvaluationCommittee/Evaluate/' + id);
    check(await page.locator('[name="technicalWeight"]').count() === 0, 'Published weights are read-only');
    check(await page.getByRole('heading', { name: 'Compliant bids', exact: true }).count() === 0, 'Published offers stay sealed');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const late = await page.request.post(base + '/EvaluationCommittee/SubmitWeights', { form: {
        tenderId: id, technicalWeight: '99', financialWeight: '1', __RequestVerificationToken: token
    } });
    check(late.status() === 409, 'Forged late weight submission is blocked by server');
    await login('admin', '/AdminCommittee/Details/' + id);
    check(await page.locator('#ManagerUserId,#DepartmentUserId,#TechnicalUserId,#FinancialUserId').count() === 0,
        'Admin sees published committee as read-only');
    const adminToken = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const lateAssignment = await page.request.post(base + '/AdminCommittee/Assign', { form: {
        TenderId: id, ManagerUserId: 'manager', DepartmentUserId: 'department', TechnicalUserId: 'technical',
        FinancialUserId: 'financial', __RequestVerificationToken: adminToken
    } });
    check(lateAssignment.status() === 409, 'Admin cannot reassign committee after publication');
    const noAssignmentToken = await page.request.post(base + '/AdminCommittee/Assign', { form: { TenderId: id } });
    check(noAssignmentToken.status() === 400, 'Admin assignment requires antiforgery protection');
    // The second synthetic tender has offers and valid approval evidence preceding publication.
    await login('officer', '/SourcingOfficer/Details/2');
    check(await page.getByRole('button', { name: 'Send tender and compliant offers to evaluation committee', exact: true }).isEnabled(),
        'Completed compliance enables actual handoff');
    await page.getByRole('button', { name: 'Send tender and compliant offers to evaluation committee', exact: true }).click();
    check((await page.locator('body').innerText()).includes('UnderEvaluation'), 'Actual handoff advances tender');
    await login('technical', '/EvaluationCommittee/Evaluate/2');
    check(await page.getByRole('heading', { name: 'Compliant bids', exact: true }).isVisible(), 'Technical committee receives compliant offers after handoff');
    check((await page.locator('body').innerText()).includes('BID-HANDOFF-PREVIEW'), 'Correct tender offer reaches committee');
    await login('financial', '/EvaluationCommittee/Evaluate/2');
    check((await page.locator('body').innerText()).includes('BID-HANDOFF-PREVIEW'), 'Financial committee receives same correct offer');
    return { checks: checks.length, tenderId: id, passed: checks };
}
