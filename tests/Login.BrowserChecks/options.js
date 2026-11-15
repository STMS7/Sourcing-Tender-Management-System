async page=>{
 const results=[];const check=(ok,label)=>{if(!ok)throw Error(label);results.push(label);};
 await page.context().clearCookies();await page.goto('http://localhost:5197/Auth/Login');await page.setViewportSize({width:1440,height:900});
 let release,requested;const gate=new Promise(r=>release=r),seen=new Promise(r=>requested=r);
 await page.route('**/Biometric/PasskeyLoginOptions',async r=>{requested();await gate;await r.fulfill({status:503,contentType:'application/json',body:JSON.stringify({error:'Fingerprint was cancelled. Try again or use your password.'})});});
 await page.locator('[data-auth-biometric="btnPasskeyLogin"]').click();await seen;
 check(!await page.locator('#auth-options-dialog').evaluate(d=>d.open),'Fingerprint starts directly without an extra method picker');
 release();await page.waitForFunction(()=>document.querySelector('#auth-options-dialog').open);
 check((await page.locator('#auth-options-title').innerText())==='Fingerprint sign-in','Failure opens fingerprint-specific help');
 check(await page.locator('#btnFaceLogin').isHidden(),'Fingerprint help does not repeat the face option');
 check(await page.locator('#btnPasskeyLogin').isVisible(),'Fingerprint failure offers a retry');
 await page.locator('[data-auth-use-password]').click();
 check(!await page.locator('#auth-options-dialog').evaluate(d=>d.open),'Password fallback closes help');
 check(await page.locator('#Email').evaluate(e=>e===document.activeElement),'Fallback focuses the empty email field');
 await page.unroute('**/Biometric/PasskeyLoginOptions');
 await page.locator('[data-auth-biometric="btnFaceLogin"]').click();
 check(!await page.locator('#auth-options-dialog').evaluate(d=>d.open),'Missing face email does not open another popup');
 check((await page.locator('.auth-validation').innerText()).includes('email address'),'Missing email guidance appears on the sign-in form');
 check(await page.locator('#Email').evaluate(e=>e===document.activeElement),'Missing email focuses the existing form input');
 await page.route('https://cdn.jsdelivr.net/**',r=>r.abort());
 await page.locator('#Email').fill('officer@example.test');await page.locator('[data-auth-biometric="btnFaceLogin"]').click();
 check(await page.locator('#auth-options-dialog').evaluate(d=>d.open),'Valid email opens face camera surface');
 check((await page.locator('#auth-options-title').innerText())==='Face recognition','Face dialog identifies the selected action');
 check(await page.locator('#btnPasskeyLogin').isHidden(),'Face surface does not repeat fingerprint selection');
 check(await page.locator('#faceEmail').inputValue()==='officer@example.test','Existing biometric handler receives the main form email');
 await page.waitForFunction(()=>document.querySelector('#bio-status').classList.contains('alert-danger'));
 for(const size of [{width:390,height:844},{width:844,height:390}]){
  await page.setViewportSize(size);
  check(await page.locator('#auth-options-dialog').evaluate(d=>{const r=d.getBoundingClientRect();return r.left>=0&&r.top>=0&&r.right<=innerWidth&&r.bottom<=innerHeight;}),'Selected-method surface fits '+size.width+'px');
 }
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:'C:/Users/user/AppData/Local/Temp/auth-direct-face.png'});
 await page.locator('[data-auth-close]').click();
 await page.waitForTimeout(100);
 check(!await page.locator('#auth-options-dialog').evaluate(d=>d.open),'Closing help does not reopen after cancellation');
 await page.unroute('https://cdn.jsdelivr.net/**');await page.emulateMedia({reducedMotion:'reduce'});await page.setViewportSize({width:1440,height:900});
 await page.locator('#Password').fill('Preview-Only!4729');await page.locator('.auth-form button[type=submit]').click();await page.waitForURL('http://localhost:5197/SourcingOfficer');
 check(true,'Password authentication and role redirect remain functional');
 return {assertions:results.length,results};
}
