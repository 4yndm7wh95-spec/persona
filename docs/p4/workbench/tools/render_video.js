/* Render one workbench demo to numbered PNG frames for a video.
   Usage: node render_video.js <dir with index.html> <tabIndex> <dur> <fps> <outDir> [t0] [t1]
   then: ffmpeg -y -framerate <fps> -i <outDir>/f%04d.png -vf "scale=1280:720:flags=lanczos,format=yuv420p" -c:v libx264 -crf 18 out.mp4
   (half speed: same frames with -framerate <fps/2>). The stage is captured at about 1280 px wide. */
const { chromium } = require('playwright');
(async () => {
  const [dir, tab, dur, fps, out, a, z] = process.argv.slice(2);
  const t0 = Number(a || 0), t1 = Number(z || dur), n = Math.round((t1 - t0) * Number(fps));
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1100, height: 900 }, deviceScaleFactor: 1.334 });
  const errs = [];
  p.on('pageerror', e => errs.push('PAGEERROR ' + e.message));
  await p.goto('file://' + dir + '/index.html');
  await p.waitForTimeout(1500);
  await (await p.$$('#demoTabs .chip'))[Number(tab)].click();
  await p.waitForTimeout(400);
  const stage = await p.$('.stage');
  await p.$eval('#scrub', el => { el.step = 'any'; });   /* the slider snaps to 0.005 steps otherwise: frames would repeat */
  for (let i = 0; i <= n; i++) {
    const t = t0 + i / Number(fps);
    await p.$eval('#scrub', (el, v) => { el.value = String(v); el.dispatchEvent(new Event('input')); }, Math.min(1, t / Number(dur)));
    await stage.screenshot({ path: out + '/f' + String(i).padStart(4, '0') + '.png' });
  }
  console.log('frames', n + 1, 'errors', JSON.stringify(errs));
  await b.close();
})();
