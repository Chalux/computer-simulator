shader_type spatial;
render_mode diffuse_burley, specular_disabled;
varying vec2 plane_mm;

void vertex() {
    plane_mm = VERTEX.xz;
}

void fragment() {
    vec2 cell = plane_mm / 25.0;
    vec2 distance_to_line = abs(fract(cell + 0.5) - 0.5) / max(fwidth(cell), vec2(0.0001));
    float grid = 1.0 - smoothstep(0.0, 0.85, min(distance_to_line.x, distance_to_line.y));
    float fade = 1.0 - smoothstep(160.0, 510.0, length(plane_mm));
    ALBEDO = mix(vec3(0.012,0.020,0.028),vec3(0.041,0.059,0.070),fade) + grid*fade*0.011;
    ROUGHNESS = 1.0;
}
