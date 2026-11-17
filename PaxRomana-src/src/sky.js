// Atmosphere: physical sky, golden-hour sun, drifting clouds, image-based lighting, adaptive sun shadow.
import * as THREE from 'three';
import { Sky } from 'three/addons/objects/Sky.js';
import { SUN_DIR, U } from './config.js';

const CLOUD_VS = `
varying vec3 vW;
void main() {
  vec4 w = modelMatrix * vec4(position, 1.0);
  vW = w.xyz;
  gl_Position = projectionMatrix * viewMatrix * w;
}`;
const CLOUD_FS = `
uniform float uTime; uniform vec3 uSun; uniform vec3 uCam;
varying vec3 vW;
float h(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
  return mix(mix(h(i), h(i+vec2(1,0)), f.x), mix(h(i+vec2(0,1)), h(i+vec2(1,1)), f.x), f.y); }
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 6; i++) { s += a*n(p); p = p*2.03 + 17.0; a *= 0.5; } return s; }
void main() {
  vec2 p = vW.xz * 0.00016 + vec2(uTime * 0.0035, uTime * 0.0012);
  float d = fbm(p);
  float cov = smoothstep(0.47, 0.72, d);
  float d2 = fbm(p + normalize(uSun.xz) * 0.035);
  float lit = clamp((d - d2) * 5.0 + 0.55, 0.0, 1.0);
  vec3 col = mix(vec3(0.42, 0.40, 0.50), vec3(1.25, 1.0, 0.74), lit);
  col = mix(col, vec3(1.3, 0.8, 0.5), (1.0 - cov) * 0.5);       // warm rim on thin edges
  float dist = length(vW.xz - uCam.xz);
  float fade = smoothstep(17000.0, 4000.0, dist);
  gl_FragColor = vec4(col, cov * fade * 0.92);
}`;

export function buildSky(scene, renderer) {
  const sky = new Sky();
  sky.scale.setScalar(450000);
  const su = sky.material.uniforms;
  su.turbidity.value = 5.5; su.rayleigh.value = 2.4; su.mieCoefficient.value = 0.006; su.mieDirectionalG.value = 0.82;
  su.sunPosition.value.copy(SUN_DIR);
  scene.add(sky);

  // IBL from the sky itself
  if (renderer) {
    const pm = new THREE.PMREMGenerator(renderer);
    const envScene = new THREE.Scene();
    const sky2 = new Sky(); sky2.scale.setScalar(450000);
    Object.keys(su).forEach((k) => { const v = su[k].value; sky2.material.uniforms[k].value = v.isVector3 ? v.clone() : v; });
    envScene.add(sky2);
    scene.environment = pm.fromScene(envScene, 0, 0.1, 1000000).texture;
    scene.environmentIntensity = 0.55;
    pm.dispose();
  }

  scene.fog = new THREE.FogExp2(0xc4b8a6, 0.00052);

  const clouds = new THREE.Mesh(
    new THREE.PlaneGeometry(40000, 40000).rotateX(Math.PI / 2),
    new THREE.ShaderMaterial({
      vertexShader: CLOUD_VS, fragmentShader: CLOUD_FS, transparent: true, depthWrite: false, fog: false,
      uniforms: { uTime: U.uTime, uSun: { value: SUN_DIR }, uCam: { value: new THREE.Vector3() } },
    }));
  clouds.position.y = 1900;
  clouds.renderOrder = 1;
  clouds.frustumCulled = false;
  scene.add(clouds);

  const sun = new THREE.DirectionalLight(0xffd9a8, 5.2);
  sun.castShadow = true;
  sun.shadow.mapSize.set(4096, 4096);
  sun.shadow.bias = -0.0003;
  sun.shadow.normalBias = 0.08;
  sun.shadow.camera.near = 10; sun.shadow.camera.far = 1600;
  scene.add(sun, sun.target);
  scene.add(new THREE.HemisphereLight(0xa9c4ee, 0x6b5a3c, 1.1));

  const focus = new THREE.Vector3(1e9, 0, 0);
  let extent = 0;
  function setShadow(center, ext) {
    if (ext === extent && focus.distanceTo(center) < ext * 0.12) return;
    focus.copy(center); extent = ext;
    sun.target.position.copy(center);
    sun.position.copy(center).addScaledVector(SUN_DIR, 700);
    const c = sun.shadow.camera;
    c.left = -ext; c.right = ext; c.top = ext; c.bottom = -ext;
    c.updateProjectionMatrix();
    sun.target.updateMatrixWorld();
  }
  setShadow(new THREE.Vector3(60, 25, 10), 200);

  return {
    sun, setShadow,
    update(camera) { clouds.material.uniforms.uCam.value.copy(camera.position); clouds.position.x = camera.position.x; clouds.position.z = camera.position.z; },
  };
}
