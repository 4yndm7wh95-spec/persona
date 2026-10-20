你在用户的 Windows 电脑上工作。任务：用 imagegen 一次性把储君**剩下的 71 张**卡图按 v3 画风重绘（每张只出 **1 张**，不出 b 版），另外把第 3 批里待审的 7 张 A 版转成定稿。只做美术，不改代码，不启动游戏。
仓库 / git / 稀疏检出 / 输入素材 / 规格 / 交付 / 自检 / 提交 / 绝对不要做 的规则全部沿用 docs/codex/codex_card_art.md（【1】【2】【4】【6】【7】【9】和末尾一节），下面只写这次不一样的地方。

【0. 背景】
- 第 1、3 批（22 张）已按 v3 画风完成，用户看过：画风思路没问题，按这个思路继续。用户评价过的已改好；没评价的基本都能用。
- 用户要求：剩余的卡**一次性生成完**，**每张只生成一张**。

【1. 先把第 3 批待审的 7 张转成定稿】
把 art\cards\v3\cand\ 下面这 7 张原样复制成 art\cards\v3\<名字>.png（覆盖不存在的文件，不改像素）：
refine_blade_a、solar_strike_a、spoils_of_battle_a、wrought_in_war_a、begone_a、alignment_a、bulwark_a（去掉 _a 后缀）。
README 第 3 批表格对应行后面追加一句"用户：没评价的基本都能用，转为定稿"（只追加，不改原文）。

【2. 参考图（每张都喂给 imagegen）】
- Reference1 = 原版卡图 research\regent_assets\images\packed\card_portraits\regent\<名字>.png（只作构图参考，不是编辑对象）。
- Reference2 = 风格板：把已定稿的 art\cards\v3\strike_regent.png、defend_regent.png、tyranny.png、the_smith.png 拼成 2×2（每格 500×380，无边框无字），存到本机中间目录（不提交）。第 3 批用的就是这块板，沿用即可。
- Reference3（画面里出现结城理的脸时才加）= 脸部定稿参考：art\cards\v3\photon_cut.png 和 art\cards\v3\convergence.png 里结城理的脸（这两张的脸型用户确认过）。

【3. 提示词】
每张的提示词 = 下面的【公共前缀】原文 + 一个空格 + 【二色带】里按卡牌类型选的那一句 + 一个空格 + 【5】里这张卡的那一段。不要改写公共前缀。
【公共前缀】（和第 3 批完全一样）
```text
Generate ONE opaque full-bleed landscape game card 1000:760 aspect. Reference1 ORIGINAL composition guide, not edit target: preserve large subject scale, positions and recognizable action. Reference2 four-panel approved P3R STYLE BOARD ONLY, do not make panels. Repaint in clean Persona3Reload anime hard two-tone cel shading, dark outlines, big flat shapes legible at250x190. At least60% navy071a4a/royalblue1f5cff/cyan39d8ff/icewhite d8f4ff. Background2-3 LARGE flat diagonal menu blocks only; one secondary stripe under25% together with signature effect. No particles/tiny dots/scattered shards/bloom/glossy gradients/tiny highlights/scenery detail. ONE main P3R motif specified. Bare pale teenage hands, black uniform blazer WHITE shirt cuffs, NO gloves/red wrist stripes. Makoto Yuki: slender teenage boy, long navy fringe hides one eye, narrow calm blue-grey visible eye, small straight neutral mouth, slender oval face fine nose, black blazer white shirt red-white neck headphones and upper-arm red armband. Face identity use supporting ref3 when present; not generic cheerful hero. Enemy MUST be redesigned black-fluid Shadow with BLUE arcana mask, no original monster eyes/tentacles/outline merely recolored. Regent servants are Persona creatures, never human hands; no Regent armor/hats/cloak/gold gauntlets/star swordguards. Faces/hands/weapontips/main effects within x8-92%,y12-75% safe box. No words/letters/numbers/watermark/UI/borders.
```
竖版的 meteor_shower、the_sealed_throne 把开头的 "landscape game card 1000:760 aspect" 换成 "portrait game card 606:852 aspect"，其余不变。

【二色带】（按 README「画风调整」：避免全是一个蓝；原版主色仍保留在主体特效上）
- 攻击（A）：`Secondary stripe: ONE crimson-magenta diagonal stripe.`
- 技能（S）：`Secondary stripe: ONE cyan/ice-white diagonal stripe.`
- 能力（P）：`Secondary stripe: ONE muted Dark-Hour yellow-green or dark-green diagonal stripe.`
- 其他（O，reserves / royal_corona）：`Secondary stripe: ONE muted gold diagonal stripe.`

【4. 第 3 批暴露的问题，每张都要防】
- solar_strike 右上多出一个人物、wrought_in_war 多出一个暗影：所以【5】每段末尾都写了 STRICT（画面里只能有哪些东西）。生成后对照 STRICT 检查，多出人物 / 怪物 / 手的重画。
- 风格板里的人物、剑、塔罗盾**不能**被搬进新图（只学画风）。
- 结城理的脸：窄而平静的眼、头发遮住一只眼、嘴小而平；不要变成热血少年脸（不像就带 Reference3 重画）。

【5. 每张卡的专属段落】
格式：名字（类型）：段落原文。照抄进提示词，不要翻译回中文、不要删 STRICT。

**原第 2 批**
- astral_pulse（A）：Original huge pink planet curve in foreground with a monster behind it. Foreground becomes ONE enormous cold white-cyan moon surface arc at the same scale and position. Behind it a black-fluid Shadow with blue arcana mask is blasted backward by a solid pink-violet shockwave ring (original pink kept only on the shockwave). STRICT only moon arc, one Shadow, one shockwave; no people.
- celestial_might（A）：Original enemy tossed in the middle with a giant four-point-star figure behind. Middle: one black-fluid blue-mask Shadow knocked into the air by three solid gold-white impact marks. Behind: ONE giant dark silhouette of Orpheus (slender mechanical humanoid guardian spirit, golden lyre on its back, red scarf) as flat navy shape. STRICT only Shadow, Orpheus silhouette and impact marks.
- cloak_of_stars（S）：Original giant floating cloak with starry lining and two flames. The cloak becomes Makoto's empty black school blazer floating open in mid-air at the same size, lining a flat deep-blue starry pattern of a few large simple stars. The two flames become two solid cyan ribbon streaks. STRICT no person inside the blazer, no hands, no extra objects.
- collision_course（A）：Original enemy rammed diagonally with debris and feathers. One black-fluid blue-mask Shadow slammed diagonally by a solid white-cyan impact wedge; a few LARGE dark rock chunks only. One corner shows a distant flat silhouette of Tartarus tower. STRICT one Shadow, rocks, tower silhouette; no people.
- cosmic_indifference（S）：Original figure crossing arms, body translucent and glowing, two beams from both sides. Makoto half-body, arms crossed over chest, calm face, edges of his body turning flat translucent glass-blue; two solid gold beams enter from left and right as original. STRICT exactly one Makoto, two arms, two beams.
- crescent_spear（A）：Original hand from lower-left raising a crescent-shaped spear glowing magenta. Makoto's bare hand (black sleeve, white cuff) from lower-left raises a long polearm with a crescent moon blade, blade cold white with thin magenta edge. STRICT one hand, one spear; no face.
- crush_under（A）：Original figure smashing downward with small green monsters flattened around. Makoto drops from above with one fist down; a ring of 3-4 small black-fluid blue-mask Shadows squashed flat around the impact; flat blue-white shockwave ring. STRICT one Makoto, small Shadows only.
- gather_light（S）：Original figure at right raising a hand, huge golden four-point star and lightning at left. Makoto at right raising one bare hand; huge golden four-point star at left kept at original size; lightning becomes two solid cyan zigzags. STRICT one Makoto, one star, two zigzags.
- glitterstream（S）：Original figure bending forward, cradling a glowing starlight swirl. Makoto bending forward, two bare hands cupping a cyan flowing light swirl, a few large round bubbles rising like underwater (max 6 bubbles, large). STRICT one Makoto.
- glow（S）：Original two four-point stars and a white arc, effects only. Keep two four-point stars; the white arc becomes a clean crescent moon shape; one large faint Dark Hour moon disk behind. STRICT no people, hands or monsters.
- guiding_star（A）：Original cyan beam striking a monster. Keep the cyan beam at the same angle; it hits one black-fluid blue-mask Shadow; the beam's origin end is a four-point star. STRICT one beam, one Shadow, one star; no people.
- hidden_cache（S）：Original green-rimmed treasure chest burst open by light. The chest becomes a small Velvet Room style deep-blue velvet box with gold corners bursting open with yellow-white light; exactly three P3R tarot cards (blue backs, no text) fly out. STRICT no people.
- know_thy_place（S）：Original hand at upper-left pointing at a cowering small monster at right. Makoto's bare hand from upper-left points down at one small cowering black-fluid Shadow whose blue arcana mask has one clean crack. STRICT one hand, one Shadow; no face.
- patter（S）：Original figure surrounded by stars and lightning, hands folded. Makoto half-body, hands folded in front, calm; a neat ring of 6 large four-point stars and short solid cyan sparks around him. STRICT one Makoto.

**原第 4 批**
- furnace（P）：Original pot of molten metal pouring on the left, forge anvil on the right. Molten metal becomes glowing flat cyan-blue liquid pouring from a dark crucible on the left; simple anvil on the right; deep navy background. STRICT no people.
- gamma_blast（A）：Original figure bringing hands together with a white light orb in the middle. Makoto's two bare hands come together holding a crackling white-and-cyan electric orb (P3R Zio lightning feel, solid shapes). STRICT exactly two hands, optional calm face behind.
- glimmer（S）：Original hand reaching into a blue/pink nebula. Makoto's bare hand reaches into a large flat blue-violet nebula swirl; inside the nebula one P3R tarot card (blue back, no text). STRICT one hand, one card.
- hegemony（A）：Original hand pressing a glowing grid floor, yellow light pillar hitting a bird. Makoto's bare hand presses a flat cyan grid floor; a solid yellow light pillar strikes a bird-shaped black-fluid Shadow with blue mask. STRICT one hand, one Shadow.
- kingly_kick（A）：Original figure kicking and shattering a rock. Makoto mid-kick, leg extended, black uniform trousers and school shoes; the rock under his foot breaks into a few LARGE flat blue chunks. STRICT one Makoto, normal anatomy.
- kingly_punch（A）：Original figure throwing a punch with impact light around the fist. Makoto throws a straight punch with a bare fist toward camera; solid white impact burst ring around the fist. STRICT one Makoto, one punching arm.
- knockout_blow（A）：Original figure swinging a fist, enemy blasted by electric light. Makoto swings a fist; one black-fluid Shadow is blasted away by solid cyan-white lightning, its blue mask cracking. STRICT one Makoto, one Shadow.
- largesse（S）：Original big servant in the dark at left handing a light orb to a figure at right. Left: Makoto as a dark navy silhouette in shadow holding out a glowing orb; right: a tiny fairy Persona with blue wings in a simple leotard receiving it. STRICT only these two figures.
- lunar_blast（S）：Original moon at left, beam shooting at a rock on the right. Keep the moon at left (cold white); a solid ice-blue beam shatters a black-fluid Shadow/rock on the right into a few large chunks (Bufu ice spell feel). STRICT no people.
- manifest_authority（S）：Original figure opening a palm, a spiked halo ring and red arrows. Makoto opens one bare palm toward the viewer; a spiked halo ring behind the hand kept; the red arrows become solid blue-white arrows. STRICT one Makoto.
- monologue（S）：Original big face with a skull in hand and floating text fragments. Makoto close-up, head bowed, holding a plain white mask in one hand; around him a few large simple music-note shapes (MP3 / headphones motif). NO text or letters. STRICT one Makoto, one mask.
- orbit（P）：Original blue light orb with orbit ring and rocks on both sides. Keep the blue orb and its orbit ring; the ring carries simple clock tick marks; two large dark rocks left and right. STRICT no people.
- pale_blue_dot（P）：Original figure at back center, four servants peeking left and right, two blue glass panels in front, tiny white dot in the middle. Makoto at back center; left and right small Persona creatures peek (small snowman imp with blue jester hat, tiny blue-winged fairy, small pumpkin-headed lantern imp with cape); two flat blue glass panels in front; one small blue-white dot of light between them. STRICT one Makoto plus small Personas; no human hands.
- parry（P）：Original figure on the left, a sword slashing from upper-right deflected. Makoto at left blocks with his slender single-edged straight sword; a black-fluid Shadow's blade slashes down from upper-right; one solid white spark burst at the crossing point. STRICT one Makoto, one Shadow arm with blade.

**原第 5 批**
- particle_wall（S）：Original plant monster and a wall of blue blocks. A plant-like black-fluid Shadow with blue mask on one side; a wall of large flat blue glass blocks in front of it. STRICT no people.
- plot（S）：Original figure and a skeleton leaning together looking at a green light. Makoto and a hooded figure wearing a blue mask lean together over a small sickly Dark-Hour green light. STRICT exactly these two figures.
- pillar_of_creation（P）：Original figure hugging a huge glowing pillar. Beside Makoto rises a huge glowing blue pillar shaped like a Tartarus obelisk; Makoto's hand rests on it. STRICT one Makoto, one pillar.
- prophesize（S）：Original figure bowing head, cradling an orange glowing constellation sphere. Makoto bows his head, two bare hands cradle an orange glowing sphere with one P3R tarot card floating inside. STRICT one Makoto.
- quasar（S）：Original nebula vortex between two hands. Makoto's two bare hands with a flat blue nebula vortex between them. STRICT exactly two hands.
- radiate（A）：Original tiny glowing humanoid in the center with radial light. Small glowing silhouette of Makoto in the center; original radial gold light rays kept; deep navy background. STRICT one small silhouette.
- reflect（S）：Original figure facing a glowing translucent double. Makoto facing a glowing translucent double of himself (flat glass/water reflection), like a mirror. STRICT exactly two figures that are mirror images.
- resonance（S）：Original figure with a tree-like shape and green-yellow swirls. Makoto with eyes closed, headphones on; concentric yellow-green sound-wave swirls spread from the headphones. STRICT one Makoto.
- royal_gamble（S）：Original pointed hat looking down from above, a hand holding a fanned hand of cards below. Top: Makoto looking down from above, only his hair and one eye visible; bottom: his bare hand holds a fan of five P3R tarot cards (blue backs, no text). STRICT no hat.
- shining_strike（A）：Original huge golden light hitting an enemy. Keep the huge golden light strike; it hits one black-fluid blue-mask Shadow, which breaks into a few large black pieces. STRICT no people.
- spectrum_shift（P）：Original rainbow light band from lower-left to upper-right. Keep the rainbow band from lower-left to upper-right, made of a few large flat faceted glass segments. STRICT no people.
- stardust（A）：Original hand sprinkling stardust downward. Makoto's bare hand sprinkles gold stardust downward (gold kept, few large flecks); where it lands it becomes a flat blue water surface. STRICT one hand.
- summon_forth（S）：Original servant kneeling on one knee, a sword flying in from the left. Orpheus (slender mechanical humanoid guardian spirit, white-and-gold body, golden lyre on its back, red scarf) kneels on one knee; a glowing sword flies from the left into its hand, like a presentation. STRICT no humans.
- supermassive（A）：Original glowing yellow-green ring between the fingers, green background. Makoto's fingers hold a glowing yellow-green ring (kept); background Dark-Hour dark green plus navy. STRICT one hand.

**原第 6 批**
- terraforming（S）：Original figure pinching a glowing star net. Makoto's fingers pinch a glowing web-like star chart. STRICT one hand or one Makoto.
- arsenal（P）：Original figure in the center surrounded by floating weapons and shields. Makoto in the center surrounded by floating slender swords and P3R tarot cards (max 8 objects, large). STRICT one Makoto.
- beat_into_shape（A）：Original raised hand holding glowing metal being hammered. Makoto raises a hand holding a lump of glowing blue metal; short solid cyan sparks around it. STRICT one Makoto.
- bombardment（A）：Original huge fiery meteor diving diagonally. Keep the orange fiery meteor diving diagonally; its tail is solid blue; distant Tartarus tower silhouette in the background. STRICT no people.
- bundle_of_joy（S）：Original servant holding a golden stone, Regent behind. A small snowman imp Persona with a blue jester hat holds up a golden gem; Makoto stands behind it. STRICT one Makoto, one imp.
- comet（A）：Original green enemy hit by a blue-white comet. Keep the blue-white comet; it hits one black-fluid blue-mask Shadow. STRICT no people.
- crash_landing（A）：Original figure crashing down from the sky, ground cracking. Makoto crashes down from the sky, ground breaks into a few large flat blue slabs; cyan-white shockwave. STRICT one Makoto.
- decisions_decisions（S）：Original figure bending forward juggling golden balls. Makoto bends forward juggling three P3R tarot cards (Shuffle Time motif) with a few large gold glints. STRICT one Makoto, three cards.
- dying_star（A）：Original huge star exploding with rock debris flying. Keep the exploding star; debris becomes a few large flat pieces; two black-fluid Shadows blown away. STRICT no people.
- foregone_conclusion（S）：Original rolled-up parchment with a green star chart. A rolled-up paper showing a green star chart plus a simple clock face. STRICT no people, no text.
- genesis（P）：Original hand reaching from upper-right touching a big four-point star with a fingertip. Makoto's bare hand from upper-right touches a big four-point star with a fingertip (Creation of Adam composition); deep navy starry background with few large stars. STRICT one hand.
- guards（S）：Original two big servants bending from left and right to protect a star in the center. Left: Orpheus (slender mechanical humanoid guardian spirit, white-and-gold body, golden lyre, red scarf); right: Thanatos (towering black reaper-like guardian spirit, pale skull-like mask, ring of chained black coffins behind its back); both bend in to shield a star in the center. STRICT no humans.
- hammer_time（P）：Original figure swinging a big hammer. Makoto swings a big hammer; hammer head flat blue glass. STRICT one Makoto, one hammer.
- heavenly_drill（A）：Original figure pushing a huge drill. Makoto pushes forward a huge spiral drill of cyan-white light. STRICT one Makoto.

**原第 7 批**
- heirloom_hammer（A）：Original figure lifting a huge hammer whose head holds something blue and glowing. Makoto lifts a huge hammer; a glowing blue glass core is set in the hammer head. STRICT one Makoto, one hammer.
- i_am_invincible（S）：Original glowing blue grid humanoid with arms spread. Makoto as a glowing blue wireframe silhouette with arms spread; behind him a flat silhouette of Orpheus. STRICT these two only.
- make_it_so（A）：Original figure waving, a swarm of cyan projectiles flying out. Makoto waves a hand; a swarm of cyan light bolts shaped like blue butterflies flies out (max 7, large). STRICT one Makoto.
- monarchs_gaze（P）：Original green round monster surrounded by orange flashes. One round black-fluid Shadow with a blue arcana mask, locked on by orange gaze lines / flashes from around it. STRICT no people.
- neutron_aegis（P）：Original figure surrounded by atom orbits. Makoto surrounded by blue-white atom orbit rings. STRICT one Makoto.
- royalties（P）：Original servant's big hand scattering gold coins. A small pumpkin-headed lantern imp Persona with a cape scatters gold coins (gold kept, large coins). STRICT no humans.
- seeking_edge（P）：Original inverted humanoid pierced by a red beam. An inverted hanging silhouette of Makoto (Hanged Man pose) pierced by one red beam. STRICT one silhouette.
- seven_stars（A）：Original line of stars hitting several enemies. The Big Dipper's seven stars joined by a line, the line striking three black-fluid blue-mask Shadows. STRICT no people.
- sword_sage（P）：Original figure surrounded by a ring of glowing swords. Makoto surrounded by a ring of slender blue glass swords. STRICT one Makoto.
- tutor（S）：Original figure sitting at a desk with a big star above the head. Makoto sitting at a school desk (Gekkoukan classroom study feel), a big four-point star above his head. STRICT one Makoto.
- void_form（P）：Original blue glowing humanoid whose head is a star, halo rings around. Makoto's blue glowing silhouette whose head is a four-point star, with halo rings around. STRICT one silhouette.
- meteor_shower（A，竖版 606:852）：Original red humanoid standing in the middle, rocks around, blue light falling from the sky. Makoto standing in the middle, a few large rocks around, original blue light pillars falling from the sky kept. STRICT one Makoto.
- the_sealed_throne（P，竖版 606:852）：Original floating stone throne in a cave. A floating throne; background the inside of Tartarus (twisted dark-blue walls, flat shapes). STRICT no people.

**其他（游戏里存在、不在卡池里）**
- reserves（O）：Original golden chest or jar chained shut with gold light bursting out. A deep-blue Velvet Room velvet box chained shut, gold light bursting out. STRICT no people.
- royal_corona（O）：Original crown made of linked stars. A crown made of linked blue starlight floating above a dark silhouette of Makoto's head. STRICT one silhouette.

【6. 生成规则（和以前不同的地方）】
- 每张只生成 1 张，直接作为定稿：art\cards\v3\<名字>.png；同一张也复制一份到 art\cards\v3\cand\<名字>_a.png（保持和以前一致）。
- 只有下面几种情况才重画那一张（最多 2 次）：没有按 STRICT（多了人物 / 怪物 / 手）、有文字、尺寸 / 透明错、脸明显不是结城理、手指或肢体明显错。其他小问题如实写进 README，不要重画。
- 被安全拒绝：换中性描述重试一次，仍被拒就跳过，写进 README 并在报告里说明。
- 每生成 15 张左右就提交并推送一次（提交信息 "art: P3R-style card portraits v3, rest part <N>"），防止长任务中途丢失。

【7. 交付】
- 预览：art\cards\v3\preview\rest_<N>.png（每部分一张：每行 原版 | 新图，左上角写卡名）；
  套卡框：python <repo>\tools\ui\card_mockup.py <repo>\art\cards\v3\preview\in_frame_rest_<N>.png <这部分的新图...>。
- README.md 追加「剩余卡一次性生成」一节：每张一行（卡名 | 是否重画 | 已知问题），再附每张的提示词全文。
- 报告（中文，简短）：生成了几张、哪些重画 / 跳过、各部分提交号、预览路径。
