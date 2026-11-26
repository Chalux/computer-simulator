import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { benchmark, rating, reportScores, thermalState } from '../../../Resource/agents/default-agent/builtins/benchmark.mjs';

const bench = JSON.parse(readFileSync(new URL('../../../Resource/hardware/clearance-bench.json', import.meta.url),'utf8'));
const setup = { ...bench.initial, front360: true, caseIndex: 1 };
const run = (changes = {}, psu = 0) => benchmark(bench, { ...setup, ...changes }, psu);

test('grade boundaries, overflow and totals use rounded axes', () => {
    assert.deepEqual(rating(99.94), { score: 99.9, grade: 'A' });
    assert.deepEqual(rating(99.96), { score: 100, grade: 'S' });
    assert.deepEqual(rating(-5), { score: 0, grade: 'E' });
    assert.equal(rating(125).score, 125);
    assert.deepEqual(reportScores([130,70,70,70,70,70], Array(6).fill(1/6)).total, { score: 80, grade: 'B' });
    assert.equal(reportScores([70.04,70.14,0,0,0,0], [0.6,0.4,0,0,0,0]).total.score, 70);
});

test('steady-state temperature uses positive airflow and throttle starts above threshold', () => {
    const exact = thermalState(25,150,0.5,1,100);
    assert.equal(exact.temperature, 100);
    assert.equal(exact.factor, 1);
    const hot = thermalState(25,150,0.5,1,90);
    assert.equal(hot.duty, 1);
    assert.ok(hot.factor < 1);
    assert.throws(() => thermalState(25,150,0.5,0,90));
});

test('unbalanced airflow loses to balanced flow; stopped case fans produce finite heat and audible throttle', () => {
    const balanced = run().report;
    const inlet = run({airflowIndex:1}).report;
    const stopped = run({airflowIndex:2}).report;
    assert.ok(balanced.gpu.temperature < inlet.gpu.temperature && inlet.gpu.temperature < stopped.gpu.temperature);
    assert.equal(stopped.gpu.fanPercent, 100);
    assert.ok(stopped.cpu.throttled && stopped.gpu.throttled);
    assert.ok(stopped.noise > balanced.noise);
    assert.ok(stopped.axes[0].score < balanced.axes[0].score && stopped.axes[3].score < balanced.axes[3].score);
    assert.ok(stopped.total.score < run({gpuIndex:1}).report.total.score);
});

test('load must finish to issue grades; a low PSU reserve reduces stability', () => {
    assert.throws(() => run({caseIndex:0}));
    assert.equal(run({},2).report, null);
    assert.equal(run({},1).status,'DEGRADED');
    assert.ok(run({},1).report.axes[5].score < run().report.axes[5].score);
    assert.deepEqual(run(),run());
});

test('workload weighting and memory channels change performance, ROI uses actual reference bill', () => {
    const highRefresh = run().report;
    const single = run({slots:[0,1]}).report;
    const fourK = run({workloadIndex:1}).report;
    assert.ok(highRefresh.axes[0].score > single.axes[0].score);
    assert.ok(fourK.axes[0].score > highRefresh.axes[0].score);
    assert.equal(highRefresh.price, 1299+369+899+569+499+3799+529+259*2);
    assert.equal(highRefresh.price, single.price);
    const sample = structuredClone(bench);
    sample.processor.performance = 38;
    const mismatch = benchmark(sample,setup,0).report;
    assert.ok(mismatch.axes[0].score < highRefresh.axes[0].score * .6);
    assert.ok(mismatch.axes[1].score < highRefresh.axes[1].score * .6);
    sample.board.price = 0;
    assert.throws(() => benchmark(sample,setup,0));
});
