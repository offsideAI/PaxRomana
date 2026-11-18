import * as THREE from 'three';

export const PLATEAU = 30;            // castle plateau height
export const WALL_H = 13;             // curtain wall height
export const WALK_Y = PLATEAU + WALL_H;
export const SPAWN = { x: 120, z: 412 };
export const CAMP = { x: 170, z: 10, r: 52, h: 9 };
export const INNER = { x0: -540, z0: -410, size: 1200, n: 400 };
export const SUN_DIR = new THREE.Vector3(-0.76, 0.19, 0.46).normalize();   // low winter sun
export const MUZZLE_V = 880;
export const GRAVITY = 9.81;
export const WIND = new THREE.Vector3(0.5, 0, 0.12);   // lateral accel on bullets (m/s^2)
export const riverZ = (x) => 210 + 30 * Math.sin(x * 0.006);
export const ROAD = [[0, 42], [8, 60], [30, 85], [70, 108], [115, 95], [150, 72], [170, 63]];
export const ROAD2 = [[70, 108], [62, 150], [55, 185], [55, 255], [72, 300]];
export const BRIDGE = { x: 55, z0: 180, z1: 260, y: 3.4 };
export const U = { uTime: { value: 0 } };  // shared shader clock
