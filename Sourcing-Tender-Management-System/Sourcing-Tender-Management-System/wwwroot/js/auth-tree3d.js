// Zaytouna sign-in: a real-time 3D olive tree (WebGL2, no libraries).
//
// The tree is a lit mesh, not a picture: a fluted trunk, limbs, boughs and twigs built as
// tapered tubes, about nine hundred leaves in opposite pairs, blossom and fruit. Every
// vertex knows when in the tree's life it appears, so one number, the growth progress
// supplied by auth-login.js, grows the whole tree in the vertex shader.
//
// Decoration only. If WebGL2 is missing, ztTree3D returns null and the page keeps its
// flat SVG tree. Nothing here reads the form or touches sign-in.
(() => {
    'use strict';
    const TAU = Math.PI * 2;
    const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
    const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    const mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
    const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const unit = a => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
    const clamp01 = v => Math.min(1, Math.max(0, v));
    const easeInOut = t => t < .5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;

    function random(seed) {
        return () => {
            seed = seed + 0x6D2B79F5 | 0;
            let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
            t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
            return ((t ^ t >>> 14) >>> 0) / 4294967296;
        };
    }

    // ── The tree, grown once from a fixed seed ─────────────────────────────────────────
    function growTree() {
        const rnd = random(11);
        const wood = [], woodIndex = [], leaves = [], blobs = [];
        const perpendicular = d => unit(cross(d, Math.abs(d[1]) < .9 ? [0, 1, 0] : [1, 0, 0]));

        // A tapered tube along a path. Each ring stores its centre, radial direction, radius and birth time.
        function tube(path, sides) {
            const first = wood.length / 10;
            let normal = null;
            path.forEach((point, i) => {
                const tangent = unit(sub(path[Math.min(i + 1, path.length - 1)].p, path[Math.max(i - 1, 0)].p));
                normal = normal ? unit(sub(normal, mul(tangent, dot(normal, tangent)))) : perpendicular(tangent);
                const side = cross(tangent, normal);
                for (let j = 0; j < sides; j++) {
                    const a = j / sides * TAU;
                    const radial = add(mul(normal, Math.cos(a)), mul(side, Math.sin(a)));
                    // An old olive trunk is fluted and twisted, never a clean cylinder.
                    const flute = point.flute ? 1 + .17 * Math.sin(3 * a + point.v * 4.5) + .07 * Math.sin(7 * a - point.v * 8) : 1;
                    wood.push(...point.p, ...radial, point.r * flute, point.t, j / sides, point.v);
                }
                if (i === path.length - 1) return;
                for (let j = 0; j < sides; j++) {
                    const a = first + i * sides + j, b = first + i * sides + (j + 1) % sides;
                    woodIndex.push(a, a + sides, b, b, a + sides, b + sides);
                }
            });
        }
        function branch(start, heading, length, radius, level, born, lasts) {
            const steps = [14, 9, 7, 5][level], lift = [0, .11, .02, -.05][level], wander = [.2, .3, .34, .4][level];
            const path = [];
            let p = start, d = unit(heading);
            for (let i = 0; i <= steps; i++) {
                const u = i / steps;
                let r = radius * (1 - u * [.32, .55, .6, .8][level]), t = born + lasts * u;
                if (level === 0) {
                    r *= 1 + .95 * Math.exp(-u * 9);                              // root flare
                    t = u < .15 ? .07 + u / .15 * .09 : .16 + (u - .15) / .85 * .18; // first the shoot, then the trunk
                }
                path.push({ p, d, r, t, v: u * length, flute: level === 0 });
                d = unit(add(d, [(rnd() - .5) * wander, lift + (rnd() - .5) * wander * .6, (rnd() - .5) * wander]));
                p = add(p, mul(d, length / steps));
            }
            tube(path, [11, 8, 6, 5][level]);
            return path;
        }
        const sample = (path, u) => path[Math.min(path.length - 1, Math.round(u * (path.length - 1)))];
        function leafPair(point, turn, size, born) {
            const side = perpendicular(point.d), other = cross(point.d, side);
            const out = unit(add(mul(side, Math.cos(turn)), mul(other, Math.sin(turn))));
            for (const sign of [1, -1]) {
                const axis = unit(add(add(mul(point.d, .55), mul(out, sign)), [0, .12, 0]));
                const up = unit(add(sub([0, 1, 0], mul(axis, axis[1])), [(rnd() - .5) * .5, 0, (rnd() - .5) * .5]));
                leaves.push(...point.p, ...axis, ...up, size * (.8 + rnd() * .45), born + rnd() * .03, 9, rnd() * TAU, rnd());
            }
        }

        const trunk = branch([0, 0, 0], [.07, 1, .03], 1.7, .27, 0, .07, .27);
        // Two seed-leaves on the young shoot; the trunk outgrows them.
        const shoot = sample(trunk, .15);
        for (const sign of [1, -1])
            leaves.push(...shoot.p, ...unit([sign, .55, .1]), 0, 1, 0, .2, .11, .22, sign, .9);
        blobs.push(0, .05, 0, .085, -1, .17, 0, .78, .64, .42);   // the olive pit it grew from

        const limbCount = 5, spin = rnd() * TAU;
        for (let i = 0; i < limbCount; i++) {
            const from = sample(trunk, [.7, .8, .88, 1, 1][i]);
            const around = spin + i / limbCount * TAU + (rnd() - .5) * .5, tilt = .85 + rnd() * .3;
            const limb = branch(from.p, [Math.cos(around) * Math.sin(tilt), Math.cos(tilt), Math.sin(around) * Math.sin(tilt)],
                1.45 + rnd() * .45, from.r * .62, 1, from.t, .15);
            [.45, .7, 1].forEach(u2 => {
                const at2 = sample(limb, u2);
                const heading2 = u2 === 1 ? at2.d : add(at2.d, mul(unit([rnd() - .5, rnd() * .5, rnd() - .5]), .95));
                const bough = branch(at2.p, heading2, .85 + rnd() * .3, Math.max(.03, at2.r * .62), 2, at2.t, .12);
                [.35, .55, .78, .92].forEach((u, k) => leafPair(sample(bough, u), k * 1.57, .3, sample(bough, u).t + .03));
                [.4, .7, 1].forEach(u3 => {
                    const at3 = sample(bough, u3);
                    const heading3 = u3 === 1 ? at3.d : add(at3.d, mul(unit([rnd() - .5, rnd() * .4 - .1, rnd() - .5]), 1.05));
                    const twig = branch(at3.p, heading3, .46 + rnd() * .2, Math.max(.016, at3.r * .55), 3, at3.t, .09);
                    // Olive leaves sit in opposite pairs, each pair a quarter-turn from the last.
                    twig.forEach((point, k) => {
                        if (!k) return;
                        leafPair(point, k * 1.57 + rnd() * .4, .31, point.t + .015);
                        leafPair(point, k * 1.57 + .8 + rnd() * .4, .26, point.t + .03);
                    });
                    const tip = twig[twig.length - 1], wait = rnd();
                    if (rnd() < .62) {
                        for (let b = 0; b < 4; b++)
                            blobs.push(tip.p[0] + (rnd() - .5) * .07, tip.p[1] + (rnd() - .5) * .07, tip.p[2] + (rnd() - .5) * .07,
                                .026, .8 + wait * .05, .93 + wait * .04, 1, 1, .98, .9);
                        if (rnd() < .65)
                            blobs.push(tip.p[0] + (rnd() - .5) * .1, tip.p[1] - .11, tip.p[2] + (rnd() - .5) * .1,
                                .058, .9 + wait * .05, 9, blobs.length % 3 ? 2 : 3, .47, .55, .2);
                    }
                });
            });
        }
        return { wood: new Float32Array(wood), woodIndex: new Uint32Array(woodIndex), leaves: new Float32Array(leaves), blobs: new Float32Array(blobs) };
    }

    // One leaf: a narrow blade folded along its midrib, drooping slightly toward the tip.
    function leafMesh() {
        const points = [], index = [], rows = 6;
        for (let i = 0; i < rows; i++) {
            const y = i / (rows - 1), half = Math.sin(Math.PI * Math.pow(y, .85)) * .105, droop = -.18 * y * y;
            points.push(-half, y, half * .5 + droop, 0, y, droop, half, y, half * .5 + droop);
            if (i === rows - 1) break;
            const l = i * 3, c = l + 1, r = l + 2;
            index.push(l, c, l + 3, c, c + 3, l + 3, c, r, c + 3, r, r + 3, c + 3);
        }
        return { points: new Float32Array(points), index: new Uint16Array(index) };
    }
    function sphereMesh() {
        const points = [], index = [], rings = 7, slices = 10;
        for (let i = 0; i <= rings; i++)
            for (let j = 0; j <= slices; j++) {
                const v = i / rings * Math.PI, h = j / slices * TAU;
                points.push(Math.sin(v) * Math.cos(h), Math.cos(v), Math.sin(v) * Math.sin(h));
            }
        for (let i = 0; i < rings; i++)
            for (let j = 0; j < slices; j++) {
                const a = i * (slices + 1) + j, b = a + slices + 1;
                index.push(a, b, a + 1, a + 1, b, b + 1);
            }
        return { points: new Float32Array(points), index: new Uint16Array(index) };
    }

    // ── Shaders ────────────────────────────────────────────────────────────────────────
    const COMMON = `#version 300 es
precision highp float;
uniform mat4 uView;
uniform vec3 uEye;
uniform float uGrowth, uTime, uWind, uShiver, uLift;
const vec3 SUN = normalize(vec3(.48, .8, .36));
const vec3 SUN_COLOR = vec3(1.08, 1., .84);
const vec3 SKY = vec3(.5, .62, .54);
const vec3 EARTH = vec3(.1, .13, .08);
vec3 sway(vec3 p) {
  float h = max(p.y, 0.);
  float a = uWind * (sin(uTime * .9 + p.x * .7 + p.z * .5) * .012 + sin(uTime * 2.3 + p.y * 1.3) * .004) * h * h
          + uShiver * sin(uTime * 38. + p.y * 3.) * .007 * h * h;
  return vec3(a, 0., a * .6);
}
vec4 project(vec3 p) { vec4 c = uView * vec4(p, 1.); c.y += uLift * c.w; return c; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
  vec2 i = floor(p), f = fract(p); f = f * f * (3. - 2. * f);
  return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
vec3 light(vec3 n, vec3 albedo) {
  return albedo * (mix(EARTH, SKY, n.y * .5 + .5) * .95 + SUN_COLOR * max(dot(n, SUN), 0.));
}
`;
    const WOOD_VS = COMMON + `
layout(location=0) in vec3 aCentre;
layout(location=1) in vec3 aRadial;
layout(location=2) in vec4 aRing;   // radius, born, around, along
out vec3 vNormal; out vec3 vPlace; out vec4 vRing;
void main() {
  float age = uGrowth - aRing.y;
  // The growing tip is a point; wood behind it thickens with age.
  float girth = clamp(age / .03, 0., 1.) * mix(.16, 1., smoothstep(0., .34, age));
  vec3 p = aCentre + aRadial * aRing.x * girth + sway(aCentre);
  vNormal = aRadial; vPlace = p; vRing = vec4(aRing.x * girth, aRing.yzw);
  gl_Position = project(p);
}`;
    const WOOD_FS = COMMON + `
in vec3 vNormal; in vec3 vPlace; in vec4 vRing;
uniform float uPulse;
out vec4 color;
void main() {
  vec3 n = normalize(vNormal);
  float around = vRing.z * 6.2832;
  float bark = noise(vec2(sin(around) * 3.2 + vRing.w * 1.1, cos(around) * 3.2 + vRing.w * 4.)) * .6
             + noise(vec2(vRing.z * 40., vRing.w * 2.2)) * .4;
  vec3 albedo = mix(vec3(.2, .165, .13), vec3(.5, .44, .35), bark);
  albedo = mix(vec3(.4, .43, .28), albedo, smoothstep(.01, .06, vRing.x));   // young wood is green
  vec3 c = light(n, albedo);
  c += pow(1. - max(dot(n, normalize(uEye - vPlace)), 0.), 3.) * vec3(.16, .2, .16);
  // Sap climbing while the server decides.
  c += vec3(.86, .92, .29) * exp(-pow((vRing.y - uPulse) / .035, 2.)) * step(0., uPulse) * 1.1;
  color = vec4(c, 1.);
}`;
    const LEAF_VS = COMMON + `
layout(location=0) in vec3 aBlade;
layout(location=1) in vec3 iBase;
layout(location=2) in vec3 iAxis;
layout(location=3) in vec3 iUp;
layout(location=4) in vec4 iLife;   // size, born, dies, phase
layout(location=5) in float iTint;
out vec3 vNormal; out vec3 vPlace; out vec2 vBlade; out float vTint; out float vShade;
void main() {
  vec3 across = normalize(cross(iAxis, iUp)), face = normalize(cross(across, iAxis));
  float open = smoothstep(0., .08, uGrowth - iLife.y) * (1. - smoothstep(0., .06, uGrowth - iLife.z));
  float flutter = uWind * sin(uTime * 2.1 + iLife.w) * .16 * aBlade.y * aBlade.y
                + uShiver * sin(uTime * 41. + iLife.w) * .22 * aBlade.y;
  // A leaf opens from a rolled bud: narrow first, then flat.
  vec3 local = across * aBlade.x * mix(.25, 1., open) + iAxis * aBlade.y + face * (aBlade.z + flutter);
  vec3 p = iBase + local * iLife.x * open + sway(iBase);
  vNormal = normalize(face - across * sign(aBlade.x) * .45);
  vPlace = p; vBlade = aBlade.xy; vTint = iTint;
  vShade = mix(.58, 1., smoothstep(.5, 2.3, distance(iBase, vec3(0., 3.1, 0.))));   // the inside of the crown is darker
  gl_Position = project(p);
}`;
    const LEAF_FS = COMMON + `
in vec3 vNormal; in vec3 vPlace; in vec2 vBlade; in float vTint; in float vShade;
out vec4 color;
void main() {
  vec3 n = normalize(vNormal);
  // Olive leaves: deep grey-green above, silver beneath.
  vec3 albedo = gl_FrontFacing ? mix(vec3(.3, .4, .22), vec3(.5, .58, .36), vTint) : vec3(.68, .74, .62);
  if (!gl_FrontFacing) n = -n;
  albedo *= 1. + .3 * (1. - smoothstep(0., .02, abs(vBlade.x)));   // midrib
  vec3 c = light(n, albedo) * vShade;
  c += vec3(.42, .5, .14) * max(dot(-n, SUN), 0.) * .34 * vShade;   // sunlight through the blade
  color = vec4(c, 1.);
}`;
    const BLOB_VS = COMMON + `
layout(location=0) in vec3 aBall;
layout(location=1) in vec3 iBase;
layout(location=2) in vec4 iLife;   // size, born, dies, kind (0 pit, 1 blossom, 2 olive, 3 olive that falls)
layout(location=3) in vec3 iColor;
uniform float uRipe, uFall;
out vec3 vNormal; out vec3 vPlace; out vec3 vColor; out float vGloss;
void main() {
  float size = iLife.x * smoothstep(0., .05, uGrowth - iLife.y) * (1. - smoothstep(0., .05, uGrowth - iLife.z));
  vec3 stretch = iLife.w < .5 ? vec3(1.35, .8, 1.) : (iLife.w > 1.5 ? vec3(1., 1.36, 1.) : vec3(1.));
  vec3 base = iBase + sway(iBase);
  if (iLife.w > 2.5) {
    float t = max(uFall - fract(iBase.x * 13.7) * .25, 0.);
    base.y = max(base.y - 4.6 * t * t, .06);
  }
  vec3 p = base + aBall * stretch * size;
  vNormal = normalize(aBall / stretch); vPlace = p;
  vColor = iLife.w > 1.5 ? mix(iColor, vec3(.2, .11, .2), uRipe * .82) : iColor;
  vGloss = iLife.w > 1.5 ? 1. : .15;
  gl_Position = project(p);
}`;
    const BLOB_FS = COMMON + `
in vec3 vNormal; in vec3 vPlace; in vec3 vColor; in float vGloss;
out vec4 color;
void main() {
  vec3 n = normalize(vNormal), eye = normalize(uEye - vPlace);
  vec3 c = light(n, vColor);
  c += pow(max(dot(reflect(-SUN, n), eye), 0.), 42.) * .75 * vGloss;
  c += pow(1. - max(dot(n, eye), 0.), 3.) * vec3(.2, .24, .2) * vGloss;
  color = vec4(c, 1.);
}`;
    const GROUND_VS = COMMON + `
out vec2 vSoil;
void main() {
  vSoil = (vec2(gl_VertexID & 1, gl_VertexID >> 1) * 2. - 1.) * 4.2;
  gl_Position = project(vec3(vSoil.x, 0., vSoil.y));
}`;
    const GROUND_FS = COMMON + `
in vec2 vSoil;
uniform float uSpread;
out vec4 color;
void main() {
  float r = length(vSoil);
  float alpha = (1. - smoothstep(1.1, 4.1, r)) * .9;
  vec3 soil = mix(vec3(.075, .085, .05), vec3(.19, .18, .1), noise(vSoil * 5.) * .6 + noise(vSoil * 19.) * .4);
  soil *= 1. - .62 * exp(-pow(r / (.35 + uSpread * 2.4), 2.) * 1.4);   // the tree's own shade
  color = vec4(soil * alpha, alpha);
}`;

    function perspective(fov, aspect, near, far) {
        const f = 1 / Math.tan(fov / 2);
        return [f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) / (near - far), -1, 0, 0, 2 * far * near / (near - far), 0];
    }
    function lookAt(eye, target) {
        const z = unit(sub(eye, target)), x = unit(cross([0, 1, 0], z)), y = cross(z, x);
        return [x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0, -dot(x, eye), -dot(y, eye), -dot(z, eye), 1];
    }
    function multiply(a, b) {
        const out = new Array(16);
        for (let c = 0; c < 4; c++)
            for (let r = 0; r < 4; r++)
                out[c * 4 + r] = a[r] * b[c * 4] + a[4 + r] * b[c * 4 + 1] + a[8 + r] * b[c * 4 + 2] + a[12 + r] * b[c * 4 + 3];
        return out;
    }

    // ── Mount ──────────────────────────────────────────────────────────────────────────
    window.ztTree3D = function mount(host) {
        const canvas = document.createElement('canvas');
        canvas.className = 'auth-tree3d';
        canvas.setAttribute('aria-hidden', 'true');
        const gl = canvas.getContext('webgl2', { antialias: true, alpha: true, premultipliedAlpha: true, powerPreference: 'low-power' });
        if (!gl) return null;

        function program(vertex, fragment) {
            const made = gl.createProgram();
            for (const [type, source] of [[gl.VERTEX_SHADER, vertex], [gl.FRAGMENT_SHADER, fragment]]) {
                const shader = gl.createShader(type);
                gl.shaderSource(shader, source);
                gl.compileShader(shader);
                if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(shader));
                gl.attachShader(made, shader);
            }
            gl.linkProgram(made);
            if (!gl.getProgramParameter(made, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(made));
            const uniforms = {};
            for (let i = 0; i < gl.getProgramParameter(made, gl.ACTIVE_UNIFORMS); i++) {
                const name = gl.getActiveUniform(made, i).name;
                uniforms[name] = gl.getUniformLocation(made, name);
            }
            return { made, uniforms };
        }
        function buffer(target, data) {
            const made = gl.createBuffer();
            gl.bindBuffer(target, made);
            gl.bufferData(target, data, gl.STATIC_DRAW);
            return made;
        }
        // layout: [location, size, offset] in floats; every: 1 = one value per instance.
        function attributes(stride, layout, every) {
            for (const [location, size, offset] of layout) {
                gl.enableVertexAttribArray(location);
                gl.vertexAttribPointer(location, size, gl.FLOAT, false, stride * 4, offset * 4);
                gl.vertexAttribDivisor(location, every);
            }
        }

        let shaders, tree, leafCount, blobCount, woodVao, leafVao, blobVao, groundVao, leaf, ball;
        try {
            shaders = { wood: program(WOOD_VS, WOOD_FS), leaf: program(LEAF_VS, LEAF_FS), blob: program(BLOB_VS, BLOB_FS), ground: program(GROUND_VS, GROUND_FS) };
            tree = growTree(); leaf = leafMesh(); ball = sphereMesh();
            leafCount = tree.leaves.length / 14; blobCount = tree.blobs.length / 10;

            woodVao = gl.createVertexArray(); gl.bindVertexArray(woodVao);
            buffer(gl.ARRAY_BUFFER, tree.wood); attributes(10, [[0, 3, 0], [1, 3, 3], [2, 4, 6]], 0);
            buffer(gl.ELEMENT_ARRAY_BUFFER, tree.woodIndex);

            leafVao = gl.createVertexArray(); gl.bindVertexArray(leafVao);
            buffer(gl.ARRAY_BUFFER, leaf.points); attributes(3, [[0, 3, 0]], 0);
            buffer(gl.ARRAY_BUFFER, tree.leaves); attributes(14, [[1, 3, 0], [2, 3, 3], [3, 3, 6], [4, 4, 9], [5, 1, 13]], 1);
            buffer(gl.ELEMENT_ARRAY_BUFFER, leaf.index);

            blobVao = gl.createVertexArray(); gl.bindVertexArray(blobVao);
            buffer(gl.ARRAY_BUFFER, ball.points); attributes(3, [[0, 3, 0]], 0);
            buffer(gl.ARRAY_BUFFER, tree.blobs); attributes(10, [[1, 3, 0], [2, 4, 3], [3, 3, 7]], 1);
            buffer(gl.ELEMENT_ARRAY_BUFFER, ball.index);

            groundVao = gl.createVertexArray();
            gl.bindVertexArray(null);
        } catch (error) {
            console.warn('3D olive tree unavailable; keeping the drawn tree.', error);
            return null;
        }

        host.prepend(canvas);
        host.classList.add('has-tree3d');

        let growth = 0, quiet = false, waiting = false, shiverAt = -1e9, ripeAt = 0, frame = 0, lost = false;
        let look = [0, 0], aim = [0, 0];

        function size() {
            const ratio = Math.min(devicePixelRatio || 1, 2);
            const width = Math.round(canvas.clientWidth * ratio), height = Math.round(canvas.clientHeight * ratio);
            if (!width || !height) return false;
            if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
            return true;
        }
        function draw(now) {
            frame = 0;
            if (lost || !size()) return;
            const time = quiet ? 0 : now / 1000;
            look = quiet ? [0, 0] : [look[0] + (aim[0] - look[0]) * .06, look[1] + (aim[1] - look[1]) * .06];

            // The camera starts close on the seed and pulls back as the tree claims the frame,
            // drifting slowly around it so the form is read as a solid.
            const aspect = canvas.width / canvas.height, pull = easeInOut(clamp01(growth / .5));
            const fit = Math.max(13.5, 7.2 / (.611 * aspect));
            const distance = 2.5 + (fit - 2.5) * pull, height = .14 + 2.2 * pull;
            const around = .5 + Math.sin(time * .13) * .42 + look[0] * .5, tilt = .3 - .17 * pull + look[1] * .06;
            const target = [0, height, 0];
            const eye = [target[0] + Math.sin(around) * Math.cos(tilt) * distance, target[1] + Math.sin(tilt) * distance, target[2] + Math.cos(around) * Math.cos(tilt) * distance];
            const view = multiply(perspective(.6, aspect, .1, 60), lookAt(eye, target));

            const shiver = quiet ? 0 : Math.max(0, 1 - (now - shiverAt) / 650);
            const ripe = ripeAt ? (quiet ? 1 : clamp01((now - ripeAt) / 650)) : 0;
            const fall = ripeAt && !quiet ? Math.max(0, (now - ripeAt - 260) / 1000) : 0;
            const pulse = waiting && !quiet ? (now / 1500 % 1) * .74 : -1;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
            gl.disable(gl.CULL_FACE);
            const use = shader => {
                gl.useProgram(shader.made);
                const u = shader.uniforms;
                gl.uniformMatrix4fv(u.uView, false, view);
                if (u.uEye) gl.uniform3fv(u.uEye, eye);
                if (u.uGrowth) gl.uniform1f(u.uGrowth, growth);
                if (u.uTime) gl.uniform1f(u.uTime, time);
                if (u.uWind) gl.uniform1f(u.uWind, quiet ? 0 : 1);
                if (u.uShiver) gl.uniform1f(u.uShiver, shiver);
                gl.uniform1f(u.uLift, .44 * pull + .1 * (1 - pull));
                return u;
            };

            gl.disable(gl.DEPTH_TEST);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
            let u = use(shaders.ground);
            gl.uniform1f(u.uSpread, clamp01((growth - .2) / .6));
            gl.bindVertexArray(groundVao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            gl.disable(gl.BLEND);
            gl.enable(gl.DEPTH_TEST);
            u = use(shaders.wood);
            gl.uniform1f(u.uPulse, pulse);
            gl.bindVertexArray(woodVao);
            gl.drawElements(gl.TRIANGLES, tree.woodIndex.length, gl.UNSIGNED_INT, 0);

            use(shaders.leaf);
            gl.bindVertexArray(leafVao);
            gl.drawElementsInstanced(gl.TRIANGLES, leaf.index.length, gl.UNSIGNED_SHORT, 0, leafCount);

            u = use(shaders.blob);
            gl.uniform1f(u.uRipe, ripe);
            gl.uniform1f(u.uFall, fall);
            gl.bindVertexArray(blobVao);
            gl.drawElementsInstanced(gl.TRIANGLES, ball.index.length, gl.UNSIGNED_SHORT, 0, blobCount);
            gl.bindVertexArray(null);

            if (!quiet) request();   // wind and the slow orbit keep the tree alive
        }
        function request() { if (!frame && !lost && !document.hidden) frame = requestAnimationFrame(draw); }

        canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); lost = true; });
        canvas.addEventListener('webglcontextrestored', () => location.reload());
        document.addEventListener('visibilitychange', request);
        new ResizeObserver(request).observe(canvas);
        request();

        return {
            progress(value) { growth = value; request(); },
            quiet(value) { quiet = value; request(); },
            waiting(value) { waiting = value; request(); },
            shiver() { shiverAt = performance.now(); request(); },
            ripen() { if (!ripeAt) ripeAt = performance.now(); request(); },
            look(x, y) { aim = [x, y]; }
        };
    };
    document.dispatchEvent(new CustomEvent('zt:tree3d-ready'));
})();
