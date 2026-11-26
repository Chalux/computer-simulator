import { inspect, testLoad, type Catalog, type Setup } from './assembly-fit.mjs';

type Finish = { price: number; color: string };
type Card = Finish & { performance: number; resistance: number; throttle: number; maxFanDb: number; lighting: string };
type Airflow = { name: string; intake: number; exhaust: number; caseNoise: number | null };
type Workload = { name: string; computeWeights: number[]; weights: number[]; referenceRoi: number; targetMargin: number; targetNoise: number };
type Bench = Catalog & {
    board: Finish & { vrmWatts: number; formFactor: string };
    cases: (Finish & { naturalAirflow: number; formFactor: string })[];
    ram: (Finish & { lighting: string })[];
    graphics: Card[];
    liquid: Finish & { maxFanDb: number; pumpDb: number; resistance: number; lighting: string };
    processor: { price: number; performance: number; throttle: number };
    storage: { price: number };
    powerSupplies: { price: number; cable: string; length: number; noiseDb: number }[];
    benchmark: { ambient: number; referenceBandwidth: number; airflows: Airflow[]; workloads: Workload[] };
};

const round = (n: number) => Math.round(n * 10) / 10;
export function rating(raw: number) {
    const score = round(Math.max(0, raw));
    const grade = score >= 100 ? 'S' : score >= 85 ? 'A' : score >= 70 ? 'B' : score >= 55 ? 'C' : score >= 40 ? 'D' : 'E';
    return { score, grade };
}

export function reportScores(values: number[], weights: number[]) {
    if (values.length !== 6 || weights.length !== 6 || values.some(v => !Number.isFinite(v)) ||
        weights.some(w => !Number.isFinite(w) || w < 0) || Math.abs(weights.reduce((a,b) => a+b, 0) - 1) > 1e-6)
        throw new RangeError('六维分数或用途权重无效');
    const axes = values.map(rating);
    const total = rating(axes.reduce((sum, axis, i) => sum + axis.score * weights[i], 0));
    return { axes, total };
}

export function thermalState(ambient: number, watts: number, resistance: number, airflow: number, limit: number) {
    if (!(airflow > 0)) throw new RangeError('通风效率必须为正数');
    const temperature = ambient + watts * resistance / airflow;
    const margin = limit - temperature;
    // Three linear fan-curve sections, followed by forced full speed on throttle.
    const duty = temperature <= 45 ? 0.30 : temperature <= 70 ? 0.30 + (temperature - 45) * 0.014 :
        margin >= 0 ? Math.min(1, 0.65 + 0.35 * (temperature - 70) / (limit - 70)) : 1;
    const factor = Math.max(0.35, 1 + Math.min(0, margin) * 0.018);
    return { temperature, margin, duty, factor };
}

function fanDb(maxDb: number, duty: number) {
    return maxDb + 32 * Math.log10(duty);
}

function combinedNoise(sources: number[]) {
    return 10 * Math.log10(sources.reduce((energy, db) => energy + 10 ** (db / 10), 0));
}

function sensor(state: ReturnType<typeof thermalState>) {
    return { temperature: round(state.temperature), fanPercent: Math.round(state.duty * 100),
        lossPercent: round((1 - state.factor) * 100), throttled: state.margin < 0 };
}

export function benchmark(bench: Bench, setup: Setup & { airflowIndex?: number; workloadIndex?: number }, psuIndex: number) {
    const power = testLoad(bench, setup, psuIndex);
    if (power.status === 'BLOCKED') return { ...power, report: null };
    const fit = inspect(bench, setup, psuIndex);
    const workload = bench.benchmark.workloads[setup.workloadIndex ?? 0];
    const fans = bench.benchmark.airflows[setup.airflowIndex ?? 0];
    if (!workload || !fans) throw new RangeError('未知用途或通风工况');
    const card = bench.graphics[setup.gpuIndex ?? 0];
    const chassis = bench.cases[setup.caseIndex];
    const ram = bench.ram[setup.ramIndex];
    const psu = bench.powerSupplies[psuIndex];
    const cableFactor = psu.cable === '全模组' ? 1 : psu.cable === '半模组' ? 0.98 : 0.95;
    const flow = chassis.naturalAirflow + cableFactor * (0.12 * Math.max(fans.intake, fans.exhaust)
        + 0.58 * Math.min(fans.intake, fans.exhaust)) / 55;
    // Take heat power from the very same sustained sample used for the PSU test.
    const steady = power.samples.findIndex(s => !s.pulse && s.watts === power.sustained);
    const cpuWatts = bench.loadCurve[steady].cpu;
    const ambient = bench.benchmark.ambient;
    const cpu = thermalState(ambient, cpuWatts, bench.liquid.resistance, flow, bench.processor.throttle);
    const gpu = thermalState(ambient, card.testWatts[steady], card.resistance, flow, card.throttle);
    const sources = [bench.liquid.pumpDb, psu.noiseDb, fanDb(bench.liquid.maxFanDb, cpu.duty) + 10*Math.log10(bench.liquid.fanCount),
        fanDb(card.maxFanDb, gpu.duty) + 10*Math.log10(card.fanCount)];
    if (fans.caseNoise !== null) sources.push(fans.caseNoise);
    const noise = combinedNoise(sources);

    // Workload weights are shares of execution time at the reference throughput.
    const subsystems = [bench.processor.performance * cpu.factor * Math.min(1, bench.board.vrmWatts / cpuWatts),
        card.performance * gpu.factor, 100 * fit.bandwidth / bench.benchmark.referenceBandwidth];
    const performance = 1 / workload.computeWeights.reduce((time, weight, i) => time + weight / subsystems[i], 0);
    const prices = [bench.processor.price, bench.storage.price, bench.board.price, chassis.price, bench.liquid.price,
        card.price, psu.price, ...setup.slots.map(() => ram.price)];
    if (prices.some(p => !(p > 0)) || !(workload.referenceRoi > 0)) throw new RangeError('配件参考价和用途基准比必须为正数');
    const price = prices.reduce((sum, p) => sum + p, 0);
    const value = 100 * (performance / price) / workload.referenceRoi;
    const thermals = 100 + (Math.min(cpu.margin, gpu.margin) - workload.targetMargin) * 1.8;
    const acoustics = 100 + (workload.targetNoise - noise) * 3.6;
    const finishes = [chassis, bench.board, bench.liquid, card, ...setup.slots.map(() => ram)];
    const harmony = Math.max(...finishes.map(part => finishes.filter(p => p.color === part.color).length)) / finishes.length;
    const lights = new Set([card.lighting, bench.liquid.lighting, ram.lighting].filter(s => s !== '无'));
    const aesthetics = 50 * harmony + (lights.size <= 1 ? 20 : 8) + 14 * Math.min(1, card.dimensions[0] / fit.gpuLimit!)
        + (bench.board.formFactor === chassis.formFactor ? 12 : 6) + (psu.cable === '全模组' ? 15 : psu.cable === '半模组' ? 9 : 3);
    const reserve = Math.min(1 - power.sustained / psu.continuous, 1 - power.peak / psu.pulse, 1 - cpuWatts / bench.board.vrmWatts);
    const stability = 77 + 110 * reserve - (power.status === 'DEGRADED' ? 16 : 0);
    const scores = reportScores([performance, value, thermals, acoustics, aesthetics, stability], workload.weights);
    const report = { ...scores, workload: workload.name, airflow: fans.name, ambient, noise: round(noise),
        price, cpu: sensor(cpu), gpu: sensor(gpu) };
    const degraded = power.status === 'DEGRADED' || fit.status === 'DEGRADED' || cpu.margin < 0 || gpu.margin < 0 || cpuWatts > bench.board.vrmWatts;
    return { ...power, status: degraded ? 'DEGRADED' : 'OPTIMAL', report };
}

export function benchmarkJson(catalog: string, setup: string, psuIndex: number) {
    return JSON.stringify(benchmark(JSON.parse(catalog), JSON.parse(setup), psuIndex));
}
