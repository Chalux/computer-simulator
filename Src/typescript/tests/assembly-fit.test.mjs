import assert from 'node:assert/strict';
import test from 'node:test';
import { inspect, testLoad } from '../../../Resource/agents/default-agent/builtins/assembly-fit.mjs';
import { loadBasemodCatalog } from './fixtures/basemod.mjs';

const bench = loadBasemodCatalog();
const evaluate = changes => inspect(bench, { ...bench.initial, ...changes });

test('48 mm DIMM intersects fan by 6 mm, lifting fan reaches both contact boundaries', () => {
    const initial = evaluate({});
    assert.equal(initial.status, 'BLOCKED');
    assert.equal(initial.issues.length, 1);
    assert.equal(initial.issues[0].overlap.size[1], 6);
    const flush = evaluate({ offset: 6 });
    assert.equal(flush.status, 'OPTIMAL');
    assert.equal(flush.headroom, 0);
    assert.equal(flush.capacity, 32);
    assert.equal(flush.bandwidth, 89.6);
    const excess = evaluate({ offset: 7 });
    assert.equal(excess.status, 'BLOCKED');
    assert.equal(excess.headroom, -1);
    assert.equal(evaluate({ caseIndex: 1, offset: 7 }).status, 'OPTIMAL');
});

test('raising fan cannot resolve a tower collision, changing DIMM height can', () => {
    const result = evaluate({ slots: [0, 3], offset: 6 });
    assert.equal(result.status, 'BLOCKED');
    assert.ok(result.issues.some(i => i.overlap.size[1] === 10));
    assert.equal(evaluate({ ramIndex: 1, slots: [0, 3] }).status, 'OPTIMAL');
});

test('occupied channels determine bandwidth and missing memory blocks assembly', () => {
    const sameChannel = evaluate({ slots: [2, 3] });
    assert.equal(sameChannel.status, 'DEGRADED');
    assert.equal(sameChannel.channels, 1);
    assert.equal(sameChannel.capacity, 32);
    assert.equal(sameChannel.bandwidth, 44.8);
    assert.equal(evaluate({ slots: [] }).status, 'BLOCKED');
});

test('the same height does not collide when X or Z only touches', () => {
    for (const axis of [0, 2]) {
        const sample = structuredClone(bench);
        const fan = sample.cooler.fan_aabb;
        sample.board.slots[1].anchor[axis] = fan.origin[axis] + fan.size[axis];
        assert.equal(inspect(sample, bench.initial).status, 'OPTIMAL');
    }
});

test('controller and board frequency caps apply independently', () => {
    const sample = structuredClone(bench);
    sample.cpuMaxRate = 4800;
    assert.equal(inspect(sample, bench.initial).bandwidth, 76.8);
    sample.board.maxRate = 4000;
    assert.equal(inspect(sample, bench.initial).bandwidth, 64);
});

test('a simultaneous channel warning cannot hide an assembly blocker', () => {
    const result = evaluate({ slots: [1] });
    assert.equal(result.status, 'BLOCKED');
    assert.deepEqual(new Set(result.issues.map(issue => issue.status)), new Set(['BLOCKED', 'DEGRADED']));
});
test('front-mounted 360 takes both core and fan depth away from the GPU bay', () => {
    const front = evaluate({ front360: true });
    assert.equal(front.status, 'BLOCKED');
    assert.equal(front.gpuLimit, 313);
    assert.equal(front.gpuMargin, -22);
    assert.deepEqual(front.issues[0].parts, ['gpu', 'radiator']);
    assert.equal(front.issues[0].overlap.size[0], 22);
    assert.ok(!front.parts.some(p => ['fan','tower'].includes(p.role)));
    assert.equal(evaluate({ front360: true, gpuIndex: 1 }).gpuMargin, 28);
    assert.equal(evaluate({ front360: true, gpuIndex: 1 }).status, 'OPTIMAL');
    assert.equal(evaluate({ front360: true, caseIndex: 1 }).gpuMargin, 23);
    assert.equal(evaluate({ front360: true, caseIndex: 1 }).status, 'OPTIMAL');
});

test('a GPU touching the radiator fits, one extra millimetre is blocked', () => {
    const sample = structuredClone(bench);
    sample.graphics[0].dimensions[0] = 313;
    const setup = {...bench.initial, front360: true};
    assert.equal(inspect(sample, setup).status, 'OPTIMAL');
    sample.liquid.fanThickness += 1;
    const result = inspect(sample, setup);
    assert.equal(result.status, 'BLOCKED');
    assert.equal(result.issues[0].overlap.size[0], 1);
});
test('power tests require a physically assembled sample and use simultaneous loads', () => {
    assert.throws(() => testLoad(bench, bench.initial, 0));
    assert.throws(() => testLoad(bench, { ...bench.initial, front360: true }, 0));
    const setup = { ...bench.initial, front360: true, caseIndex: 1 };
    const pass = testLoad(bench, setup, 0);
    assert.equal(pass.status, 'OPTIMAL');
    assert.equal(pass.sustained, 500);
    assert.equal(pass.peak, 650);
    assert.equal(testLoad(bench, setup, 1).status, 'DEGRADED');
    const trip = testLoad(bench, setup, 2);
    assert.equal(trip.status, 'BLOCKED');
    assert.deepEqual(testLoad(bench, setup, 2), trip);
    assert.equal(testLoad(bench, { ...setup, gpuIndex: 1 }, 2).status, 'OPTIMAL');
});

test('continuous, pulse and recommended limits all allow exact equality', () => {
    const sample = structuredClone(bench);
    const setup = { ...bench.initial, front360: true, caseIndex: 1 };
    const psu = sample.powerSupplies[0];
    psu.continuous = 500;
    psu.recommended = 650;
    psu.pulse = 651;
    assert.equal(testLoad(sample, setup, 0).status, 'OPTIMAL');
    psu.recommended = 649;
    psu.pulse = 650;
    assert.equal(testLoad(sample, setup, 0).status, 'DEGRADED');
    psu.pulse = 649;
    assert.equal(testLoad(sample, setup, 0).status, 'BLOCKED');
    psu.pulse = 700;
    psu.continuous = 499;
    assert.match(testLoad(sample, setup, 0).message, /持续满载/);
});
