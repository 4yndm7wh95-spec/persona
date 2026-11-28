/* Render frames of one workbench demo with the pre-installed Playwright.
   Usage: node render.js <dir with index.html> <tabIndex> <t1,t2,...> <prefix> <dur> [scale]
   - <dir>: a folder holding a copy of docs/p4/workbench/p4r_workbench.html named index.html
            (put the artifact's gen/ images next to it in gen/ if the demo needs them; missing images just don't draw)
   - <tabIndex>: position of the demo in the tab row (0 = first visible tab, the All-Out Attack)
   - <dur>: the demo's dur (seconds), used to turn times into the scrub value
   Writes <dir>/<prefix>_<t>.png for every time and prints page errors (other than missing files). */
const { chromium } = require('playwright');
(async () => {
  const [dir, tab, times, prefix, dur, scale] = process.argv.slice(2);
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1100, height: 900 }, deviceScaleFactor: Number(scale || 1) });
  const errs = [];
  p.on('pageerror', e => errs.push('PAGEERROR ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !/ERR_FILE_NOT_FOUND/.test(m.text())) errs.push(m.text()); });
  await p.goto('file://' + dir + '/index.html');
  await p.waitForTimeout(1500);
  const tabs = await p.$$('#demoTabs .chip');
  await tabs[Number(tab)].click();
  await p.waitForTimeout(400);
  for (const t of times.split(',').map(Number)) {
    await p.$eval('#scrub', (el, v) => { el.value = String(v); el.dispatchEvent(new Event('input')); }, t / Number(dur));
    await p.waitForTimeout(90);
    await (await p.$('.stage')).screenshot({ path: dir + '/' + prefix + '_' + t.toFixed(2) + '.png' });
  }
  console.log('errors:', JSON.stringify(errs));
  await b.close();
})();
