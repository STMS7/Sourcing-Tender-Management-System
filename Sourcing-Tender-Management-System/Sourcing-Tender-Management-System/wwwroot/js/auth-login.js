(() => {
    'use strict';
    const body = document.body;
    const email = document.getElementById('Email');
    const password = document.getElementById('Password');
    const toggle = document.querySelector('[data-password-toggle]');
    const story = document.querySelector('.auth-story');
    const dialog = document.getElementById('auth-options-dialog');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const motionControl = document.querySelector('[data-auth-motion]');
    let paused = false, frame = null, pointer = { x: 0, y: 0 }, lastTrigger;
    let signingIn = false, celebrating = false;

    // ── Olive tree ────────────────────────────────────────────────────────
    // One life cycle, driven by the sign-in itself: seed → sprout → trunk →
    // limbs → leaves → blossom → fruit. The tree is drawn from a fixed seed so
    // it is the same tree on every visit. It is decoration only: it reads the
    // length of the fields, never their content, and stores nothing.
    const tree = (() => {
        const NS = 'http://www.w3.org/2000/svg';
        const G = 588, CX = 300;                       // ground line and trunk base in the 600×640 viewBox
        const clamp01 = v => Math.min(1, Math.max(0, v));
        const easeOut = t => 1 - Math.pow(1 - t, 3);
        const easeInOut = t => t < .5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
        const f = n => Math.round(n * 10) / 10;
        const LEAF = 'M0 0C4.2-6 4.6-17 0-27C-4.6-17-4.2-6 0 0Z';
        const STAGES = [[.06, 'seed'], [.16, 'sprout'], [.34, 'trunk'], [.56, 'limbs'], [.8, 'leaves'], [.9, 'blossom'], [2, 'fruit']];

        function random(seed) {
            return () => {
                seed = seed + 0x6D2B79F5 | 0;
                let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
                t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
                return ((t ^ t >>> 14) >>> 0) / 4294967296;
            };
        }
        function bezier(p0, p1, p2, p3) {
            return {
                d: `M${f(p0[0])} ${f(p0[1])}C${f(p1[0])} ${f(p1[1])} ${f(p2[0])} ${f(p2[1])} ${f(p3[0])} ${f(p3[1])}`,
                at(t) {
                    const u = 1 - t, a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, e = t * t * t;
                    return [a * p0[0] + b * p1[0] + c * p2[0] + e * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + e * p3[1]];
                },
                // Heading in radians, 0 = straight up, positive = leaning right.
                heading(t) {
                    const u = 1 - t;
                    const dx = 3 * u * u * (p1[0] - p0[0]) + 6 * u * t * (p2[0] - p1[0]) + 3 * t * t * (p3[0] - p2[0]);
                    const dy = 3 * u * u * (p1[1] - p0[1]) + 6 * u * t * (p2[1] - p1[1]) + 3 * t * t * (p3[1] - p2[1]);
                    return Math.atan2(dx, -dy);
                }
            };
        }
        // A limb leaves (x,y) on a heading and arches upward before its tip settles.
        function limb(x, y, heading, length, arch) {
            const dx = Math.sin(heading), dy = -Math.cos(heading), side = heading < 0 ? 1 : -1;
            const nx = Math.cos(heading) * side, ny = Math.sin(heading) * side, k = arch * length;
            return bezier([x, y],
                [x + dx * length * .33 + nx * k, y + dy * length * .33 + ny * k],
                [x + dx * length * .7 + nx * k * .9, y + dy * length * .7 + ny * k * .9],
                [x + dx * length, y + dy * length]);
        }

        function build(svg) {
            const mini = !!svg.closest('.auth-mini-tree');
            const rnd = random(mini ? 23 : 7);
            const parts = [];
            const make = (name, attrs, parent) => {
                const node = document.createElementNS(NS, name);
                for (const key in attrs) node.setAttribute(key, attrs[key]);
                (parent || svg).append(node);
                return node;
            };
            svg.replaceChildren();
            svg.setAttribute('viewBox', '0 0 600 640');
            svg.classList.toggle('auth-tree--mini', mini);

            const shadow = make('ellipse', { class: 't-shadow', cx: CX, cy: G + 9, rx: 40, ry: 7 });
            const roots = make('g', { class: 't-roots' });
            make('path', { class: 't-ground', d: `M120 ${G + 4}C220 ${G - 5} 380 ${G - 5} 480 ${G + 4}` });
            const wood = make('g', { class: 't-woods' });
            const sap = make('g', { class: 't-saps' });
            const crown = make('g', { class: 't-crown' });
            const sprout = make('g', { class: 't-sprout' });

            // raw: true marks a window measured in growth time, mapped to progress once the tree is known.
            const draw = (curve, width, cls, s, e, parent, raw) => {
                const el = make('path', { class: `t-wood ${cls}`, d: curve.d, pathLength: 1, style: `--w:${f(width)}` }, parent);
                parts.push({ el, kind: 'draw', s, e, raw });
                return el;
            };
            const leaf = (x, y, heading, scale, s, e, parent, cls) => {
                const holder = make('g', { transform: `translate(${f(x)} ${f(y)}) rotate(${f(heading * 57.2958)}) scale(${f(scale)})` }, parent);
                parts.push({ el: make('path', { class: `t-leaf ${cls}`, d: LEAF }, holder), kind: 'leaf', s, e, raw: true });
            };
            const tint = () => { const r = rnd(); return r < .5 ? 't-leaf-a' : r < .86 ? 't-leaf-b' : 't-leaf-c'; };

            // Seed, first roots and the shoot with its two seed-leaves.
            [[-24, 34], [-9, 42], [10, 40], [27, 30]].forEach(([dx, dy], i) =>
                draw(bezier([CX, G + 2], [CX + dx * .2, G + dy * .4], [CX + dx * .9, G + dy * .6], [CX + dx, G + dy]), 2.4, 't-root', .06 + i * .012, .17 + i * .012, roots));
            const halves = [make('path', { class: 't-seed', d: `M${CX} ${G + 6}C${CX - 11} ${G + 3} ${CX - 11} ${G - 13} ${CX} ${G - 17}Z` }, sprout),
                make('path', { class: 't-seed t-seed--b', d: `M${CX} ${G + 6}C${CX + 11} ${G + 3} ${CX + 11} ${G - 13} ${CX} ${G - 17}Z` }, sprout)];
            halves.forEach((el, i) => parts.push({ el, kind: 'seed', s: .05, e: .12, turn: i ? 30 : -30 }));
            draw(bezier([CX, G - 4], [CX - 5, G - 20], [CX + 6, G - 34], [CX, G - 50]), 4, 't-shoot', .07, .15, sprout);
            [-.95, .95].forEach(side => {
                const holder = make('g', { transform: `translate(${CX} ${G - 49}) rotate(${f(side * 57.2958)}) scale(1.25)` }, sprout);
                parts.push({ el: make('path', { class: 't-leaf t-leaf-c', d: LEAF }, holder), kind: 'leaf', s: .12, e: .2 });
            });
            parts.push({ el: sprout, kind: 'fade', s: .17, e: .25 }, { el: roots, kind: 'fade', s: .24, e: .36 });

            // Trunk: three strands that cross, the way an old olive trunk twists.
            const trunk = [
                { b: bezier([CX + 1, G + 2], [CX + 15, G - 54], [CX - 19, G - 100], [CX - 6, G - 180]), w: 27, s: 0, dur: 1, cls: 't-bark-a' },
                { b: bezier([CX - 8, G + 2], [CX - 21, G - 50], [CX + 15, G - 110], [CX + 1, G - 187]), w: 20, s: .07, dur: 1, cls: 't-bark-b' },
                { b: bezier([CX + 10, G + 2], [CX + 3, G - 32], [CX + 19, G - 72], [CX + 8, G - 160]), w: 13, s: .15, dur: .85, cls: 't-bark-c' }
            ];
            [[-5, -40, 13], [9, 44, 13], [2, 6, 10]].forEach(([from, to, w], i) =>
                draw(bezier([CX + from, G - 16], [CX + from + to * .3, G - 8], [CX + to * .7, G + 1], [CX + to, G + 7]), w, 't-bark-b', .02 * i, .3 + .02 * i, wood, true));
            trunk.forEach(strand => {
                draw(strand.b, strand.w, strand.cls, strand.s, strand.s + strand.dur, wood, true);
                if (!mini) make('path', { class: 't-sap', d: strand.b.d, pathLength: 1 }, sap);
            });
            if (!mini) [[-6, 7, .2], [5, -6, .35]].forEach(([x, bend, s]) =>
                draw(bezier([CX + x, G - 14], [CX + x + bend, G - 60], [CX + x - bend, G - 104], [CX + x + bend * .4, G - 150]), 1.6, 't-grain', s, s + .9, wood, true));

            // Limbs → boughs → twigs. Each child starts when its parent has grown past the fork.
            const [A, B, C] = trunk;
            const limbs = [[A, .8, -64, 168, .16, 16], [A, 1, -25, 176, .08, 19], [B, 1, 19, 174, .1, 18],
                [B, .8, 57, 166, .16, 15], [C, 1, 84, 126, .2, 10], [A, .6, -88, 118, .2, 9]];
            const cap = heading => Math.max(-2.05, Math.min(2.05, heading));
            let fruit = 0;
            limbs.forEach(([from, at, degrees, length, arch, width], index) => {
                const [x, y] = from.b.at(at);
                const main = { b: limb(x, y, degrees / 57.2958, length, arch), s: from.s + at * from.dur * .9, dur: .85 };
                draw(main.b, width, index % 2 ? 't-bark-b' : 't-bark-a', main.s, main.s + main.dur, wood, true);
                if (!mini) make('path', { class: 't-sap t-sap--limb', d: main.b.d, pathLength: 1 }, sap);
                (mini ? [.62, 1] : [.42, .7, 1]).forEach((t2, i2) => {
                    const [x2, y2] = main.b.at(t2), turn2 = (i2 + index) % 2 ? 1 : -1;
                    const head2 = cap(main.b.heading(t2) + (t2 === 1 ? (rnd() - .5) * .4 : turn2 * (.5 + rnd() * .35)));
                    const length2 = length * (.52 + rnd() * .14), width2 = Math.max(2.6, width * .55);
                    const bough = { b: limb(x2, y2, head2, length2, .1 + rnd() * .1), s: main.s + t2 * main.dur * .9, dur: .65 };
                    if (mini) {
                        draw(bough.b, width2, 't-bark-b', bough.s, bough.s + bough.dur, wood, true);
                        [.3, .55, .8, 1].forEach(t => {
                            const [lx, ly] = bough.b.at(t), at0 = bough.s + t * bough.dur;
                            (t === 1 ? [0] : [-1, 1]).forEach(side =>
                                leaf(lx, ly, bough.b.heading(t) + side * (.6 + rnd() * .35), 2.1 + rnd() * .7, at0, at0, crown, tint()));
                        });
                        return;
                    }
                    draw(bough.b, width2, 't-bark-b', bough.s, bough.s + bough.dur, wood, true);
                    [.5, .78, 1].forEach((t3, i3) => {
                        const [x3, y3] = bough.b.at(t3), turn3 = (i3 + i2) % 2 ? 1 : -1;
                        const head3 = cap(bough.b.heading(t3) + (t3 === 1 ? (rnd() - .5) * .5 : turn3 * (.55 + rnd() * .4)));
                        const twig = { b: limb(x3, y3, head3, length2 * (.5 + rnd() * .16), .08 + rnd() * .12), s: bough.s + t3 * bough.dur * .9, dur: .45 };
                        const cluster = make('g', { class: 't-cluster', style: `transform-origin:${f(x3)}px ${f(y3)}px;--sway:${f(3.4 + rnd() * 2.8)}s;--lag:${f(-rnd() * 5)}s` }, crown);
                        draw(twig.b, Math.max(1.6, width2 * .5), 't-bark-c', twig.s, twig.s + twig.dur, cluster, true);
                        [.2, .4, .6, .8, 1].forEach(t => {
                            const [lx, ly] = twig.b.at(t), at0 = twig.s + t * twig.dur + rnd() * .12;
                            (t === 1 ? [0] : [-1, 1]).forEach(side =>
                                leaf(lx, ly, twig.b.heading(t) + side * (.6 + rnd() * .35), .78 + rnd() * .5, at0, at0, cluster, tint()));
                        });
                        // Blossom at the tip, then an olive hanging where the blossom was.
                        const [tx, ty] = twig.b.at(1), wait = rnd();
                        if (rnd() < .62) {
                            const bloom = make('g', { class: 't-blossom' }, make('g', { transform: `translate(${f(tx)} ${f(ty)})` }, cluster));
                            [[0, -3.4], [3.2, 1.6], [-3.2, 1.6]].forEach(([px, py]) => make('circle', { cx: px, cy: py, r: 3 }, bloom));
                            make('circle', { class: 't-blossom-eye', r: 1.5 }, bloom);
                            parts.push({ el: bloom, kind: 'pop', s: .8 + wait * .05, e: .85 + wait * .05, out: [.92, .98] });
                            if (rnd() < .6) {
                                const holder = make('g', { transform: `translate(${f(tx + (rnd() - .5) * 8)} ${f(ty + 4)})` }, cluster);
                                const olive = make('g', { class: 't-olive', style: `--ripen:${Math.round(wait * 420)}ms;--fall:${Math.round(rnd() * 260)}ms` }, holder);
                                make('path', { class: 't-olive-stem', d: 'M0 0V5' }, olive);
                                make('ellipse', { class: 't-olive-body', cy: 12, rx: 5.6, ry: 7.6 }, olive);
                                make('ellipse', { class: 't-olive-light', cx: -1.8, cy: 9.4, rx: 1.4, ry: 2.4 }, olive);
                                parts.push({ el: olive, kind: 'pop', s: .9 + wait * .05, e: .95 + wait * .05, fruit: fruit++ });
                            }
                        }
                    });
                });
            });

            // Growth time → progress: wood fills .16–.66, each leaf opens just after its twig reaches it.
            const span = Math.max(...parts.filter(p => p.raw && p.kind === 'draw').map(p => p.e));
            for (const part of parts) {
                if (!part.raw) continue;
                part.s = .16 + part.s / span * .5;
                part.e = part.kind === 'leaf' ? part.s + .1 : .16 + part.e / span * .5;
                if (part.kind === 'leaf') part.s += .012;
            }
            for (const part of parts) part.t = -1;
            return { svg, mini, parts, shadow };
        }

        function paint(model, p) {
            for (const part of model.parts) {
                const t = clamp01((p - part.s) / (part.e - part.s));
                const fade = part.out ? 1 - clamp01((p - part.out[0]) / (part.out[1] - part.out[0])) : 1;
                const key = t + fade * 2;
                if (key === part.t) continue;
                part.t = key;
                const style = part.el.style, k = easeOut(t);
                if (part.kind === 'draw') {
                    style.visibility = t > 0 ? 'visible' : 'hidden';
                    style.strokeDashoffset = String(1 - k);
                } else if (part.kind === 'leaf') {
                    style.visibility = t > 0 ? 'visible' : 'hidden';
                    style.opacity = String(Math.min(1, t * 3));
                    style.transform = `rotate(${f((1 - k) * -38)}deg) scale(${f(.12 + k * .88)},${f(.3 + k * .7)})`;
                } else if (part.kind === 'pop') {
                    style.visibility = t > 0 && fade > 0 ? 'visible' : 'hidden';
                    style.opacity = String(Math.min(1, t * 2.5) * fade);
                    style.transform = `scale(${f(k * (.6 + fade * .4))})`;
                } else if (part.kind === 'seed') {
                    style.transform = `rotate(${f(part.turn * k)}deg)`;
                } else if (part.kind === 'fade') {
                    style.opacity = String(1 - t);
                    style.visibility = t < 1 ? 'visible' : 'hidden';
                }
            }
            // The camera starts close on the seed and pulls back as the tree claims the frame.
            const zoom = 1 + (model.mini ? 1.5 : 1.5) * (1 - easeInOut(clamp01(p / .36)));
            model.svg.style.setProperty('--girth', String(f((.3 + .7 * easeOut(clamp01((p - .16) / .42))) * 100) / 100));
            model.svg.style.transform = `scale(${f(zoom * 100) / 100})`;
            model.shadow.setAttribute('rx', f(34 + clamp01((p - .16) / .6) * 150));
            model.svg.classList.toggle('is-alive', p > .56);
        }

        let models = [...document.querySelectorAll('[data-auth-tree]')].map(build);
        // Where WebGL2 is available the story panel shows the tree as a lit 3D mesh
        // (auth-tree3d.js) and the drawn tree stays only in the small mobile header.
        let scene = null;
        function enter3D() {
            if (scene || !story || typeof window.ztTree3D !== 'function') return;
            scene = window.ztTree3D(story);
            if (!scene) return;
            models = models.filter(model => {
                if (model.mini) return true;
                model.svg.replaceChildren();
                return false;
            });
            scene.quiet(quiet);
            scene.progress(progress);
        }
        let progress = 0, target = .05, frame = 0, last = 0, tween = null, quiet = false, stage = '';

        function render() {
            for (const model of models) paint(model, progress);
            scene?.progress(progress);
            const name = STAGES.find(([limit]) => progress < limit)[1];
            if (name !== stage) { stage = name; body.dataset.treeStage = name; }
            const step = String(Math.round(progress * 10) / 10);
            if (body.style.getPropertyValue('--auth-tree-growth') !== step) {
                body.style.setProperty('--auth-tree-growth', step);
                story?.style.setProperty('--auth-growth', step);
            }
        }
        function tick(now) {
            frame = 0;
            const dt = Math.min(64, Math.max(1, now - last));
            last = now;
            if (tween) {
                const k = clamp01((now - tween.from) / tween.ms);
                progress = tween.p0 + (tween.p1 - tween.p0) * easeInOut(k);
                if (k >= 1) { const done = tween.done; tween = null; target = progress; done?.(); }
            } else {
                progress += (target - progress) * (1 - Math.exp(-dt / 430));
                if (Math.abs(target - progress) < .0008) progress = target;
            }
            render();
            if (tween || progress !== target) run();
        }
        function run() { if (!frame) { last = performance.now(); frame = requestAnimationFrame(tick); } }
        function settle() {
            if (tween) { const done = tween.done; progress = tween.p1; tween = null; done?.(); }
            progress = target;
            render();
        }
        function mark(name, on) { for (const model of models) model.svg.classList.toggle(name, on); }

        enter3D();
        document.addEventListener('zt:tree3d-ready', enter3D);
        render();
        return {
            get progress() { return progress; },
            look(x, y) { scene?.look(x, y); },
            // Raising or lowering the target grows or recedes the tree toward it.
            aim(value) {
                if (tween) return;
                target = clamp01(value);
                if (quiet) settle(); else run();
            },
            still(value) { quiet = value; mark('is-still', value); scene?.quiet(value); if (value) settle(); },
            waiting(value) { mark('is-waiting', value && !quiet); scene?.waiting(value && !quiet); },
            // A refused sign-in: the crown shivers, nothing else is lost.
            shiver() {
                if (quiet) return;
                scene?.shiver();
                mark('is-shiver', false);
                void models[0]?.svg.getBoundingClientRect();
                mark('is-shiver', true);
                setTimeout(() => mark('is-shiver', false), 700);
            },
            // Confirmed sign-in: finish the tree, ripen the fruit, let a few olives fall.
            // Always resolves, and quickly, so it can never hold the workspace back.
            harvest() {
                return new Promise(resolve => {
                    let settled = false;
                    const finish = () => { if (!settled) { settled = true; resolve(); } };
                    mark('is-waiting', false);
                    if (quiet || (!models.length && !scene)) { target = 1; settle(); mark('is-ripe', true); scene?.ripen(); finish(); return; }
                    setTimeout(finish, 2600);
                    tween = {
                        p0: progress, p1: 1, from: performance.now(), ms: 520 + (1 - progress) * 900,
                        done() {
                            mark('is-ripe', true);
                            scene?.ripen();
                            for (const model of models)
                                for (const part of model.parts)
                                    if (part.fruit !== undefined && part.fruit % 3 === 0) part.el.classList.add('is-falling');
                            setTimeout(finish, 760);
                        }
                    };
                    run();
                });
            }
        };
    })();

    // How far the sign-in has come, read only from field lengths and validity.
    let touched = false;
    function treeTarget() {
        const name = email?.value.trim() ?? '', secret = password?.value.length ?? 0;
        let value = touched ? .11 : .05;
        if (name) value = Math.max(value, .16 + Math.min(name.length, 14) / 14 * .2);
        if (/^\S+@\S+\.\S+$/.test(name)) value = Math.max(value, .42);
        if (secret) value = Math.max(value, .46 + Math.min(secret, 10) / 10 * .34);
        if (name && secret >= 6 && email.validity.valid) value = Math.max(value, .88);
        return value;
    }
    function syncTree() { if (!signingIn && !celebrating) tree.aim(treeTarget()); }
    try { paused = localStorage.getItem('zt.login.motion') === 'paused'; } catch { }
    function syncMotion() {
        const quiet = paused || reduced.matches;
        body.classList.toggle('auth-motion-paused', quiet);
        motionControl?.setAttribute('aria-pressed', String(quiet));
        motionControl?.setAttribute('aria-label', quiet ? 'Enable decorative motion' : 'Pause decorative motion');
        if (motionControl) motionControl.innerHTML = quiet ? 'Motion paused <span aria-hidden="true">▷</span>' : 'Pause motion <span aria-hidden="true">Ⅱ</span>';
        tree.still(quiet);
    }
    syncMotion();
    motionControl?.addEventListener('click', () => {
        paused = !paused;
        try { localStorage.setItem('zt.login.motion', paused ? 'paused' : 'enabled'); } catch { }
        syncMotion();
    });
    reduced.addEventListener('change', syncMotion);
    function paint() {
        frame = null;
        if (!story || paused || reduced.matches || !story.getClientRects().length) return;
        const focus = body.dataset.authFocus;
        story.style.setProperty('--auth-branch-x', `${pointer.x * 17 + (focus === 'email' ? -12 : 0)}px`);
        story.style.setProperty('--auth-branch-y', `${pointer.y * 12}px`);
        story.style.setProperty('--auth-branch-tilt', `${pointer.x * 2 + (focus === 'email' ? -1 : 0)}deg`);
    }
    function schedule() { if (frame === null) frame = requestAnimationFrame(paint); }
    document.addEventListener('pointermove', event => {
        if (event.pointerType !== 'mouse' || paused || reduced.matches) return;
        pointer = { x: Math.max(-1, Math.min(1, event.clientX / innerWidth * 2 - 1)), y: Math.max(-1, Math.min(1, event.clientY / innerHeight * 2 - 1)) };
        tree.look(pointer.x, pointer.y);
        schedule();
    }, { passive: true });
    document.documentElement.addEventListener('pointerleave', () => { pointer = { x: 0, y: 0 }; schedule(); });
    document.addEventListener('focusin', event => {
        body.dataset.authFocus = event.target === email ? 'email' : event.target === password || event.target.closest?.('.auth-password') ? 'password' : 'idle';
        if (event.target === email || event.target === password) touched = true;
        syncTree();
        schedule();
    });
    document.addEventListener('input', syncTree);
    document.addEventListener('change', syncTree);
    // Browsers fill saved credentials after load without an input event.
    syncTree();
    [350, 1200].forEach(delay => setTimeout(syncTree, delay));
    toggle?.addEventListener('click', () => {
        const visible = password.type === 'password';
        password.type = visible ? 'text' : 'password';
        toggle.setAttribute('aria-pressed', String(visible));
        toggle.setAttribute('aria-label', visible ? 'Hide password' : 'Show password');
    });
    function openOptions(trigger) {
        if (!dialog) return;
        lastTrigger = trigger;
        if (!dialog.open) dialog.showModal();
    }
    document.querySelectorAll('[data-auth-biometric]').forEach(button => button.addEventListener('click', () => {
        openOptions(button);
        const method = document.getElementById(button.dataset.authBiometric);
        if (method && !method.disabled) method.click();
    }));
    document.querySelectorAll('[data-auth-options]').forEach(button => button.addEventListener('click', () => openOptions(button)));
    document.querySelector('[data-auth-close]')?.addEventListener('click', () => dialog.close());
    dialog?.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
    dialog?.addEventListener('close', () => {
        const video = document.getElementById('faceVideo');
        video?.srcObject?.getTracks().forEach(track => track.stop());
        document.getElementById('facePanel')?.classList.add('d-none');
        lastTrigger?.focus({ preventScroll: true });
    });
    // "Skip" in the biometric module: back to the password form, cursor where typing continues.
    document.addEventListener('stms:biometric-skip', () => {
        lastTrigger = email?.value.trim() ? password : email;
        if (dialog?.open) dialog.close(); else lastTrigger?.focus({ preventScroll: true });
    });
    // Existing biometric code can insert a remembered-user card. Keep that extra
    // content in the options dialog so it cannot move the password form.
    const area = document.querySelector('.auth-form-area');
    const moveWelcome = () => {
        const card = area?.querySelector('#welcomeBack');
        if (card && dialog) { card.classList.remove('col-md-5'); dialog.insertBefore(card, dialog.querySelector('#bio-root')); }
    };
    if (area) new MutationObserver(moveWelcome).observe(area, { childList: true });
    moveWelcome();
    const form = document.querySelector('.auth-form');
    const submit = form?.querySelector('.auth-submit');
    const defaultSubmit = submit?.innerHTML;
    const summary = form?.querySelector('.auth-validation');
    function showErrors(errors) {
        if (!summary) return;
        summary.replaceChildren();
        const list = document.createElement('ul');
        for (const message of errors) {
            const item = document.createElement('li');
            item.textContent = message;
            list.append(item);
        }
        summary.append(list);
        summary.classList.remove('validation-summary-valid');
        summary.classList.add('validation-summary-errors');
        tree.shiver();
    }
    async function completeSignIn(destination) {
        if (typeof destination !== 'string' || !destination.trim()) throw new Error('Missing workspace destination.');
        const target = new URL(destination, location.origin);
        if (target.origin !== location.origin || !['http:', 'https:'].includes(target.protocol)) throw new Error('Invalid workspace destination.');
        if (celebrating) return;
        celebrating = true;
        // The tree finishes its harvest in about a second, then the workspace opens.
        // harvest() always resolves (instantly when motion is paused or reduced),
        // and finally guarantees navigation even if the decoration throws. The
        // destination page consumes a one-use flag and lets the olives land.
        try {
            window.stmsQueueLoginCelebration?.();
            if (submit) { submit.disabled = true; submit.textContent = 'Opening workspace…'; }
            await tree.harvest();
        } finally { location.replace(target.href); }
    }
    // Called only by the existing biometric handler after its server confirms success.
    window.stmsLoginSuccess = completeSignIn;
    form?.addEventListener('submit', async event => {
        if (!window.fetch || !window.FormData) return; // Original POST/redirect works without enhancement.
        event.preventDefault();
        if (signingIn || celebrating) return;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        if (window.jQuery?.validator && !window.jQuery(form).valid()) return;
        signingIn = true;
        submit.style.height = `${submit.getBoundingClientRect().height}px`;
        submit.disabled = true;
        submit.textContent = 'Signing in…';
        form.setAttribute('aria-busy', 'true');
        // While the server decides, sap climbs the trunk and the first fruit sets.
        tree.aim(Math.max(tree.progress, .93));
        tree.waiting(true);
        const controller = new AbortController();
        const deadline = setTimeout(() => controller.abort(), 20000);
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin', signal: controller.signal,
                headers: { 'X-Login-Animation': '1', 'Accept': 'application/json' }
            });
            if (response.redirected) {
                const target = new URL(response.url);
                if (target.origin === location.origin && !/\/Auth\/Login\/?$/i.test(target.pathname)) {
                    await completeSignIn(target.href);
                    return;
                }
            }
            if (!response.headers.get('content-type')?.includes('application/json')) {
                showErrors([response.status === 400 ? 'Your session may have expired. Refresh the page and try again.' : 'Could not confirm sign-in. Please try again.']);
                return;
            }
            const result = await response.json();
            if (!response.ok || result.succeeded !== true) {
                showErrors(Array.isArray(result.errors) && result.errors.length ? result.errors : ['Check your sign-in details and try again.']);
                return;
            }
            await completeSignIn(result.redirect);
        } catch (error) {
            celebrating = false;
            showErrors([error.name === 'AbortError' ? 'Sign-in took too long. Please try again.' : 'Could not complete sign-in. Check your connection and try again.']);
        } finally {
            clearTimeout(deadline);
            if (!celebrating) {
                signingIn = false;
                tree.waiting(false);
                syncTree();
                submit.disabled = false;
                submit.innerHTML = defaultSubmit;
                submit.style.height = '';
                form.removeAttribute('aria-busy');
            }
        }
    });
})();
