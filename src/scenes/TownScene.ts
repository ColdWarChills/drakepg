import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import type { NpcDef } from '../data/types';
import { START_TOWN_ID } from '../data/world';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { addBackground } from '../ui/art';
import { Button } from '../ui/Button';
import { drawFigure, PLAYER_LOOK } from '../ui/figures';
import { Hud } from '../ui/Hud';
import { openCharacterPanel, openDialogue, openShop, type DialogueAction } from '../ui/panels';
import { addText, toast } from '../ui/text';
import { enterScene, goTo, SceneKeys } from './keys';

const GROUND_Y = 600;

export class TownScene extends Phaser.Scene {
  private hud!: Hud;

  constructor() {
    super(SceneKeys.Town);
  }

  create(data: { intro?: boolean } = {}): void {
    enterScene(this);
    const town = db.town(START_TOWN_ID);
    const player = session.player;

    this.drawBackdrop(town.background);
    for (const npc of town.npcs) this.addNpc(npc);
    drawFigure(this, 610, GROUND_Y + 40, PLAYER_LOOK, 1);

    addText(this, this.scale.width / 2, 40, town.name, { size: 36, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5);

    this.hud = new Hud(this, 16, 16);
    this.hud.refresh(player, false);

    const right = this.scale.width - 16;
    new Button(this, right - 70, 40, 'Title', () => this.saveAndQuit(), { width: 140, height: 48, fontSize: 19 });
    new Button(this, right - 70, 98, 'Save', () => this.save(true), { width: 140, height: 48, fontSize: 19 });
    new Button(this, right - 70, 156, 'Character', () => this.openCharacter(), {
      width: 140,
      height: 48,
      fontSize: 19,
    });

    this.input.keyboard?.on('keydown-C', () => this.openCharacter());

    this.save(false);
    if (data.intro) {
      const elder = town.npcs.find((n) => n.role.kind === 'talk');
      if (elder) this.time.delayedCall(400, () => this.interact(elder));
    }
  }

  private refresh = (): void => {
    this.hud.refresh(session.player);
    this.save(false);
  };

  private save(announce: boolean): void {
    const ok = SaveSystem.save(session.player);
    if (announce) toast(this, ok ? 'Game saved' : 'Save failed (storage blocked?)', ok ? COLORS.textGood : COLORS.textBad);
  }

  private saveAndQuit(): void {
    this.save(false);
    goTo(this, SceneKeys.Title);
  }

  private openCharacter(): void {
    openCharacterPanel(this, session.player, this.refresh);
  }

  private drawBackdrop(art: string | undefined): void {
    if (addBackground(this, art)) return;
    const { width, height } = this.scale;
    this.add.rectangle(0, 0, width, height, 0x87a9c9).setOrigin(0);
    this.add.rectangle(0, 260, width, 200, 0xa9c4d9).setOrigin(0);
    this.add.ellipse(250, 470, 700, 200, 0x6f8f6a);
    this.add.ellipse(950, 480, 900, 220, 0x5f7f5c);

    // Buildings behind each NPC.
    this.building(200, 260, 230, 0x8e7b65, 0x5d4037, 'Elder');
    this.building(470, 280, 250, 0x9c8462, 0x7b3f00, 'Shop');
    this.building(740, 300, 260, 0xa1887f, 0x4e342e, 'Inn');

    // Town wall and gate.
    this.add.rectangle(960, GROUND_Y, 320, 260, 0x7f8c8d).setOrigin(0, 1);
    this.add.rectangle(1080, GROUND_Y, 120, 170, 0x3e2723).setOrigin(0.5, 1).setRounded(20);
    this.add.rectangle(1080, GROUND_Y - 85, 4, 170, 0x2b1b17);

    // Street.
    this.add.rectangle(0, GROUND_Y, width, height - GROUND_Y, 0x8d7b68).setOrigin(0);
    this.add.rectangle(0, GROUND_Y, width, 8, 0x6d5d4b).setOrigin(0);
  }

  private building(x: number, w: number, h: number, wall: number, roof: number, sign: string): void {
    this.add.rectangle(x, GROUND_Y, w, h, wall).setOrigin(0.5, 1);
    const top = GROUND_Y - h;
    this.add.graphics().fillStyle(roof).fillTriangle(x - w / 2 - 20, top, x, top - 90, x + w / 2 + 20, top);
    this.add.rectangle(x - w / 4, GROUND_Y - h + 60, 50, 44, 0xf9e79f).setStrokeStyle(4, roof);
    this.add.rectangle(x + w / 4, GROUND_Y - h + 60, 50, 44, 0xf9e79f).setStrokeStyle(4, roof);
    this.add.rectangle(x, top - 30, 90, 28, 0x3e2723).setStrokeStyle(2, 0xc9a66b);
    addText(this, x, top - 30, sign, { size: 16, bold: true, color: '#f3e9d2' }).setOrigin(0.5);
  }

  private addNpc(npc: NpcDef): void {
    const figure = drawFigure(this, npc.x, GROUND_Y + 30, npc.look, 1);
    const label = addText(this, npc.x, GROUND_Y - 215, `${npc.name}\n${npc.title}`, {
      size: 18,
      bold: true,
      align: 'center',
      stroke: true,
    }).setOrigin(0.5);

    const hit = this.add.rectangle(npc.x, GROUND_Y - 70, 130, 230, 0xffffff, 0).setInteractive({ useHandCursor: true });
    hit.on('pointerover', () => {
      label.setColor(COLORS.textGold);
      this.tweens.add({ targets: figure, scale: npc.look.scale * 1.05, duration: 120 });
    });
    hit.on('pointerout', () => {
      label.setColor(COLORS.text);
      this.tweens.add({ targets: figure, scale: npc.look.scale, duration: 120 });
    });
    hit.on('pointerup', () => this.interact(npc));
  }

  private interact(npc: NpcDef): void {
    const player = session.player;
    const actions: DialogueAction[] = [];
    const role = npc.role;

    switch (role.kind) {
      case 'shop':
        actions.push({
          label: 'Browse wares',
          run: (close) => {
            close();
            openShop(this, player, `${npc.name}'s Shop`, role.stock, this.refresh);
          },
        });
        break;

      case 'inn': {
        const rested = player.hp >= player.maxHp && player.mp >= player.maxMp;
        actions.push({
          label: 'Rest',
          sublabel: rested ? 'Already rested' : `${BALANCE.innCost} g`,
          enabled: !rested && player.gold >= BALANCE.innCost,
          run: (close) => {
            player.data.gold -= BALANCE.innCost;
            player.restoreFull();
            close();
            toast(this, 'You feel refreshed.', COLORS.textGood);
            this.refresh();
          },
        });
        break;
      }

      case 'gate':
        for (const zoneId of role.zoneIds) {
          const zone = db.zone(zoneId);
          actions.push({
            label: zone.name,
            sublabel: `Lv ${zone.recommendedLevel[0]}-${zone.recommendedLevel[1]}`,
            run: (close) => {
              close();
              goTo(this, SceneKeys.Battle, { zoneId });
            },
          });
        }
        break;

      case 'talk':
        break;
    }

    openDialogue(this, npc, actions);
  }
}
