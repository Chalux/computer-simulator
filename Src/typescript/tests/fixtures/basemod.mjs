import { readFileSync, readdirSync } from 'node:fs';

const modRoot = new URL('../../../../mods/basemod/content/', import.meta.url);

const readJson = (path) => JSON.parse(readFileSync(new URL(path, modRoot), 'utf8').replace(/^\uFEFF/, ''));

const readFolder = (folder) =>
  readdirSync(new URL(`${folder}/`, modRoot))
    .filter((name) => name.endsWith('.json'))
    .map((name) => ({ ...readJson(`${folder}/${name}`), id: name.slice(0, -5) }))
    .sort((a, b) => (a.order ?? 0) - (b.order ?? 0) || String(a.id ?? '').localeCompare(String(b.id ?? '')));

/**
 * 把 basemod 的分文件内容重组成规则脚本使用的扁平目录。
 * 迁移测试使用旧目录的测试夹具对照规则数据。
 */
export function loadBasemodCatalog() {
  const cases = readFolder('hardware/cases');
  const motherboards = readFolder('hardware/motherboards');
  const processors = readFolder('hardware/processors');
  const graphics = readFolder('hardware/graphics_cards');
  const memory = readFolder('hardware/memory');
  const storage = readFolder('hardware/storage');
  const coolers = readFolder('hardware/coolers');
  const powerSupplies = readFolder('hardware/power_supplies');
  const benchmark = readFolder('benchmarks')[0];

  const air = coolers.find((item) => item.kind !== 'liquid');
  const liquid = coolers.find((item) => item.kind === 'liquid');
  if (!air || !liquid) throw new Error('basemod 缺少风冷或水冷定义');

  const airflows = [...benchmark.airflows].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const workloads = [...benchmark.workloads].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const initial = benchmark.initial;

  return {
    board: {
      name: motherboards[0].name,
      dimensions: motherboards[0].dimensions,
      slots: motherboards[0].slots.map((slot) => ({ name: slot.name, channel: slot.channel, anchor: slot.anchor })),
      maxRate: motherboards[0].maxRate,
      price: motherboards[0].price,
      vrmWatts: motherboards[0].vrmWatts,
      color: motherboards[0].color,
      formFactor: motherboards[0].formFactor,
    },
    cooler: {
      name: air.name,
      body_aabb: air.body_aabb,
      fan_aabb: air.fan_aabb,
      adjustable_y_max: air.adjustable_y_max,
    },
    cases: cases.map((item) => ({
      name: item.name,
      heightLimit: item.heightLimit,
      gpuLimit: item.gpuLimit,
      psuLimit: item.psuLimit,
      naturalAirflow: item.naturalAirflow,
      price: item.price,
      color: item.color,
      formFactor: item.formFactor,
    })),
    ram: memory.map((item) => ({
      name: item.name,
      dimensions: item.dimensions,
      price: item.price,
      color: item.color,
      lighting: item.lighting,
      rgb: item.rgb,
      gb: item.gb,
      rate: item.rate,
    })),
    graphics: graphics.map((item) => ({
      name: item.name,
      dimensions: item.dimensions,
      fanCount: item.fanCount,
      price: item.price,
      performance: item.performance,
      resistance: item.resistance,
      throttle: item.throttle,
      maxFanDb: item.maxFanDb,
      color: item.color,
      lighting: item.lighting,
      testWatts: item.testWatts,
    })),
    liquid: {
      name: liquid.name,
      price: liquid.price,
      resistance: liquid.resistance,
      maxFanDb: liquid.maxFanDb,
      pumpDb: liquid.pumpDb,
      color: liquid.color,
      lighting: liquid.lighting,
      coreThickness: liquid.coreThickness,
      fanThickness: liquid.fanThickness,
      fanSize: liquid.fanSize,
      fanCount: liquid.fanCount,
    },
    powerSupplies: powerSupplies.map((item) => ({
      name: item.name,
      continuous: item.continuous,
      pulse: item.pulse,
      recommended: item.recommended,
      length: item.length,
      price: item.price,
      cable: item.cable,
      noiseDb: item.noiseDb,
    })),
    loadCurve: benchmark.loadCurve.map((point) => ({
      name: point.name,
      cpu: point.cpu,
      other: point.other,
      pulse: point.pulse,
    })),
    processor: {
      name: processors[0].name,
      price: processors[0].price,
      performance: processors[0].performance,
      throttle: processors[0].throttle,
    },
    storage: {
      name: storage[0].name,
      price: storage[0].price,
    },
    benchmark: {
      ambient: benchmark.ambient,
      referenceBandwidth: benchmark.referenceBandwidth,
      airflows: airflows.map((item) => ({
        name: item.name,
        intake: item.intake,
        exhaust: item.exhaust,
        caseNoise: item.caseNoise ?? null,
      })),
      workloads: workloads.map((item) => ({
        name: item.name,
        computeWeights: item.computeWeights,
        weights: item.weights,
        referenceRoi: item.referenceRoi,
        targetMargin: item.targetMargin,
        targetNoise: item.targetNoise,
      })),
    },
    cpuMaxRate: benchmark.cpuMaxRate,
    initial: {
      caseIndex: cases.findIndex((item) => item.id === initial.caseId),
      ramIndex: memory.findIndex((item) => item.id === initial.ramId),
      slots: initial.slots,
      offset: initial.offset,
    },
  };
}
