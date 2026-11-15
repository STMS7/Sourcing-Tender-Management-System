async page=>{
 const base='http://localhost:5197', results=[]; const check=(ok,label)=>{if(!ok)throw Error(label);results.push(label);};
 await page.context().clearCookies(); await page.emulateMedia({reducedMotion:'reduce'});await page.setViewportSize({width:1440,height:1000});
 await page.goto(base+'/Auth/Login');await page.locator('#Email').fill('officer@example.test');await page.locator('#Password').fill('Preview-Only!4729');await page.locator('.auth-form button[type=submit]').click();await page.waitForURL(base+'/SourcingOfficer');
 check((await page.locator('.ow-kpi.is-dark').getAttribute('href')).includes('status=Closed'),'Compliance summary links to closed tenders');
 check((await page.getByRole('link',{name:/Create a tender/}).getAttribute('href')).includes('status=Approved'),'Create tender starts from approved requests');
 await page.goto(base+'/SourcingOfficer/RequestDetails/2');
 check(await page.getByRole('link',{name:'Return for changes',exact:true}).isVisible(),'Return action is visible beside acceptance');
 await page.getByRole('link',{name:'Reject permanently',exact:true}).click();
 check(await page.locator('#request-reject').evaluate(e=>e.open),'Rejection shortcut expands its required-reason form');
 check(await page.locator('#reject-reason').getAttribute('required')!==null,'Permanent rejection still requires a reason');
 await page.getByRole('link',{name:'Accept and create tender',exact:true}).click();
 await page.locator('#TermsAndConditions').fill('Synthetic UI QA supplier terms.');
 const date=new Date(Date.now()+7*86400000).toISOString().slice(0,16);
 await page.locator('#SubmissionDeadline').fill(date);await page.locator('#OpeningDate').fill(new Date(Date.now()+8*86400000).toISOString().slice(0,16));
 await page.getByRole('button',{name:'Accept and create draft',exact:true}).click();
 await page.waitForURL(/\/SourcingOfficer\/Details\/\d+/);const tenderId=Number(page.url().split('/').pop());
 check(await page.getByRole('link',{name:'Edit draft',exact:true}).isVisible(),'New draft exposes editing action');
 check(await page.getByRole('button',{name:'Publish and send invitations',exact:true}).isDisabled(),'Incomplete draft cannot publish');
 check((await page.locator('#publication-readiness').innerText()).includes('Select at least one'),'Publication blockers are explained');
 await page.getByRole('link',{name:'Manage supplier invitations',exact:true}).click();
 check(await page.locator('#tab-suppliers').isVisible(),'Supplier shortcut opens the correct tab');
 const supplier=page.locator('input[name=supplierIds]').first();await supplier.check();
 await page.getByRole('button',{name:'Add selected suppliers',exact:true}).click();await page.waitForURL(base+'/SourcingOfficer/Details/'+tenderId);
 check((await page.locator('#tab-suppliers').innerText()).includes('Prepared'),'Existing invitation POST remains connected');
 await page.getByRole('link',{name:'Manage required documents',exact:true}).click();
 await page.locator('.od-add summary').click();await page.locator('#document-type').fill('Synthetic UI certificate');await page.locator('#document-description').fill('Browser-check document requirement');
 await page.getByRole('button',{name:'Add requirement',exact:true}).click();await page.waitForURL(base+'/SourcingOfficer/Details/'+tenderId);
 check((await page.locator('#tab-documents').innerText()).includes('Synthetic UI certificate'),'Document requirement action works in the existing tab');
 check(await page.locator('form[action$="AddCriterion"],form[action$="AssignEvaluationCommittee"]').count()===0,'Forbidden criteria and assignment actions stay absent');
 await page.screenshot({path:'C:/Users/user/AppData/Local/Temp/officer-actions-draft.png',fullPage:true});
 await page.goto(base+'/SourcingOfficer/RequestDetails/2');
 check((await page.getByRole('link',{name:'Open linked tender',exact:true}).getAttribute('href')).endsWith('/'+tenderId),'Converted request links to its actual tender');
 await page.goto(base+'/SourcingOfficer/Offers/1');
 check(await page.getByRole('button',{name:'Analyze offers with AI assistant',exact:true}).isVisible(),'AI action is visible on offer comparison');
 check((await page.getByRole('link',{name:'Review handoff and send to evaluation',exact:true}).getAttribute('href')).endsWith('#evaluation'),'Offer handoff link targets the evaluation tab');
 check(await page.locator('form[action$="Compliance"]').count()===0,'Compliance editing stays unavailable after handoff');
 await page.getByRole('link',{name:'Review handoff and send to evaluation',exact:true}).click();
 check(await page.locator('#tab-evaluation').isVisible(),'Handoff link opens evaluation instead of the overview');
 await page.goto(base+'/SourcingOfficer/Details/1#evaluation-handoff');
 check(await page.locator('#tab-evaluation').isVisible(),'Legacy handoff links continue to open evaluation');
 for (const size of [{width:390,height:844},{width:844,height:390}]){
  await page.setViewportSize(size);await page.goto(base+'/SourcingOfficer/Details/'+tenderId);
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No page overflow at '+size.width+'px');
 }
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:'C:/Users/user/AppData/Local/Temp/officer-actions-mobile.png',fullPage:true});
 return {assertions:results.length,tenderId,results};
}
