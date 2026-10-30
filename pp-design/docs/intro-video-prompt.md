# Intro video: AI video generator uchun prompt

Saytda intro hozir kodda chiziladi (Canvas + Web Audio ovozlar) va qo'shimcha hech narsa talab qilmaydi.
Agar tayyor "kino" darajasidagi video xohlasangiz, quyidagi promptlar bilan **Sora**, **Veo** yoki **Runway**da
video yarating, keyin uni **Admin panel → Intro video** bo'limidan yuklang. Video yuklangach, sayt kod animatsiya
o'rniga o'sha videoni ovozi bilan o'ynatadi.

**Texnik talablar:** 16:9, 1920×1080 (yoki 1280×720), 10–11 soniya, MP4 (H.264 + AAC), 80 MB gacha.
Telefonda ham yaxshi ko'rinishi uchun muhim narsalar kadr markazida bo'lsin.

---

## 1-variant: bitta prompt (generator 10 soniyani bir martada qila olsa)

```
A seamless 10-second cinematic journey through the history of visual design, one continuous camera
push-in, no cuts, smooth morphing transitions between eras. Deep navy blue (#0B1530) color grade
connects all scenes, accent light in cyan (#38C8FF), violet (#7B61FF) and magenta (#FF5CCD).

0–1.7s: Dark prehistoric cave lit by a flickering torch. A hand with red ochre pigment paints a
bison on the rough rock wall; red hand stencils appear around it. Warm firelight, dust particles.
1.7–3.3s: The cave paint morphs into an ancient Egyptian sandstone wall; golden hieroglyphs and a
lapis-blue Eye of Horus are carved in sequence, sunlight sweeping across.
3.3–5.0s: The wall turns into Renaissance parchment: ink lines draw a Vitruvian-style figure inside
a circle and square, a golden spiral and proportion grids, quill pen moving.
5.0–6.6s: A Gutenberg printing press slams down, metal letters print a page; the page slides away
revealing a bold 1920s constructivist poster — red circle, black diagonal bar, yellow triangle.
6.6–8.3s: The poster becomes a 1990s computer screen: a design app interface, a mouse cursor draws
a bezier curve with anchor points, pixels resolve into smooth vectors.
8.3–10.5s: The screen dissolves into thousands of glowing particles forming a neural network in
deep navy space, which converge into the glowing text "PP DESIGN" in a cyan-violet-magenta
gradient. Final frame: the logo on a clean deep navy background.

Style: premium, cinematic, high detail, soft volumetric light, 24fps, shallow depth of field,
no people's faces, no extra text except "PP DESIGN" at the end.
```

## 2-variant: sahnama-sahna (har birini alohida yaratib, montajda ulash)

Har bir klip ~2 soniya. Montajda (CapCut, Premiere, DaVinci) 0.3 soniyalik "cross dissolve" bilan ulang.

| # | Davomiylik | Prompt |
|---|---|---|
| 1 | 0:00–0:01.7 | `Macro shot of a prehistoric cave wall lit by flickering torchlight, a hand with red ochre pigment painting a bison in the style of Lascaux, red hand stencils, warm orange light, dust in the air, cinematic, slow push-in` |
| 2 | 0:01.7–0:03.3 | `Ancient Egyptian sandstone temple wall, golden hieroglyphs and a lapis-blue Eye of Horus appearing as if being carved one by one, warm sunlight beam sweeping across, cinematic, slow push-in` |
| 3 | 0:03.3–0:05.0 | `Renaissance parchment, sepia ink drawing itself: a human figure inside a circle and square (Vitruvian proportions), golden spiral and geometry lines, quill pen, candlelight, cinematic close-up` |
| 4 | 0:05.0–0:06.6 | `A vintage printing press stamping a page with black and red letters, then a bold 1920s constructivist poster slides in: red circle, black diagonal bar, yellow triangle on cream paper, dynamic` |
| 5 | 0:06.6–0:08.3 | `1990s computer screen with a design software interface, a mouse cursor drawing a smooth blue bezier curve with anchor points and handles, pixelated circle turning into a smooth vector shape` |
| 6 | 0:08.3–0:10.5 | `Thousands of glowing particles forming a neural network in deep navy space, converging into glowing 3D text "PP DESIGN" with cyan, violet and magenta gradient light, premium tech aesthetic, final logo reveal` |

**Umumiy uslub (har bir promptga qo'shing):**
`cinematic, premium, 4k, soft volumetric light, deep navy blue color grading, consistent camera push-in, no watermark, no subtitles`

## Ovoz (musiqa va effektlar)

Video generator ovoz bermasa, musiqani **Suno**, **Udio** yoki **ElevenLabs Sound Effects**da yarating:

```
10-second cinematic trailer music: starts with a deep tribal drum and crackling fire, then a
mysterious Egyptian gong, a gentle lute arpeggio, a mechanical printing press thump, retro computer
bleeps and mouse clicks, and ends with a rising synth swell and a powerful modern bass impact with
shimmering high notes. Epic, inspiring, no vocals.
```

Ixtiyoriy ovozli matn (diktor, o'zbekcha, ~10 soniya):

> «G'or devoridagi birinchi chiziqdan… sun'iy intellektgacha. Dizayn — insoniyat bilan birga o'sdi. PP Design.»
