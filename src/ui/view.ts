import Phaser from 'phaser';
import { GAME_HEIGHT, GAME_WIDTH } from '../config';

/**
 * Sharp rendering on big screens. Scenes lay things out in the 1280x720 design space (`DESIGN` in config.ts), but
 * the canvas is RES times bigger, so a 1440p screen gets real 2560x1440 pixels instead of a stretched 1280x720
 * picture. Every camera is zoomed by RES (`fitCamera`); text renders at RES (`textStyle`); art with more pixels
 * than its on-screen size stays crisp (`setTextureRes`). `?res=1` in the URL gives the old 1280x720 canvas.
 */
export const RES = pickRes();

function pickRes(): number {
  /** Biggest render scale: 3 covers 4K (3840x2160). */
  const MAX_RES = 3;
  if (typeof window === 'undefined') return 1;
  const asked = Number(new URLSearchParams(window.location.search).get('res'));
  if (asked > 0) return Math.min(MAX_RES, Math.max(0.5, asked));
  // The screen, not the window: going fullscreen later should still be sharp. (Some embedded browsers report a 0x0
  // screen, hence the window as a floor.)
  const dpr = window.devicePixelRatio || 1;
  const w = Math.max(window.screen.width || 0, window.innerWidth || 0);
  const h = Math.max(window.screen.height || 0, window.innerHeight || 0);
  const long = Math.max(w, h) * dpr;
  const short = Math.min(w, h) * dpr;
  const fit = Math.min(long / GAME_WIDTH, short / GAME_HEIGHT);
  // Quarter steps, rounded up (a 1440p screen gets exactly 2).
  return Math.min(MAX_RES, Math.max(1, Math.ceil(fit * 4 - 0.01) / 4));
}

/** Point a camera at the whole design space, unzoomed and unrotated. */
export function fitCamera(cam: Phaser.Cameras.Scene2D.Camera): Phaser.Cameras.Scene2D.Camera {
  return cam.setZoom(RES).setRotation(0).centerOn(GAME_WIDTH / 2, GAME_HEIGHT / 2);
}

/** A second camera over the whole screen (cutscene bars that stay put while the main camera zooms). */
export function addOverlayCamera(scene: Phaser.Scene): Phaser.Cameras.Scene2D.Camera {
  return fitCamera(scene.cameras.add(0, 0, scene.scale.width, scene.scale.height));
}

/** Camera zoom effect in design terms: 1 = the normal view. */
export function zoomTo(
  cam: Phaser.Cameras.Scene2D.Camera,
  zoom: number,
  duration: number,
  ease?: string,
  force?: boolean,
  callback?: Phaser.Types.Cameras.Scene2D.CameraZoomCallback,
): void {
  cam.zoomTo(zoom * RES, duration, ease, force, callback);
}

/**
 * Screen shake, as strong as it was on the 1280x720 canvas. Phaser's shake grows with the camera's size and zoom
 * (both RES times bigger), hence the RES squared.
 */
export function shake(scene: Phaser.Scene, duration: number, intensity: number): void {
  scene.cameras.main.shake(duration, intensity / (RES * RES));
}

/**
 * Make a texture with `res` pixels per design pixel draw at its design size: a 960px-tall `@2x` sprite then acts
 * exactly like the 480px one everywhere (width, origin, hit area, particles), but shows all its pixels when zoomed.
 * The GPU still samples the whole image (the frame's cut and UVs are untouched). WebGL only.
 */
export function setTextureRes(texture: Phaser.Textures.Texture, res: number): void {
  if (res === 1) return;
  for (const source of texture.source) source.resolution = res;
  for (const frame of Object.values(texture.frames) as Phaser.Textures.Frame[]) {
    const w = frame.cutWidth / res;
    const h = frame.cutHeight / res;
    frame.width = w;
    frame.height = h;
    frame.halfWidth = Math.floor(w / 2);
    frame.halfHeight = Math.floor(h / 2);
    frame.centerX = Math.floor(w / 2);
    frame.centerY = Math.floor(h / 2);
    // Not in Phaser's typings; `realWidth`/`realHeight` (an object's own size) read sourceSize.
    const data = (frame as unknown as { data: { sourceSize: { w: number; h: number }; spriteSourceSize: { w: number; h: number }; radius: number } }).data;
    data.sourceSize.w = w;
    data.sourceSize.h = h;
    data.spriteSourceSize.w = w;
    data.spriteSourceSize.h = h;
    data.radius = 0.5 * Math.sqrt(w * w + h * h);
  }
}
