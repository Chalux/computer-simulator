import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { loadBasemodCatalog } from './fixtures/basemod.mjs';

// 迁移护栏：分文件内容必须能还原出与旧内嵌目录完全一致的规则数据。
// 任一硬件数值、顺序或初始配置被改动，都会在这里失败。
test('basemod content composes into the legacy rule catalog', () => {
  const legacy = JSON.parse(
    readFileSync(new URL('./fixtures/legacy-clearance-bench.json', import.meta.url), 'utf8')
      .replace(/^\uFEFF/, ''),
  );

  assert.deepEqual(loadBasemodCatalog(), legacy);
});
