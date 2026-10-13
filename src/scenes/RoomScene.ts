import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { ROOMS } from '../data/world';
import type { Blocker, MonsterDef, NpcDef, QuestDef, RoomDef, RoomExit } from '../data/types';
import { makeElite, rollElite } from '../game/Elites';
import { acceptQuest, activeQuests, questForNpc, questNeedsKill, questStarted, questWantsElite, turnInQuest } from '../game/Quests';
import { SaveSystem } from '../game/SaveSystem';
import { defaultRng, weightedPick } from '../game/rng';
import { session, type RoamingMonster } from '../game/session';
import { drawScenery } from '../ui/backdrops';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { Hud } from '../ui/Hud';
import { Modal } from '../ui/Modal';
import { openInventory, openShop } from '../ui/inventory';
import { lootName } from '../ui/itemUi';
import { openCharacterPanel, openDialogue, type DialogueAction } from '../ui/panels';
import { addText, toast } from '../ui/text';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type RoomSceneData } from './keys';

/** Characters are drawn a bit smaller on maps than in battle. */
const MAP_SCALE = 0.85;
const EDGE = 36;
const TALK_RANGE = { x: 130, y: 80 };
const TOUCH_RANGE = { x: 62, y: 34 };
const UI_DEPTH = 3000;
/** Gold glow around Elites. */
const ELITE_GLOW = 0xffc23d;

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
  /** Elite affix id, with its gold aura and "ELITE" tag. */
  elite?: string;
  aura?: Phaser.GameObjects.Image;
  tag?: Phaser.GameObjects.Text;
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
  /** Weapon id the player figure was drawn with. */
  private playerWeapon: string | null = null;
  private npcs: Npc[] = [];
  private mobs: Mob[] = [];
  private keys!: Keys;
  private hud!: Hud;
  private soundBtn!: Button;
  private charBtn!: Button;
  private invBtn!: Button;
  private inventory: Modal | null = null;
  private tracker!: Phaser.GameObjects.Container;
  private markers = new Map<Npc, Phaser.GameObjects.Text>();
  private boss: (Actor & { def: MonsterDef; label: Phaser.GameObjects.Text }) | null = null;
  private wallBumpAt = 0;
  /** Set once walking into the boss has asked "fight?"; cleared after stepping away. */
  private bossAsked = false;
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
    this.markers = new Map();
    this.boss = null;
    this.inventory = null;
    this.wallBumpAt = 0;
    this.bossAsked = false;
    this.moveTarget = null;
    this.pendingNpc = null;
    this.safeUntil = 0;
    this.leaving = false;
    this.dustTimer = 0;

    const [top] = this.room.walk;
    drawScenery(this, this.room.background, this.room.backdrop, top - 10);
    for (const kind of this.room.ambient ?? []) fx.ambient(this, [kind], kind === 'dust' || kind === 'mist' ? 1 : 1500);
    this.drawExits();

    for (const def of this.room.npcs) this.addNpc(def);
    this.spawnPlayer(data);
    // A moment of safety on arrival so nothing jumps you mid-fade.
    if (data.via !== 'spawn') this.safeUntil = this.time.now + 1200;
    this.spawnMonsters(data);
    this.spawnBoss();
    this.buildUi(data.via !== 'battle');
    this.refreshQuests();

    // Movement keys are polled each frame; one-shot actions use key-down events so quick taps are never missed.
    const kb = this.input.keyboard!;
    kb.enableGlobalCapture();
    this.keys = kb.addKeys('W,A,S,D,UP,DOWN,LEFT,RIGHT') as Keys;
    kb.on('keydown-ESC', () => this.closeTopModal());
    kb.on('keydown-C', () => this.openCharacter());
    kb.on('keydown-I', () => this.toggleInventory());
    kb.on('keydown-M', () => this.toggleSound());
    const talk = () => {
      if (this.modalOpen() || this.leaving) return;
      const target = this.nearestTarget();
      if (target === 'boss') this.confirmBoss();
      else if (target) this.interact(target);
    };
    kb.on('keydown-E', talk);
    kb.on('keydown-SPACE', talk);
    this.input.on('pointerdown', this.onPointerDown, this);

    this.saveLocation();
    if (data.via === 'spawn' && data.intro) {
      const elder = this.npcs.find((n) => n.def.role.kind === 'talk');
      if (elder) this.time.delayedCall(500, () => this.interact(elder, true));
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
    const look = session.player.classDef.look;
    const fig = drawFigure(this, x, y, look, 1, session.player.weapon, session.player.weaponSigil);
    this.player = this.makeActor(fig, x, y, look.scale);
    this.playerWeapon = session.player.data.equipment.weapon?.uid ?? null;
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
      const wantsElite = questWantsElite(session.player);
      let elites = 0;
      roaming = [];
      for (let i = 0; i < (room.monsterCount ?? 0); i++) {
        const pos = this.findSpawnPoint();
        // Monsters are visible, so tougher ones can appear; their level label warns you.
        const monsterId = weightedPick(defaultRng, zone.encounters, (e) => e.weight).monsterId;
        const elite = elites < BALANCE.elite.maxPerRoom ? rollElite(defaultRng, wantsElite) : undefined;
        if (elite) elites++;
        roaming.push({ uid: this.nextUid++, monsterId, elite, ...pos });
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

  /** The hero holds the equipped weapon, so redraw them after a weapon swap. */
  private redrawPlayerIfRearmed(): void {
    const weapon = session.player.data.equipment.weapon?.uid ?? null;
    if (weapon === this.playerWeapon) return;
    this.playerWeapon = weapon;
    const old = this.player.fig;
    this.player.fig = drawFigure(this, this.player.x, this.player.y, session.player.classDef.look, 1, session.player.weapon, session.player.weaponSigil);
    old.destroy();
    this.place(this.player);
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
    const base = db.monster(r.monsterId);
    const def = r.elite ? makeElite(base, r.elite, db.zone(this.room.zoneId!)) : base;
    const fig = drawFigure(this, r.x, r.y, def.look, 1);
    const actor = this.makeActor(fig, r.x, r.y, def.look.scale * 0.85);
    const diff = def.level - session.player.level;
    const color = diff >= 2 ? COLORS.textBad : diff === 1 ? COLORS.textGold : diff <= -2 ? COLORS.textDim : COLORS.text;
    const label = addText(this, r.x, 0, `${def.name}  Lv ${def.level}`, { size: 15, bold: true, color, stroke: true }).setOrigin(0.5, 1);
    const mob: Mob = { ...actor, uid: r.uid, def, label, state: 'idle', timer: Phaser.Math.FloatBetween(0.3, 1.5), target: null };
    mob.facing = r.x > this.player.x ? -1 : 1;
    if (r.elite) {
      mob.elite = r.elite;
      mob.aura = this.add.image(r.x, r.y, 'fx_soft').setTint(ELITE_GLOW).setBlendMode('ADD').setScale(5).setAlpha(0.3);
      this.tweens.add({ targets: mob.aura, alpha: 0.55, scale: 5.8, duration: 900, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
      mob.tag = addText(this, r.x, 0, 'ELITE', { size: 14, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5, 1);
    }
    this.mobs.push(mob);
    this.place(mob);
    this.placeEliteMarks(mob);
    // Pop in.
    fig.setAlpha(0);
    this.tweens.add({ targets: fig, alpha: 1, duration: 300, delay: 150 });
  }

  /** Keep an Elite's aura and tag with it. */
  private placeEliteMarks(m: Mob): void {
    m.aura?.setPosition(m.x, m.y - m.height * m.scale * 0.45).setDepth(m.y - 1);
    m.tag?.setPosition(m.x, m.label.y - m.label.height).setDepth(m.y + 0.5);
  }

  private drawExits(): void {
    const { width } = this.scale;
    const [top, bottom] = this.room.walk;
    const y = (top + bottom) / 2 - 70;
    for (const exit of this.room.exits) {
      const dir = exit.side === 'right' ? 1 : -1;
      const x = exit.side === 'right' ? width - 46 : 46;
      if (!this.exitOpen(exit)) {
        this.drawBlocker(exit.side, exit.blocker ?? 'thorns');
        continue;
      }
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

  private exitOpen(exit: RoomExit): boolean {
    return !exit.requiresQuest || questStarted(session.player, exit.requiresQuest);
  }

  /** Thorns, a barricade or flooded reeds across a locked exit. */
  private drawBlocker(side: 'left' | 'right', kind: Blocker): void {
    const { width } = this.scale;
    const [top, bottom] = this.room.walk;
    const x0 = side === 'right' ? width - 90 : 0;
    const g = this.add.graphics().setDepth(bottom + 1);
    if (kind === 'thorns') {
      for (let i = 0; i < 70; i++) {
        const x = x0 + ((i * 37) % 90);
        const y = top - 260 + ((i * 53) % (bottom - top + 280));
        g.lineStyle(6, i % 3 ? 0x2b1d14 : 0x1f3a1c, 1);
        g.beginPath();
        g.moveTo(x, y);
        g.lineTo(x + ((i * 29) % 50) - 25, y + 40);
        g.strokePath();
        g.fillStyle(0x4a3426, 1).fillTriangle(x - 4, y + 10, x + 4, y + 10, x + (i % 2 ? 12 : -12), y + 2);
      }
    } else if (kind === 'barricade') {
      // Posts, then planks nailed across them.
      for (let i = 0; i < 3; i++) {
        g.fillStyle(0x4e342e, 1).fillRect(x0 + 10 + i * 30, top - 230, 18, bottom - top + 250);
        g.fillStyle(0x3e2723, 1).fillTriangle(x0 + 10 + i * 30, top - 230, x0 + 19 + i * 30, top - 250, x0 + 28 + i * 30, top - 230);
      }
      for (let i = 0; i < 7; i++) {
        const y = top - 200 + i * ((bottom - top + 200) / 7);
        const tilt = i % 2 ? 14 : -14;
        g.fillStyle(i % 2 ? 0x8d6e63 : 0x795548, 1);
        g.fillPoints([
          new Phaser.Math.Vector2(x0 - 4, y + tilt),
          new Phaser.Math.Vector2(x0 + 94, y - tilt),
          new Phaser.Math.Vector2(x0 + 94, y - tilt + 22),
          new Phaser.Math.Vector2(x0 - 4, y + tilt + 22),
        ], true);
        g.lineStyle(2, 0x3e2723, 1).strokeRect(x0 + 2, y - 6, 86, 2);
      }
    } else {
      // Deep water with tall reeds and cattails.
      g.fillStyle(0x24485a, 0.85).fillRect(x0, top - 20, 90, bottom - top + 40);
      for (let i = 0; i < 40; i++) {
        const x = x0 + ((i * 41) % 90);
        const base = bottom + 10 - ((i * 23) % 60);
        const h = 160 + ((i * 67) % 220);
        g.lineStyle(4, i % 3 ? 0x5d7a3a : 0x47602b, 1);
        g.beginPath();
        g.moveTo(x, base);
        g.lineTo(x + ((i * 13) % 20) - 10, base - h);
        g.strokePath();
        if (i % 3 === 0) g.fillStyle(0x5d3a1e, 1).fillEllipse(x + ((i * 13) % 20) - 10, base - h + 16, 10, 30);
      }
    }
    const label = kind === 'thorns' ? 'Blocked by\nthorns' : kind === 'barricade' ? 'Barricaded' : 'Flooded';
    addText(this, side === 'right' ? width - 50 : 50, top - 290, label, { size: 15, bold: true, align: 'center', color: COLORS.textDim, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH - 1);
  }

  private bumpWall(side: 'left' | 'right'): void {
    if (this.time.now < this.wallBumpAt) return;
    this.wallBumpAt = this.time.now + 2000;
    sfx.play(this, 'denied');
    this.cameras.main.shake(120, 0.004);
    const kind = this.room.exits.find((e) => e.side === side)?.blocker ?? 'thorns';
    const text =
      kind === 'barricade'
        ? 'The road is barricaded. The Elder will know why.'
        : kind === 'reeds'
          ? 'Deep water and thick reeds block the way.'
          : 'A wall of thorns blocks the way.';
    toast(this, text, COLORS.textDim);
  }

  private spawnBoss(): void {
    const b = this.room.boss;
    if (!b || !questStarted(session.player, b.questId)) return;
    const def = db.monster(b.monsterId);
    // Bosses face the middle of the room, where the player comes from.
    const faceLeft = b.x > this.scale.width / 2;
    const fig = drawFigure(this, b.x, b.y, def.look, faceLeft ? -1 : 1);
    const actor = this.makeActor(fig, b.x, b.y, def.look.scale);
    actor.facing = 1;
    const label = addText(this, b.x, 0, `${def.name}  ·  Boss`, { size: 18, bold: true, color: COLORS.textBad, stroke: true }).setOrigin(0.5, 1);
    this.boss = { ...actor, def, label };
    this.place(this.boss);
    const aura = this.add.image(b.x, b.y - 120, 'fx_soft').setTint(0x7dff9a).setBlendMode('ADD').setScale(9).setAlpha(0.25).setDepth(b.y - 1);
    this.tweens.add({ targets: aura, alpha: 0.45, scale: 10, duration: 1400, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
    const hit = this.add
      .rectangle(b.x, b.y - actor.height * actor.scale * 0.5, 260, actor.height * actor.scale, 0xffffff, 0)
      .setInteractive({ useHandCursor: true })
      .setDepth(b.y + 1);
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      if (this.nearestTarget() === 'boss') this.confirmBoss();
      else this.moveTarget = { x: b.x + (faceLeft ? -170 : 170), y: b.y };
    });
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
    this.charBtn = btn(156, 'Character', () => this.openCharacter());
    this.invBtn = btn(214, 'Inventory', () => this.toggleInventory());
    this.soundBtn = new Button(this, right - 70, 272, sfx.isMuted() ? 'Sound: off' : 'Sound: on', () => this.toggleSound(), {
      width: 140,
      height: 48,
      fontSize: 17,
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

    this.tracker = this.add.container(16, 176).setDepth(UI_DEPTH);

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
    const exitL = this.room.exits.find((e) => e.side === 'left' && this.exitOpen(e));
    const exitR = this.room.exits.find((e) => e.side === 'right' && this.exitOpen(e));
    if (exitR && p.x > width - EDGE / 2) return this.leave('right');
    if (exitL && p.x < EDGE / 2) return this.leave('left');
    const wallR = !exitR && this.room.exits.some((e) => e.side === 'right');
    const wallL = !exitL && this.room.exits.some((e) => e.side === 'left');
    const maxX = exitR ? width : wallR ? width - 110 : width - EDGE;
    const minX = exitL ? 0 : wallL ? 110 : EDGE;
    if (wallR && p.x >= maxX && vx > 0) this.bumpWall('right');
    if (wallL && p.x <= minX && vx < 0) this.bumpWall('left');
    this.clampToWalk(p, minX, maxX);
    this.place(p, time);
    if (this.boss) {
      this.place(this.boss, time);
      const dx = Math.abs(this.boss.x - p.x);
      if (dx > 280) this.bossAsked = false;
      else if (!blocked && !this.bossAsked && dx < 130 && Math.abs(this.boss.y - p.y) < 70) {
        this.bossAsked = true;
        this.moveTarget = null;
        this.confirmBoss();
      }
    }
  }

  private updateNpc(npc: Npc, time: number): void {
    // Turn toward the player when they're close.
    if (Math.abs(npc.x - this.player.x) < 260) npc.facing = this.player.x < npc.x ? -1 : 1;
    this.place(npc, time);
    const marker = this.markers.get(npc);
    if (marker) marker.setPosition(npc.x, npc.label.y - npc.label.height - 8 + Math.sin(time * 0.005) * 5);
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
    this.placeEliteMarks(m);

    if (!blocked && !safe && Math.abs(dx) < TOUCH_RANGE.x && Math.abs(dy) < TOUCH_RANGE.y) this.startBattle(m);
  }

  private updatePrompt(blocked: boolean): void {
    const near = blocked ? null : this.nearestTarget();
    if (!near) {
      this.prompt.setVisible(false);
      return;
    }
    if (near === 'boss') {
      this.promptText.setText('E  Fight');
      this.prompt.setVisible(true).setPosition(this.boss!.x, this.boss!.label.y - this.boss!.label.height - 22);
      return;
    }
    const q = questForNpc(session.player, near.def.id);
    const verb =
      q?.status === 'ready' ? 'Turn in' : q?.status === 'available' ? 'Quest' : near.def.role.kind === 'shop' ? 'Shop' : near.def.role.kind === 'inn' ? 'Rest' : 'Talk';
    this.promptText.setText(`E  ${verb}`);
    const marker = this.markers.get(near);
    const above = marker?.visible ? marker.y - 46 : near.label.y - near.label.height - 22;
    this.prompt.setVisible(true).setPosition(near.x, above);
  }

  /** Closest thing the player can interact with: an NPC, or the boss. */
  private nearestTarget(): Npc | 'boss' | null {
    if (this.boss && Math.abs(this.boss.x - this.player.x) < 280 && Math.abs(this.boss.y - this.player.y) < 110) return 'boss';
    return this.nearestNpc();
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
      monsters: this.mobs.map((x) => ({ uid: x.uid, monsterId: x.def.id, elite: x.elite, x: x.x, y: x.y })),
    });
    this.saveLocation();

    sfx.play(this, m.elite ? 'warning' : 'encounter');
    fx.whiteFlash(this, m.fig, 140);
    fx.hitSparks(this, (m.x + this.player.x) / 2, m.y - 80, false, true);
    const cam = this.cameras.main;
    cam.shake(250, 0.012);
    cam.flash(180, 255, 255, 255);
    cam.pan((m.x + this.player.x) / 2, m.y - 120, 450, 'Cubic.easeIn');
    cam.zoomTo(1.6, 450, 'Cubic.easeIn');
    cam.rotateTo(0.08, false, 450);
    const data: BattleSceneData = { zoneId, monsterId: m.def.id, elite: m.elite, roomId: this.room.id, x: this.player.x, y: this.player.y };
    this.time.delayedCall(380, () => goTo(this, SceneKeys.Battle, data, 200));
  }

  // --- NPCs and menus ---

  private refresh = (): void => {
    this.hud.refresh(session.player);
    this.redrawPlayerIfRearmed();
    if (this.tracker) this.refreshQuests();
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
    openCharacterPanel(this, session.player, this.refresh, () => this.toggleInventory());
  }

  /** I opens the inventory, and closes it again. */
  private toggleInventory(): void {
    if (this.inventory) {
      this.inventory.close();
      return;
    }
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    this.inventory = openInventory(this, session.player, this.refresh);
    this.inventory.once(Phaser.GameObjects.Events.DESTROY, () => (this.inventory = null));
  }

  private interact(npc: Npc, intro = false): void {
    if (this.modalOpen()) return;
    const player = session.player;
    const role = npc.def.role;
    const actions: DialogueAction[] = [];
    let lines = npc.def.dialogue;

    const q = questForNpc(player, npc.def.id);
    if (q?.status === 'ready') {
      const quest = q.quest;
      lines = quest.complete;
      actions.push({ label: 'Complete', run: (close) => (close(), this.completeQuest(quest, npc)) });
      this.moveTarget = null;
      this.tweens.add({ targets: npc.fig, y: npc.fig.y - 10, duration: 100, yoyo: true });
      openDialogue(this, npc.def, actions, lines);
      return;
    }
    if (q?.status === 'available') {
      const quest = q.quest;
      lines = intro ? [...npc.def.dialogue, ...quest.offer] : quest.offer;
      actions.push({
        label: 'Accept',
        run: (close) => {
          close();
          acceptQuest(player, quest);
          sfx.play(this, 'quest');
          toast(this, `New quest: ${quest.name}`, COLORS.textGold);
          this.refreshQuests();
          this.save(false);
        },
      });
    } else if (q?.status === 'active') {
      const count = player.data.quests[q.quest.id]?.count ?? 0;
      lines = [`${q.quest.reminder}  (${q.quest.goal}: ${count}/${q.quest.objective.count})`];
    }
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

    openDialogue(this, npc.def, actions, lines);
  }

  // --- Quests ---

  /** Update NPC markers, the quest tracker, and the stat-point hint on the Character button. */
  private refreshQuests(): void {
    const player = session.player;
    for (const npc of this.npcs) {
      const q = questForNpc(player, npc.def.id);
      const symbol = q?.status === 'ready' ? '?' : q?.status === 'available' ? '!' : '';
      let marker = this.markers.get(npc);
      if (!marker) {
        marker = addText(this, npc.x, 0, '', { size: 40, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5, 1);
        marker.setDepth(UI_DEPTH - 3);
        this.markers.set(npc, marker);
      }
      marker.setText(symbol).setVisible(symbol !== '');
    }

    this.tracker.removeAll(true);
    const quests = activeQuests(player);
    if (quests.length > 0) {
      const rows = quests.slice(0, 3).map((q) =>
        q.status === 'ready'
          ? `${q.quest.name}: done! Return to ${this.giverName(q.quest)}`
          : `${q.quest.name}: ${q.quest.goal} ${q.count}/${q.quest.objective.count}`,
      );
      const text = addText(this, 12, 8, rows.join('\n'), { size: 16, color: COLORS.text }).setLineSpacing(6);
      const bg = this.add
        .rectangle(0, 0, Math.max(340, text.width + 24), text.height + 16, COLORS.panel, 0.8)
        .setOrigin(0)
        .setRounded(8)
        .setStrokeStyle(2, COLORS.panelBorder);
      this.tracker.add([bg, text]);
    }

    const points = player.data.statPoints;
    this.charBtn.setLabel(points > 0 ? `Character (${points})` : 'Character');
    const fresh = player.data.gear.filter((g) => g.isNew).length;
    this.invBtn.setLabel(fresh > 0 ? `Inventory (${fresh})` : 'Inventory');
    this.tweens.killTweensOf(this.charBtn);
    this.charBtn.setScale(1);
    if (points > 0) this.tweens.add({ targets: this.charBtn, scale: 1.07, duration: 500, yoyo: true, repeat: -1 });
  }

  private giverName(quest: QuestDef): string {
    return ROOMS.flatMap((r) => r.npcs).find((n) => n.id === quest.giver)?.name ?? 'the quest giver';
  }

  private completeQuest(quest: QuestDef, npc: Npc): void {
    const player = session.player;
    const result = turnInQuest(player, quest);
    if (!result) return;
    sfx.play(this, 'quest');
    const { rewards, levelsGained, newSkills } = result;
    toast(this, `Quest complete: ${quest.name}`, COLORS.textGold);
    const items = rewards.items.map((g) => lootName(db.item(g.itemId), g));
    toast(this, [`+${rewards.xp} XP`, `+${rewards.gold} gold`, ...items].join('   '), COLORS.textGood);
    void fx.coinShower(this, { x: npc.x, y: npc.y - 120 }, { x: 320, y: 40 }, Math.ceil(rewards.gold / 6), () => sfx.play(this, 'coin'));
    if (levelsGained > 0) {
      this.time.delayedCall(500, () => {
        fx.levelUpBurst(this, this.player.x, this.player.y);
        sfx.play(this, 'levelup');
        toast(this, `Level up! Now level ${player.level}. +${levelsGained * BALANCE.statPointsPerLevel} stat points (C)`, COLORS.textGood);
        newSkills.forEach((s) => toast(this, `New skill: ${s.name}`, COLORS.textMagic));
      });
    }
    this.refresh();
    this.refreshQuests();
    if (quest.endsChapter) this.time.delayedCall(1400, () => this.chapterComplete(quest.endsChapter!));
  }

  private chapterComplete(title: string): void {
    const { width, height } = this.scale;
    const layer = this.add.container(0, 0).setDepth(6500);
    const shade = this.add.rectangle(0, 0, width, height, 0x000000, 0.7).setOrigin(0).setInteractive();
    const head = addText(this, width / 2, 220, 'Chapter Complete!', { size: 72, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5);
    const sub = addText(this, width / 2, 300, title, { size: 30, color: COLORS.text }).setOrigin(0.5);
    const note = addText(this, width / 2, 370, 'Thanks for playing this early build. More chapters are on the way.\nYou can keep exploring, leveling and shopping.', {
      size: 20,
      align: 'center',
      color: COLORS.textDim,
    }).setOrigin(0.5);
    layer.add([shade, head, sub, note]);
    head.setScale(0.3);
    this.tweens.add({ targets: head, scale: 1, duration: 600, ease: 'Back.easeOut' });
    sfx.play(this, 'victory');
    fx.confetti(this);
    for (let i = 0; i < 6; i++) {
      this.time.delayedCall(i * 350, () => {
        const x = Phaser.Math.Between(200, width - 200);
        const y = Phaser.Math.Between(120, 420);
        fx.explosion(this, x, y, [0xffffff, Phaser.Utils.Array.GetRandom([0xffd75e, 0x7ec8ff, 0xff7b9c, 0x7dff9a]), 0xffffff]);
        sfx.play(this, 'explode');
      });
    }
    const btn = new Button(this, width / 2, 480, 'Continue', () => layer.destroy(), { width: 240, height: 60 });
    layer.add(btn);
  }

  // --- Boss ---

  private confirmBoss(): void {
    if (!this.boss || this.modalOpen()) return;
    const def = this.boss.def;
    const rematch = !questNeedsKill(session.player, def.id);
    const modal = new Modal(this, 700, 300, rematch ? `${def.name} (rematch)` : def.name);
    const intro = this.room.boss?.intro ?? 'It turns toward you.';
    const text = rematch
      ? `It is back. There is no running from this fight.\nRematch: ${Math.round(BALANCE.bossRepeatRewards * 100)}% XP and gold, full loot (Sigils drop more often from bosses).`
      : `${intro}\nThere is no running from this fight.`;
    modal.content.add(addText(this, 350, 110, text, { size: rematch ? 19 : 22, align: 'center', wrap: 640 }).setOrigin(0.5));
    modal.content.add(new Button(this, 230, 230, 'Fight', () => (modal.close(), this.startBossBattle()), { width: 220 }));
    modal.content.add(new Button(this, 470, 230, 'Not yet', () => modal.close(), { width: 220 }));
  }

  private startBossBattle(): void {
    if (!this.boss || this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    session.saveRoom({ roomId: this.room.id, fightingUid: -1, monsters: [] });
    this.saveLocation();
    sfx.play(this, 'encounter');
    const cam = this.cameras.main;
    cam.shake(500, 0.015);
    cam.flash(300, 255, 60, 60);
    cam.zoomTo(1.4, 600, 'Cubic.easeIn');
    const data: BattleSceneData = { zoneId: this.room.zoneId!, monsterId: this.boss.def.id, roomId: this.room.id, x: this.player.x, y: this.player.y };
    this.time.delayedCall(600, () => goTo(this, SceneKeys.Battle, data, 300));
  }
}
