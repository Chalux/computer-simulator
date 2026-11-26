shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform bool powered = true;
uniform float speed : hint_range(0.0, 1.0) = 0.095;
uniform float brightness : hint_range(0.0, 4.0) = 1.25;
varying vec3 bench_position;

void vertex() {
    bench_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    float phase = dot(bench_position, vec3(0.0024, 0.0015, 0.0035)) - TIME * speed;
    vec3 wave = 0.50 + 0.50 * cos(6.28318 * (phase + vec3(0.02, 0.68, 0.36)));
    vec3 led = mix(vec3(0.12), wave, 0.85);
    ALBEDO = powered ? mix(led, vec3(0.90), 0.18) : vec3(0.24, 0.27, 0.29);
    EMISSION = powered ? led * brightness : vec3(0.0);
    ROUGHNESS = 0.38;
    METALLIC = 0.04;
}
