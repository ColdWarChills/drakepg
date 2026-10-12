import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import type { MonsterDef, NpcDef, RoomDef } from '../data/types';
import { SaveSystem } from '../game/SaveSystem';
import { defaultRng, weightedPick } from '../game/rng';
import { session, type RoamingMonster } from '../game/session';
import { drawScenery } from '../ui/backdrops';
import { Button } from '../ui/Button';
import { drawFigure, PLAYER_LOOK } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { Hud } from '../ui/Hud';
import { Modal } from '../ui/Modal';
import { openCharacterPanel, openDialogue, openShop, type DialogueAction } from '../ui/panels';
import { addText, toast } from '../ui/text';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type RoomSceneData } from './keys';

/** Characters are drawn a bit smaller on maps than in battle. */
const MAP_SCALE = 0.85;
const EDGE = 36;
const TALK_RANGE = { x: 130, y: 80 };
const TOUCH_RANGE = { x: 62, y: 34 };
const UI_DEPTH = 3000;

interface Actor {
  fig: Phaser.GameObjects.Container;
  x: number;
  y: number;
  scale: number;
  /** Figure height at scale 1, for placing labels above heads. */
  height: number;
  facing: 1 | -1;
  walkT: number;
  moving: boolean;
}

interface Npc extends Actor {
  def: NpcDef;
  label: Phaser.GameObjects.Text;
}

interface Mob extends Actor {
  uid: number;
  def: MonsterDef;
  label: Phaser.GameObjects.Text;
  state: 'idle' | 'wander' | 'alert' | 'chase';
  timer: number;
  target: { x: number; y: number } | null;
}

type Keys = Record<'W' | 'A' | 'S' | 'D' | 'UP' | 'DOWN' | 'LEFT' | 'RIGHT', Phaser.Input.Keyboard.Key>;

let showedControls = false;

/** A walkable screen: move with WASD/arrows or by clicking, talk to NPCs, bump into monsters to fight. */
export class RoomScene extends Phaser.Scene {
  private room!: RoomDef;
  private player!: Actor;
  private npcs: Npc[] = [];
  private mobs: Mob[] = [];
  private keys!: Keys;
  private hud!: Hud;
  private soundBtn!: Button;
  private prompt!: Phaser.GameObjects.Container;
  private promptText!: Phaser.GameObjects.Text;
  private moveTarget: { x: number; y: number } | null = null;
  private pendingNpc: Npc | null = null;
  private safeUntil = 0;
  private leaving = false;
  private dustTimer = 0;
  private nextUid = 1;

  constructor() {
    super(SceneKeys.Room);
  }

  create(data: RoomSceneData): void {
    enterScene(this);
    this.room = db.room(data.roomId);
    this.npcs = [];
    this.mobs = [];
    this.moveTarget = null;
    this.pendingNpc = null;
    this.safeUntil = 0;
    this.leaving = false;
    this.dustTimer = 0;

    const [top] = this.room.walk;
    drawScenery(this, this.room.background, this.room.backdrop, top - 10);
    for (const kind of this.room.ambient ?? []) fx.ambient(this, [kind], kind === 'dust' ? 1 : 1500);
    this.drawExits();

    for (const def of this.room.npcs) this.addNpc(def);
    this.spawnPlayer(data);
    // A moment of safety on arrival so nothing jumps you mid-fade.
    if (data.via !== 'spawn') this.safeUntil = this.time.now + 1200;
    this.spawnMonsters(data);
    this.buildUi(data.via !== 'battle');

    // Movement keys are polled each frame; one-shot actions use key-down events so quick taps are never missed.
    const kb = this.input.keyboard!;
    this.keys = kb.addKeys('W,A,S,D,UP,DOWN,LEFT,RIGHT') as Keys;
    kb.on('keydown-ESC', () => this.closeTopModal());
    kb.on('keydown-C', () => this.openCharacter());
    kb.on('keydown-M', () => this.toggleSound());
    const talk = () => {
      const npc = this.modalOpen() || this.leaving ? null : this.nearestNpc();
      if (npc) this.interact(npc);
    };
    kb.on('keydown-E', talk);
    kb.on('keydown-SPACE', talk);
    this.input.on('pointerdown', this.onPointerDown, this);

    this.saveLocation();
    if (data.via === 'spawn' && data.intro) {
      const elder = this.npcs.find((n) => n.def.role.kind === 'talk');
      if (elder) this.time.delayedCall(500, () => this.interact(elder));
    }
  }

  // --- Setup ---

  private spawnPlayer(data: RoomSceneData): void {
    const { width } = this.scale;
    let x: number;
    let y: number;
    if (data.via === 'exit') {
      x = data.side === 'left' ? EDGE + 50 : width - EDGE - 50;
      y = data.y;
    } else {
      x = data.x;
      y = data.y;
    }
    const fig = drawFigure(this, x, y, PLAYER_LOOK, 1);
    this.player = this.makeActor(fig, x, y, PLAYER_LOOK.scale);
    if (data.via === 'exit') this.player.facing = data.side === 'left' ? 1 : -1;
    this.clampToWalk(this.player);
    this.place(this.player);
  }

  private spawnMonsters(data: RoomSceneData): void {
    const room = this.room;
    if (!room.zoneId) return;

    let roaming: RoamingMonster[] | null = null;
    if (data.via === 'battle') {
      const snap = session.takeRoom(room.id);
      if (snap) {
        roaming = snap.monsters.filter((m) => !(data.outcome === 'victory' && m.uid === snap.fightingUid));
        this.nextUid = Math.max(0, ...snap.monsters.map((m) => m.uid)) + 1;
        // Give some room: nobody stays right next to you after a fight.
        for (const m of roaming) {
          if (Math.abs(m.x - this.player.x) < 260) {
            const away = m.x < this.player.x ? -1 : 1;
            const x = this.player.x + away * 260;
            m.x = x < 80 || x > this.scale.width - 80 ? this.player.x - away * 260 : x;
          }
        }
        if (data.outcome === 'fled') {
          this.safeUntil = this.time.now + BALANCE.fleeGraceSeconds * 1000;
          this.blinkPlayer();
        }
      }
    }

    if (!roaming) {
      const zone = db.zone(room.zoneId);
      roaming = [];
      for (let i = 0; i < (room.monsterCount ?? 0); i++) {
        const pos = this.findSpawnPoint();
        // Monsters are visible, so tougher ones can appear; their level label warns you.
        const monsterId = weightedPick(defaultRng, zone.encounters, (e) => e.weight).monsterId;
        roaming.push({ uid: this.nextUid++, monsterId, ...pos });
      }
    }
    for (const r of roaming) this.addMob(r);
  }

  private findSpawnPoint(): { x: number; y: number } {
    const [top, bottom] = this.room.walk;
    let best = { x: 640, y: (top + bottom) / 2 };
    for (let i = 0; i < 30; i++) {
      const p = { x: Phaser.Math.Between(180, this.scale.width - 140), y: Phaser.Math.Between(top + 10, bottom - 10) };
      const farFromPlayer = Math.abs(p.x - this.player.x) > 320;
      const farFromOthers = this.mobs.every((m) => Math.abs(m.x - p.x) > 140);
      best = p;
      if (farFromPlayer && farFromOthers) break;
    }
    return best;
  }

  private makeActor(fig: Phaser.GameObjects.Container, x: number, y: number, scale: number): Actor {
    const height = fig.getBounds().height / Math.max(0.01, fig.scaleY);
    return { fig, x, y, scale: scale * MAP_SCALE, height, facing: 1, walkT: 0, moving: false };
  }

  private addNpc(def: NpcDef): void {
    const fig = drawFigure(this, def.x, def.y, def.look, 1);
    const actor = this.makeActor(fig, def.x, def.y, def.look.scale);
    const label = addText(this, def.x, 0, `${def.name}\n${def.title}`, { size: 17, bold: true, align: 'center', stroke: true })
      .setOrigin(0.5, 1);
    const npc: Npc = { ...actor, def, label };
    this.npcs.push(npc);
    this.place(npc);

    const hit = this.add
      .rectangle(def.x, def.y - npc.height * npc.scale * 0.5, 120, npc.height * npc.scale, 0xffffff, 0)
      .setInteractive({ useHandCursor: true })
      .setDepth(def.y + 1);
    hit.on('pointerover', () => label.setColor(COLORS.textGold));
    hit.on('pointerout', () => label.setColor(COLORS.text));
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      if (this.inTalkRange(npc)) this.interact(npc);
      else {
        this.pendingNpc = npc;
        const side = this.player.x < npc.x ? -1 : 1;
        this.moveTarget = { x: npc.x + side * 90, y: npc.y + 10 };
      }
    });
  }

  private addMob(r: RoamingMonster): void {
    const def = db.monster(r.monsterId);
    const fig = drawFigure(this, r.x, r.y, def.look, 1);
    const actor = this.makeActor(fig, r.x, r.y, def.look.scale * 0.85);
    const diff = def.level - session.player.level;
    const color = diff >= 2 ? COLORS.textBad : diff === 1 ? COLORS.textGold : diff <= -2 ? COLORS.textDim : COLORS.text;
    const label = addText(this, r.x, 0, `${def.name}  Lv ${def.level}`, { size: 15, bold: true, color, stroke: true }).setOrigin(0.5, 1);
    const mob: Mob = { ...actor, uid: r.uid, def, label, state: 'idle', timer: Phaser.Math.FloatBetween(0.3, 1.5), target: null };
    mob.facing = r.x > this.player.x ? -1 : 1;
    this.mobs.push(mob);
    this.place(mob);
    // Pop in.
    fig.setAlpha(0);
    this.tweens.add({ targets: fig, alpha: 1, duration: 300, delay: 150 });
  }

  private drawExits(): void {
    const { width } = this.scale;
    const [top, bottom] = this.room.walk;
    const y = (top + bottom) / 2 - 70;
    for (const exit of this.room.exits) {
      const dir = exit.side === 'right' ? 1 : -1;
      const x = exit.side === 'right' ? width - 46 : 46;
      const c = this.add.container(x, y).setDepth(UI_DEPTH - 1);
      const arrow = this.add.graphics();
      arrow.fillStyle(0xf1c40f, 1).lineStyle(4, 0x3e2723, 1);
      const pts = [new Phaser.Math.Vector2(-16 * dir, -26), new Phaser.Math.Vector2(22 * dir, 0), new Phaser.Math.Vector2(-16 * dir, 26)];
      arrow.fillPoints(pts, true).strokePoints(pts, true, true);
      const label = addText(this, -10 * dir, -48, exit.label, { size: 16, bold: true, stroke: true, align: 'center' }).setOrigin(
        exit.side === 'right' ? 1 : 0,
        0.5,
      );
      c.add([arrow, label]);
      this.tweens.add({ targets: arrow, x: 8 * dir, duration: 500, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
      const hit = this.add.rectangle(x, y, 90, 90, 0xffffff, 0).setInteractive({ useHandCursor: true }).setDepth(UI_DEPTH - 1);
      hit.on('pointerup', () => {
        if (this.modalOpen() || this.leaving) return;
        this.pendingNpc = null;
        this.moveTarget = { x: exit.side === 'right' ? width + 40 : -40, y: this.player.y };
      });
    }
  }

  private buildUi(titleCard: boolean): void {
    const { width, height } = this.scale;
    this.hud = new Hud(this, 16, 16);
    this.hud.refresh(session.player, false);

    const right = width - 16;
    const btn = (y: number, label: string, run: () => void) =>
      new Button(this, right - 70, y, label, run, { width: 140, height: 48, fontSize: 19 }).setDepth(UI_DEPTH);
    btn(40, 'Title', () => this.saveAndQuit());
    btn(98, 'Save', () => this.save(true));
    btn(156, 'Character', () => this.openCharacter());
    this.soundBtn = new Button(this, right - 172, 40, sfx.isMuted() ? 'Sound: off' : 'Sound: on', () => this.toggleSound(), {
      width: 120,
      height: 48,
      fontSize: 15,
    }).setDepth(UI_DEPTH);

    addText(this, width / 2, 36, this.room.name, { size: 30, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH);

    if (titleCard) this.titleCard();

    if (!showedControls) {
      showedControls = true;
      const hint = addText(this, width / 2, height - 18, 'WASD / arrows: move    E / Space: talk    C: character    Click: walk there', {
        size: 17,
        stroke: true,
      })
        .setOrigin(0.5, 1)
        .setDepth(UI_DEPTH);
      this.tweens.add({ targets: hint, alpha: 0, delay: 9000, duration: 1000, onComplete: () => hint.destroy() });
    }

    // "E Talk" bubble shown over the nearest NPC.
    const bg = this.add.rectangle(0, 0, 110, 34, 0x1d1826, 0.92).setRounded(10).setStrokeStyle(2, COLORS.panelBorder);
    this.promptText = addText(this, 0, 0, '', { size: 17, bold: true, color: COLORS.textGold }).setOrigin(0.5);
    this.prompt = this.add.container(0, 0, [bg, this.promptText]).setDepth(UI_DEPTH - 2).setVisible(false);
    this.tweens.add({ targets: this.prompt, scale: 1.06, yoyo: true, repeat: -1, duration: 450, ease: 'Sine.easeInOut' });
  }

  /** Big room name that fades in and out on arrival. */
  private titleCard(): void {
    const card = addText(this, this.scale.width / 2, 250, this.room.name, { size: 64, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH)
      .setAlpha(0)
      .setScale(0.8);
    this.tweens.chain({
      targets: card,
      tweens: [
        { alpha: 1, scale: 1, duration: 350, ease: 'Back.easeOut' },
        { alpha: 0, y: 220, duration: 600, delay: 900 },
      ],
      onComplete: () => card.destroy(),
    });
  }

  // --- Frame loop ---

  update(time: number, delta: number): void {
    if (this.leaving) return;
    const dt = Math.min(delta, 100) / 1000;
    const blocked = this.modalOpen();

    this.movePlayer(dt, time, blocked);
    for (const npc of this.npcs) this.updateNpc(npc, time);
    for (const mob of [...this.mobs]) this.updateMob(mob, dt, time, blocked);
    this.updatePrompt(blocked);
  }

  private movePlayer(dt: number, time: number, blocked: boolean): void {
    const p = this.player;
    const k = this.keys;
    let vx = 0;
    let vy = 0;
    if (!blocked) {
      vx = (k.D.isDown || k.RIGHT.isDown ? 1 : 0) - (k.A.isDown || k.LEFT.isDown ? 1 : 0);
      vy = (k.S.isDown || k.DOWN.isDown ? 1 : 0) - (k.W.isDown || k.UP.isDown ? 1 : 0);
      if (vx || vy) {
        this.moveTarget = null;
        this.pendingNpc = null;
      } else if (this.moveTarget) {
        const dx = this.moveTarget.x - p.x;
        const dy = this.moveTarget.y - p.y;
        if (Math.abs(dx) < 6 && Math.abs(dy) < 6) {
          this.moveTarget = null;
          if (this.pendingNpc && this.inTalkRange(this.pendingNpc)) this.interact(this.pendingNpc);
          this.pendingNpc = null;
        } else {
          const len = Math.hypot(dx, dy);
          vx = dx / len;
          vy = dy / len;
        }
      }
    }

    const len = Math.hypot(vx, vy);
    p.moving = len > 0;
    if (p.moving) {
      const speed = BALANCE.walkSpeed;
      p.x += (vx / len) * speed * dt;
      p.y += (vy / len) * speed * 0.7 * dt;
      if (Math.abs(vx) > 0.1) p.facing = vx > 0 ? 1 : -1;
      p.walkT += dt * 13;
      this.dustTimer -= dt;
      if (this.dustTimer <= 0) {
        this.dustTimer = 0.22;
        fx.dust(this, p.x - p.facing * 20, p.y, 2, p.y - 1);
        sfx.play(this, 'step');
      }
    }

    // Exits.
    const { width } = this.scale;
    const exitL = this.room.exits.find((e) => e.side === 'left');
    const exitR = this.room.exits.find((e) => e.side === 'right');
    if (exitR && p.x > width - EDGE / 2) return this.leave('right');
    if (exitL && p.x < EDGE / 2) return this.leave('left');
    this.clampToWalk(p, exitL ? 0 : EDGE, exitR ? width : width - EDGE);
    this.place(p, time);
  }

  private updateNpc(npc: Npc, time: number): void {
    // Turn toward the player when they're close.
    if (Math.abs(npc.x - this.player.x) < 260) npc.facing = this.player.x < npc.x ? -1 : 1;
    this.place(npc, time);
  }

  private updateMob(m: Mob, dt: number, time: number, blocked: boolean): void {
    const p = this.player;
    const dx = p.x - m.x;
    const dy = p.y - m.y;
    const dist = Math.hypot(dx, dy * 1.6);
    const safe = time < this.safeUntil;
    let speed = 0;
    let target: { x: number; y: number } | null = null;

    if (!blocked) {
      if (!safe && m.state !== 'chase' && m.state !== 'alert' && dist < BALANCE.monsterAggroRange) {
        m.state = 'alert';
        m.timer = 0.45;
        this.alertPop(m);
      }
      switch (m.state) {
        case 'alert':
          m.facing = dx < 0 ? -1 : 1;
          m.timer -= dt;
          if (m.timer <= 0) m.state = 'chase';
          break;
        case 'chase':
          if (safe || dist > BALANCE.monsterAggroRange * 1.6) {
            m.state = 'idle';
            m.timer = 1;
          } else {
            target = { x: p.x, y: p.y };
            speed = BALANCE.monsterChaseSpeed;
          }
          break;
        case 'idle':
          m.timer -= dt;
          if (m.timer <= 0) {
            const [top, bottom] = this.room.walk;
            m.target = {
              x: Phaser.Math.Clamp(m.x + Phaser.Math.Between(-220, 220), 120, this.scale.width - 120),
              y: Phaser.Math.Between(top + 5, bottom - 5),
            };
            m.state = 'wander';
          }
          break;
        case 'wander':
          target = m.target;
          speed = BALANCE.monsterWanderSpeed;
          if (!target || (Math.abs(target.x - m.x) < 5 && Math.abs(target.y - m.y) < 5)) {
            m.state = 'idle';
            m.timer = Phaser.Math.FloatBetween(1, 2.5);
            target = null;
          }
          break;
      }
    }

    m.moving = false;
    if (target && speed > 0) {
      const tx = target.x - m.x;
      const ty = target.y - m.y;
      const len = Math.hypot(tx, ty);
      if (len > 1) {
        const step = Math.min(len, speed * dt);
        m.x += (tx / len) * step;
        m.y += (ty / len) * step * 0.8;
        m.facing = tx < 0 ? -1 : 1;
        m.moving = true;
        m.walkT += dt * (m.state === 'chase' ? 14 : 8);
      }
    }
    this.clampToWalk(m, 60, this.scale.width - 60);
    this.place(m, time);

    if (!blocked && !safe && Math.abs(dx) < TOUCH_RANGE.x && Math.abs(dy) < TOUCH_RANGE.y) this.startBattle(m);
  }

  private updatePrompt(blocked: boolean): void {
    const near = blocked ? null : this.nearestNpc();
    if (!near) {
      this.prompt.setVisible(false);
      return;
    }
    const verb = near.def.role.kind === 'shop' ? 'Shop' : near.def.role.kind === 'inn' ? 'Rest' : 'Talk';
    this.promptText.setText(`E  ${verb}`);
    this.prompt.setVisible(true).setPosition(near.x, near.label.y - near.label.height - 22);
  }

  // --- Helpers ---

  /** Position, depth, scale and walk bob of an actor. Lower on screen = closer = bigger and in front. */
  private place(a: Actor & { label?: Phaser.GameObjects.Text }, time = 0): void {
    const [top, bottom] = this.room.walk;
    const t = Phaser.Math.Clamp((a.y - top) / (bottom - top), 0, 1);
    const s = a.scale * (0.9 + 0.1 * t);
    const bob = a.moving ? -Math.abs(Math.sin(a.walkT)) * 8 : 0;
    const breathe = a.moving ? 1 : 1 + Math.sin(time * 0.003 + a.x * 0.01) * 0.015;
    a.fig.setPosition(a.x, a.y + bob).setDepth(a.y);
    a.fig.setScale(s * a.facing, s * breathe);
    a.fig.setRotation(a.moving ? Math.sin(a.walkT) * 0.05 : 0);
    a.label?.setPosition(a.x, a.y - a.height * s - 8).setDepth(a.y + 0.5);
  }

  private clampToWalk(a: Actor, minX = EDGE, maxX = this.scale.width - EDGE): void {
    const [top, bottom] = this.room.walk;
    a.y = Phaser.Math.Clamp(a.y, top, bottom);
    a.x = Phaser.Math.Clamp(a.x, minX, maxX);
  }

  private inTalkRange(npc: Npc): boolean {
    return Math.abs(npc.x - this.player.x) < TALK_RANGE.x && Math.abs(npc.y - this.player.y) < TALK_RANGE.y;
  }

  private nearestNpc(): Npc | null {
    let best: Npc | null = null;
    let bestD = Infinity;
    for (const npc of this.npcs) {
      if (!this.inTalkRange(npc)) continue;
      const d = Math.abs(npc.x - this.player.x);
      if (d < bestD) {
        best = npc;
        bestD = d;
      }
    }
    return best;
  }

  private modalOpen(): boolean {
    return this.children.list.some((c) => c instanceof Modal);
  }

  private closeTopModal(): void {
    const modals = this.children.list.filter((c): c is Modal => c instanceof Modal);
    modals[modals.length - 1]?.close();
  }

  private onPointerDown(_pointer: Phaser.Input.Pointer, over: Phaser.GameObjects.GameObject[]): void {
    if (over.length > 0 || this.modalOpen() || this.leaving) return;
    this.pendingNpc = null;
    const [top, bottom] = this.room.walk;
    this.moveTarget = { x: _pointer.worldX, y: Phaser.Math.Clamp(_pointer.worldY, top, bottom) };
    // Little marker where you clicked.
    const ring = this.add.ellipse(this.moveTarget.x, this.moveTarget.y, 40, 14).setStrokeStyle(3, 0xf1c40f).setDepth(2);
    this.tweens.add({ targets: ring, scale: 1.6, alpha: 0, duration: 450, onComplete: () => ring.destroy() });
  }

  private alertPop(m: Mob): void {
    sfx.play(this, 'alert');
    const mark = addText(this, m.x, m.y - m.height * m.scale - 30, '!', { size: 44, bold: true, color: COLORS.textBad, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH - 3)
      .setScale(0);
    this.tweens.chain({
      targets: mark,
      tweens: [
        { scale: 1.3, duration: 140, ease: 'Back.easeOut' },
        { scale: 1, duration: 100 },
        { alpha: 0, y: mark.y - 20, duration: 300, delay: 300 },
      ],
      onComplete: () => mark.destroy(),
    });
    this.tweens.add({ targets: m.fig, y: m.fig.y - 18, duration: 120, yoyo: true });
  }

  private blinkPlayer(): void {
    this.tweens.add({
      targets: this.player.fig,
      alpha: 0.35,
      duration: 120,
      yoyo: true,
      repeat: Math.round((BALANCE.fleeGraceSeconds * 1000) / 240),
      onComplete: () => this.player.fig.setAlpha(1),
    });
  }

  // --- Transitions ---

  private leave(side: 'left' | 'right'): void {
    const exit = this.room.exits.find((e) => e.side === side);
    if (!exit || this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    const data: RoomSceneData = { roomId: exit.to, via: 'exit', side: side === 'right' ? 'left' : 'right', y: this.player.y };
    goTo(this, SceneKeys.Room, data);
  }

  private startBattle(m: Mob): void {
    if (this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    const zoneId = this.room.zoneId!;
    session.saveRoom({
      roomId: this.room.id,
      fightingUid: m.uid,
      monsters: this.mobs.map((x) => ({ uid: x.uid, monsterId: x.def.id, x: x.x, y: x.y })),
    });
    this.saveLocation();

    sfx.play(this, 'encounter');
    fx.whiteFlash(this, m.fig, 140);
    fx.hitSparks(this, (m.x + this.player.x) / 2, m.y - 80, false, true);
    const cam = this.cameras.main;
    cam.shake(250, 0.012);
    cam.flash(180, 255, 255, 255);
    cam.pan((m.x + this.player.x) / 2, m.y - 120, 450, 'Cubic.easeIn');
    cam.zoomTo(1.6, 450, 'Cubic.easeIn');
    cam.rotateTo(0.08, false, 450);
    const data: BattleSceneData = { zoneId, monsterId: m.def.id, roomId: this.room.id, x: this.player.x, y: this.player.y };
    this.time.delayedCall(380, () => goTo(this, SceneKeys.Battle, data, 200));
  }

  // --- NPCs and menus ---

  private refresh = (): void => {
    this.hud.refresh(session.player);
    this.save(false);
  };

  private saveLocation(): void {
    session.player.data.location = { roomId: this.room.id, x: Math.round(this.player.x), y: Math.round(this.player.y) };
    SaveSystem.save(session.player);
  }

  private save(announce: boolean): void {
    session.player.data.location = { roomId: this.room.id, x: Math.round(this.player.x), y: Math.round(this.player.y) };
    const ok = SaveSystem.save(session.player);
    if (announce) toast(this, ok ? 'Game saved' : 'Save failed (storage blocked?)', ok ? COLORS.textGood : COLORS.textBad);
  }

  private saveAndQuit(): void {
    this.save(false);
    this.leaving = true;
    goTo(this, SceneKeys.Title);
  }

  private toggleSound(): void {
    const muted = sfx.toggleMute();
    this.soundBtn.setLabel(muted ? 'Sound: off' : 'Sound: on');
  }

  private openCharacter(): void {
    if (this.modalOpen() || this.leaving) return;
    openCharacterPanel(this, session.player, this.refresh);
  }

  private interact(npc: Npc): void {
    if (this.modalOpen()) return;
    const player = session.player;
    const role = npc.def.role;
    const actions: DialogueAction[] = [];
    this.moveTarget = null;
    this.player.facing = npc.x < this.player.x ? -1 : 1;
    this.tweens.add({ targets: npc.fig, y: npc.fig.y - 10, duration: 100, yoyo: true });

    if (role.kind === 'shop') {
      actions.push({
        label: 'Browse wares',
        run: (close) => {
          close();
          openShop(this, player, `${npc.def.name}'s Shop`, role.stock, this.refresh);
        },
      });
    } else if (role.kind === 'inn') {
      const rested = player.hp >= player.maxHp && player.mp >= player.maxMp;
      actions.push({
        label: 'Rest',
        sublabel: rested ? 'Already rested' : `${BALANCE.innCost} g`,
        enabled: !rested && player.gold >= BALANCE.innCost,
        run: (close) => {
          player.data.gold -= BALANCE.innCost;
          player.restoreFull();
          close();
          this.cameras.main.fadeOut(300, 0, 0, 0);
          this.cameras.main.once('camerafadeoutcomplete', () => {
            this.cameras.main.fadeIn(500, 0, 0, 0);
            fx.healSparkles(this, this.player.x, this.player.y);
            sfx.play(this, 'heal');
            toast(this, 'You feel refreshed.', COLORS.textGood);
            this.refresh();
          });
        },
      });
    }

    openDialogue(this, npc.def, actions);
  }
}
