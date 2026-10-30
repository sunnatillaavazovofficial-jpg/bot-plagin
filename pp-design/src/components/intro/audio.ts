/**
 * Intro uchun ovozlar — hammasi Web Audio API bilan "sintez" qilinadi (tashqi audio fayl kerak emas).
 * Barcha tovushlar boshlanishda vaqt jadvali bo'yicha rejalashtiriladi.
 */
import { SCENES, sceneStart, TOTAL_DURATION } from "./scenes";

type Note = number;

export class IntroAudio {
  readonly ctx: AudioContext;
  private master: GainNode;
  private noise: AudioBuffer;
  private t0 = 0;

  constructor() {
    const Ctor = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    this.ctx = new Ctor();
    const compressor = this.ctx.createDynamicsCompressor();
    compressor.threshold.value = -14;
    compressor.ratio.value = 4;
    this.master = this.ctx.createGain();
    this.master.gain.value = 0.9;
    this.master.connect(compressor).connect(this.ctx.destination);

    const length = this.ctx.sampleRate * 2;
    this.noise = this.ctx.createBuffer(1, length, this.ctx.sampleRate);
    const data = this.noise.getChannelData(0);
    for (let i = 0; i < length; i++) data[i] = Math.random() * 2 - 1;
  }

  setMuted(muted: boolean) {
    const now = this.ctx.currentTime;
    this.master.gain.cancelScheduledValues(now);
    this.master.gain.setTargetAtTime(muted ? 0 : 0.9, now, 0.05);
  }

  close() {
    const now = this.ctx.currentTime;
    this.master.gain.cancelScheduledValues(now);
    this.master.gain.setTargetAtTime(0, now, 0.15);
    setTimeout(() => this.ctx.close().catch(() => undefined), 800);
  }

  /** Butun ovoz jadvalini rejalashtiradi. `offset` — intro boshlanishigacha kechikish. */
  play(offset = 0.05) {
    this.t0 = this.ctx.currentTime + offset;
    const at = (s: number) => this.t0 + s;

    // --- Fon musiqasi: har bir davr uchun o'z akkordi ---
    const chords: Note[][] = [
      [55, 82.41, 110],
      [73.42, 110, 116.54, 146.83],
      [110, 164.81, 220, 261.63],
      [130.81, 196, 261.63, 329.63],
      [174.61, 220, 261.63, 349.23],
      [110, 220, 277.18, 329.63, 440, 554.37],
    ];
    SCENES.forEach((scene, i) => {
      const start = sceneStart(i);
      const end = start + scene.duration + (i === SCENES.length - 1 ? 0 : 0.35);
      this.pad(chords[i], at(start), at(end), i === 5 ? 0.06 : 0.045, i === 4 ? "square" : "sawtooth");
      if (i > 0) this.whoosh(at(start - 0.22), 0.5);
    });

    // Yurak urishidek ritm — kuchayib boradi
    for (let s = 0; s < sceneStart(5) - 0.2; s += 0.5) this.kick(at(s), 0.35 + (s / sceneStart(5)) * 0.5);

    // 1. G'or: gumbirlash, olov chirsillashi, qo'l izlari
    const s0 = sceneStart(0);
    this.boom(at(s0 + 0.02), 0.9);
    for (let k = 0; k < 14; k++) this.crackle(at(s0 + Math.random() * 1.7));
    [0.55, 0.62, 0.69].forEach((p) => this.thud(at(s0 + p * SCENES[0].duration)));

    // 2. Misr: gong va qum shitirlashi
    const s1 = sceneStart(1);
    this.gong(at(s1 + 0.02));
    this.sand(at(s1 + 0.1), 1.3);

    // 3. Uyg'onish: torli cholg'u arpedjiosi
    const s2 = sceneStart(2);
    [220, 261.63, 329.63, 440, 523.25, 659.25].forEach((f, k) => this.pluck(at(s2 + 0.05 + k * 0.17), f));

    // 4. Bosma: press zarbalari va plakatning "shuv" etishi
    const s3 = sceneStart(3);
    [0.08, 0.26, 0.44].forEach((d) => this.thunk(at(s3 + d)));
    this.whoosh(at(s3 + 0.68), 0.45, 1.4);

    // 5. Kompyuter: sichqoncha klik va "bip"lar
    const s4 = sceneStart(4);
    [0.45, 0.58, 0.71, 0.84, 0.97, 1.1].forEach((d, k) => {
      this.click(at(s4 + d));
      this.bleep(at(s4 + d + 0.01), 880 + k * 110);
    });

    // 6. AI: ko'tariluvchi sintez, zarba va jarang
    const s5 = sceneStart(5);
    this.riser(at(s5 - 0.6), 1.7);
    this.impact(at(s5 + 1.12));
    [880, 1108.73, 1318.51, 1760].forEach((f, k) => this.pluck(at(s5 + 1.2 + k * 0.09), f, 0.12));

    // Oxirida jim bo'lish
    this.master.gain.setValueAtTime(0.9, at(TOTAL_DURATION - 0.5));
    this.master.gain.linearRampToValueAtTime(0.0001, at(TOTAL_DURATION + 0.2));
  }

  // ---------------- Tovush "asboblari" ----------------

  private env(gain: GainNode, start: number, attack: number, peak: number, release: number) {
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(peak, start + attack);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + attack + release);
  }

  private pad(notes: Note[], start: number, end: number, level: number, type: OscillatorType) {
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(type === "square" ? 900 : 700, start);
    filter.frequency.linearRampToValueAtTime(type === "square" ? 1800 : 1400, end);
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.linearRampToValueAtTime(level, start + 0.3);
    gain.gain.setValueAtTime(level, end - 0.35);
    gain.gain.linearRampToValueAtTime(0.0001, end);
    filter.connect(gain).connect(this.master);
    for (const f of notes) {
      for (const detune of [-7, 7]) {
        const osc = this.ctx.createOscillator();
        osc.type = type;
        osc.frequency.value = f;
        osc.detune.value = detune;
        osc.connect(filter);
        osc.start(start);
        osc.stop(end + 0.05);
      }
    }
  }

  private noiseSource(start: number, duration: number) {
    const src = this.ctx.createBufferSource();
    src.buffer = this.noise;
    src.loop = true;
    src.start(start, Math.random());
    src.stop(start + duration + 0.05);
    return src;
  }

  private kick(time: number, level: number) {
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.frequency.setValueAtTime(120, time);
    osc.frequency.exponentialRampToValueAtTime(42, time + 0.25);
    this.env(gain, time, 0.005, level, 0.32);
    osc.connect(gain).connect(this.master);
    osc.start(time);
    osc.stop(time + 0.4);
  }

  private boom(time: number, level: number) {
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.frequency.setValueAtTime(70, time);
    osc.frequency.exponentialRampToValueAtTime(30, time + 1.2);
    this.env(gain, time, 0.01, level, 1.3);
    osc.connect(gain).connect(this.master);
    osc.start(time);
    osc.stop(time + 1.4);
  }

  private crackle(time: number) {
    const src = this.noiseSource(time, 0.04);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "highpass";
    filter.frequency.value = 2500 + Math.random() * 3000;
    const gain = this.ctx.createGain();
    this.env(gain, time, 0.002, 0.08 + Math.random() * 0.1, 0.03);
    src.connect(filter).connect(gain).connect(this.master);
  }

  private thud(time: number) {
    const src = this.noiseSource(time, 0.2);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 400;
    const gain = this.ctx.createGain();
    this.env(gain, time, 0.004, 0.5, 0.16);
    src.connect(filter).connect(gain).connect(this.master);
    this.kick(time, 0.4);
  }

  private gong(time: number) {
    [98, 147.3, 196.5, 263.1, 331.2].forEach((f, k) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = "sine";
      osc.frequency.value = f;
      this.env(gain, time, 0.01, 0.22 / (k + 1), 1.8);
      osc.connect(gain).connect(this.master);
      osc.start(time);
      osc.stop(time + 2);
    });
  }

  private sand(time: number, duration: number) {
    const src = this.noiseSource(time, duration);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "bandpass";
    filter.frequency.value = 6000;
    filter.Q.value = 0.8;
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.linearRampToValueAtTime(0.05, time + duration / 2);
    gain.gain.linearRampToValueAtTime(0.0001, time + duration);
    src.connect(filter).connect(gain).connect(this.master);
  }

  private pluck(time: number, freq: number, level = 0.18) {
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    const filter = this.ctx.createBiquadFilter();
    osc.type = "triangle";
    osc.frequency.value = freq;
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(freq * 6, time);
    filter.frequency.exponentialRampToValueAtTime(freq * 1.5, time + 0.6);
    this.env(gain, time, 0.004, level, 0.9);
    osc.connect(filter).connect(gain).connect(this.master);
    osc.start(time);
    osc.stop(time + 1);
  }

  private thunk(time: number) {
    const src = this.noiseSource(time, 0.12);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "bandpass";
    filter.frequency.value = 900;
    const gain = this.ctx.createGain();
    this.env(gain, time, 0.002, 0.5, 0.1);
    src.connect(filter).connect(gain).connect(this.master);
    this.kick(time, 0.55);
  }

  private whoosh(time: number, duration: number, level = 1) {
    const src = this.noiseSource(time, duration);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "bandpass";
    filter.Q.value = 1.2;
    filter.frequency.setValueAtTime(300, time);
    filter.frequency.exponentialRampToValueAtTime(4000, time + duration);
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.linearRampToValueAtTime(0.12 * level, time + duration * 0.7);
    gain.gain.linearRampToValueAtTime(0.0001, time + duration);
    src.connect(filter).connect(gain).connect(this.master);
  }

  private click(time: number) {
    const src = this.noiseSource(time, 0.02);
    const filter = this.ctx.createBiquadFilter();
    filter.type = "highpass";
    filter.frequency.value = 3000;
    const gain = this.ctx.createGain();
    this.env(gain, time, 0.001, 0.25, 0.015);
    src.connect(filter).connect(gain).connect(this.master);
  }

  private bleep(time: number, freq: number) {
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = "square";
    osc.frequency.value = freq;
    this.env(gain, time, 0.003, 0.05, 0.09);
    osc.connect(gain).connect(this.master);
    osc.start(time);
    osc.stop(time + 0.12);
  }

  private riser(time: number, duration: number) {
    const osc = this.ctx.createOscillator();
    const filter = this.ctx.createBiquadFilter();
    const gain = this.ctx.createGain();
    osc.type = "sawtooth";
    osc.frequency.setValueAtTime(110, time);
    osc.frequency.exponentialRampToValueAtTime(440, time + duration);
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(300, time);
    filter.frequency.exponentialRampToValueAtTime(5000, time + duration);
    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.exponentialRampToValueAtTime(0.12, time + duration);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + duration + 0.1);
    osc.connect(filter).connect(gain).connect(this.master);
    osc.start(time);
    osc.stop(time + duration + 0.15);
    this.whoosh(time, duration, 1.3);
  }

  private impact(time: number) {
    this.boom(time, 1);
    [220, 277.18, 329.63, 440].forEach((f) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = "sawtooth";
      osc.frequency.value = f;
      const filter = this.ctx.createBiquadFilter();
      filter.type = "lowpass";
      filter.frequency.setValueAtTime(4000, time);
      filter.frequency.exponentialRampToValueAtTime(600, time + 1.2);
      this.env(gain, time, 0.01, 0.07, 1.3);
      osc.connect(filter).connect(gain).connect(this.master);
      osc.start(time);
      osc.stop(time + 1.4);
    });
  }
}
