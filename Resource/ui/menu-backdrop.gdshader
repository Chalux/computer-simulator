shader_type canvas_item;

// 主菜单背景（极简版）：近黑渐变 + 单侧冷光 + 暗角 + 极轻颗粒。
// 不再包含网格、扫描线、数据闪线等装饰层，避免与排版争夺注意力。

uniform vec4 ink : source_color = vec4(0.0196, 0.0314, 0.0392, 1.0);
uniform vec4 graphite : source_color = vec4(0.0392, 0.0627, 0.0784, 1.0);
uniform vec4 signal : source_color = vec4(0.3569, 0.8902, 0.8157, 1.0);
uniform float glow : hint_range(0.0, 0.3) = 0.085;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void fragment() {
	vec2 uv = UV;
	vec3 col = mix(ink.rgb, graphite.rgb, smoothstep(0.0, 1.05, uv.y));

	// 单一光源，落在右侧线框背后，让画面有“被照亮”的层次而不是平涂。
	vec2 p = vec2(uv.x * 1.7778, uv.y);
	float d = distance(p, vec2(1.22, 0.44));
	col += signal.rgb * exp(-d * 2.9) * glow;
	col += signal.rgb * exp(-d * 7.0) * (0.008 + 0.004 * sin(TIME * 0.25));

	float vig = smoothstep(0.34, 1.05, length((uv - 0.5) * vec2(1.12, 1.0)) * 1.12);
	col *= 1.0 - vig * 0.58;

	col += (hash21(FRAGCOORD.xy + fract(TIME) * 41.0) - 0.5) * 0.007;

	COLOR = vec4(col, 1.0);
}
