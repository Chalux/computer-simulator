type Vec = [number, number, number];
type Volume = { origin: Vec; size: Vec };
type Part = Volume & { id: string; role: string; fans?: number; rgb?: boolean; fanThickness?: number };
export type Setup = { caseIndex: number; ramIndex: number; slots: number[]; offset: number; front360?: boolean; gpuIndex?: number; psuIndex?: number };
export type Catalog = {
    board: { dimensions: Vec; slots: { name: string; channel: string; anchor: Vec }[]; maxRate: number };
    cooler: { body_aabb: Volume[]; fan_aabb: Volume; adjustable_y_max: number };
    cases: { heightLimit: number; gpuLimit: number; psuLimit: number }[];
    graphics: { name: string; dimensions: Vec; fanCount: number; testWatts: number[] }[];
    liquid: { coreThickness: number; fanThickness: number; fanSize: number; fanCount: number };
    ram: { dimensions: Vec; gb: number; rate: number; rgb: boolean }[];
    loadCurve: { name: string; cpu: number; other: number; pulse: boolean }[];
    powerSupplies: { name: string; continuous: number; pulse: number; recommended: number; length: number; cable: string }[];
    cpuMaxRate: number;
};
type Issue = { status: 'BLOCKED' | 'DEGRADED'; message: string; parts: string[]; overlap?: Volume; excess?: number };

function intersection(a: Volume, b: Volume): Volume | undefined {
    const origin: Vec = [0, 0, 0];
    const size: Vec = [0, 0, 0];
    for (let axis = 0; axis < 3; axis++) {
        origin[axis] = Math.max(a.origin[axis], b.origin[axis]);
        size[axis] = Math.min(a.origin[axis] + a.size[axis], b.origin[axis] + b.size[axis]) - origin[axis];
        if (size[axis] <= 0) return undefined;
    }
    return { origin, size };
}

// World axes: X crosses the slots, Y rises from the PCB, Z follows the DIMMs.
// Catalog dimensions use [length, width, height]; AABBs use world XYZ in mm.
export function inspect(catalog: Catalog, setup: Setup, psuIndex = setup.psuIndex ?? 0) {
    const { board, cooler } = catalog;
    const ram = catalog.ram[setup.ramIndex];
    const chassis = catalog.cases[setup.caseIndex];
    const occupied = new Set(setup.slots);
    if (!ram || !chassis || !Number.isInteger(setup.offset) || setup.offset < 0 || setup.offset > cooler.adjustable_y_max
        || occupied.size !== setup.slots.length || setup.slots.some(i => !Number.isInteger(i) || !board.slots[i])) {
        throw new RangeError('无效的工作台配置');
    }
    let psuLimit: number | undefined;
    let psuMargin: number | undefined;
    const parts: Part[] = [];
    const issues: Issue[] = [];
    const [length, width, thickness] = board.dimensions;
    parts.push({ id: 'board', role: 'board', origin: [0, -thickness, 0], size: [width, thickness, length] });
    const towers = cooler.body_aabb.map((bounds, i) => ({ ...bounds, id: 'tower' + i, role: 'tower' }));
    const { origin: [x, y, z], size } = cooler.fan_aabb;
    const fan: Part = { id: 'fan', role: 'fan', origin: [x, y + setup.offset, z], size };
    const obstacles = setup.front360 ? [] : [...towers, fan];
    parts.push(...obstacles);
    let gpuLimit: number | undefined;
    let gpuMargin: number | undefined;
    let caseDepth = length;
    let caseLength = width;
    if (setup.front360) {
        const card = catalog.graphics[setup.gpuIndex ?? 0];
        if (!card) throw new RangeError('无效的显卡');
        const [cardLength, cardThickness, cardHeight] = card.dimensions;
        const liquid = catalog.liquid;
        const depth = liquid.coreThickness + liquid.fanThickness;
        caseLength = chassis.gpuLimit;
        caseDepth = liquid.fanSize * liquid.fanCount;
        gpuLimit = caseLength - depth;
        gpuMargin = gpuLimit - cardLength;
        const gpu: Part = { id: 'gpu', role: 'gpu', fans: card.fanCount,
            origin: [0, 6, 202], size: [cardLength, cardHeight, cardThickness] };
        const radiator: Part = { id: 'radiator', role: 'radiator', fans: liquid.fanCount, fanThickness: liquid.fanThickness,
            origin: [gpuLimit, 5, 0], size: [depth, liquid.fanSize, caseDepth] };
        parts.push(gpu, radiator, { id: 'pump', role: 'pump', origin: [68, 0, 91], size: [60, 43, 60] });
        if (gpuMargin < 0) issues.push({ status: 'BLOCKED', parts: [gpu.id, radiator.id],
            excess: -gpuMargin, overlap: intersection(gpu, radiator),
            message: `显卡长 ${cardLength} mm，前置冷排含风扇占 ${depth} mm，有效限长 ${gpuLimit} mm，干涉 +${-gpuMargin} mm。请换短卡或大机箱。` });
        const psu = catalog.powerSupplies[psuIndex];
        if (!psu) throw new RangeError('无效的电源');
        const reserve = psu.cable === '全模组' ? 0 : 20;
        psuLimit = chassis.psuLimit;
        psuMargin = psuLimit - psu.length - reserve;
        if (psuMargin < 0) {
            const reserveText = reserve > 0 ? `，${psu.cable}需预留 ${reserve} mm 藏线` : '';
            issues.push({ status: 'BLOCKED', parts: [], excess: -psuMargin,
                message: `电源长 ${psu.length} mm${reserveText}（有效占用 ${psu.length + reserve} mm），机箱电源仓限长 ${psuLimit} mm，干涉 +${-psuMargin} mm。请换短电源或换大机箱。` });
        }
    }
    const channels = new Set<string>();
    const [ramLength, ramWidth, ramHeight] = ram.dimensions;
    for (const [i, slot] of board.slots.entries()) {
        const [sx, sy, sz] = slot.anchor;
        parts.push({ id: 'slot' + i, role: 'slot', origin: [sx - 1, sy, sz - 2], size: [ramWidth + 2, 4, ramLength + 4] });
        if (!occupied.has(i)) continue;
        channels.add(slot.channel);
        const stick: Part = { id: slot.name, role: 'ram', rgb: ram.rgb, origin: slot.anchor, size: [ramWidth, ramHeight, ramLength] };
        parts.push(stick);
        for (const obstacle of obstacles) {
            const overlap = intersection(stick, obstacle);
            if (!overlap) continue;
            const clearance = obstacle.origin[1] - sy;
            const label = obstacle === fan ? '前扇' : '塔体';
            const remedy = obstacle === fan ? '可上移前扇、换槽或换矮马甲。' : '可换槽或换矮马甲。';
            issues.push({ status: 'BLOCKED', parts: [stick.id, obstacle.id], overlap,
                message: slot.name + ' 内存高 ' + ramHeight + ' mm，' + label + '净空 ' + clearance
                    + ' mm，干涉 +' + (ramHeight - clearance) + ' mm。' + remedy });
        }
    }
    const heightLimit = chassis.heightLimit;
    const panel: Part = { id: 'panel', role: 'panel', origin: [0, heightLimit, 0], size: [caseLength, 2, caseDepth] };
    const tallest = Math.max(...parts.map(p => p.origin[1] + p.size[1]));
    for (const part of parts) {
        const excess = part.origin[1] + part.size[1] - heightLimit;
        if (excess > 0) issues.push({ status: 'BLOCKED', parts: [part.id, panel.id], excess, overlap: intersection(panel, part),
            message: '侧板限高 ' + heightLimit + ' mm，超出 ' + excess + ' mm。请降低前扇或换宽机箱。' });
    }
    parts.push(panel);
    if (!channels.size) issues.push({ status: 'BLOCKED', parts: [], message: '未安装内存，请选择至少一个插槽。' });
    if (channels.size === 1) issues.push({ status: 'DEGRADED', parts: [], message: '仅启用一个内存通道。分别使用 A、B 通道可恢复双通道带宽。' });
    const status = issues.some(i => i.status === 'BLOCKED') ? 'BLOCKED' : issues.length ? 'DEGRADED' : 'OPTIMAL';
    const rate = Math.min(board.maxRate, catalog.cpuMaxRate, ram.rate);
    return { parts, issues, status, heightLimit, gpuLimit, gpuMargin, psuLimit, psuMargin,
        headroom: heightLimit - tallest, channels: channels.size,
        capacity: occupied.size * ram.gb, bandwidth: channels.size * rate * 8 / 1000 };
}

export function testLoad(catalog: Catalog, setup: Setup, psuIndex = setup.psuIndex ?? 0) {
    const fit = inspect(catalog, setup, psuIndex);
    if (!setup.front360 || fit.status === 'BLOCKED')
        throw new Error('完整水冷样机须先通过装配检查');
    const psu = catalog.powerSupplies[psuIndex];
    if (!psu) throw new RangeError('无效的电源');
    const card = catalog.graphics[setup.gpuIndex ?? 0];
    const curve = catalog.loadCurve.map((point, index) => ({
        name: point.name, pulse: point.pulse, watts: point.cpu + card.testWatts[index] + point.other
    }));
    const trip = curve.findIndex(s => s.watts > (s.pulse ? psu.pulse : psu.continuous));
    const samples = trip < 0 ? curve : curve.slice(0, trip + 1);
    const sustained = Math.max(...samples.filter(s => !s.pulse).map(s => s.watts));
    const peak = Math.max(...samples.map(s => s.watts));
    const failed = curve[trip];
    const status = failed ? 'BLOCKED' : peak > psu.recommended ? 'DEGRADED' : 'OPTIMAL';
    const message = failed ? `${failed.name}达到 ${failed.watts} W，超出${failed.pulse ? '短时' : '持续'}承载 ${failed.pulse ? psu.pulse : psu.continuous} W，电源保护断电。`
        : status === 'DEGRADED' ? `测试通过。峰值超过建议上界 ${psu.recommended} W，瞬时余量偏低。` : '测试通过。持续负载与瞬时峰值均有足够余量。';
    return { status, sustained, peak, samples, message };
}

export function testLoadJson(catalog: string, setup: string, psuIndex: number): string {
    return JSON.stringify(testLoad(JSON.parse(catalog), JSON.parse(setup), psuIndex));
}

export function inspectJson(catalog: string, setup: string): string {
    return JSON.stringify(inspect(JSON.parse(catalog), JSON.parse(setup)));
}
