var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/builtins/assembly-fit.mts
function intersection(a, b) {
  const origin = [0, 0, 0];
  const size = [0, 0, 0];
  for (let axis = 0; axis < 3; axis++) {
    origin[axis] = Math.max(a.origin[axis], b.origin[axis]);
    size[axis] = Math.min(a.origin[axis] + a.size[axis], b.origin[axis] + b.size[axis]) - origin[axis];
    if (size[axis] <= 0) return void 0;
  }
  return { origin, size };
}
__name(intersection, "intersection");
function inspect(catalog, setup, psuIndex = setup.psuIndex ?? 0) {
  const { board, cooler } = catalog;
  const ram = catalog.ram[setup.ramIndex];
  const chassis = catalog.cases[setup.caseIndex];
  const occupied = new Set(setup.slots);
  if (!ram || !chassis || !Number.isInteger(setup.offset) || setup.offset < 0 || setup.offset > cooler.adjustable_y_max || occupied.size !== setup.slots.length || setup.slots.some((i) => !Number.isInteger(i) || !board.slots[i])) {
    throw new RangeError("\u65E0\u6548\u7684\u5DE5\u4F5C\u53F0\u914D\u7F6E");
  }
  let psuLimit;
  let psuMargin;
  const parts = [];
  const issues = [];
  const [length, width, thickness] = board.dimensions;
  parts.push({ id: "board", role: "board", origin: [0, -thickness, 0], size: [width, thickness, length] });
  const towers = cooler.body_aabb.map((bounds, i) => ({ ...bounds, id: "tower" + i, role: "tower" }));
  const { origin: [x, y, z], size } = cooler.fan_aabb;
  const fan = { id: "fan", role: "fan", origin: [x, y + setup.offset, z], size };
  const obstacles = setup.front360 ? [] : [...towers, fan];
  parts.push(...obstacles);
  let gpuLimit;
  let gpuMargin;
  let caseDepth = length;
  let caseLength = width;
  if (setup.front360) {
    const card = catalog.graphics[setup.gpuIndex ?? 0];
    if (!card) throw new RangeError("\u65E0\u6548\u7684\u663E\u5361");
    const [cardLength, cardThickness, cardHeight] = card.dimensions;
    const liquid = catalog.liquid;
    const depth = liquid.coreThickness + liquid.fanThickness;
    caseLength = chassis.gpuLimit;
    caseDepth = liquid.fanSize * liquid.fanCount;
    gpuLimit = caseLength - depth;
    gpuMargin = gpuLimit - cardLength;
    const gpu = {
      id: "gpu",
      role: "gpu",
      fans: card.fanCount,
      origin: [0, 6, 202],
      size: [cardLength, cardHeight, cardThickness]
    };
    const radiator = {
      id: "radiator",
      role: "radiator",
      fans: liquid.fanCount,
      fanThickness: liquid.fanThickness,
      origin: [gpuLimit, 5, 0],
      size: [depth, liquid.fanSize, caseDepth]
    };
    parts.push(gpu, radiator, { id: "pump", role: "pump", origin: [68, 0, 91], size: [60, 43, 60] });
    if (gpuMargin < 0) issues.push({
      status: "BLOCKED",
      parts: [gpu.id, radiator.id],
      excess: -gpuMargin,
      overlap: intersection(gpu, radiator),
      message: `\u663E\u5361\u957F ${cardLength} mm\uFF0C\u524D\u7F6E\u51B7\u6392\u542B\u98CE\u6247\u5360 ${depth} mm\uFF0C\u6709\u6548\u9650\u957F ${gpuLimit} mm\uFF0C\u5E72\u6D89 +${-gpuMargin} mm\u3002\u8BF7\u6362\u77ED\u5361\u6216\u5927\u673A\u7BB1\u3002`
    });
    const psu = catalog.powerSupplies[psuIndex];
    if (!psu) throw new RangeError("\u65E0\u6548\u7684\u7535\u6E90");
    const reserve = psu.cable === "\u5168\u6A21\u7EC4" ? 0 : 20;
    psuLimit = chassis.psuLimit;
    psuMargin = psuLimit - psu.length - reserve;
    if (psuMargin < 0) {
      const reserveText = reserve > 0 ? `\uFF0C${psu.cable}\u9700\u9884\u7559 ${reserve} mm \u85CF\u7EBF` : "";
      issues.push({
        status: "BLOCKED",
        parts: [],
        excess: -psuMargin,
        message: `\u7535\u6E90\u957F ${psu.length} mm${reserveText}\uFF08\u6709\u6548\u5360\u7528 ${psu.length + reserve} mm\uFF09\uFF0C\u673A\u7BB1\u7535\u6E90\u4ED3\u9650\u957F ${psuLimit} mm\uFF0C\u5E72\u6D89 +${-psuMargin} mm\u3002\u8BF7\u6362\u77ED\u7535\u6E90\u6216\u6362\u5927\u673A\u7BB1\u3002`
      });
    }
  }
  const channels = /* @__PURE__ */ new Set();
  const [ramLength, ramWidth, ramHeight] = ram.dimensions;
  for (const [i, slot] of board.slots.entries()) {
    const [sx, sy, sz] = slot.anchor;
    parts.push({ id: "slot" + i, role: "slot", origin: [sx - 1, sy, sz - 2], size: [ramWidth + 2, 4, ramLength + 4] });
    if (!occupied.has(i)) continue;
    channels.add(slot.channel);
    const stick = { id: slot.name, role: "ram", rgb: ram.rgb, origin: slot.anchor, size: [ramWidth, ramHeight, ramLength] };
    parts.push(stick);
    for (const obstacle of obstacles) {
      const overlap = intersection(stick, obstacle);
      if (!overlap) continue;
      const clearance = obstacle.origin[1] - sy;
      const label = obstacle === fan ? "\u524D\u6247" : "\u5854\u4F53";
      const remedy = obstacle === fan ? "\u53EF\u4E0A\u79FB\u524D\u6247\u3001\u6362\u69FD\u6216\u6362\u77EE\u9A6C\u7532\u3002" : "\u53EF\u6362\u69FD\u6216\u6362\u77EE\u9A6C\u7532\u3002";
      issues.push({
        status: "BLOCKED",
        parts: [stick.id, obstacle.id],
        overlap,
        message: slot.name + " \u5185\u5B58\u9AD8 " + ramHeight + " mm\uFF0C" + label + "\u51C0\u7A7A " + clearance + " mm\uFF0C\u5E72\u6D89 +" + (ramHeight - clearance) + " mm\u3002" + remedy
      });
    }
  }
  const heightLimit = chassis.heightLimit;
  const panel = { id: "panel", role: "panel", origin: [0, heightLimit, 0], size: [caseLength, 2, caseDepth] };
  const tallest = Math.max(...parts.map((p) => p.origin[1] + p.size[1]));
  for (const part of parts) {
    const excess = part.origin[1] + part.size[1] - heightLimit;
    if (excess > 0) issues.push({
      status: "BLOCKED",
      parts: [part.id, panel.id],
      excess,
      overlap: intersection(panel, part),
      message: "\u4FA7\u677F\u9650\u9AD8 " + heightLimit + " mm\uFF0C\u8D85\u51FA " + excess + " mm\u3002\u8BF7\u964D\u4F4E\u524D\u6247\u6216\u6362\u5BBD\u673A\u7BB1\u3002"
    });
  }
  parts.push(panel);
  if (!channels.size) issues.push({ status: "BLOCKED", parts: [], message: "\u672A\u5B89\u88C5\u5185\u5B58\uFF0C\u8BF7\u9009\u62E9\u81F3\u5C11\u4E00\u4E2A\u63D2\u69FD\u3002" });
  if (channels.size === 1) issues.push({ status: "DEGRADED", parts: [], message: "\u4EC5\u542F\u7528\u4E00\u4E2A\u5185\u5B58\u901A\u9053\u3002\u5206\u522B\u4F7F\u7528 A\u3001B \u901A\u9053\u53EF\u6062\u590D\u53CC\u901A\u9053\u5E26\u5BBD\u3002" });
  const status = issues.some((i) => i.status === "BLOCKED") ? "BLOCKED" : issues.length ? "DEGRADED" : "OPTIMAL";
  const rate = Math.min(board.maxRate, catalog.cpuMaxRate, ram.rate);
  return {
    parts,
    issues,
    status,
    heightLimit,
    gpuLimit,
    gpuMargin,
    psuLimit,
    psuMargin,
    headroom: heightLimit - tallest,
    channels: channels.size,
    capacity: occupied.size * ram.gb,
    bandwidth: channels.size * rate * 8 / 1e3
  };
}
__name(inspect, "inspect");
function testLoad(catalog, setup, psuIndex = setup.psuIndex ?? 0) {
  const fit = inspect(catalog, setup, psuIndex);
  if (!setup.front360 || fit.status === "BLOCKED")
    throw new Error("\u5B8C\u6574\u6C34\u51B7\u6837\u673A\u987B\u5148\u901A\u8FC7\u88C5\u914D\u68C0\u67E5");
  const psu = catalog.powerSupplies[psuIndex];
  if (!psu) throw new RangeError("\u65E0\u6548\u7684\u7535\u6E90");
  const card = catalog.graphics[setup.gpuIndex ?? 0];
  const curve = catalog.loadCurve.map((point, index) => ({
    name: point.name,
    pulse: point.pulse,
    watts: point.cpu + card.testWatts[index] + point.other
  }));
  const trip = curve.findIndex((s) => s.watts > (s.pulse ? psu.pulse : psu.continuous));
  const samples = trip < 0 ? curve : curve.slice(0, trip + 1);
  const sustained = Math.max(...samples.filter((s) => !s.pulse).map((s) => s.watts));
  const peak = Math.max(...samples.map((s) => s.watts));
  const failed = curve[trip];
  const status = failed ? "BLOCKED" : peak > psu.recommended ? "DEGRADED" : "OPTIMAL";
  const message = failed ? `${failed.name}\u8FBE\u5230 ${failed.watts} W\uFF0C\u8D85\u51FA${failed.pulse ? "\u77ED\u65F6" : "\u6301\u7EED"}\u627F\u8F7D ${failed.pulse ? psu.pulse : psu.continuous} W\uFF0C\u7535\u6E90\u4FDD\u62A4\u65AD\u7535\u3002` : status === "DEGRADED" ? `\u6D4B\u8BD5\u901A\u8FC7\u3002\u5CF0\u503C\u8D85\u8FC7\u5EFA\u8BAE\u4E0A\u754C ${psu.recommended} W\uFF0C\u77AC\u65F6\u4F59\u91CF\u504F\u4F4E\u3002` : "\u6D4B\u8BD5\u901A\u8FC7\u3002\u6301\u7EED\u8D1F\u8F7D\u4E0E\u77AC\u65F6\u5CF0\u503C\u5747\u6709\u8DB3\u591F\u4F59\u91CF\u3002";
  return { status, sustained, peak, samples, message };
}
__name(testLoad, "testLoad");
function testLoadJson(catalog, setup, psuIndex) {
  return JSON.stringify(testLoad(JSON.parse(catalog), JSON.parse(setup), psuIndex));
}
__name(testLoadJson, "testLoadJson");
function inspectJson(catalog, setup) {
  return JSON.stringify(inspect(JSON.parse(catalog), JSON.parse(setup)));
}
__name(inspectJson, "inspectJson");
export {
  inspect,
  inspectJson,
  testLoad,
  testLoadJson
};
