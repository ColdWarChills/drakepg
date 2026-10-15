import Phaser from 'phaser';
import { BALANCE, COLORS, DESIGN } from '../config';
import { db } from '../data/db';
import { ELEMENT_LABELS } from '../data/elements';
import { ROOMS, START } from '../data/world';
import type { Blocker, InspectDef, MonsterDef, NpcDef, QuestDef, RoomDef, RoomExit } from '../data/types';
import { cheats } from '../game/cheats';
import { featureOn, markSeen, unseenNews } from '../game/Features';
import { makeElite, rollElite } from '../game/Elites';
import { activeHunts, anyHuntReady, huntGoal, huntWantsElite, rankId, type TurnInHunt } from '../game/Guild';
import { isLucky, makeImpling, rollLucky } from '../game/Lucky';
import { Player } from '../game/Player';
import { acceptQuest, activeQuests, questForNpc, questNeedsKill, questProgress, questStatus, questWaitingOnRank, questWantsElite, turnInQuest } from '../game/Quests';
import { saveSettings, settings } from '../game/settings';
import { SaveSystem } from '../game/SaveSystem';
import { RECALL_SCROLL, exitOpen, isGuard, recallCheck, roomBoss, useRecall, visitRoom } from '../game/Travel';
import { defaultRng, weightedPick } from '../game/rng';
import { MIMIC_UID, rollChest, rollChestLoot } from '../game/Chests';
import { session, type Chest, type RoamingMonster } from '../game/session';
import { drawScenery } from '../ui/backdrops';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { Hud } from '../ui/Hud';
import { Modal } from '../ui/Modal';
import { openCheatMenu } from '../ui/cheatMenu';
import { openForge } from '../ui/forge';
import { openGuild } from '../ui/guild';
import { openInventory, openShop } from '../ui/inventory';
import { lootName } from '../ui/itemUi';
import { openMap } from '../ui/mapPanel';
import { openCharacterPanel, openDialogue, openNarration, type DialogueAction } from '../ui/panels';
import { drawCampfire, drawProp } from '../ui/props';
import { addText, isTouchScreen, labelScale, toast } from '../ui/text';
import { addOverlayCamera, shake, zoomTo } from '../ui/view';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type RoomSceneData } from './keys';

/** Characters are drawn a bit smaller on maps than in battle. */
const MAP_SCALE = 0.85;
const EDGE = 36;
const TALK_RANGE = { x: 130, y: 80 };
const TOUCH_RANGE = { x: 62, y: 34 };
/** Roaming monsters keep at least this far apart (x), unless they are on clearly different rows (y), so labels stay readable. */
const MOB_GAP = { x: 150, y: 70 };
const UI_DEPTH = 3000;
/** Gold glow around Elites. */
const ELITE_GLOW = 0xffc23d;
/** Lucky Implings: mint tag and glow, gold twinkles. */
const LUCKY_GLOW = 0x7dffb0;
const LUCKY_CSS = '#8dffbe';

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
  /** Gold twinkles around a Lucky Impling. */
  sparkle?: Phaser.GameObjects.Particles.ParticleEmitter;
  /** `run`: a Lucky Impling running from you. */
  state: 'idle' | 'wander' | 'alert' | 'chase' | 'run';
  timer: number;
  target: { x: number; y: number } | null;
}

/** Things in a room you walk up to and use with E: a campfire, or something to look at. */
type Thing = { kind: 'campfire'; x: number; y: number } | { kind: 'inspect'; def: InspectDef; x: number; y: number; fig: Phaser.GameObjects.Container };

type Keys = Record<'W' | 'A' | 'S' | 'D' | 'UP' | 'DOWN' | 'LEFT' | 'RIGHT', Phaser.Input.Keyboard.Key>;

let showedControls = false;

/** A walkable screen: move with WASD/arrows or by clicking, talk to NPCs, bump into monsters to fight. */
/** Shown over a closed exit. */
const BLOCKER_LABELS: Record<Blocker, string> = {
  thorns: 'Blocked by\nthorns',
  barricade: 'Barricaded',
  reeds: 'Flooded',
  snowdrift: 'Snowed in',
};

/** Toast when you walk into a closed exit. */
const BLOCKER_BUMP: Record<Blocker, string> = {
  thorns: 'A wall of thorns blocks the way.',
  barricade: 'The road is barricaded. The Elder will know why.',
  reeds: 'Deep water and thick reeds block the way.',
  snowdrift: 'Snow is piled higher than your head. Someone in town might know a way through.',
};

export class RoomScene extends Phaser.Scene {
  private room!: RoomDef;
  private player!: Actor;
  /** Weapon and armor uids the player figure was drawn with. */
  private playerGear = '';
  private npcs: Npc[] = [];
  private mobs: Mob[] = [];
  private keys!: Keys;
  private hud!: Hud;
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
  /** Back or down exit the player was sent walking to (click/tap); entered on arrival. */
  private pendingExit: RoomExit | null = null;
  /** Campfire or note the player was sent walking to (click/tap); used on arrival. */
  private pendingThing: Thing | null = null;
  private things: Thing[] = [];
  private mapModal: Modal | null = null;
  /** A room's first-visit lines playing over the scene (blocks movement like a modal). */
  private cutscene: { next: () => void; skip: () => void } | null = null;
  private safeUntil = 0;
  private leaving = false;
  private dustTimer = 0;
  private nextUid = 1;
  /** Next time to look for unannounced unlocks and skills. */
  private newsAt = 0;
  private chest: { data: Chest; fig: Phaser.GameObjects.Container; hit: Phaser.GameObjects.Rectangle } | null = null;
  /** Name labels grow on small screens (see labelScale). */
  private ls = 1;

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
    this.pendingExit = null;
    this.pendingThing = null;
    this.things = [];
    this.mapModal = null;
    this.cutscene = null;
    this.chest = null;
    this.safeUntil = 0;
    this.leaving = false;
    this.dustTimer = 0;
    this.ls = labelScale(this);

    const [top] = this.room.walk;
    drawScenery(this, this.room.background, this.room.backdrop, top - 10);
    for (const kind of this.room.ambient ?? []) fx.ambient(this, [kind], kind === 'dust' || kind === 'mist' ? 1 : 1500);
    this.drawExits();
    // A fight in the room doesn't end the visit; walking in again does (one campfire rest per visit).
    if (data.via !== 'battle') session.restedAt = null;
    this.drawThings();

    // Some people only show up once their chapter unlocks them (Hulda and her forge).
    for (const def of this.room.npcs) if (!def.feature || featureOn(session.player, def.feature)) this.addNpc(def);
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
    kb.on('keydown-M', () => this.toggleMap());
    // Hidden testing menu. On touch screens: tap the room name 5 times.
    kb.on('keydown-BACKTICK', () => this.openCheats());
    const talk = () => {
      if (this.cutscene) return this.cutscene.next();
      if (this.modalOpen() || this.leaving) return;
      const target = this.nearestTarget();
      if (target === 'boss') this.confirmBoss();
      else if (target) this.interact(target);
      else {
        const thing = this.nearestThing();
        if (thing) this.useThing(thing);
      }
    };
    kb.on('keydown-E', talk);
    kb.on('keydown-SPACE', talk);
    this.input.on('pointerdown', this.onPointerDown, this);

    const firstVisit = visitRoom(session.player, this.room.id);
    this.saveLocation();
    if (data.via === 'spawn' && data.recalled) {
      sfx.play(this, 'recall');
      fx.healSparkles(this, this.player.x, this.player.y, 0xffe9a8);
      toast(this, `You arrive in ${this.room.name}.`, COLORS.textGold);
    }
    if (firstVisit && this.room.firstVisit && data.via !== 'battle') {
      // Wait out any popup, then play the room's opening lines.
      const play = () => (this.modalOpen() ? this.time.delayedCall(300, play) : this.playFirstVisit());
      this.time.delayedCall(600, play);
    }
    if (data.via === 'spawn' && data.intro) {
      const elder = this.npcs.find((n) => n.def.role.kind === 'talk');
      // Wait out the "New!" popup (a new hero's first skill) so the greeting isn't skipped.
      const greet = (npc: Npc) => (this.modalOpen() ? this.time.delayedCall(300, () => greet(npc)) : this.interact(npc, true));
      if (elder) this.time.delayedCall(500, () => greet(elder));
    }
  }

  // --- Setup ---

  private spawnPlayer(data: RoomSceneData): void {
    const { width } = DESIGN;
    let x: number;
    let y: number;
    if (data.via === 'exit' && data.side === 'back') {
      // Step out of the doorway, just below it.
      x = data.x ?? width / 2;
      y = this.room.walk[0] + 24;
    } else if (data.via === 'exit' && data.side === 'down') {
      // Climb back up from the path at the bottom edge.
      x = data.x ?? width / 2;
      y = this.room.walk[1] - 24;
    } else if (data.via === 'exit') {
      x = data.side === 'left' ? EDGE + 50 : width - EDGE - 50;
      y = data.y;
    } else {
      x = data.x;
      y = data.y;
    }
    const look = session.player.look;
    const fig = drawFigure(this, x, y, look, 1, session.player.weapon, session.player.weaponSigil);
    this.player = this.makeActor(fig, x, y, look.scale);
    this.playerGear = this.gearKey();
    if (data.via === 'exit') this.player.facing = data.side === 'right' ? -1 : 1;
    this.clampToWalk(this.player);
    this.place(this.player);
  }

  private spawnMonsters(data: RoomSceneData): void {
    const room = this.room;
    if (!room.zoneId) return;
    const memory = session.roomMemory(room.id);

    let roaming: RoamingMonster[] | null = null;
    let chest: Chest | null = null;
    if (data.via === 'battle') {
      const snap = session.takeRoom(room.id);
      if (snap) {
        // Beaten, or ran away (a Lucky Impling): either way it's gone.
        const won = data.outcome === 'victory';
        const gone = won || data.outcome === 'escaped';
        roaming = snap.monsters.filter((m) => !(gone && m.uid === snap.fightingUid));
        this.nextUid = Math.max(0, ...snap.monsters.map((m) => m.uid)) + 1;
        // A beaten mimic is gone; a chest you didn't touch is still there.
        chest = won && snap.fightingUid === MIMIC_UID ? null : (memory?.chest ?? null);
        if (data.outcome === 'fled') {
          this.safeUntil = this.time.now + BALANCE.fleeGraceSeconds * 1000;
          this.blinkPlayer();
        }
        if (data.outcome === 'escaped') this.safeUntil = this.time.now + BALANCE.fleeGraceSeconds * 1000;
      }
    }

    if (!roaming && memory) {
      // Back in a room you cleared: the dead stay dead, and one monster wanders back per `respawnSeconds` away.
      roaming = memory.monsters.map((m) => ({ ...m }));
      chest = memory.chest;
      this.nextUid = Math.max(0, ...roaming.map((m) => m.uid)) + 1;
      const back = Math.floor((Date.now() - memory.leftAt) / (BALANCE.respawnSeconds * 1000));
      const missing = Math.max(0, (room.monsterCount ?? 0) - roaming.length);
      for (let i = 0; i < Math.min(back, missing); i++) roaming.push(this.newRoamer(roaming));
      // Fully refilled: another chance at a chest.
      if (!chest && missing > 0 && back >= missing) chest = this.newChest(roaming);
    }

    if (!roaming) {
      roaming = [];
      for (let i = 0; i < (room.monsterCount ?? 0); i++) roaming.push(this.newRoamer(roaming));
      chest = this.newChest(roaming, true);
      // Testing: a new character's first monster room has a Lucky Impling.
      if (session.luckyNext && roaming.length && !roaming.some((m) => isLucky(m.monsterId))) {
        session.luckyNext = false;
        roaming[roaming.length - 1] = { ...roaming[roaming.length - 1], monsterId: 'lucky_impling', elite: undefined };
      }
    }
    // Give some room: nobody stays right next to you when you arrive or come back from a fight.
    for (const m of roaming) {
      if (Math.abs(m.x - this.player.x) < 260) {
        const away = m.x < this.player.x ? -1 : 1;
        const x = this.player.x + away * 260;
        m.x = x < 80 || x > DESIGN.width - 80 ? this.player.x - away * 260 : x;
      }
    }
    for (const r of roaming) this.addMob(r);
    if (chest) this.addChest(chest);
  }

  /** A freshly spawned roaming monster (maybe an Elite, at most `BALANCE.elite.maxPerRoom` per room). */
  private newRoamer(taken: RoamingMonster[]): RoamingMonster {
    const zone = db.zone(this.room.zoneId!);
    const pos = this.findSpawnPoint(taken);
    // Monsters are visible, so tougher ones can appear; their level label warns you.
    // Now and then a Lucky Impling instead (at most one per room).
    if (!taken.some((m) => isLucky(m.monsterId)) && rollLucky()) return { uid: this.nextUid++, monsterId: 'lucky_impling', ...pos };
    const monsterId = weightedPick(defaultRng, zone.encounters, (e) => e.weight).monsterId;
    const elites = taken.filter((m) => m.elite).length;
    const elitesOn = featureOn(session.player, 'elites') || cheats.elites;
    const elite = elitesOn && (elites < BALANCE.elite.maxPerRoom || cheats.elites) ? rollElite(defaultRng, questWantsElite(session.player) || huntWantsElite(session.player)) : undefined;
    return { uid: this.nextUid++, monsterId, elite, ...pos };
  }

  /** Maybe a treasure chest, placed away from the monsters. A side room's own chest is always there on its first fill. */
  private newChest(taken: RoamingMonster[], first = false): Chest | null {
    const player = session.player;
    if (!featureOn(player, 'chests')) return null;
    if (first && this.room.chest) return { ...this.room.chest, mimic: false };
    const roll = rollChest();
    return roll ? { ...this.findSpawnPoint(taken), mimic: roll.mimic && featureOn(player, 'mimics') } : null;
  }

  /** Keep this room's survivors and chest for when you come back. */
  private rememberRoom(): void {
    if (!this.room.zoneId) return;
    session.rememberRoom(this.room.id, {
      monsters: this.mobs.map((x) => ({ uid: x.uid, monsterId: x.def.id, elite: x.elite, x: x.x, y: x.y })),
      chest: this.chest?.data ?? null,
      leftAt: Date.now(),
    });
  }

  // --- Treasure chests ---

  /** A drawn chest. Mimics give themselves away a little: they breathe, and now and then their lid twitches. */
  private addChest(data: Chest): void {
    const fig = this.add.container(data.x, data.y).setDepth(data.y);
    const shadow = this.add.ellipse(0, 0, 90, 18, 0x000000, 0.35);
    const box = this.add.rectangle(0, -26, 76, 46, 0x8d5a2b).setStrokeStyle(3, 0x3e2723);
    const band = this.add.rectangle(0, -26, 76, 8, 0xd4a017);
    const lid = this.add.container(0, -52);
    lid.add([this.add.rectangle(0, 0, 80, 22, 0x9c6631).setStrokeStyle(3, 0x3e2723), this.add.rectangle(0, 0, 80, 6, 0xd4a017)]);
    const lock = this.add.rectangle(0, -40, 12, 14, 0xf1c40f).setStrokeStyle(2, 0x3e2723);
    fig.add([shadow, box, band, lid, lock]);
    if (data.mimic) {
      const teeth = this.add.graphics();
      teeth.fillStyle(0xf5f0e1, 1);
      for (let i = -3; i <= 3; i++) teeth.fillTriangle(i * 10 - 4, -41, i * 10, -34, i * 10 + 4, -41);
      teeth.setAlpha(0);
      fig.addAt(teeth, 3);
      this.tweens.add({ targets: fig, scaleY: 1.04, duration: 1100, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
      this.time.addEvent({
        delay: 3200,
        loop: true,
        callback: () => {
          if (!fig.active || Math.abs(this.player.x - data.x) > 360) return;
          teeth.setAlpha(1);
          this.tweens.add({ targets: lid, y: -60, angle: -8, duration: 120, yoyo: true, onComplete: () => teeth.setAlpha(0) });
        },
      });
    } else {
      this.time.addEvent({
        delay: 2600,
        loop: true,
        callback: () => fig.active && fx.hitSparks(this, data.x + 20, data.y - 50, false, false),
      });
    }
    const hit = this.add.rectangle(data.x, data.y - 35, 100, 80, 0xffffff, 0).setInteractive({ useHandCursor: true }).setDepth(data.y + 1);
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      this.pendingNpc = null;
      this.pendingExit = null;
      this.moveTarget = { x: data.x + (this.player.x < data.x ? -40 : 40), y: data.y };
    });
    this.chest = { data, fig, hit };
  }

  /** "New!": systems this chapter just opened, and skills just learned. Shown once each. */
  private showNews(): void {
    const player = session.player;
    const news = unseenNews(player);
    if (!news.features.length && !news.skills.length) return;
    markSeen(player, news);
    this.save(false);
    const rows: { title: string; text: string; color: string }[] = [
      ...news.features.map((f) => ({ title: f.name, text: f.text, color: COLORS.textGold })),
      ...news.skills.map((s) => {
        const element = s.element && s.element !== 'weapon' ? ` · ${ELEMENT_LABELS[s.element]}` : '';
        const cd = s.cooldown > 0 ? ` · cooldown ${s.cooldown}` : '';
        const slot = player.isCarried(s.id) ? '' : ` Your ${BALANCE.skillSlots} skill slots are full: carry it from the Character panel (C).`;
        return { title: `New skill: ${s.name}  (${s.mpCost} MP${element}${cd})`, text: `${s.description}${slot}`, color: COLORS.textMagic };
      }),
    ];
    sfx.play(this, 'quest');
    const height = Math.min(640, 150 + rows.length * 84);
    const modal = new Modal(this, 760, height, 'New!', () => this.refresh());
    let y = 80;
    for (const r of rows) {
      modal.content.add(addText(this, 40, y, r.title, { size: 20, bold: true, color: r.color }));
      const t = addText(this, 40, y + 28, r.text, { size: 16, wrap: 680 });
      modal.content.add(t);
      y += 40 + t.height + 10;
    }
    modal.content.add(new Button(this, 380, height - 44, 'Got it', () => modal.close(() => this.refresh()), { width: 180, height: 48 }));
  }

  /** Walking into a chest opens it. */
  private checkChest(): void {
    const c = this.chest;
    if (!c || this.modalOpen()) return;
    if (Math.abs(this.player.x - c.data.x) > 56 || Math.abs(this.player.y - c.data.y) > 34) return;
    this.chest = null;
    c.hit.destroy();
    if (c.data.mimic) return this.springMimic(c.fig, c.data);

    const player = session.player;
    const rewards = rollChestLoot(db.zone(this.room.zoneId!), undefined, featureOn(player, 'sigils'), featureOn(player, 'materials'));
    const { lost } = player.grantRewards(rewards);
    sfx.play(this, 'quest');
    this.tweens.add({ targets: c.fig.list[3], y: -70, angle: -25, duration: 220 });
    fx.healSparkles(this, c.data.x, c.data.y, 0xffd34d);
    this.tweens.add({ targets: c.fig, alpha: 0, delay: 900, duration: 400, onComplete: () => c.fig.destroy() });
    const items = rewards.items.filter((g) => !lost.includes(g));
    // Stacks read as "Iron Scrap x2".
    const counts = new Map<string, number>();
    const names: string[] = [];
    for (const g of items) {
      const def = db.item(g.itemId);
      if (g.rolls) names.push(lootName(def, g));
      else counts.set(def.name, (counts.get(def.name) ?? 0) + 1);
    }
    for (const [name, n] of counts) names.push(n > 1 ? `${name} x${n}` : name);
    toast(this, 'Treasure!', COLORS.textGold);
    toast(this, [`+${rewards.gold} gold`, ...names].join('   '), COLORS.textGood);
    if (lost.length) toast(this, `Bag full! ${lost.length} left behind`, COLORS.textBad);
    void fx.coinShower(this, { x: c.data.x, y: c.data.y - 60 }, { x: 320, y: 40 }, Math.ceil(rewards.gold / 8), () => sfx.play(this, 'coin'));
    this.refresh();
  }

  /** It was a mimic: it jumps up, and the fight starts. */
  private springMimic(fig: Phaser.GameObjects.Container, data: Chest): void {
    if (this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    sfx.play(this, 'warning');
    toast(this, 'The chest has teeth!', COLORS.textBad);
    shake(this, 300, 0.012);
    this.tweens.add({ targets: fig, y: data.y - 30, duration: 160, yoyo: true, repeat: 1 });
    // Remember the room with the mimic still in it, in case you run away.
    session.saveRoom({
      roomId: this.room.id,
      fightingUid: MIMIC_UID,
      monsters: this.mobs.map((x) => ({ uid: x.uid, monsterId: x.def.id, elite: x.elite, x: x.x, y: x.y })),
    });
    this.chest = { data, fig, hit: this.add.rectangle(0, 0, 1, 1) };
    this.rememberRoom();
    this.saveLocation();
    const back = { roomId: this.room.id, x: this.player.x, y: this.player.y };
    const battle: BattleSceneData = { zoneId: this.room.zoneId!, monsterId: 'mimic', mimic: true, ...back };
    this.time.delayedCall(650, () => goTo(this, SceneKeys.Battle, battle, 200));
  }

  /** A spot away from the player and from `taken` (monsters placed so far). Falls back to the roomiest try. */
  private findSpawnPoint(taken: { x: number; y: number }[]): { x: number; y: number } {
    const [top, bottom] = this.room.walk;
    let best = { x: 640, y: (top + bottom) / 2 };
    let bestScore = -Infinity;
    for (let i = 0; i < 40; i++) {
      const p = { x: Phaser.Math.Between(180, DESIGN.width - 140), y: Phaser.Math.Between(top + 10, bottom - 10) };
      const fromPlayer = Math.abs(p.x - this.player.x);
      const fromOthers = Math.min(Infinity, ...taken.map((m) => Math.abs(m.x - p.x)));
      if (fromPlayer > 320 && fromOthers > MOB_GAP.x) return p;
      const score = Math.min(fromPlayer, 320) + Math.min(fromOthers, MOB_GAP.x * 2);
      if (score > bestScore) {
        best = p;
        bestScore = score;
      }
    }
    return best;
  }

  /** Push roaming monsters that crowd each other apart, a little each frame. */
  private separateMobs(dt: number): void {
    const push = 90 * dt;
    for (let i = 0; i < this.mobs.length; i++) {
      for (let j = i + 1; j < this.mobs.length; j++) {
        const a = this.mobs[i];
        const b = this.mobs[j];
        const dx = b.x - a.x;
        if (Math.abs(dx) >= MOB_GAP.x || Math.abs(b.y - a.y) >= MOB_GAP.y) continue;
        const dir = dx === 0 ? (a.uid < b.uid ? 1 : -1) : Math.sign(dx);
        // Chasers keep their pace; only the calmer one yields.
        if (a.state !== 'chase') a.x -= dir * push;
        if (b.state !== 'chase') b.x += dir * push;
      }
    }
  }

  private gearKey(): string {
    const { weapon, body } = session.player.data.equipment;
    return `${weapon?.uid ?? ''}|${body?.uid ?? ''}`;
  }

  /** The hero holds the equipped weapon and wears the armor's outfit, so redraw them after a gear swap. */
  private redrawPlayerIfRearmed(): void {
    const key = this.gearKey();
    if (key === this.playerGear) return;
    this.playerGear = key;
    const old = this.player.fig;
    this.player.fig = drawFigure(this, this.player.x, this.player.y, session.player.look, 1, session.player.weapon, session.player.weaponSigil);
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
    const label = addText(this, def.x, 0, def.name, { size: Math.round(17 * this.ls), bold: true, align: 'center', stroke: true })
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
    const base = isLucky(r.monsterId) ? makeImpling(db.zone(this.room.zoneId!)) : db.monster(r.monsterId);
    const def = r.elite ? makeElite(base, r.elite) : base;
    const fig = drawFigure(this, r.x, r.y, def.look, 1);
    const actor = this.makeActor(fig, r.x, r.y, def.look.scale * 0.85);
    const diff = def.level - session.player.level;
    const color = diff >= 2 ? COLORS.textBad : diff === 1 ? COLORS.textGold : diff <= -2 ? COLORS.textDim : COLORS.text;
    // A shape as well as the color, so level vs yours reads without color: ▲▲ much higher, ▲ higher, ▼ much lower.
    const mark = diff >= 2 ? '▲▲ ' : diff === 1 ? '▲ ' : diff <= -2 ? '▼ ' : '';
    const label = addText(this, r.x, 0, `${def.name}  ${mark}Lv ${def.level}${def.tough ? '  ·  Tough' : ''}`, { size: Math.round(15 * this.ls), bold: true, color, stroke: true }).setOrigin(0.5, 1);
    const mob: Mob = { ...actor, uid: r.uid, def, label, state: 'idle', timer: Phaser.Math.FloatBetween(0.3, 1.5), target: null };
    mob.facing = r.x > this.player.x ? -1 : 1;
    if (r.elite) {
      mob.elite = r.elite;
      mob.aura = this.add.image(r.x, r.y, 'fx_soft').setTint(ELITE_GLOW).setBlendMode('ADD').setScale(5).setAlpha(0.3);
      this.tweens.add({ targets: mob.aura, alpha: 0.55, scale: 5.8, duration: 900, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
      mob.tag = addText(this, r.x, 0, 'ELITE', { size: Math.round(14 * this.ls), bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5, 1);
    }
    if (def.lucky) {
      mob.aura = this.add.image(r.x, r.y, 'fx_soft').setTint(LUCKY_GLOW).setBlendMode('ADD').setScale(3.5).setAlpha(0.3);
      this.tweens.add({ targets: mob.aura, alpha: 0.6, scale: 4.2, duration: 600, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
      mob.tag = addText(this, r.x, 0, 'LUCKY', { size: Math.round(14 * this.ls), bold: true, color: LUCKY_CSS, stroke: true }).setOrigin(0.5, 1);
      mob.sparkle = this.add.particles(r.x, r.y, 'fx_star', {
        x: { min: -40, max: 40 },
        y: { min: -90, max: 0 },
        speedY: { min: -30, max: -10 },
        scale: { start: 0.55, end: 0 },
        alpha: { start: 1, end: 0 },
        lifespan: 700,
        frequency: 110,
        tint: [0xffe066, 0xfff6c4, 0x8dffbe],
        blendMode: 'ADD',
      });
    }
    this.mobs.push(mob);
    this.place(mob);
    this.placeEliteMarks(mob);
    // Pop in.
    fig.setAlpha(0);
    this.tweens.add({ targets: fig, alpha: 1, duration: 300, delay: 150 });
  }

  /** Keep an Elite's (or Lucky Impling's) aura, tag and twinkles with it. */
  private placeEliteMarks(m: Mob): void {
    m.aura?.setPosition(m.x, m.y - m.height * m.scale * 0.45).setDepth(m.y - 1);
    m.tag?.setPosition(m.x, m.label.y - m.label.height).setDepth(m.y + 0.5);
    m.sparkle?.setPosition(m.x, m.y).setDepth(m.y + 0.4);
  }

  private drawExits(): void {
    const { width } = DESIGN;
    const [top, bottom] = this.room.walk;
    const y = (top + bottom) / 2 - 70;
    for (const exit of this.room.exits) {
      if (exit.side === 'back') {
        this.drawBackExit(exit);
        continue;
      }
      if (exit.side === 'down') {
        this.drawDownExit(exit);
        continue;
      }
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
      const label = addText(this, -10 * dir, -48, exit.label, { size: Math.round(16 * this.ls), bold: true, stroke: true, align: 'center' }).setOrigin(
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

  /** A bobbing up-arrow and label over a doorway in the art; tap it to walk in. A closed one gets its blocker drawn instead. */
  private drawBackExit(exit: RoomExit): void {
    const [top] = this.room.walk;
    const x = exit.x!;
    // A sealed way you can't open yet is just clutter: show it once its quest can be picked up.
    if (exit.requiresQuest && questStatus(session.player, db.quest(exit.requiresQuest)) === 'locked') return;
    if (exit.frame) this.drawDoorway(x, exit.frame);
    if (!this.exitOpen(exit)) {
      this.drawBackBlocker(x, exit.blocker ?? 'barricade');
      return;
    }
    const y = top - 70;
    const c = this.add.container(x, y).setDepth(UI_DEPTH - 1);
    const arrow = this.add.graphics();
    arrow.fillStyle(0xf1c40f, 1).lineStyle(4, 0x3e2723, 1);
    const pts = [new Phaser.Math.Vector2(-26, 14), new Phaser.Math.Vector2(0, -20), new Phaser.Math.Vector2(26, 14)];
    arrow.fillPoints(pts, true).strokePoints(pts, true, true);
    const label = addText(this, 0, -46, exit.label, { size: Math.round(16 * this.ls), bold: true, stroke: true, align: 'center' }).setOrigin(0.5);
    c.add([arrow, label]);
    this.tweens.add({ targets: arrow, y: -8, duration: 500, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
    const hit = this.add.rectangle(x, y - 10, 120, 100, 0xffffff, 0).setInteractive({ useHandCursor: true }).setDepth(UI_DEPTH - 1);
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      this.pendingNpc = null;
      this.pendingExit = exit;
      this.moveTarget = { x, y: top };
    });
  }

  /** A bobbing down-arrow at the bottom edge where a path leads off (side rooms); tap it to walk there. Hidden until it's open. */
  private drawDownExit(exit: RoomExit): void {
    if (!this.exitOpen(exit)) return;
    const [, bottom] = this.room.walk;
    const x = exit.x!;
    const y = bottom + 2;
    // A worn path running off the bottom of the screen.
    const path = this.add.graphics().setDepth(1);
    path.fillStyle(0x3b2a1a, 0.45).fillEllipse(x, bottom + 14, 170, 40);
    path.fillStyle(0x3b2a1a, 0.3).fillEllipse(x, bottom - 14, 120, 26);
    const c = this.add.container(x, y).setDepth(UI_DEPTH - 1);
    const arrow = this.add.graphics();
    arrow.fillStyle(0xf1c40f, 1).lineStyle(4, 0x3e2723, 1);
    const pts = [new Phaser.Math.Vector2(-22, -12), new Phaser.Math.Vector2(0, 14), new Phaser.Math.Vector2(22, -12)];
    arrow.fillPoints(pts, true).strokePoints(pts, true, true);
    const label = addText(this, 34, -2, exit.label, { size: Math.round(16 * this.ls), bold: true, stroke: true }).setOrigin(0, 0.5);
    c.add([arrow, label]);
    this.tweens.add({ targets: arrow, y: 6, duration: 500, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
    const hit = this.add.rectangle(x, y - 10, 140, 70, 0xffffff, 0).setInteractive({ useHandCursor: true }).setDepth(UI_DEPTH - 1);
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      this.pendingNpc = null;
      this.pendingThing = null;
      this.pendingExit = exit;
      this.moveTarget = { x, y: bottom };
    });
  }

  /** The down exit whose path the player is standing on (at the bottom of the walkable strip). */
  private downExitAt(p: Actor): RoomExit | undefined {
    const [, bottom] = this.room.walk;
    return this.room.exits.find((e) => e.side === 'down' && this.exitOpen(e) && Math.abs(p.x - e.x!) < 70 && p.y >= bottom - 8);
  }

  /** A gate or cave mouth drawn at the top of the walkable strip, behind everyone. */
  private drawDoorway(x: number, frame: 'gate' | 'cave'): void {
    const [top] = this.room.walk;
    const g = this.add.graphics().setDepth(top - 2);
    if (frame === 'cave') {
      g.fillStyle(0x4a4146, 1).fillEllipse(x, top - 70, 220, 190);
      g.fillStyle(0x151012, 1).fillEllipse(x, top - 55, 170, 150);
      g.fillStyle(0xff8a3d, 0.25).fillEllipse(x, top - 20, 120, 50);
      g.fillStyle(0x4a4146, 1).fillRect(x - 120, top + 6, 240, 14);
      return;
    }
    // Stone posts, a lintel and open wooden doors on a dark passage.
    g.fillStyle(0x1d1a18, 1).fillRect(x - 60, top - 170, 120, 176);
    g.fillStyle(0x8d8478, 1).fillRect(x - 86, top - 190, 28, 196).fillRect(x + 58, top - 190, 28, 196);
    g.fillStyle(0x6f675c, 1).fillRect(x - 96, top - 212, 192, 30);
    g.fillStyle(0x6d4c33, 1).fillRect(x - 58, top - 166, 22, 170).fillRect(x + 36, top - 166, 22, 170);
    g.lineStyle(3, 0x3e2723, 1).strokeRect(x - 96, top - 212, 192, 30);
  }

  /** The back exit whose doorway the player is standing in (it sits at the top of the walkable strip). */
  private backExitAt(p: Actor): RoomExit | undefined {
    const [top] = this.room.walk;
    return this.room.exits.find(
      (e) =>
        e.side === 'back' &&
        Math.abs(p.x - e.x!) < 50 &&
        p.y <= top + 8 &&
        !(e.requiresQuest && questStatus(session.player, db.quest(e.requiresQuest)) === 'locked'),
    );
  }

  private exitOpen(exit: RoomExit): boolean {
    return exitOpen(session.player, this.room, exit);
  }

  /** Thorns, a barricade or flooded reeds across a locked exit. */
  private drawBlocker(side: 'left' | 'right', kind: Blocker): void {
    const { width } = DESIGN;
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
    } else if (kind === 'snowdrift') {
      this.drawSnowdrift(g, x0 + 45, bottom + 10, 110, bottom - top + 200);
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
    const label = BLOCKER_LABELS[kind];
    addText(this, side === 'right' ? width - 50 : 50, top - 290, label, { size: 15, bold: true, align: 'center', color: COLORS.textDim, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH - 1);
  }

  /** Snow piled into a mound: `cx` center, `base` bottom y, `w` x `h`. */
  private drawSnowdrift(g: Phaser.GameObjects.Graphics, cx: number, base: number, w: number, h: number): void {
    // Overlapping humps on a flat base, shaded darker at the bottom.
    const humps = [
      [-0.32, 0.55, 0.5],
      [0.28, 0.6, 0.55],
      [0, 1, 0.6],
    ];
    for (const [dx, hh, ww] of humps) g.fillStyle(0xc9d7e4, 1).fillEllipse(cx + dx * w, base - (h * hh) / 2, w * ww, h * hh);
    for (const [dx, hh, ww] of humps) g.fillStyle(0xf4f8fc, 1).fillEllipse(cx + dx * w - w * 0.04, base - (h * hh) / 2 - h * 0.06, w * ww * 0.85, h * hh * 0.8);
    g.fillStyle(0xffffff, 1).fillEllipse(cx - w * 0.06, base - h * 0.78, w * 0.22, h * 0.14);
    g.fillStyle(0xb3c4d6, 1).fillRect(cx - w * 0.6, base - 8, w * 1.2, 10);
  }

  /** What blocks a closed back exit, drawn over its doorway. */
  private drawBackBlocker(x: number, kind: Blocker): void {
    const [top] = this.room.walk;
    const g = this.add.graphics().setDepth(top - 1);
    if (kind === 'snowdrift') this.drawSnowdrift(g, x, top + 14, 200, 150);
    else {
      for (let i = 0; i < 4; i++) {
        g.fillStyle(i % 2 ? 0x8d6e63 : 0x795548, 1).fillRect(x - 70, top - 130 + i * 34, 140, 22);
        g.lineStyle(2, 0x3e2723, 1).strokeRect(x - 70, top - 130 + i * 34, 140, 22);
      }
    }
    addText(this, x, top - 190, BLOCKER_LABELS[kind].replace(/\s+/g, ' '), { size: 15, bold: true, align: 'center', color: COLORS.textDim, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH - 1);
  }

  private bumpWall(exit: RoomExit): void {
    if (this.time.now < this.wallBumpAt) return;
    this.wallBumpAt = this.time.now + 2000;
    sfx.play(this, 'denied');
    shake(this, 120, 0.004);
    const guard = exit.guarded && this.boss ? `The ${this.boss.def.name.replace(/^The /, '')} blocks the way. Beat it to get through.` : null;
    toast(this, guard ?? BLOCKER_BUMP[exit.blocker ?? (exit.side === 'back' ? 'barricade' : 'thorns')], COLORS.textDim);
  }

  private spawnBoss(): void {
    const b = this.room.boss;
    const monsterId = roomBoss(session.player, this.room);
    if (!b || !monsterId) return;
    const def = db.monster(monsterId);
    // Bosses face the middle of the room, where the player comes from.
    const faceLeft = b.x > DESIGN.width / 2;
    const fig = drawFigure(this, b.x, b.y, def.look, faceLeft ? -1 : 1);
    const actor = this.makeActor(fig, b.x, b.y, def.look.scale);
    actor.facing = 1;
    const label = addText(this, b.x, 0, `${def.name}  ·  Boss`, { size: Math.round(18 * this.ls), bold: true, color: COLORS.textBad, stroke: true }).setOrigin(0.5, 1);
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
    const { width, height } = DESIGN;
    this.hud = new Hud(this, 16, 16);
    this.hud.refresh(session.player, false);

    const right = width - 16;
    const btn = (y: number, label: string, run: () => void) =>
      new Button(this, right - 70, y, label, run, { width: 140, height: 48, fontSize: 19 }).setDepth(UI_DEPTH);
    this.charBtn = btn(40, 'Character', () => this.openCharacter());
    this.invBtn = btn(98, 'Inventory', () => this.toggleInventory());
    btn(156, 'Map', () => this.toggleMap());
    btn(214, 'Menu', () => this.openMenu());

    const title = addText(this, width / 2, 36, this.room.name, { size: 30, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5)
      .setDepth(UI_DEPTH)
      .setInteractive();
    let taps: number[] = [];
    title.on('pointerup', () => {
      const now = this.time.now;
      taps = [...taps.filter((t) => now - t < 2500), now];
      if (taps.length >= 5) {
        taps = [];
        this.openCheats();
      }
    });

    if (titleCard) this.titleCard();

    if (!showedControls) {
      showedControls = true;
      const controls = isTouchScreen()
        ? 'Tap the ground to walk    Tap people to talk    Hold a battle button to read it'
        : 'WASD / arrows: move    E / Space: talk    C: character    M: map    Click: walk there';
      const hint = addText(this, width / 2, height - 18, controls, {
        size: Math.round(17 * Math.min(this.ls, 1.25)),
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
    this.prompt = this.add.container(0, 0, [bg, this.promptText]).setDepth(UI_DEPTH - 2).setVisible(false).setScale(this.ls);
    this.tweens.add({ targets: this.prompt, scale: 1.06 * this.ls, yoyo: true, repeat: -1, duration: 450, ease: 'Sine.easeInOut' });
  }

  /** Big room name that fades in and out on arrival. */
  private titleCard(): void {
    const card = addText(this, DESIGN.width / 2, 250, this.room.name, { size: 64, bold: true, color: COLORS.textGold, stroke: true })
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
    if (!blocked) this.separateMobs(dt);
    for (const mob of [...this.mobs]) this.updateMob(mob, dt, time, blocked);
    if (!blocked && !this.leaving) this.checkChest();
    if (!blocked && !this.leaving && time > this.newsAt) {
      this.newsAt = time + 500;
      this.showNews();
    }
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
        this.pendingThing = null;
      } else if (this.moveTarget) {
        const dx = this.moveTarget.x - p.x;
        const dy = this.moveTarget.y - p.y;
        if (Math.abs(dx) < 6 && Math.abs(dy) < 6) {
          this.moveTarget = null;
          if (this.pendingNpc && this.inTalkRange(this.pendingNpc)) this.interact(this.pendingNpc);
          if (this.pendingThing && this.thingInRange(this.pendingThing)) this.useThing(this.pendingThing);
          this.pendingNpc = null;
          this.pendingThing = null;
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
    const { width } = DESIGN;
    const exitL = this.room.exits.find((e) => e.side === 'left' && this.exitOpen(e));
    const exitR = this.room.exits.find((e) => e.side === 'right' && this.exitOpen(e));
    if (exitR && p.x > width - EDGE / 2) return this.leave(exitR);
    if (exitL && p.x < EDGE / 2) return this.leave(exitL);
    const shutR = exitR ? undefined : this.room.exits.find((e) => e.side === 'right');
    const shutL = exitL ? undefined : this.room.exits.find((e) => e.side === 'left');
    const maxX = exitR ? width : shutR ? width - 110 : width - EDGE;
    const minX = exitL ? 0 : shutL ? 110 : EDGE;
    if (shutR && p.x >= maxX && vx > 0) this.bumpWall(shutR);
    if (shutL && p.x <= minX && vx < 0) this.bumpWall(shutL);
    // Back exits: walk up into the doorway (or arrive there after tapping it).
    const back = this.backExitAt(p);
    if (back && (vy < 0 || this.pendingExit === back)) {
      this.pendingExit = null;
      if (this.exitOpen(back)) return this.leave(back);
      this.bumpWall(back);
    }
    // Down exits: walk off the bottom edge along the path (or arrive there after tapping it).
    const down = this.downExitAt(p);
    if (down && (vy > 0 || this.pendingExit === down)) {
      this.pendingExit = null;
      return this.leave(down);
    }
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

    if (!blocked && m.def.lucky) {
      // A Lucky Impling runs from you instead, and slips along the walls when cornered.
      if (dist < BALANCE.monsterAggroRange * 1.15) {
        if (m.state !== 'run') this.alertPop(m);
        m.state = 'run';
        target = this.runTarget(m);
        speed = BALANCE.lucky.runSpeed;
      } else if (m.state === 'run') {
        m.state = 'idle';
        m.timer = 0.6;
      }
    }
    if (!blocked && m.state !== 'run') {
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
            // Wander somewhere no other monster is standing.
            for (let i = 0; i < 6; i++) {
              m.target = {
                x: Phaser.Math.Clamp(m.x + Phaser.Math.Between(-220, 220), 120, DESIGN.width - 120),
                y: Phaser.Math.Between(top + 5, bottom - 5),
              };
              const t = m.target;
              if (this.mobs.every((o) => o === m || Math.abs(o.x - t.x) >= MOB_GAP.x || Math.abs(o.y - t.y) >= MOB_GAP.y)) break;
            }
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
        m.walkT += dt * (m.state === 'chase' || m.state === 'run' ? 14 : 8);
      }
    }
    this.clampToWalk(m, 60, DESIGN.width - 60);
    this.place(m, time);
    this.placeEliteMarks(m);

    if (!blocked && !safe && Math.abs(dx) < TOUCH_RANGE.x && Math.abs(dy) < TOUCH_RANGE.y) this.startBattle(m);
  }

  /** Where a running Lucky Impling heads: away from you; along the wall (up or down, toward more room) once it's backed into one. */
  private runTarget(m: Mob): { x: number; y: number } {
    const p = this.player;
    const [top, bottom] = this.room.walk;
    const away = m.x === p.x ? (m.uid % 2 ? 1 : -1) : Math.sign(m.x - p.x);
    let x = m.x + away * 200;
    let y = m.y + (m.y - p.y >= 0 ? 60 : -60);
    const margin = 90;
    if (x < margin || x > DESIGN.width - margin) {
      // Cornered: dash past you on the roomier side.
      x = Phaser.Math.Clamp(x, margin, DESIGN.width - margin);
      y = m.y - top > bottom - m.y ? top : bottom;
      if (Math.abs(m.x - p.x) < 140) x = p.x - away * 220;
    }
    return { x, y: Phaser.Math.Clamp(y, top, bottom) };
  }

  private updatePrompt(blocked: boolean): void {
    const near = blocked ? null : this.nearestTarget();
    if (!near) {
      const thing = blocked ? null : this.nearestThing();
      if (!thing) {
        this.prompt.setVisible(false);
        return;
      }
      this.promptText.setText(`${this.keyHint}${thing.kind === 'campfire' ? 'Rest' : 'Look'}`);
      this.prompt.setVisible(true).setPosition(thing.x, thing.y - (thing.kind === 'campfire' ? 130 : 80));
      return;
    }
    if (near === 'boss') {
      this.promptText.setText(`${this.keyHint}Fight`);
      this.prompt.setVisible(true).setPosition(this.boss!.x, this.boss!.label.y - this.boss!.label.height - 22);
      return;
    }
    const q = questForNpc(session.player, near.def.id);
    const kind = near.def.role.kind;
    const verb =
      q?.status === 'ready' || (kind === 'guild' && anyHuntReady(session.player))
        ? 'Turn in'
        : q?.status === 'available'
          ? 'Quest'
          : kind === 'shop'
            ? 'Shop'
            : kind === 'forge'
              ? 'Forge'
              : kind === 'inn'
                ? 'Rest'
                : kind === 'guild'
                  ? 'Hunts'
                  : 'Talk';
    this.promptText.setText(`${this.keyHint}${verb}`);
    const marker = this.markers.get(near);
    const above = marker?.visible ? marker.y - 46 : near.label.y - near.label.height - 22;
    this.prompt.setVisible(true).setPosition(near.x, above);
  }

  /** "E  " before the bubble's verb, except on touch screens (you tap the person instead). */
  private get keyHint(): string {
    return isTouchScreen() ? '' : 'E  ';
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

  private clampToWalk(a: Actor, minX = EDGE, maxX = DESIGN.width - EDGE): void {
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
    return !!this.cutscene || this.children.list.some((c) => c instanceof Modal);
  }

  private closeTopModal(): void {
    if (this.cutscene) {
      this.cutscene.skip();
      return;
    }
    const modals = this.children.list.filter((c): c is Modal => c instanceof Modal);
    modals[modals.length - 1]?.close();
  }

  private onPointerDown(_pointer: Phaser.Input.Pointer, over: Phaser.GameObjects.GameObject[]): void {
    if (this.cutscene) return this.cutscene.next();
    if (over.length > 0 || this.modalOpen() || this.leaving) return;
    this.pendingNpc = null;
    this.pendingThing = null;
    const [top, bottom] = this.room.walk;
    this.moveTarget = { x: _pointer.worldX, y: Phaser.Math.Clamp(_pointer.worldY, top, bottom) };
    // Little marker where you clicked.
    const ring = this.add.ellipse(this.moveTarget.x, this.moveTarget.y, 40, 14).setStrokeStyle(3, 0xf1c40f).setDepth(2);
    this.tweens.add({ targets: ring, scale: 1.6, alpha: 0, duration: 450, onComplete: () => ring.destroy() });
  }

  private alertPop(m: Mob): void {
    sfx.play(this, m.def.lucky ? 'coin' : 'alert');
    const mark = addText(this, m.x, m.y - m.height * m.scale - 30, '!', { size: 44, bold: true, color: m.def.lucky ? LUCKY_CSS : COLORS.textBad, stroke: true })
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

  private leave(exit: RoomExit): void {
    if (this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    // Arrive at the way back: the destination's exit to this room, else the opposite edge.
    const back = db.room(exit.to).exits.find((e) => e.to === this.room.id);
    const side = back?.side ?? (exit.side === 'right' ? 'left' : 'right');
    const data: RoomSceneData = { roomId: exit.to, via: 'exit', side, x: back?.x, y: this.player.y };
    this.rememberRoom();
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
    this.rememberRoom();
    this.saveLocation();

    sfx.play(this, m.elite ? 'warning' : m.def.lucky ? 'coin' : 'encounter');
    fx.whiteFlash(this, m.fig, 140);
    fx.hitSparks(this, (m.x + this.player.x) / 2, m.y - 80, false, true);
    const cam = this.cameras.main;
    shake(this, 250, 0.012);
    cam.flash(180, 255, 255, 255);
    cam.pan((m.x + this.player.x) / 2, m.y - 120, 450, 'Cubic.easeIn');
    zoomTo(cam, 1.6, 450, 'Cubic.easeIn');
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

  /** Save, sound, story text, fullscreen and back to the title, in one place so the screen stays clear. */
  private openMenu(): void {
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    const modal = new Modal(this, 460, 470, 'Menu', () => {});
    const render = () => {
      const c = modal.content;
      c.removeAll(true);
      const rows: { label: string; run: () => void }[] = [
        { label: 'Save', run: () => this.save(true) },
        { label: sfx.isMuted() ? 'Sound: off' : 'Sound: on', run: () => (sfx.toggleMute(), render()) },
        {
          label: settings.storyText ? 'Story text: on' : 'Story text: off',
          run: () => ((settings.storyText = !settings.storyText), saveSettings(), render()),
        },
      ];
      // Not every browser has it (iPhone Safari doesn't), so only offer it where it works.
      if (this.sys.game.device.fullscreen.available) rows.push({ label: 'Fullscreen', run: () => this.scale.toggleFullscreen() });
      rows.push({ label: 'Save and quit to title', run: () => (modal.close(), this.saveAndQuit()) });
      rows.forEach((r, i) => c.add(new Button(this, 230, 100 + i * 66, r.label, r.run, { width: 300, height: 52, fontSize: 19 })));
      c.add(
        addText(this, 230, 440, 'Story text off: quests say in one line what to do.', { size: 14, color: COLORS.textDim, align: 'center', wrap: 400 }).setOrigin(0.5),
      );
    };
    render();
  }

  private openCharacter(): void {
    if (this.modalOpen() || this.leaving) return;
    openCharacterPanel(this, session.player, this.refresh, () => this.toggleInventory());
  }

  private openCheats(): void {
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    openCheatMenu(this, session.player, {
      onChange: this.refresh,
      travel: (roomId, x, y) => {
        const [top, bottom] = db.room(roomId).walk;
        this.leaving = true;
        this.save(false);
        goTo(this, SceneKeys.Room, { roomId, via: 'spawn', x: x ?? 640, y: y ?? (top + bottom) / 2 } satisfies RoomSceneData);
      },
      resetCharacter: () => {
        const old = session.player;
        const fresh = Player.createNew(old.name, old.data.classId);
        session.start(fresh, true);
        SaveSystem.save(fresh);
        this.leaving = true;
        goTo(this, SceneKeys.Room, { roomId: START.roomId, via: 'spawn', x: START.x, y: START.y } satisfies RoomSceneData);
      },
    });
  }

  /** I opens the inventory, and closes it again. */
  private toggleInventory(): void {
    if (this.inventory) {
      this.inventory.close();
      return;
    }
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    this.inventory = openInventory(this, session.player, this.refresh, () => this.readRecall());
    this.inventory.once(Phaser.GameObjects.Events.DESTROY, () => (this.inventory = null));
  }

  /** M or the Map button opens the map, and closes it again. */
  private toggleMap(): void {
    if (this.mapModal) {
      this.mapModal.close();
      return;
    }
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    this.mapModal = openMap(this, session.player, this.room.id, () => this.readRecall());
    this.mapModal.once(Phaser.GameObjects.Events.DESTROY, () => (this.mapModal = null));
  }

  // --- Recall Scroll ---

  /** Asks first (it's one way), then reads a scroll. Called from the inventory and the map. */
  private readRecall(): void {
    if (this.leaving) return;
    const player = session.player;
    const check = recallCheck(player, this.room.id);
    if (!check.ok) {
      sfx.play(this, 'denied');
      toast(this, check.reason, COLORS.textDim);
      return;
    }
    this.inventory?.close();
    this.mapModal?.close();
    const left = player.countItem(RECALL_SCROLL);
    const modal = new Modal(this, 640, 280, 'Recall Scroll');
    const text = `Read a scroll and return to ${check.town.name}?\nOne way: you walk back out from there.\n(${left} scroll${left === 1 ? '' : 's'} left)`;
    modal.content.add(addText(this, 320, 120, text, { size: 20, align: 'center', wrap: 580 }).setOrigin(0.5).setLineSpacing(6));
    modal.content.add(new Button(this, 200, 225, 'Read it', () => (modal.close(), this.recallTo()), { width: 220 }));
    modal.content.add(new Button(this, 440, 225, 'Not now', () => modal.close(), { width: 220 }));
  }

  private recallTo(): void {
    const dest = useRecall(session.player, this.room.id);
    if (!dest || this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    this.rememberRoom();
    sfx.play(this, 'recall');
    fx.healSparkles(this, this.player.x, this.player.y, 0xffe9a8);
    this.tweens.add({ targets: this.player.fig, alpha: 0, scaleY: this.player.fig.scaleY * 1.3, duration: 500, ease: 'Cubic.easeIn' });
    this.cameras.main.flash(400, 255, 240, 200);
    // Save where you'll land, so a reload mid-fade doesn't undo the trip (the scroll is already spent).
    session.player.data.location = { ...dest };
    SaveSystem.save(session.player);
    const data: RoomSceneData = { roomId: dest.roomId, via: 'spawn', x: dest.x, y: dest.y, recalled: true };
    this.time.delayedCall(450, () => goTo(this, SceneKeys.Room, data, 300));
  }

  // --- Campfires and things to look at ---

  private drawThings(): void {
    const cf = this.room.campfire;
    if (cf) {
      const fire = drawCampfire(this, cf.x, cf.y);
      const thing: Thing = { kind: 'campfire', x: cf.x, y: cf.y };
      this.things.push(thing);
      this.thingHit(fire, thing, 150, 120);
    }
    for (const def of this.room.inspect ?? []) {
      const fig = drawProp(this, def, !session.inspected.has(def.id));
      const thing: Thing = { kind: 'inspect', def, x: def.x, y: def.y, fig };
      this.things.push(thing);
      addText(this, def.x, def.y - (def.look === 'sign' ? 110 : 44), def.label, { size: Math.round(15 * this.ls), bold: true, stroke: true, color: COLORS.textDim })
        .setOrigin(0.5, 1)
        .setDepth(def.y + 1);
      this.thingHit(fig, thing, 130, 90);
    }
  }

  /** Tap a campfire or note: walk next to it, then use it. */
  private thingHit(fig: Phaser.GameObjects.Container, thing: Thing, w: number, h: number): void {
    const hit = this.add.rectangle(thing.x, thing.y - h / 2, w, h, 0xffffff, 0).setInteractive({ useHandCursor: true }).setDepth(fig.depth + 1);
    hit.on('pointerup', () => {
      if (this.modalOpen() || this.leaving) return;
      if (this.thingInRange(thing)) return this.useThing(thing);
      this.pendingNpc = null;
      this.pendingExit = null;
      this.pendingThing = thing;
      const side = this.player.x < thing.x ? -1 : 1;
      const [top, bottom] = this.room.walk;
      this.moveTarget = { x: thing.x + side * 90, y: Phaser.Math.Clamp(thing.y, top, bottom) };
    });
  }

  private thingInRange(thing: Thing): boolean {
    return Math.abs(thing.x - this.player.x) < TALK_RANGE.x && Math.abs(thing.y - this.player.y) < TALK_RANGE.y;
  }

  private nearestThing(): Thing | null {
    let best: Thing | null = null;
    for (const t of this.things) if (this.thingInRange(t) && (!best || Math.abs(t.x - this.player.x) < Math.abs(best.x - this.player.x))) best = t;
    return best;
  }

  private useThing(thing: Thing): void {
    if (this.modalOpen() || this.leaving) return;
    this.moveTarget = null;
    this.player.facing = thing.x < this.player.x ? -1 : 1;
    if (thing.kind === 'campfire') return this.restAtFire();
    sfx.play(this, 'page');
    if (!session.inspected.has(thing.def.id)) {
      session.inspected.add(thing.def.id);
      // The glint goes once it's been read.
      for (const child of thing.fig.list) if (child instanceof Phaser.GameObjects.Image) child.destroy();
    }
    openNarration(this, thing.def.label, thing.def.lines);
  }

  /** One rest per visit: full HP and MP, free. */
  private restAtFire(): void {
    const player = session.player;
    if (session.restedAt === this.room.id) {
      toast(this, 'The fire has burned low. Come back another time.', COLORS.textDim);
      return;
    }
    if (player.hp >= player.maxHp && player.mp >= player.maxMp) {
      toast(this, 'You are already rested.', COLORS.textDim);
      return;
    }
    session.restedAt = this.room.id;
    player.restoreFull();
    this.cameras.main.fadeOut(300, 0, 0, 0);
    this.cameras.main.once('camerafadeoutcomplete', () => {
      this.cameras.main.fadeIn(600, 0, 0, 0);
      fx.healSparkles(this, this.player.x, this.player.y, 0xffb347);
      sfx.play(this, 'heal');
      toast(this, 'You rest by the fire. HP and MP restored.', COLORS.textGood);
      this.refresh();
    });
  }

  // --- First visit ---

  /** A quiet room's opening: dark bars, a low sound, the camera drifts toward what's ahead, and a few lines. */
  private playFirstVisit(): void {
    const fv = this.room.firstVisit;
    if (!fv || this.leaving || this.cutscene) return;
    const { width, height } = DESIGN;
    this.moveTarget = null;
    sfx.play(this, 'omen');

    // The bars and text are drawn by a second camera, so the room can zoom while they stay put.
    const layer = this.add.container(0, 0).setDepth(UI_DEPTH + 50);
    const topBar = this.add.rectangle(0, -70, width, 70, 0x000000).setOrigin(0);
    const bottomBar = this.add.rectangle(0, height, width, 150, 0x000000, 0.88).setOrigin(0);
    const text = addText(this, width / 2, height - 88, '', { size: 24, align: 'center', wrap: 1040 }).setOrigin(0.5).setLineSpacing(6).setAlpha(0);
    const hint = addText(this, width - 24, height - 16, isTouchScreen() ? 'Tap to continue' : 'E / Space / click: continue', { size: 14, color: COLORS.textDim })
      .setOrigin(1, 1)
      .setAlpha(0);
    layer.add([topBar, bottomBar, text, hint]);
    const ui = addOverlayCamera(this);
    ui.ignore(this.children.list.filter((o) => o !== layer));
    const main = this.cameras.main;
    main.ignore(layer);
    // The room's own buttons and labels would zoom too: hide them for the scene.
    const hidden = this.children.list.filter((o) => o !== layer && (o as unknown as { depth: number }).depth >= UI_DEPTH - 3 && (o as unknown as { visible: boolean }).visible);
    hidden.forEach((o) => (o as unknown as Phaser.GameObjects.Components.Visible).setVisible(false));

    this.tweens.add({ targets: topBar, y: 0, duration: 400, ease: 'Cubic.easeOut' });
    this.tweens.add({ targets: bottomBar, y: height - 150, duration: 400, ease: 'Cubic.easeOut' });
    const zoom = 1.15;
    const half = width / (2 * zoom);
    const panX = Phaser.Math.Clamp(fv.panTo ?? width / 2, half, width - half);
    main.pan(panX, height / 2 + 20, 2400, 'Sine.easeInOut');
    zoomTo(main, zoom, 2400, 'Sine.easeInOut');

    const lines = fv.lines;
    const show = (i: number) => {
      text.setText(lines[i]).setAlpha(0);
      this.tweens.add({ targets: text, alpha: 1, duration: 450 });
    };
    const finish = () => {
      if (!this.cutscene) return;
      this.cutscene = null;
      // Forced: skipping (Esc) during the slow drift in would otherwise leave the room zoomed.
      main.pan(width / 2, height / 2, 600, 'Sine.easeInOut', true);
      zoomTo(main, 1, 600, 'Sine.easeInOut', true);
      this.tweens.add({ targets: topBar, y: -70, duration: 400 });
      this.tweens.add({
        targets: [bottomBar, text, hint],
        alpha: 0,
        duration: 400,
        onComplete: () => {
          layer.destroy();
          this.cameras.remove(ui);
          hidden.forEach((o) => o.active && (o as unknown as Phaser.GameObjects.Components.Visible).setVisible(true));
        },
      });
    };
    // A short pause before taps count, so the key that walked you in doesn't skip the first line.
    let ready = this.time.now + 700;
    let line = 0;
    this.cutscene = {
      next: () => {
        if (this.time.now < ready) return;
        ready = this.time.now + 250;
        line += 1;
        if (line >= lines.length) finish();
        else show(line);
      },
      skip: finish,
    };
    this.time.delayedCall(400, () => {
      show(0);
      this.tweens.add({ targets: hint, alpha: 1, duration: 400, delay: 600 });
    });
  }

  private interact(npc: Npc, intro = false): void {
    if (this.modalOpen()) return;
    const player = session.player;
    const role = npc.def.role;
    const actions: DialogueAction[] = [];
    // Story text off: one line of small talk, and quests say what to do instead of telling the story.
    const story = settings.storyText;
    let lines = story ? npc.def.dialogue : npc.def.dialogue.slice(0, 1);

    if (role.kind === 'guild') {
      this.moveTarget = null;
      this.player.facing = npc.x < this.player.x ? -1 : 1;
      this.tweens.add({ targets: npc.fig, y: npc.fig.y - 10, duration: 100, yoyo: true });
      openGuild(this, player, npc.def, () => this.refresh(), (r) => this.huntTurnedIn(r, npc));
      return;
    }

    const q = questForNpc(player, npc.def.id);
    if (q?.status === 'ready') {
      const quest = q.quest;
      // Story text off still keeps the giver's voice: their first thank-you line.
      lines = story ? quest.complete : [`${quest.name}: done!  ${quest.complete[0]}`];
      actions.push({ label: 'Complete', run: (close) => (close(), this.completeQuest(quest, npc)) });
      this.moveTarget = null;
      this.tweens.add({ targets: npc.fig, y: npc.fig.y - 10, duration: 100, yoyo: true });
      openDialogue(this, npc.def, actions, lines);
      return;
    }
    if (q?.status === 'available') {
      const quest = q.quest;
      lines = story ? (intro ? [...npc.def.dialogue, ...quest.offer] : quest.offer) : [`New quest: ${quest.name}.  ${quest.reminder}`];
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
      lines = [`${q.quest.reminder}  (${q.quest.goal}: ${questProgress(player, q.quest)}/${q.quest.objective.count})`];
    } else if (q?.status === 'rank') {
      lines = [
        `I have work for you, but not for a Rank ${rankId(player.data.guild.rank)}. Come back once the Adventurers' Guild ranks you ${q.quest.requiresRank}: take their hunts (a Guild Clerk in any town).`,
      ];
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
    } else if (role.kind === 'forge') {
      actions.push({
        label: 'Use the forge',
        run: (close) => {
          close();
          openForge(this, player, `${npc.def.name}'s Forge`, role.recipes, this.refresh);
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
      const huntDone = npc.def.role.kind === 'guild' && anyHuntReady(player);
      const symbol = q?.status === 'ready' || huntDone ? '?' : q?.status === 'available' ? '!' : '';
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
    const hunts = activeHunts(player);
    const waiting = quests.length === 0 ? questWaitingOnRank(player) : null;
    if (quests.length > 0 || hunts.length > 0 || waiting) {
      const rows = [
        ...quests.map((q) =>
          q.status === 'ready'
            ? `${q.quest.name}: done! Return to ${this.giverName(q.quest)}`
            : `${q.quest.name}: ${q.quest.goal} ${q.count}/${q.quest.objective.count}`,
        ),
        ...hunts.map((h) =>
          h.ready ? `Hunt (${rankId(h.hunt.rank)}): done! Return to a Guild Clerk` : `Hunt (${rankId(h.hunt.rank)}): ${huntGoal(h.hunt)} ${h.count}/${h.hunt.count}`,
        ),
        ...(waiting ? [`Next: reach Guild Rank ${waiting.requiresRank} (you are ${rankId(player.data.guild.rank)})`] : []),
      ].slice(0, 5);
      const text = addText(this, 12, 8, rows.join('\n'), { size: Math.round(16 * Math.min(this.ls, 1.25)), color: COLORS.text }).setLineSpacing(6);
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

  /** A hunt handed in at a clerk: coins, then level-ups and promotions over the board. */
  private huntTurnedIn(result: TurnInHunt, npc: Npc): void {
    const player = session.player;
    const { rewards, levelsGained, promotions } = result;
    const items = rewards.items.map((g) => lootName(db.item(g.itemId), g));
    toast(this, [`+${rewards.xp} XP`, `+${rewards.gold} gold`, ...items].join('   '), COLORS.textGood);
    void fx.coinShower(this, { x: npc.x, y: npc.y - 120 }, { x: 320, y: 40 }, Math.ceil(rewards.gold / 6), () => sfx.play(this, 'coin'));
    for (const rank of promotions) {
      this.time.delayedCall(300, () => {
        fx.confetti(this);
        sfx.play(this, 'levelup');
        toast(this, `Guild promotion: Rank ${rank}!`, COLORS.textGold);
      });
    }
    if (levelsGained > 0) {
      const skills = player.skillsLearnedSince(player.level - levelsGained);
      this.time.delayedCall(700, () => {
        fx.levelUpBurst(this, this.player.x, this.player.y);
        sfx.play(this, 'levelup');
        toast(this, `Level up! Now level ${player.level}. +${levelsGained * BALANCE.statPointsPerLevel} stat points (C)`, COLORS.textGood);
        skills.forEach((sk) => toast(this, `New skill: ${sk.name}`, COLORS.textMagic));
      });
    }
    this.save(false);
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
    const { width, height } = DESIGN;
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
    // A gate guard is only ever fought until it's beaten, then it's gone.
    const rematch = !isGuard(this.room) && !questNeedsKill(session.player, def.id);
    const heroic = rematch && def.heroic;
    const h = BALANCE.heroic;
    const modal = new Modal(this, 760, heroic ? 360 : 300, rematch ? `${def.name} (rematch)` : def.name);
    const intro = this.room.boss?.intro ?? 'It turns toward you.';
    const text = rematch
      ? `It is back. There is no running from this fight.\nRematch: ${Math.round(BALANCE.bossRepeatRewards * 100)}% XP and gold, full loot${featureOn(session.player, 'sigils') ? ' (Sigils drop more often from bosses)' : ''}.` +
        (heroic
          ? `\nHeroic: +${h.levels} levels, +${Math.round((h.damage - 1) * 100)}% damage and a new move (${def.heroic!.text}) Full XP and gold, ${h.lootRolls} gear rolls.`
          : '')
      : `${intro}\nThere is no running from this fight.`;
    modal.content.add(addText(this, 380, heroic ? 130 : 110, text, { size: rematch ? 18 : 22, align: 'center', wrap: 700 }).setOrigin(0.5));
    const y = heroic ? 290 : 230;
    if (heroic) {
      modal.content.add(new Button(this, 150, y, 'Fight', () => (modal.close(), this.startBossBattle()), { width: 200 }));
      modal.content.add(new Button(this, 380, y, 'Heroic', () => (modal.close(), this.startBossBattle(true)), { width: 200 }));
      modal.content.add(new Button(this, 610, y, 'Not yet', () => modal.close(), { width: 200 }));
    } else {
      modal.content.add(new Button(this, 260, y, 'Fight', () => (modal.close(), this.startBossBattle()), { width: 220 }));
      modal.content.add(new Button(this, 500, y, 'Not yet', () => modal.close(), { width: 220 }));
    }
  }

  private startBossBattle(heroic = false): void {
    if (!this.boss || this.leaving) return;
    this.leaving = true;
    this.prompt.setVisible(false);
    session.saveRoom({ roomId: this.room.id, fightingUid: -1, monsters: [] });
    this.rememberRoom();
    this.saveLocation();
    sfx.play(this, 'encounter');
    const cam = this.cameras.main;
    shake(this, 500, 0.015);
    cam.flash(300, 255, 60, 60);
    zoomTo(cam, 1.4, 600, 'Cubic.easeIn');
    const data: BattleSceneData = { zoneId: this.room.zoneId!, monsterId: this.boss.def.id, roomId: this.room.id, x: this.player.x, y: this.player.y, heroic };
    this.time.delayedCall(600, () => goTo(this, SceneKeys.Battle, data, 300));
  }
}
