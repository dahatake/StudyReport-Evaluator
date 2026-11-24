(() => {
  const doc = document.documentElement;
  const motionOn = () => doc.dataset.motion !== 'off';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];

  // header state
  const header = $('.site-header');
  const onScroll = () => header && header.classList.toggle('scrolled', scrollY > 8);
  addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  // motion toggle
  const mt = $('#motion-toggle');
  const syncMotion = () => {
    if (!mt) return;
    mt.setAttribute('aria-pressed', String(motionOn()));
    mt.setAttribute('aria-label', motionOn() ? 'アニメーションを停止' : 'アニメーションを再開');
  };
  syncMotion();
  mt && mt.addEventListener('click', () => {
    doc.dataset.motion = motionOn() ? 'off' : 'on';
    localStorage.setItem('motion', doc.dataset.motion);
    syncMotion();
    $$('.reveal').forEach(e => e.classList.add('in'));
  });

  // reveal on scroll
  const reveals = $$('.reveal');
  if ('IntersectionObserver' in window) {
    const io = new IntersectionObserver(es => es.forEach(e => {
      if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
    }), { threshold: .12, rootMargin: '0px 0px -6% 0px' });
    reveals.forEach((el, i) => { el.style.transitionDelay = `${(i % 4) * 70}ms`; io.observe(el); });
  } else reveals.forEach(e => e.classList.add('in'));

  // hero screenshot tilt
  const tilt = $('[data-tilt] img');
  if (tilt) {
    const host = tilt.closest('.hero');
    host.addEventListener('pointermove', e => {
      if (!motionOn()) return;
      const r = host.getBoundingClientRect();
      const x = (e.clientX - r.left) / r.width - .5, y = (e.clientY - r.top) / r.height - .5;
      tilt.style.transform = `rotateY(${-10 + x * 10}deg) rotateX(${4 - y * 8}deg)`;
    });
    host.addEventListener('pointerleave', () => { tilt.style.transform = ''; });
  }

  // flow steps: click/focus/scroll to switch
  const flow = $('[data-steps]');
  if (flow) {
    const steps = $$('.step', flow), shots = $$('.shot', flow);
    const set = i => {
      steps.forEach((s, k) => s.classList.toggle('is-active', k === i));
      shots.forEach((s, k) => s.classList.toggle('is-active', k === i));
    };
    steps.forEach((s, i) => {
      s.addEventListener('click', () => set(i));
      s.addEventListener('focus', () => set(i));
      s.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); set(i); } });
    });
    let auto = 0, timer;
    const play = () => { clearInterval(timer); if (motionOn()) timer = setInterval(() => { auto = (auto + 1) % steps.length; set(auto); }, 4200); };
    flow.addEventListener('pointerenter', () => clearInterval(timer));
    flow.addEventListener('pointerleave', play);
    steps.forEach((s, i) => s.addEventListener('click', () => { auto = i; }));
    new IntersectionObserver(es => es.forEach(e => e.isIntersecting ? play() : clearInterval(timer))).observe(flow);
  }

  // hero particle network (reacts to the pointer)
  const cv = $('#hero-canvas');
  if (cv && cv.getContext) {
    const ctx = cv.getContext('2d');
    let w, h, pts = [], mouse = { x: -999, y: -999 }, raf, visible = true;
    const resize = () => {
      const d = Math.min(devicePixelRatio || 1, 2);
      w = cv.clientWidth; h = cv.clientHeight;
      cv.width = w * d; cv.height = h * d; ctx.setTransform(d, 0, 0, d, 0, 0);
      const n = Math.round(Math.min(90, w * h / 16000));
      pts = Array.from({ length: n }, () => ({ x: Math.random() * w, y: Math.random() * h, vx: (Math.random() - .5) * .35, vy: (Math.random() - .5) * .35 }));
    };
    const draw = () => {
      ctx.clearRect(0, 0, w, h);
      for (const p of pts) {
        if (motionOn()) {
          p.x += p.vx; p.y += p.vy;
          const dx = p.x - mouse.x, dy = p.y - mouse.y, dd = dx * dx + dy * dy;
          if (dd < 14000) { p.x += dx / 60; p.y += dy / 60; }
          if (p.x < 0 || p.x > w) p.vx *= -1;
          if (p.y < 0 || p.y > h) p.vy *= -1;
        }
        ctx.fillStyle = 'rgba(160,190,255,.8)';
        ctx.beginPath(); ctx.arc(p.x, p.y, 1.6, 0, 6.283); ctx.fill();
      }
      for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) {
        const a = pts[i], b = pts[j], d = Math.hypot(a.x - b.x, a.y - b.y);
        if (d < 130) { ctx.strokeStyle = `rgba(130,170,255,${.22 * (1 - d / 130)})`; ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke(); }
      }
      if (visible && motionOn()) raf = requestAnimationFrame(draw);
    };
    const start = () => { cancelAnimationFrame(raf); draw(); };
    resize(); start();
    addEventListener('resize', () => { resize(); start(); });
    cv.parentElement.addEventListener('pointermove', e => { const r = cv.getBoundingClientRect(); mouse = { x: e.clientX - r.left, y: e.clientY - r.top }; });
    new IntersectionObserver(es => { visible = es[0].isIntersecting; if (visible) start(); }).observe(cv);
    mt && mt.addEventListener('click', start);
  }

  // on-page TOC highlight
  const tocLinks = $$('.doc-toc a');
  if (tocLinks.length && 'IntersectionObserver' in window) {
    const map = new Map(tocLinks.map(a => [decodeURIComponent(a.hash.slice(1)), a]));
    const io = new IntersectionObserver(es => es.forEach(e => {
      if (e.isIntersecting) { tocLinks.forEach(a => a.classList.remove('active')); const a = map.get(e.target.id); a && a.classList.add('active'); }
    }), { rootMargin: '-90px 0px -70% 0px' });
    map.forEach((_, id) => { const el = document.getElementById(id); el && io.observe(el); });
  }

  // site search
  const dlg = $('#search'), input = $('#search-input'), list = $('#search-results'), openBtn = $('#search-open');
  let index = null;
  const base = (() => { const s = $('script[src$="/assets/app.js"]'); return s ? s.getAttribute('src').replace(/assets\/app\.js$/, '') : '/'; })();
  const load = async () => { if (!index) { try { index = await (await fetch(base + 'search-index.json')).json(); } catch { index = []; } } };
  const esc = s => s.replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const render = q => {
    const terms = q.toLowerCase().split(/\s+/).filter(Boolean);
    if (!terms.length) { list.innerHTML = ''; return; }
    const hits = index.map(d => {
      const t = d.t.toLowerCase(), x = d.x.toLowerCase();
      let score = 0;
      for (const w of terms) { if (t.includes(w)) score += 10; else if (x.includes(w)) score += 1; else return null; }
      return { d, score };
    }).filter(Boolean).sort((a, b) => b.score - a.score).slice(0, 8);
    list.innerHTML = hits.length
      ? hits.map(({ d }) => `<li><a href="${esc(d.u)}">${esc(d.t)}<small>${esc(d.s)}</small></a></li>`).join('')
      : '<li class="muted" style="padding:10px 12px">見つかりませんでした</li>';
  };
  const open = async () => { dlg.hidden = false; input.value = ''; list.innerHTML = ''; input.focus(); await load(); };
  const close = () => { dlg.hidden = true; openBtn && openBtn.focus(); };
  if (dlg) {
    openBtn.addEventListener('click', open);
    dlg.addEventListener('click', e => { if (e.target === dlg) close(); });
    input.addEventListener('input', () => index && render(input.value));
    addEventListener('keydown', e => {
      if (e.key === 'Escape' && !dlg.hidden) close();
      else if (e.key === '/' && dlg.hidden && !/input|textarea/i.test(document.activeElement.tagName)) { e.preventDefault(); open(); }
    });
  }
})();
