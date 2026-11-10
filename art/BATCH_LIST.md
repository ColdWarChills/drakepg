# Asset batch (scripts/asset_batch.py)

Every prompt of the long Qwen run, by group. Tries land in `art/redo/<key>/`; review with `python scripts/redo_art.py sheet <keys>` and take one with `python scripts/redo_art.py pick <key>:<seed>`.
354 prompts listed (`hero_outfits` only lists poses whose base pose exists).

## prop (34)

- `prop_campfire`: A small campfire: a ring of grey stones, three crossed logs and bright orange and yellow flames.
- `prop_campfire_b`: The same kind of small campfire a moment later: a ring of grey stones, three crossed logs, the orange and yellow flames leaning the other way with two sparks above.
- `prop_campfire_out`: A burnt-out campfire: a ring of grey stones around charred black logs and pale ash, one thin wisp of smoke.
- `prop_bones`: A small pile of old bleached animal bones with one antlered deer skull on top.
- `prop_thorn_wall`: A tall impassable wall of tangled black thorny briars with a few sickly green glowing buds, as wide as a gate.
- `prop_snowdrift`: A big heaped snowdrift blocking a path, taller than a person, with a few dark rocks and a broken fence post sticking out.
- `prop_reed_wall`: A dense tall wall of marsh reeds and cattails tied with rope into a barrier.
- `prop_rockfall`: A heap of fallen grey boulders and rubble blocking a mountain path.
- `prop_fallen_log`: A huge fallen mossy tree trunk lying across a path, with mushrooms on its bark.
- `prop_signpost_right`: A weathered wooden signpost with one arrow-shaped board pointing to the right, blank board.
- `prop_signpost_left`: A weathered wooden signpost with one arrow-shaped board pointing to the left, blank board.
- `prop_signpost_up`: A weathered wooden signpost with one arrow-shaped board pointing up and away, blank board.
- `prop_trapdoor`: A wooden trapdoor with an iron ring set in a low frame of stones, a rope ladder going down.
- `prop_chest`: A sturdy wooden treasure chest with iron bands and a brass lock, closed.
- `prop_chest_open`: A sturdy wooden treasure chest with iron bands, its lid thrown open, gold coins heaped inside.
- `prop_mimic_chest`: A wooden treasure chest that is secretly a monster: closed, but with a row of small teeth along the lid's edge and one sly eye peeking through the lock.
- `prop_herb_patch`: A patch of wild healing herbs with small white flowers growing from a tuft of grass.
- `prop_ore_vein`: A grey boulder split open to show a vein of glittering copper ore.
- `prop_mushroom_ring`: A ring of big red-capped spotted mushrooms on a mossy mound.
- `prop_lichen_rock`: A frosted boulder covered in pale blue-green lichen tufts.
- `prop_bog_lilies`: A cluster of pale pink bog lilies on broad green pads.
- `prop_notice_board`: A wooden notice board on two posts with a small shingle roof, a few pinned paper notices.
- `prop_crates`: A stack of three wooden crates and a small barrel tied with rope.
- `prop_tally_board`: A rough plank board nailed to a post, scratched with rows of tally marks.
- `prop_note_stake`: A wooden stake in the ground with a folded paper note pinned to it by a knife.
- `prop_shrine_stone`: A waist-high weathered standing stone carved with a simple spiral, a few offerings at its foot.
- `prop_offering_bowl`: A shallow stone offering bowl holding coins, shells and a candle stub.
- `prop_rune_pillar`: A tall narrow ice-rimmed stone pillar carved with faintly glowing blue runes.
- `prop_footprints`: A short trail of large clawed footprints pressed into snow, seen at a low angle.
- `prop_sprouting_stump`: An old tree stump with a single bright green sapling sprouting from its middle.
- `prop_cart_wheel`: A broken wooden cart wheel leaning against a spilled sack of grain.
- `prop_steam_vent`: A cracked rocky vent in the ground with a curling plume of white steam.
- `prop_ice_block`: A big block of clear blue ice with a dim dark shape frozen inside.
- `prop_tent`: A small patched canvas hunter's tent with a bedroll and a drying rack of pelts beside it.

## vfx (49)

- `fx_slash_a`: A single wide crescent sword-slash arc in white with a pale yellow edge, sweeping left to right.
- `fx_slash_b`: A long thin diagonal sword-slash streak in white with pale blue edges.
- `fx_slash_heavy`: A thick heavy overhead chop arc in white and orange, with three small chips flying off.
- `fx_stab`: A narrow straight thrust streak ending in a small four-point star, white and pale yellow.
- `fx_claw`: Three parallel curved claw-rake marks in white with red edges.
- `fx_bite`: Two rows of jagged white fangs snapping shut, upper and lower, with a red burst between.
- `fx_impact`: A spiky comic-book impact burst: a white many-pointed star with a yellow rim.
- `fx_impact_blunt`: A blunt crushing impact: a white ring crack with chunky fragments flying outward, grey and yellow.
- `fx_crit`: A big dramatic comic impact star in white, yellow and red with long sharp points.
- `fx_block`: A round blue shield-shaped flash with a white rim and two small sparks bouncing off.
- `fx_miss`: A light grey curved whoosh line with a small puff at its end.
- `fx_fire_burst`: A burst of flames: several pointed tongues of fire in red, orange and yellow spreading from a centre.
- `fx_fire_bolt`: A fireball flying to the right: a round orange and yellow core with a trailing pointed tail.
- `fx_water_burst`: A splash of water: a crown of blue droplets and curled waves in deep blue and white.
- `fx_water_bolt`: A ball of water flying to the right with a trailing ribbon of droplets, deep blue and white.
- `fx_ice_burst`: A burst of sharp ice shards in pale blue and white radiating from a centre.
- `fx_ice_bolt`: A long pointed icicle lance flying to the right, pale blue with a white streak.
- `fx_wind_burst`: A swirl of wind: three curled grey-green ribbons spiralling around a centre with small leaves.
- `fx_wind_bolt`: A crescent blade of wind flying to the right, pale grey-green with a white edge.
- `fx_earth_burst`: A burst of rocks and clods: angular brown and grey stones flying up from a centre with green leaf bits.
- `fx_earth_bolt`: A jagged brown boulder flying to the right with a short trail of pebbles.
- `fx_energy_burst`: A burst of lightning: jagged yellow and violet bolts forking out from a white centre.
- `fx_energy_bolt`: A single jagged lightning bolt running left to right, yellow with a violet edge.
- `fx_light_burst`: A sunburst of holy light: a white disc with long gold rays of two lengths.
- `fx_light_bolt`: A slim beam of white-gold light flying to the right with a small star at its tip.
- `fx_dark_burst`: A burst of dark wisps: curling purple-black tendrils around a small violet core.
- `fx_dark_bolt`: A purple-black orb flying to the right with a ragged trailing tail.
- `fx_poison_cloud`: A puff of sickly green poison gas made of three rounded lobes with small bubbles.
- `fx_spores`: A loose cloud of round yellow-green spores of different sizes drifting.
- `fx_blood_splat`: A liquid splat seen from the side in pure white: a main blob with teardrop drops flying off to one side. Pure white only, so it can be tinted.
- `fx_blood_pool`: A flat puddle seen at a low angle in pure white with an irregular rounded edge. Pure white only, so it can be tinted.
- `fx_dust_puff`: A small low puff of dust made of four rounded lobes, pale tan.
- `fx_smoke_puff`: A rounded puff of smoke made of five lobes, light grey with a darker underside.
- `fx_sparkle`: A single four-pointed sparkle star, white with a pale yellow centre.
- `fx_stars_stun`: Three small yellow five-point stars in an arc, as if circling a dazed head.
- `fx_heal`: A rising cluster of green plus signs and two small leaves, bright green and white.
- `fx_shield`: A translucent pale blue dome shield seen from the side with a white hex-pattern rim.
- `fx_buff_up`: Three stacked upward-pointing chevron arrows, orange and yellow.
- `fx_debuff_down`: Three stacked downward-pointing chevron arrows, purple and blue.
- `fx_shockwave`: A flat ground shockwave ring seen at a low angle, white with a yellow inner edge.
- `fx_levelup`: A tall column of golden light with rising stars and a ring at its base.
- `fx_coin`: A single gold coin seen at a slight angle with a simple leaf emblem.
- `fx_confetti`: A scatter of small paper confetti pieces in red, yellow, blue, green and white.
- `fx_snowflake`: A single six-armed snowflake, white with pale blue edges.
- `fx_leaf`: A single falling leaf, green turning yellow at the tip.
- `fx_ember`: A single glowing ember spark, orange with a yellow centre and a tiny tail.
- `fx_freeze`: A jagged block of ice big enough to encase a creature, pale blue with white cracks.
- `fx_chain`: A dark iron chain of six links stretched in a line with a small padlock.
- `fx_charge_ring`: A ring of small pointed light wedges all aimed inward at an empty centre, white and gold.

## hero (15)

- `pose_idle`: a relaxed standing pose: weight on one leg, both arms hanging loosely at his sides with open hands, shoulders down, a calm friendly look
- `pose_idle_b`: a relaxed standing pose: one hand on his hip, the other arm hanging loosely, head turned a little toward the viewer
- `pose_walk_a`: a walking stride to the right: the leg nearer the viewer forward, the other leg back, arms swinging naturally in the opposite way
- `pose_walk_b`: a walking stride to the right: the leg farther from the viewer forward, the other leg back, arms swinging naturally in the opposite way
- `pose_talk`: a friendly talking pose: standing upright, one open hand raised at chest height in a small gesture, mouth slightly open as if speaking
- `pose_cheer`: a victory pose: one fist punched high into the air, the other fist at his hip, a wide grin
- `pose_hurt`: a hurt pose: down on one knee, one hand pressed to his side, head lowered, teeth gritted
- `pose_down`: a defeated pose: collapsed and lying on the ground on his side, eyes closed, one arm stretched out
- `pose_sit`: a resting pose: sitting on the ground cross-legged, leaning forward with both hands held out as if warming them at a fire
- `pose_drink`: a drinking pose: standing with his head tipped back, drinking from a small round red potion bottle held in one hand
- `pose_read`: a reading pose: standing and holding an open book in both hands at chest height, looking down at it
- `pose_gather`: a gathering pose: crouching low on one knee and reaching down toward the ground with one hand
- `pose_wave`: a greeting pose: standing upright with one hand raised high in a wave, smiling
- `pose_point`: a pointing pose: standing and pointing forward to the right with a straight arm, a determined look
- `pose_guard`: a guarding pose: feet planted wide, both forearms crossed in front of his chest and face, bracing against a blow

## npc (38)

- `npc_captain_hale_talk`: npc_captain_hale: talk
- `portrait_captain_hale`: npc_captain_hale: portrait
- `npc_elder_maren_talk`: npc_elder_maren: talk
- `portrait_elder_maren`: npc_elder_maren: portrait
- `npc_ferry_nell_talk`: npc_ferry_nell: talk
- `portrait_ferry_nell`: npc_ferry_nell: portrait
- `npc_gate_guard_talk`: npc_gate_guard: talk
- `portrait_gate_guard`: npc_gate_guard: portrait
- `npc_guild_edda_talk`: npc_guild_edda: talk
- `portrait_guild_edda`: npc_guild_edda: portrait
- `npc_guild_ivo_talk`: npc_guild_ivo: talk
- `portrait_guild_ivo`: npc_guild_ivo: portrait
- `npc_guild_tamsin_talk`: npc_guild_tamsin: talk
- `portrait_guild_tamsin`: npc_guild_tamsin: portrait
- `npc_gunnar_talk`: npc_gunnar: talk
- `portrait_gunnar`: npc_gunnar: portrait
- `npc_hearthkeeper_moll_talk`: npc_hearthkeeper_moll: talk
- `portrait_hearthkeeper_moll`: npc_hearthkeeper_moll: portrait
- `npc_hulda_talk`: npc_hulda: talk
- `portrait_hulda`: npc_hulda: portrait
- `npc_ilsa_talk`: npc_ilsa: talk
- `portrait_ilsa`: npc_ilsa: portrait
- `npc_odo_talk`: npc_odo: talk
- `portrait_odo`: npc_odo: portrait
- `npc_ottilie_talk`: npc_ottilie: talk
- `portrait_ottilie`: npc_ottilie: portrait
- `npc_pell_talk`: npc_pell: talk
- `portrait_pell`: npc_pell: portrait
- `npc_ranger_sabine_talk`: npc_ranger_sabine: talk
- `portrait_ranger_sabine`: npc_ranger_sabine: portrait
- `npc_sled_bram_talk`: npc_sled_bram: talk
- `portrait_sled_bram`: npc_sled_bram: portrait
- `npc_spire_warden_talk`: npc_spire_warden: talk
- `portrait_spire_warden`: npc_spire_warden: portrait
- `npc_tobin_talk`: npc_tobin: talk
- `portrait_tobin`: npc_tobin: portrait
- `npc_wren_talk`: npc_wren: talk
- `portrait_wren`: npc_wren: portrait

## wpn (6)

- `wpn_iron_sword`: A single sword for a fantasy RPG, named "Iron Sword": A sturdy blade from the town forge. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. It must not look like a training sword: a short, broad, leaf-shaped iron blade with a round pommel.
- `wpn_steel_broadsword`: A single sword for a fantasy RPG, named "Steel Broadsword": Reedwatch steel. Heavy, honest and sharp. It is well-made and sturdy, with one clear feature. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. A wide heavy blade with a deep central groove and a broad brass crossguard: clearly a broadsword.
- `wpn_stormcleaver`: A single battle axe for a fantasy RPG, named "Stormcleaver": Sparks jump from its edge on every swing. It is well-made and sturdy, with one clear feature. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another battle axe. Clearly an axe, not a sword: a long haft with one broad crescent axe blade at the top.
- `wpn_tempered_longsword`: A single sword for a fantasy RPG, named "Tempered Longsword": Hearthold steel, quenched in snowmelt. It is fine and master-crafted, with one bold ornament. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. A long slender two-handed blade with a blue-tempered sheen, a long grip and a ring guard.
- `wpn_larchwood_staff`: A single tall wizard's staff for a fantasy RPG, named "Larchwood Staff": Mountain larch, banded in iron and warm to the touch. It is fine and master-crafted, with one bold ornament. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff. A fine staff: pale larch wood ending in a carved spiral crook that cradles a clear crystal.
- `wpn_galecutter`: A single dagger for a fantasy RPG, named "Galecutter": So light it seems to cut before you swing. It is legendary, with an unmistakable shape. Light grey-green accents with feather and swirl shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger. A legendary curved dagger: a sweeping crescent blade like a gust of wind, with a feathered guard.

## mon (15)

- `mon_mossling_wind`: Mossling: wind
- `mon_mossling_strike`: Mossling: strike
- `mon_mossling_hurt`: Mossling: hurt
- `mon_thornback_boar_wind`: Thornback Boar: wind
- `mon_thornback_boar_strike`: Thornback Boar: strike
- `mon_thornback_boar_hurt`: Thornback Boar: hurt
- `mon_ash_imp_wind`: Ash Imp: wind
- `mon_ash_imp_strike`: Ash Imp: strike
- `mon_ash_imp_hurt`: Ash Imp: hurt
- `mon_bramble_bandit_wind`: Bramble Bandit: wind
- `mon_bramble_bandit_strike`: Bramble Bandit: strike
- `mon_bramble_bandit_hurt`: Bramble Bandit: hurt
- `mon_thornheart_wind`: The Thornheart: wind
- `mon_thornheart_strike`: The Thornheart: strike
- `mon_thornheart_hurt`: The Thornheart: hurt

## wpn_all (40) - later, not in `all`

- `wpn_training_sword`: A single sword for a fantasy RPG, named "Training Sword": Dull, but it beats fists. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_worn_staff`: A single tall wizard's staff for a fantasy RPG, named "Worn Staff": An apprentice staff. Hits with magic. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_rusty_dagger`: A single dagger for a fantasy RPG, named "Rusty Dagger": Short, quick, a bit rusty. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_iron_sword`: A single sword for a fantasy RPG, named "Iron Sword": A sturdy blade from the town forge. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. It must not look like a training sword: a short, broad, leaf-shaped iron blade with a round pommel.
- `wpn_oak_staff`: A single tall wizard's staff for a fantasy RPG, named "Oak Staff": Hits with magic. Scales with INT. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_tusk_dagger`: A single dagger for a fantasy RPG, named "Tusk Dagger": Carved from a boar tusk. Quick and sharp. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_hunting_knife`: A single dagger for a fantasy RPG, named "Hunting Knife": A trapper's knife. Quick in the hand. It is plain, worn and humble. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_cinder_fang`: A single dagger for a fantasy RPG, named "Cinder Fang": A dagger of black glass, warm to the touch. It is plain, worn and humble. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_emberbrand`: A single sword for a fantasy RPG, named "Emberbrand": Its edge never quite stops glowing. It is plain, worn and humble. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_ember_wand`: A single short magic wand for a fantasy RPG, named "Ember Wand": A charred wand with a coal for a heart. It is plain, worn and humble. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another short magic wand.
- `wpn_thornheart_blade`: A single sword for a fantasy RPG, named "Thornheart Blade": Carved from the Thornheart. Still smells of spring rain. It is well-made and sturdy, with one clear feature. Moss-green and brown accents with stone, bark and root shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_heartwood_staff`: A single tall wizard's staff for a fantasy RPG, named "Heartwood Staff": A living staff that hums with old magic. It is well-made and sturdy, with one clear feature. Moss-green and brown accents with stone, bark and root shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_briar_fang`: A single dagger for a fantasy RPG, named "Briar Fang": A dagger grown from a single great thorn. It is well-made and sturdy, with one clear feature. Moss-green and brown accents with stone, bark and root shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_steel_broadsword`: A single sword for a fantasy RPG, named "Steel Broadsword": Reedwatch steel. Heavy, honest and sharp. It is well-made and sturdy, with one clear feature. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. A wide heavy blade with a deep central groove and a broad brass crossguard: clearly a broadsword.
- `wpn_yew_staff`: A single tall wizard's staff for a fantasy RPG, named "Yew Staff": Hits with magic. Scales with INT. It is well-made and sturdy, with one clear feature. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_fenwatch_stiletto`: A single dagger for a fantasy RPG, named "Fenwatch Stiletto": A scout's blade, thin enough to slip through mail. It is well-made and sturdy, with one clear feature. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_tidebreaker`: A single sword for a fantasy RPG, named "Tidebreaker": A sea-green blade that drags at whatever it cuts. It is well-made and sturdy, with one clear feature. Deep blue and sea-green accents with wave shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_stormcleaver`: A single battle axe for a fantasy RPG, named "Stormcleaver": Sparks jump from its edge on every swing. It is well-made and sturdy, with one clear feature. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another battle axe. Clearly an axe, not a sword: a long haft with one broad crescent axe blade at the top.
- `wpn_tidecaller_rod`: A single short magic wand for a fantasy RPG, named "Tidecaller Rod": Driftwood and pearl. The air around it is always damp. It is well-made and sturdy, with one clear feature. Deep blue and sea-green accents with wave shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another short magic wand.
- `wpn_sparkglass_wand`: A single short magic wand for a fantasy RPG, named "Sparkglass Wand": A wand of fused lightning-glass. It crackles in damp air. It is well-made and sturdy, with one clear feature. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another short magic wand.
- `wpn_brinefang`: A single dagger for a fantasy RPG, named "Brinefang": A kris with a salt-crusted wavy blade. Its cuts sting. It is well-made and sturdy, with one clear feature. Deep blue and sea-green accents with wave shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_sparkfang`: A single dagger for a fantasy RPG, named "Sparkfang": A crackling spur on a copper hilt. It bites twice. It is well-made and sturdy, with one clear feature. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_thunderhead_maul`: A single two-handed war hammer for a fantasy RPG, named "Thunderhead Maul": Forged around a Stormcoil scale. Every blow rumbles. It is fine and master-crafted, with one bold ornament. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another two-handed war hammer.
- `wpn_stormcoil_scepter`: A single tall wizard's staff for a fantasy RPG, named "Stormcoil Scepter": A coil of living lightning caged in silver. It is fine and master-crafted, with one bold ornament. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_coilfang`: A single dagger for a fantasy RPG, named "Coilfang": A serpent fang that still hums with stored lightning. It is fine and master-crafted, with one bold ornament. Yellow and violet accents with jagged lightning shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_tempered_longsword`: A single sword for a fantasy RPG, named "Tempered Longsword": Hearthold steel, quenched in snowmelt. It is fine and master-crafted, with one bold ornament. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword. A long slender two-handed blade with a blue-tempered sheen, a long grip and a ring guard.
- `wpn_larchwood_staff`: A single tall wizard's staff for a fantasy RPG, named "Larchwood Staff": Mountain larch, banded in iron and warm to the touch. It is fine and master-crafted, with one bold ornament. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff. A fine staff: pale larch wood ending in a carved spiral crook that cradles a clear crystal.
- `wpn_ridgerunner_kris`: A single dagger for a fantasy RPG, named "Ridgerunner Kris": A wavy blade for climbers who fight light. It is fine and master-crafted, with one bold ornament. Plain steel, wood and leather, no magic. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_frostbite`: A single sword for a fantasy RPG, named "Frostbite": A pale blade rimed with frost that never melts. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_hoarfrost_staff`: A single tall wizard's staff for a fantasy RPG, named "Hoarfrost Staff": Ice crystals grow along it overnight. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_icicle_kris`: A single dagger for a fantasy RPG, named "Icicle Kris": Sharp, cold and a little too eager. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_hearthfire_blade`: A single sword for a fantasy RPG, named "Hearthfire Blade": Forged over the Hearthold fire. It smells of pine smoke. It is fine and master-crafted, with one bold ornament. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_kindling_rod`: A single tall wizard's staff for a fantasy RPG, named "Kindling Rod": A rod of charred wood with an ember that will not go out. It is fine and master-crafted, with one bold ornament. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_salamander_kris`: A single dagger for a fantasy RPG, named "Salamander Kris": Scale-hilted and always warm. It is fine and master-crafted, with one bold ornament. Ember-orange and charred-black accents and a faintly glowing hot edge. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_snowmaw_cleaver`: A single battle axe for a fantasy RPG, named "Snowmaw Cleaver": Hewn from the old beast's ice-hard tusk. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another battle axe.
- `wpn_mawtooth_staff`: A single tall wizard's staff for a fantasy RPG, named "Mawtooth Staff": A staff crowned with one of Snowmaw's teeth. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_snowmaw_claw`: A single dagger for a fantasy RPG, named "Snowmaw Claw": A curved claw set in a bone grip. It is fine and master-crafted, with one bold ornament. Pale blue and white accents with frost crystals. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger.
- `wpn_whiteout`: A single sword for a fantasy RPG, named "Whiteout": A greatsword that howls like a blizzard when swung. It is legendary, with an unmistakable shape. Light grey-green accents with feather and swirl shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another sword.
- `wpn_squall_stave`: A single tall wizard's staff for a fantasy RPG, named "Squall Stave": The Frostbinder's own stave. The wind still answers it. It is legendary, with an unmistakable shape. Light grey-green accents with feather and swirl shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another tall wizard's staff.
- `wpn_galecutter`: A single dagger for a fantasy RPG, named "Galecutter": So light it seems to cut before you swing. It is legendary, with an unmistakable shape. Light grey-green accents with feather and swirl shapes. Give it a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another dagger. A legendary curved dagger: a sweeping crescent blade like a gust of wind, with a feathered guard.

## mon_all (93) - later, not in `all`

- `mon_burrow_rat_wind`: Burrow Rat: wind
- `mon_burrow_rat_strike`: Burrow Rat: strike
- `mon_burrow_rat_hurt`: Burrow Rat: hurt
- `mon_dew_slug_wind`: Dew Slug: wind
- `mon_dew_slug_strike`: Dew Slug: strike
- `mon_dew_slug_hurt`: Dew Slug: hurt
- `mon_hedge_pixie_wind`: Hedge Pixie: wind
- `mon_hedge_pixie_strike`: Hedge Pixie: strike
- `mon_hedge_pixie_hurt`: Hedge Pixie: hurt
- `mon_millstone_crab_wind`: Millstone Crab: wind
- `mon_millstone_crab_strike`: Millstone Crab: strike
- `mon_millstone_crab_hurt`: Millstone Crab: hurt
- `mon_dusk_wraith_wind`: Dusk Wraith: wind
- `mon_dusk_wraith_strike`: Dusk Wraith: strike
- `mon_dusk_wraith_hurt`: Dusk Wraith: hurt
- `mon_hedge_troll_wind`: Hedge Troll: wind
- `mon_hedge_troll_strike`: Hedge Troll: strike
- `mon_hedge_troll_hurt`: Hedge Troll: hurt
- `mon_mossling_wind`: Mossling: wind
- `mon_mossling_strike`: Mossling: strike
- `mon_mossling_hurt`: Mossling: hurt
- `mon_thornback_boar_wind`: Thornback Boar: wind
- `mon_thornback_boar_strike`: Thornback Boar: strike
- `mon_thornback_boar_hurt`: Thornback Boar: hurt
- `mon_ash_imp_wind`: Ash Imp: wind
- `mon_ash_imp_strike`: Ash Imp: strike
- `mon_ash_imp_hurt`: Ash Imp: hurt
- `mon_bramble_bandit_wind`: Bramble Bandit: wind
- `mon_bramble_bandit_strike`: Bramble Bandit: strike
- `mon_bramble_bandit_hurt`: Bramble Bandit: hurt
- `mon_barkshell_beetle_wind`: Barkshell Beetle: wind
- `mon_barkshell_beetle_strike`: Barkshell Beetle: strike
- `mon_barkshell_beetle_hurt`: Barkshell Beetle: hurt
- `mon_thornheart_wind`: The Thornheart: wind
- `mon_thornheart_strike`: The Thornheart: strike
- `mon_thornheart_hurt`: The Thornheart: hurt
- `mon_mire_toad_wind`: Mire Toad: wind
- `mon_mire_toad_strike`: Mire Toad: strike
- `mon_mire_toad_hurt`: Mire Toad: hurt
- `mon_spark_wisp_wind`: Spark Wisp: wind
- `mon_spark_wisp_strike`: Spark Wisp: strike
- `mon_spark_wisp_hurt`: Spark Wisp: hurt
- `mon_bog_lurker_wind`: Bog Lurker: wind
- `mon_bog_lurker_strike`: Bog Lurker: strike
- `mon_bog_lurker_hurt`: Bog Lurker: hurt
- `mon_sunken_revenant_wind`: Sunken Revenant: wind
- `mon_sunken_revenant_strike`: Sunken Revenant: strike
- `mon_sunken_revenant_hurt`: Sunken Revenant: hurt
- `mon_stormcrest_heron_wind`: Stormcrest Heron: wind
- `mon_stormcrest_heron_strike`: Stormcrest Heron: strike
- `mon_stormcrest_heron_hurt`: Stormcrest Heron: hurt
- `mon_volt_eel_wind`: Volt Eel: wind
- `mon_volt_eel_strike`: Volt Eel: strike
- `mon_volt_eel_hurt`: Volt Eel: hurt
- `mon_bogmother_wind`: The Bogmother: wind
- `mon_bogmother_strike`: The Bogmother: strike
- `mon_bogmother_hurt`: The Bogmother: hurt
- `mon_stormcoil_wind`: The Stormcoil: wind
- `mon_stormcoil_strike`: The Stormcoil: strike
- `mon_stormcoil_hurt`: The Stormcoil: hurt
- `mon_drift_wolf_wind`: Drift Wolf: wind
- `mon_drift_wolf_strike`: Drift Wolf: strike
- `mon_drift_wolf_hurt`: Drift Wolf: hurt
- `mon_crag_ram_wind`: Crag Ram: wind
- `mon_crag_ram_strike`: Crag Ram: strike
- `mon_crag_ram_hurt`: Crag Ram: hurt
- `mon_gust_sprite_wind`: Gust Sprite: wind
- `mon_gust_sprite_strike`: Gust Sprite: strike
- `mon_gust_sprite_hurt`: Gust Sprite: hurt
- `mon_rime_acolyte_wind`: Rime Acolyte: wind
- `mon_rime_acolyte_strike`: Rime Acolyte: strike
- `mon_rime_acolyte_hurt`: Rime Acolyte: hurt
- `mon_glacier_golem_wind`: Glacier Golem: wind
- `mon_glacier_golem_strike`: Glacier Golem: strike
- `mon_glacier_golem_hurt`: Glacier Golem: hurt
- `mon_ember_salamander_wind`: Ember Salamander: wind
- `mon_ember_salamander_strike`: Ember Salamander: strike
- `mon_ember_salamander_hurt`: Ember Salamander: hurt
- `mon_vent_tortoise_wind`: Vent Tortoise: wind
- `mon_vent_tortoise_strike`: Vent Tortoise: strike
- `mon_vent_tortoise_hurt`: Vent Tortoise: hurt
- `mon_old_snowmaw_wind`: Old Snowmaw: wind
- `mon_old_snowmaw_strike`: Old Snowmaw: strike
- `mon_old_snowmaw_hurt`: Old Snowmaw: hurt
- `mon_frostbinder_wind`: The Frostbinder: wind
- `mon_frostbinder_strike`: The Frostbinder: strike
- `mon_frostbinder_hurt`: The Frostbinder: hurt
- `mon_mimic_wind`: Mimic: wind
- `mon_mimic_strike`: Mimic: strike
- `mon_mimic_hurt`: Mimic: hurt
- `mon_lucky_impling_wind`: Lucky Impling: wind
- `mon_lucky_impling_strike`: Lucky Impling: strike
- `mon_lucky_impling_hurt`: Lucky Impling: hurt

## npc_idle (19) - later, not in `all`

- `npc_captain_hale_idle2`: npc_captain_hale: idle2
- `npc_elder_maren_idle2`: npc_elder_maren: idle2
- `npc_ferry_nell_idle2`: npc_ferry_nell: idle2
- `npc_gate_guard_idle2`: npc_gate_guard: idle2
- `npc_guild_edda_idle2`: npc_guild_edda: idle2
- `npc_guild_ivo_idle2`: npc_guild_ivo: idle2
- `npc_guild_tamsin_idle2`: npc_guild_tamsin: idle2
- `npc_gunnar_idle2`: npc_gunnar: idle2
- `npc_hearthkeeper_moll_idle2`: npc_hearthkeeper_moll: idle2
- `npc_hulda_idle2`: npc_hulda: idle2
- `npc_ilsa_idle2`: npc_ilsa: idle2
- `npc_odo_idle2`: npc_odo: idle2
- `npc_ottilie_idle2`: npc_ottilie: idle2
- `npc_pell_idle2`: npc_pell: idle2
- `npc_ranger_sabine_idle2`: npc_ranger_sabine: idle2
- `npc_sled_bram_idle2`: npc_sled_bram: idle2
- `npc_spire_warden_idle2`: npc_spire_warden: idle2
- `npc_tobin_idle2`: npc_tobin: idle2
- `npc_wren_idle2`: npc_wren: idle2

## blunt (6) - later, not in `all`

- `wpn_cobble_mace`: A plain, humble one-handed mace: a rounded river cobble bound to an oak handle with leather straps and iron nails.
- `wpn_millwrights_maul`: A sturdy two-handed maul with a square iron-banded wooden head, like a millwright's mallet grown huge.
- `wpn_bog_iron_mace`: A well-made flanged mace of dark bog iron, six blunt flanges, a reed-wrapped grip, deep blue and sea-green accents.
- `wpn_tidebreaker`: A fine two-handed war hammer with a head shaped like a breaking wave in blue-grey steel and a long ash haft bound with rope.
- `wpn_frostknuckle_mace`: A fine one-handed mace whose head is a fist-sized lump of blue glacier ice in a steel cage, frost on the haft.
- `wpn_avalanche_maul`: A legendary two-handed maul: a great wedge of snow-capped grey mountain rock on a long dark haft with fur wrapping.

## flask (9) - later, not in `all`

- `icon_poison_flask`: A round glass throwing flask of bubbling sickly green poison, a cork stopper tied with string and a small skull tag.
- `icon_oil_fire`: A small squat bottle of weapon oil: glowing orange oil with a tiny flame on the cork, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_water`: A small squat bottle of weapon oil: clear blue oil with a wave label, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_ice`: A small squat bottle of weapon oil: pale frosty blue oil, the glass rimed with frost, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_wind`: A small squat bottle of weapon oil: swirling pale grey-green oil, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_earth`: A small squat bottle of weapon oil: thick moss-green oil with a leaf label, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_energy`: A small squat bottle of weapon oil: crackling yellow oil with a lightning label, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_light`: A small squat bottle of weapon oil: bright white-gold oil that shines softly, a cork stopper and a cloth rag tied to its neck.
- `icon_oil_dark`: A small squat bottle of weapon oil: inky purple-black oil, a cork stopper and a cloth rag tied to its neck.

## bg (13) - later, not in `all`

- `bg_moss_glade`: A sunny round forest glade ringed by old oaks, the ground soft with thick green moss and dotted with mushrooms and small burrow mounds, shafts of light through the leaves.
- `bg_mill_stream`: A narrow forest path beside a fast stream with a ruined water mill, its broken wheel half in the water, mossy stones and ferns, dimmer and wilder than a road.
- `bg_imp_den`: A scorched hollow in the forest: blackened tree stumps, ash on the ground, small smouldering cracks glowing orange, a dark cave mouth in a burnt bank at the back.
- `bg_root_tunnel`: A secret tunnel under the forest: the walls and ceiling are huge twisted tree roots, glowing pale mushrooms light the way, a little daylight far ahead.
- `bg_boar_run`: A trampled forest track churned to mud, rooted-up earth, snapped saplings and thorn bushes on both sides, tusk-scarred tree trunks.
- `bg_lily_shallows`: A wide still shallows in a marsh under a grey sky, carpets of lily pads and pink bog lilies, low wooden walkway planks, reeds at the edges.
- `bg_toad_wallow`: A muddy marsh wallow with fat bubbling mud pools, toadstools, drooping willows and half-sunk logs in drizzle.
- `bg_wreck_den`: The broken hull of an old river barge stranded in the marsh, ribs showing, a dark opening in its side, ropes and barnacles, storm clouds above.
- `bg_reed_maze`: A narrow hidden path winding between walls of reeds twice a person's height, lantern posts with faded cloth strips, mist.
- `bg_wolf_run`: A snowy mountain meadow crossed by many paw tracks, wind-bent dark pines, a frozen brook, grey peaks behind. Only snow, ice, snow-laden dark pines and bare grey rock: no green leafy trees, no grass, no bushes, no autumn colours. The bottom 30% of the image is flat packed snow running across the full width.
- `bg_ram_ledges`: Broad stepped rock ledges on a snowy mountainside with tufts of frosted grass, a sheer drop and far white peaks behind. Only snow, ice, snow-laden dark pines and bare grey rock: no green leafy trees, no grass, no bushes, no autumn colours. The bottom 30% of the image is flat packed snow running across the full width.
- `bg_ice_cellar`: A cave of blue ice under a glacier: smooth ice walls, hanging icicles, a frozen pool, old wooden props and a dark passage at the back. Only snow, ice, snow-laden dark pines and bare grey rock: no green leafy trees, no grass, no bushes, no autumn colours. The bottom 30% of the image is flat packed snow running across the full width.
- `bg_frozen_falls`: A hidden frozen waterfall in a mountain cleft, a curtain of pale blue ice columns with a narrow gap behind it, snow-laden dark pines. Only snow, ice, snow-laden dark pines and bare grey rock: no green leafy trees, no grass, no bushes, no autumn colours. The bottom 30% of the image is flat packed snow running across the full width.

## helm (9) - later, not in `all`

- `helm_leather_cap`: Leather Cap: Boiled leather. Keeps branches out of your eyes.
- `helm_apprentice_hood`: Apprentice Hood: Warding thread stitched round the rim.
- `helm_scale_coif`: Scale Coif: A hood of small steel scales.
- `helm_reedweave_hood`: Reedweave Hood: Woven reeds with a faint blue glow in the weave.
- `helm_ridgeplate_helm`: Ridgeplate Helm: Thick steel over a fur lining.
- `helm_fleecewool_hood`: Fleecewool Hood: Warm wool with blue warding runes.
- `helm_thornwarden_helm`: Thornwarden Helm: Bark and bramble, grown into a helm.
- `helm_stormwatch_helm`: Stormwatch Helm: Copper-crested. Lightning likes it, and it likes lightning.
- `helm_frostward_helm`: Frostward Helm: Rime-steel that never fogs.

## unique (8) - later, not in `all`

- `icon_unique_heartwood_charm`: A pendant: a knot of dark heartwood with a small green glowing seed at its centre on a braided cord.
- `icon_unique_millstone_ring`: A chunky ring carved from grey millstone with a tiny iron crab claw as its setting.
- `icon_unique_bogmother_veil`: A hood of dark green woven marsh weed hung with small pale shells and one glowing blue bead.
- `icon_unique_stormcoil_band`: An armband of coiled copper wire shaped like a serpent biting its tail, small sparks along it.
- `icon_unique_snowmaw_mantle`: A heavy white fur mantle with a clasp made of two crossed ice-blue fangs.
- `icon_unique_frostbinder_circlet`: A thin circlet of clear ice with three upright frost crystals and a pale blue gem.
- `icon_unique_bandit_gloves`: A pair of patched dark leather gloves with brass knuckle studs and a red wrist wrap.
- `icon_unique_impling_boots`: A pair of small pointed golden boots with tiny wings at the heels and a coin buckle.

## hero_outfits (0) - later, not in `all`

