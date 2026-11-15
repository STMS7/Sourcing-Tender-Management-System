// Zaytouna sign-in: a real-time 3D olive tree (WebGL2, no libraries), with photographed
// bark and soil and shadows cast by the tree itself.
//
// The tree is a lit mesh, not a picture: roots in translucent soil, a fluted trunk, limbs, boughs and twigs built as
// tapered tubes, about nine hundred leaves in opposite pairs, blossom and fruit. Every
// vertex knows when in the tree's life it appears, so one number, the growth progress
// supplied by auth-login.js, grows the whole tree in the vertex shader.
//
// Decoration only. If WebGL2 is missing, ztTree3D returns null and the page keeps its
// flat SVG tree. Nothing here reads the form or touches sign-in.
(() => {
    'use strict';
    // Photographed bark and soil (Poly Haven, CC0) sit next to the script's site root.
    const MATERIALS = new URL('../login/tree/', document.currentScript?.src || location.href);
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
                for (let j = 0; j <= sides; j++) {
                    const a = j / sides * TAU;
                    const radial = add(mul(normal, Math.cos(a)), mul(side, Math.sin(a)));
                    // An old olive trunk is fluted and twisted, never a clean cylinder.
                    const flute = point.flute ? 1 + .17 * Math.sin(3 * a + point.v * 4.5) + .07 * Math.sin(7 * a - point.v * 8) : 1;
                    wood.push(...point.p, ...radial, point.r * flute, point.t, j / sides, point.v);
                }
                if (i === path.length - 1) return;
                for (let j = 0; j < sides; j++) {
                    const a = first + i * (sides + 1) + j, b = a + 1;
                    woodIndex.push(a, a + sides + 1, b, b, a + sides + 1, b + sides + 1);
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

        // Life starts below ground: roots spread through the soil before anything shows above it,
        // and keep thickening for as long as the tree grows.
        function root(start, heading, length, radius, born, lasts, steps) {
            const path = [];
            let p = start, d = unit(heading);
            for (let i = 0; i <= steps; i++) {
                const u = i / steps;
                path.push({ p, d, r: radius * (1 - u * .85), t: born + lasts * u, v: u * length, flute: false });
                d = unit(add(d, [(rnd() - .5) * .42, -.05 + (rnd() - .5) * .2, (rnd() - .5) * .42]));
                p = add(p, mul(d, length / steps));
                if (p[1] > -.05) p = [p[0], -.05, p[2]];   // roots stay under the surface
            }
            tube(path, 6);
            return path;
        }
        const rootCount = 7, rootSpin = rnd() * TAU;
        for (let i = 0; i < rootCount; i++) {
            const around = rootSpin + i / rootCount * TAU + (rnd() - .5) * .5, dive = .35 + rnd() * .55;
            const main = root([0, .02, 0], [Math.cos(around), -dive, Math.sin(around)], .75 + rnd() * .3, .1, 0, .055, 9);
            [.4, .7].forEach(u => {
                const at = sample(main, u);
                root(at.p, add(at.d, mul(unit([rnd() - .5, -rnd() * .6, rnd() - .5]), .9)), .34 + rnd() * .22, Math.max(.022, at.r * .55), at.t, .05, 5);
            });
        }
        root([0, .02, 0], [.05, -1, .03], .7, .12, 0, .05, 7);   // the tap root

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
    function sphereMesh(rings, slices) {
        const points = [], index = [];
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
uniform mat4 uLight;
uniform float uGrowth, uTime, uWind, uShiver, uLift, uShadowOn;
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
vec3 light(vec3 n, vec3 albedo, float sun) {
  return albedo * (mix(EARTH, SKY, n.y * .5 + .5) * .95 + SUN_COLOR * max(dot(n, SUN), 0.) * sun);
}
`;
    // Fragment-only: real cast shadows from a depth map, and relief from a photographed normal map.
    const FRAG = COMMON + `
uniform highp sampler2DShadow uShadowMap;
float sunlight(vec3 p, vec3 n) {
  if (uShadowOn < .5) return 1.;
  vec4 l = uLight * vec4(p + n * .045, 1.);
  vec3 s = l.xyz / l.w * .5 + .5;
  if (s.x < 0. || s.x > 1. || s.y < 0. || s.y > 1. || s.z > 1.) return 1.;
  float lit = 0.;
  for (int x = -1; x <= 1; x++)
    for (int y = -1; y <= 1; y++)
      lit += texture(uShadowMap, vec3(s.xy + vec2(x, y) * (1.4 / 2048.), s.z - .0022));
  return lit / 9.;
}
vec3 bump(vec3 n, vec3 p, vec2 uv, vec3 relief, float strength) {
  vec3 dp1 = dFdx(p), dp2 = dFdy(p);
  vec2 du1 = dFdx(uv), du2 = dFdy(uv);
  vec3 a = cross(dp2, n), b = cross(n, dp1);
  vec3 tangent = a * du1.x + b * du2.x, bitangent = a * du1.y + b * du2.y;
  float scale = inversesqrt(max(max(dot(tangent, tangent), dot(bitangent, bitangent)), 1e-12));
  relief.xy *= strength;
  return normalize(mat3(tangent * scale, bitangent * scale, n) * relief);
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
  // Roots start at half their final thickness so they read from the first frame.
  float girth = clamp(age / .03, 0., 1.) * mix(aCentre.y < -.01 ? .5 : .16, 1., smoothstep(0., .34, age));
  vec3 p = aCentre + aRadial * aRing.x * girth + sway(aCentre);
  vNormal = aRadial; vPlace = p; vRing = vec4(aRing.x * girth, aRing.yzw);
  gl_Position = project(p);
}`;
    const WOOD_FS = FRAG + `
in vec3 vNormal; in vec3 vPlace; in vec4 vRing;
uniform float uPulse, uTextured;
uniform sampler2D uBark, uBarkN;
out vec4 color;
void main() {
  vec3 smoothN = normalize(vNormal), n = smoothN;
  vec3 albedo;
  if (uTextured > .5) {
    vec2 uv = vec2(vRing.z * 2., vRing.w * 1.15);
    vec3 photo = texture(uBark, uv).rgb;
    albedo = mix(photo, vec3(dot(photo, vec3(.3, .59, .11))), .3) * 1.25;   // olive bark is greyer than the sample
    n = bump(n, vPlace, uv, texture(uBarkN, uv).xyz * 2. - 1., 1.5 * smoothstep(0., .05, vRing.x));
  } else {
    float around = vRing.z * 6.2832;
    float bark = noise(vec2(sin(around) * 3.2 + vRing.w * 1.1, cos(around) * 3.2 + vRing.w * 4.)) * .6
               + noise(vec2(vRing.z * 40., vRing.w * 2.2)) * .4;
    albedo = mix(vec3(.2, .165, .13), vec3(.5, .44, .35), bark);
  }
  float tone = dot(albedo, vec3(.3, .59, .11));
  albedo = mix(vec3(.4, .43, .28), albedo, smoothstep(.01, .06, vRing.x));                   // young wood is green
  albedo = mix(albedo, vec3(.78, .66, .47) * (.55 + tone), smoothstep(.03, -.07, vPlace.y));   // roots are pale
  vec3 c = light(n, albedo, sunlight(vPlace, smoothN));
  c += pow(1. - max(dot(n, normalize(uEye - vPlace)), 0.), 3.) * vec3(.12, .15, .12);
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
  vShade = mix(.8, 1., smoothstep(.5, 2.3, distance(iBase, vec3(0., 3.1, 0.))));   // the inside of the crown is darker
  gl_Position = project(p);
}`;
    const LEAF_FS = FRAG + `
in vec3 vNormal; in vec3 vPlace; in vec2 vBlade; in float vTint; in float vShade;
out vec4 color;
void main() {
  vec3 n = normalize(vNormal), eye = normalize(uEye - vPlace);
  // Olive leaves: deep grey-green and leathery above, silver beneath.
  vec3 albedo = gl_FrontFacing ? mix(vec3(.23, .33, .17), vec3(.42, .52, .28), vTint) : vec3(.66, .72, .6);
  if (!gl_FrontFacing) n = -n;
  albedo *= 1. + .3 * (1. - smoothstep(0., .02, abs(vBlade.x)));   // midrib
  float sun = sunlight(vPlace, n);
  vec3 c = light(n, albedo, sun) * vShade;
  c += vec3(.42, .5, .14) * max(dot(-n, SUN), 0.) * .34 * vShade * (.4 + .6 * sun);   // sunlight through the blade
  if (gl_FrontFacing) c += SUN_COLOR * pow(max(dot(n, normalize(SUN + eye)), 0.), 26.) * .22 * sun;   // waxy sheen
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
    const BLOB_FS = FRAG + `
in vec3 vNormal; in vec3 vPlace; in vec3 vColor; in float vGloss;
out vec4 color;
void main() {
  vec3 n = normalize(vNormal), eye = normalize(uEye - vPlace);
  float sun = sunlight(vPlace, n);
  vec3 c = light(n, vColor, sun);
  c += pow(max(dot(reflect(-SUN, n), eye), 0.), 42.) * .75 * vGloss * sun;
  c += pow(1. - max(dot(n, eye), 0.), 3.) * vec3(.2, .24, .2) * vGloss;
  color = vec4(c, 1.);
}`;
    // The earth the tree stands in: a flat top and a rounded body below it, drawn translucent
    // so the roots can be seen inside the soil.
    const SOIL_VS = COMMON + `
layout(location=0) in vec3 aBall;
out vec3 vBall; out vec3 vPlace;
void main() {
  vec3 p = vec3(aBall.x * 1.8, min(aBall.y, 0.) * 1.12, aBall.z * 1.8);
  vBall = aBall; vPlace = p;
  gl_Position = project(p);
}`;
    const SOIL_FS = FRAG + `
in vec3 vBall; in vec3 vPlace;
uniform float uSpread, uTextured;
uniform sampler2D uSoil, uSoilN;
out vec4 color;
void main() {
  float r = length(vPlace.xz);
  vec4 c;
  if (vBall.y > 0.) {
    // The surface: photographed soil, lit by the same sun and shaded by the tree itself.
    vec3 up = vec3(0., 1., 0.), n = up;
    vec3 earth = mix(vec3(.2, .15, .1), vec3(.42, .33, .22), noise(vPlace.xz * 6.) * .6 + noise(vPlace.xz * 21.) * .4);
    if (uTextured > .5) {
      vec2 uv = vPlace.xz * .42;
      earth = texture(uSoil, uv).rgb * .95;
      n = bump(n, vPlace, uv, texture(uSoilN, uv).xyz * 2. - 1., 1.3);
    }
    earth = light(n, earth, sunlight(vPlace, up)) * .82;
    if (uShadowOn < .5) earth *= 1. - .5 * exp(-pow(r / (.35 + uSpread * 1.5), 2.) * 1.4);
    c = vec4(earth, .97 * (1. - smoothstep(1.55, 1.8, r)));
  } else {
    // The body: denser at its outline, clear enough in the middle to show the roots.
    float rim = 1. - abs(dot(normalize(vBall), normalize(uEye - vPlace)));
    c = vec4(mix(vec3(.4, .3, .2), vec3(.2, .14, .09), rim), .42 + .5 * rim * rim);
  }
  color = vec4(c.rgb * c.a, c.a);
}`;
    const GROUND_VS = COMMON + `
out vec2 vSoil;
void main() {
  vSoil = (vec2(gl_VertexID & 1, gl_VertexID >> 1) * 2. - 1.) * 4.2;
  gl_Position = project(vec3(vSoil.x, -1.16, vSoil.y));
}`;
    const GROUND_FS = COMMON + `
in vec2 vSoil;
out vec4 color;
void main() {
  // The studio floor shows only the soft shadow under the earth.
  float alpha = .3 * exp(-pow(length(vSoil) / 1.5, 2.) * 1.2);
  color = vec4(vec3(.1, .17, .11) * alpha, alpha);
}`;

    function perspective(fov, aspect, near, far) {
        const f = 1 / Math.tan(fov / 2);
        return [f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) / (near - far), -1, 0, 0, 2 * far * near / (near - far), 0];
    }
    function orthographic(half, near, far) {
        return [1 / half, 0, 0, 0, 0, 1 / half, 0, 0, 0, 0, -2 / (far - near), 0, 0, 0, -(far + near) / (far - near), 1];
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

        let shaders, tree, leafCount, blobCount, woodVao, leafVao, blobVao, groundVao, soilVao, leaf, ball, earth;
        try {
            shaders = { wood: program(WOOD_VS, WOOD_FS), leaf: program(LEAF_VS, LEAF_FS), blob: program(BLOB_VS, BLOB_FS), ground: program(GROUND_VS, GROUND_FS), soil: program(SOIL_VS, SOIL_FS) };
            tree = growTree(); leaf = leafMesh(); ball = sphereMesh(7, 10); earth = sphereMesh(18, 36);
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

            soilVao = gl.createVertexArray(); gl.bindVertexArray(soilVao);
            buffer(gl.ARRAY_BUFFER, earth.points); attributes(3, [[0, 3, 0]], 0);
            buffer(gl.ELEMENT_ARRAY_BUFFER, earth.index);

            groundVao = gl.createVertexArray();
            gl.bindVertexArray(null);
        } catch (error) {
            console.warn('3D olive tree unavailable; keeping the drawn tree.', error);
            return null;
        }

        // Photographed materials load in the background; until then the tree uses its drawn bark and soil.
        let materialsLeft = 4;
        function photograph(file, unit) {
            const made = gl.createTexture();
            gl.activeTexture(gl.TEXTURE0 + unit);
            gl.bindTexture(gl.TEXTURE_2D, made);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([128, 128, 128, 255]));
            const image = new Image();
            image.onload = () => {
                if (lost) return;
                gl.activeTexture(gl.TEXTURE0 + unit);
                gl.bindTexture(gl.TEXTURE_2D, made);
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
                gl.generateMipmap(gl.TEXTURE_2D);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
                const sharp = gl.getExtension('EXT_texture_filter_anisotropic');
                if (sharp) gl.texParameterf(gl.TEXTURE_2D, sharp.TEXTURE_MAX_ANISOTROPY_EXT, Math.min(8, gl.getParameter(sharp.MAX_TEXTURE_MAX_ANISOTROPY_EXT)));
                materialsLeft--;
                request();
            };
            image.src = new URL(file, MATERIALS).href;
            return made;
        }
        const materials = [photograph('bark-color.jpg', 0), photograph('bark-normal.jpg', 1), photograph('soil-color.jpg', 3), photograph('soil-normal.jpg', 4)];

        // The sun's own view of the tree, as depth: what it cannot see is in shadow.
        const SHADOW = 2048, sun = unit([.48, .8, .36]);
        const lightView = multiply(orthographic(5.2, 1, 27), lookAt(add([0, 2.2, 0], mul(sun, 14)), [0, 2.2, 0]));
        function depthTexture(edge) {
            const made = gl.createTexture();
            gl.activeTexture(gl.TEXTURE2);
            gl.bindTexture(gl.TEXTURE_2D, made);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.DEPTH_COMPONENT24, edge, edge, 0, gl.DEPTH_COMPONENT, gl.UNSIGNED_INT, null);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_COMPARE_MODE, gl.COMPARE_REF_TO_TEXTURE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_COMPARE_FUNC, gl.LEQUAL);
            return made;
        }
        const shadowMap = depthTexture(SHADOW), noShadow = depthTexture(1);
        let shadowTarget = gl.createFramebuffer();
        gl.bindFramebuffer(gl.FRAMEBUFFER, shadowTarget);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.TEXTURE_2D, shadowMap, 0);
        gl.drawBuffers([gl.NONE]);
        gl.readBuffer(gl.NONE);
        if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) shadowTarget = null;   // no shadows, still a tree
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);

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
            const fit = Math.max(15.5, 8.2 / (.611 * aspect));
            const distance = 9.8 + (fit - 9.8) * pull, height = -.5 + 2.84 * pull;
            // A directed move, not a free orbit: it opens level with the soil, on the roots, then
            // rises and swings to three-quarters as the tree gains height. A slight breath keeps
            // the solid readable once it has settled.
            const around = .08 + .5 * pull + Math.sin(time * .16) * .07 + look[0] * .22;
            const tilt = .2 - .07 * pull + look[1] * .05;
            const target = [0, height, 0];
            const eye = [target[0] + Math.sin(around) * Math.cos(tilt) * distance, target[1] + Math.sin(tilt) * distance, target[2] + Math.cos(around) * Math.cos(tilt) * distance];
            const view = multiply(perspective(.6, aspect, .1, 60), lookAt(eye, target));

            const shiver = quiet ? 0 : Math.max(0, 1 - (now - shiverAt) / 650);
            const ripe = ripeAt ? (quiet ? 1 : clamp01((now - ripeAt) / 650)) : 0;
            const fall = ripeAt && !quiet ? Math.max(0, (now - ripeAt - 260) / 1000) : 0;
            const pulse = waiting && !quiet ? (now / 1500 % 1) * .74 : -1;

            const lift = .5 * pull + .38 * (1 - pull);
            gl.disable(gl.CULL_FACE);
            const use = (shader, matrix, raise, shadowed) => {
                gl.useProgram(shader.made);
                const u = shader.uniforms;
                gl.uniformMatrix4fv(u.uView, false, matrix);
                if (u.uLight) gl.uniformMatrix4fv(u.uLight, false, lightView);
                if (u.uEye) gl.uniform3fv(u.uEye, eye);
                if (u.uGrowth) gl.uniform1f(u.uGrowth, growth);
                if (u.uTime) gl.uniform1f(u.uTime, time);
                if (u.uWind) gl.uniform1f(u.uWind, quiet ? 0 : 1);
                if (u.uShiver) gl.uniform1f(u.uShiver, shiver);
                if (u.uShadowOn) gl.uniform1f(u.uShadowOn, shadowed ? 1 : 0);
                if (u.uTextured) gl.uniform1f(u.uTextured, materialsLeft ? 0 : 1);
                // Every sampler gets its own unit; a shadow sampler may never share one with a colour sampler.
                if (u.uBark) gl.uniform1i(u.uBark, 0);
                if (u.uBarkN) gl.uniform1i(u.uBarkN, 1);
                if (u.uShadowMap) gl.uniform1i(u.uShadowMap, 2);
                if (u.uSoil) gl.uniform1i(u.uSoil, 3);
                if (u.uSoilN) gl.uniform1i(u.uSoilN, 4);
                gl.uniform1f(u.uLift, raise);
                return u;
            };
            const solids = (matrix, raise, shadowed) => {
                let u = use(shaders.wood, matrix, raise, shadowed);
                gl.uniform1f(u.uPulse, pulse);
                gl.bindVertexArray(woodVao);
                gl.drawElements(gl.TRIANGLES, tree.woodIndex.length, gl.UNSIGNED_INT, 0);

                use(shaders.leaf, matrix, raise, shadowed);
                gl.bindVertexArray(leafVao);
                gl.drawElementsInstanced(gl.TRIANGLES, leaf.index.length, gl.UNSIGNED_SHORT, 0, leafCount);

                u = use(shaders.blob, matrix, raise, shadowed);
                gl.uniform1f(u.uRipe, ripe);
                gl.uniform1f(u.uFall, fall);
                gl.bindVertexArray(blobVao);
                gl.drawElementsInstanced(gl.TRIANGLES, ball.index.length, gl.UNSIGNED_SHORT, 0, blobCount);
            };
            [0, 1, 3, 4].forEach((unit, i) => { gl.activeTexture(gl.TEXTURE0 + unit); gl.bindTexture(gl.TEXTURE_2D, materials[i]); });
            gl.disable(gl.BLEND);
            gl.enable(gl.DEPTH_TEST);

            // Pass one: depth from the sun. The map being written must not be sampled meanwhile.
            gl.activeTexture(gl.TEXTURE2);
            gl.bindTexture(gl.TEXTURE_2D, noShadow);
            if (shadowTarget) {
                gl.bindFramebuffer(gl.FRAMEBUFFER, shadowTarget);
                gl.viewport(0, 0, SHADOW, SHADOW);
                gl.clear(gl.DEPTH_BUFFER_BIT);
                gl.enable(gl.POLYGON_OFFSET_FILL);
                gl.polygonOffset(1.5, 3);
                solids(lightView, 0, false);
                gl.disable(gl.POLYGON_OFFSET_FILL);
                gl.bindFramebuffer(gl.FRAMEBUFFER, null);
                gl.activeTexture(gl.TEXTURE2);
                gl.bindTexture(gl.TEXTURE_2D, shadowMap);
            }

            // Pass two: the picture.
            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
            gl.disable(gl.DEPTH_TEST);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
            use(shaders.ground, view, lift, false);
            gl.bindVertexArray(groundVao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            gl.disable(gl.BLEND);
            gl.enable(gl.DEPTH_TEST);
            solids(view, lift, !!shadowTarget);

            // Soil last, blended over what is inside it, without hiding what stands in front of it.
            gl.enable(gl.BLEND);
            gl.depthMask(false);
            const u = use(shaders.soil, view, lift, !!shadowTarget);
            gl.uniform1f(u.uSpread, clamp01((growth - .2) / .6));
            gl.bindVertexArray(soilVao);
            gl.drawElements(gl.TRIANGLES, earth.index.length, gl.UNSIGNED_SHORT, 0);
            gl.depthMask(true);
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
