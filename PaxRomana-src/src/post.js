// HDR post chain: MSAA scene -> bloom -> lens/grade (scope optics, vignette, grain) -> ACES tone-map.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const Grade = {
  uniforms: { tDiffuse: { value: null }, uTime: { value: 0 }, uScope: { value: 0 }, uAspect: { value: 1.78 }, uFlash: { value: 0 }, uCine: { value: 0 } },
  vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
  fragmentShader: `
    uniform sampler2D tDiffuse; uniform float uTime, uScope, uAspect, uFlash, uCine; varying vec2 vUv;
    float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
    void main() {
      vec2 c = vUv - 0.5; vec2 ca = vec2(c.x * uAspect, c.y); float r = length(ca);
      // scope: mild barrel distortion + stronger fringe toward the edge of the glass
      vec2 uv = vUv + c * r * r * 0.22 * uScope;
      float ab = mix(0.0025, 0.012, uScope) * r * r;
      vec3 col = vec3(texture2D(tDiffuse, uv + c * ab).r, texture2D(tDiffuse, uv).g, texture2D(tDiffuse, uv - c * ab).b);
      float l = dot(col, vec3(0.2126, 0.7152, 0.0722));
      col = mix(vec3(l), col, 1.1 - uCine * 0.25);
      col *= mix(vec3(0.93, 0.99, 1.08), vec3(1.06, 1.0, 0.92), smoothstep(0.04, 0.9, l));   // teal shadows, warm highlights
      col *= mix(1.0, smoothstep(1.05, 0.3, r) * 0.42 + 0.58, 1.0 - uScope);
      float R = 0.46;
      float glass = smoothstep(R, R - 0.008, r);
      float shade = 0.5 + 0.5 * smoothstep(R - 0.01, R - 0.2, r);
      col = mix(col, col * glass * shade, uScope);
      col += uFlash * vec3(1.0, 0.9, 0.75) * (1.0 - r);
      col *= 1.0 + (hash(vUv * vec2(1811.0, 1013.0) + fract(uTime) * 37.0) - 0.5) * 0.07;
      float bar = step(abs(c.y), 0.5 - 0.105 * uCine);
      gl_FragColor = vec4(col * bar, 1.0);
    }`,
};

export function buildPost(renderer, scene, camera) {
  const size = renderer.getDrawingBufferSize(new THREE.Vector2());
  const rt = new THREE.WebGLRenderTarget(size.x, size.y, { type: THREE.HalfFloatType, samples: 4 });
  const composer = new EffectComposer(renderer, rt);
  const renderPass = new RenderPass(scene, camera);
  const bloom = new UnrealBloomPass(new THREE.Vector2(size.x / 2, size.y / 2), 0.22, 0.7, 0.92);
  const grade = new ShaderPass(Grade);
  composer.addPass(renderPass); composer.addPass(bloom); composer.addPass(grade); composer.addPass(new OutputPass());
  return {
    composer, renderPass, grade: grade.uniforms,
    resize(w, h) { composer.setSize(w, h); grade.uniforms.uAspect.value = w / h; },
  };
}
