const { chromium } = require('playwright');
(async () => {
  const [page, outdir, t0, t1, fps] = process.argv.slice(2);
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto('file://' + page); await p.waitForTimeout(300);
  const n = Math.round((Number(t1) - Number(t0)) * Number(fps));
  for (let i = 0; i <= n; i++) {
    const t = Number(t0) + i / Number(fps);
    await p.evaluate(tt => window.renderAt(tt), t);
    await (await p.$('#c')).screenshot({ path: `${outdir}/f${String(i).padStart(4, '0')}.png` });
  }
  if (errs.length) console.log('errors', errs);
  await b.close();
})();
