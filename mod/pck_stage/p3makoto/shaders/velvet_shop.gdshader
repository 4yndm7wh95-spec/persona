shader_type canvas_item;

uniform float hover_glow : hint_range(0.0, 1.0) = 0.0;
uniform float scene_time = 0.0;

uniform sampler2D blink_half : filter_linear;
uniform sampler2D blink_closed : filter_linear;
uniform float blink_amount = 0.0;
uniform float left_sway = 0.0;
uniform float right_sway = 0.0;

// 0.15.26 lattice room: P3R-style cage walls painted without lanterns. The elevator
// shows only through the lattice gaps (mask: white = solid iron, black = gap);
// both lanterns are separate layers drawn on top with their sway.
uniform bool layered = false;
uniform sampler2D room_tex : filter_linear;
uniform sampler2D lattice_mask_tex : filter_linear;
uniform sampler2D lantern_left_tex : filter_linear;
uniform sampler2D lantern_right_tex : filter_linear;
uniform vec2 left_pivot = vec2(199.0, 75.0);   // measured chain hook of lantern_left.png
uniform vec2 right_pivot = vec2(1474.0, 75.0);
// One wide car behind the whole room, seen through both lattice walls.
uniform float car_x0 = 110.0;
uniform float car_x1 = 1562.0;
uniform float car_h = 540.0;
uniform float car_y = 2000.0;                 // top edge, room pixels
uniform float car_on = 0.0;
uniform float car_blur = 0.0;                 // trail length below the car, room pixels

vec4 tex_bounded(sampler2D tex, vec2 uv) {
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return vec4(0.0);
    return texture(tex, uv);
}

vec4 lantern_layer(sampler2D tex, vec2 pixel, vec2 pivot, float angle) {
    vec2 d = pixel - pivot;
    float a = -angle;
    vec2 src = pivot + vec2(cos(a)*d.x - sin(a)*d.y, sin(a)*d.x + cos(a)*d.y);
    return tex_bounded(tex, src / vec2(1672.0, 941.0));
}

// Procedural cage car: dark roof with a lamp strip, lit interior behind a folding
// gate (vertical bars + accordion diagonals), dark floor slab. Crisp at any size.
vec3 car_color(vec2 p, out float inside) {
    float ly = p.y - car_y;
    inside = step(car_x0, p.x) * step(p.x, car_x1) * step(0.0, ly) * step(ly, car_h);
    if (inside < 0.5) return vec3(0.0);
    float edge = min(p.x - car_x0, car_x1 - p.x);
    // Thick dark roof with a bright lamp strip under it, and a dark floor slab:
    // the two strong horizontal edges are what reads through the lattice.
    if (ly < 72.0) return ly > 62.0 ? vec3(0.95, 0.97, 1.0) : vec3(0.03, 0.035, 0.08);
    if (ly > car_h - 56.0) return ly < car_h - 50.0 ? vec3(0.55, 0.65, 0.90) : vec3(0.03, 0.035, 0.08);
    if (edge < 22.0) return vec3(0.05, 0.06, 0.12);
    float k = (ly - 72.0) / (car_h - 128.0);
    vec3 c = mix(vec3(0.82, 0.90, 1.0), vec3(0.34, 0.48, 0.88), k);
    // Handrail across the car at waist height.
    float rail = 1.0 - smoothstep(4.0, 7.0, abs(ly - car_h * 0.62));
    c = mix(c, vec3(0.10, 0.12, 0.22), rail);
    float gx = mod(p.x - car_x0, 30.0);
    c *= 1.0 - 0.55 * (1.0 - smoothstep(3.0, 5.0, gx));
    return c;
}

vec4 car_sample(vec2 pixel) {
    vec4 acc = vec4(0.0);
    for (int i = 0; i < 12; i++) {
        // Moving up: earlier positions were lower, so the trail lies below the car.
        float s = car_blur * float(i) / 11.0;
        float inside;
        vec3 c = car_color(pixel - vec2(0.0, s), inside);
        acc += vec4(c * inside, inside);
    }
    return acc / 12.0;
}

float cable_mask(vec2 pixel) {
    float w = car_x1 - car_x0, c = 0.0;
    for (int i = 0; i < 2; i++) {
        float cx = car_x0 + w * (i == 0 ? 0.2 : 0.8);
        float d = abs(pixel.x - cx);
        c += (1.0 - smoothstep(1.0, 2.6, abs(d - 9.0)));
    }
    c *= 1.0 - smoothstep(car_y - 4.0, car_y + 2.0, pixel.y);
    return clamp(c, 0.0, 1.0) * (1.0 - smoothstep(330.0, 620.0, car_y));
}

vec3 passing_car(vec3 painted, vec2 pixel) {
    if (car_on <= 0.001) return painted;
    float rail = texture(lattice_mask_tex, pixel / vec2(1672.0, 941.0)).r;
    float k = (1.0 - rail) * car_on;
    vec4 car = car_sample(pixel);
    vec3 color = painted * (1.0 - car.a * k) + car.rgb * k;
    color = mix(color, vec3(0.20, 0.23, 0.33), cable_mask(pixel) * 0.85 * k);
    // Cool light washing over the iron in front of the car.
    float top = car_y - 90.0, bottom = car_y + car_h + car_blur + 90.0;
    float band = smoothstep(top - 80.0, top + 80.0, pixel.y) * (1.0 - smoothstep(bottom - 80.0, bottom + 80.0, pixel.y));
    float sx = smoothstep(car_x0 - 120.0, car_x0 + 40.0, pixel.x) * (1.0 - smoothstep(car_x1 - 40.0, car_x1 + 120.0, pixel.x));
    sx *= 1.0 - smoothstep(560.0, 640.0, pixel.x) * (1.0 - smoothstep(1032.0, 1112.0, pixel.x)); // walls only, not Igor
    float metal = dot(painted, vec3(0.299, 0.587, 0.114));
    color += vec3(0.20, 0.34, 0.66) * band * sx * car_on * rail * (0.20 + metal * 1.1)
        * (1.0 - smoothstep(560.0, 690.0, pixel.y));
    return color;
}

vec3 draw_lanterns(vec3 color, vec2 pixel) {
    vec4 l = lantern_layer(lantern_left_tex, pixel, left_pivot, left_sway);
    color = mix(color, l.rgb, l.a);
    vec4 r = lantern_layer(lantern_right_tex, pixel, right_pivot, right_sway);
    return mix(color, r.rgb, r.a);
}

vec2 sway_lantern(vec2 pixel, vec2 pivot, float angle) {
    float mask = smoothstep(-65.0, -52.0, pixel.x - pivot.x)
        * (1.0 - smoothstep(52.0, 65.0, pixel.x - pivot.x))
        * smoothstep(-20.0, 0.0, pixel.y - pivot.y)
        * (1.0 - smoothstep(265.0, 285.0, pixel.y - pivot.y));
    float a = -angle * mask;
    vec2 d = pixel - pivot;
    return pivot + vec2(cos(a)*d.x - sin(a)*d.y, sin(a)*d.x + cos(a)*d.y);
}

void fragment() {
	vec2 pixel = UV * vec2(1672.0, 941.0);
    vec2 source = sway_lantern(pixel, vec2(192.0,75.0), left_sway);
    source = sway_lantern(source, vec2(1474.0,75.0), right_sway);
    vec4 base = layered ? texture(room_tex, pixel / vec2(1672.0,941.0)) : texture(TEXTURE, source / vec2(1672.0,941.0));
    // Small eye-only patches: never replace the whole head or background.
    vec2 eye_uv = (pixel - vec2(788.0,365.0)) / vec2(97.0,50.0);
    float eye_mask = max(1.0-smoothstep(0.82,1.0,length((pixel-vec2(813.0,386.0))/vec2(20.0,15.0))),
                        1.0-smoothstep(0.82,1.0,length((pixel-vec2(860.0,386.0))/vec2(20.0,15.0))));
    vec3 half_eye = texture(blink_half, clamp(eye_uv,vec2(0.0),vec2(1.0))).rgb;
    vec3 shut_eye = texture(blink_closed, clamp(eye_uv,vec2(0.0),vec2(1.0))).rgb;
    vec3 blink_color = blink_amount < 0.5 ? mix(base.rgb,half_eye,blink_amount*2.0)
        : mix(half_eye,shut_eye,(blink_amount-0.5)*2.0);
    base.rgb = mix(base.rgb,blink_color,eye_mask);
    if (layered) base.rgb = draw_lanterns(passing_car(base.rgb, pixel), pixel);
	float t = scene_time > 0.0 ? scene_time : TIME;
	vec3 color = base.rgb;

	// Soft, slowly drifting blue shafts. Keep the added light well below the
	// source image luminance so the room's painted details remain readable.
	float beam_x = 0.25 + UV.y * 0.34 + sin(t * 0.32) * 0.07;
	float beam = exp(-pow((UV.x - beam_x) / 0.13, 2.0));
	beam *= smoothstep(0.04, 0.24, UV.y) * (1.0 - smoothstep(0.72, 0.96, UV.y));
	float beam_two_x = 0.78 - UV.y * 0.22 + sin(t * 0.25 + 1.7) * 0.055;
	float beam_two = exp(-pow((UV.x - beam_two_x) / 0.17, 2.0));
	beam_two *= smoothstep(0.08, 0.32, UV.y) * (1.0 - smoothstep(0.78, 0.98, UV.y));
	color += vec3(0.032, 0.09, 0.18) * beam;
	color += vec3(0.022, 0.06, 0.12) * beam_two;

	// Low, slow haze along the floor.
	float floor_band = smoothstep(0.66, 0.84, UV.y);
	float haze_wave = 0.5 + 0.5 * sin(UV.x * 10.0 + t * 0.34 + sin(UV.x * 4.0 - t * 0.12));
	float haze = floor_band * (0.22 + 0.78 * haze_wave);
	color += vec3(0.03, 0.072, 0.125) * haze * 0.52;

	// Two sparse, independently drifting layers of soft firefly-like motes.
	// Translating the grids makes the points float up and sway without a loop.
	vec2 near_grid = UV * vec2(9.0, 6.0) + vec2(sin(t * 0.17) * 0.16, t * 0.13);
	vec2 near_cell = floor(near_grid);
	vec2 near_hash2 = fract(sin(vec2(dot(near_cell, vec2(127.1, 311.7)),
		dot(near_cell, vec2(269.5, 183.3)))) * 43758.5453);
	vec2 near_center = near_cell + mix(vec2(0.22), vec2(0.78), near_hash2);
	float near_hash = fract(sin(dot(near_cell, vec2(41.7, 289.3))) * 24634.6345);
	float near_dist = length((near_grid - near_center) * vec2(0.9, 1.0));
	float near_halo = exp(-near_dist * near_dist * 38.0);
	float near_core = 1.0 - smoothstep(0.025, 0.065, near_dist);
	float near_glint = (0.45 + 0.55 * sin(t * 1.1 + near_hash * 6.283));
	float near_motes = step(0.68, near_hash) * (near_halo * 0.38 + near_core * 0.62) * max(near_glint, 0.0);

	vec2 far_grid = UV * vec2(12.0, 8.0) + vec2(-sin(t * 0.12 + 1.0) * 0.12, t * 0.075);
	vec2 far_cell = floor(far_grid);
	vec2 far_hash2 = fract(sin(vec2(dot(far_cell, vec2(93.7, 251.9)),
		dot(far_cell, vec2(157.1, 67.3)))) * 37581.271);
	vec2 far_center = far_cell + mix(vec2(0.18), vec2(0.82), far_hash2);
	float far_hash = fract(sin(dot(far_cell, vec2(19.1, 73.7))) * 16453.543);
	float far_dist = length((far_grid - far_center) * vec2(0.85, 1.0));
	float far_halo = exp(-far_dist * far_dist * 52.0);
	float far_glint = (0.5 + 0.5 * sin(t * 0.72 + far_hash * 6.283));
	float far_motes = step(0.84, far_hash) * far_halo * max(far_glint, 0.0);
	float motes = (near_motes * 0.72 + far_motes * 0.46) * (1.0 - floor_band * 0.35);
	color += vec3(0.045, 0.19, 0.28) * motes;

	// Gentle cool focus around the merchant's upper-body position.
	float merchant_dist = length((UV - vec2(0.5, 0.43)) * vec2(1.0, 1.15));
	float merchant_glow = (1.0 - smoothstep(0.035, 0.24, merchant_dist)) * hover_glow;
	color += vec3(0.035, 0.075, 0.13) * merchant_glow;

	// Soft cool vignette, preserving full opacity and all source texture detail.
	vec2 vignette_uv = (UV - vec2(0.5)) * vec2(0.92, 0.86);
	float vignette = smoothstep(0.42, 0.9, length(vignette_uv));
	color *= 1.0 - vignette * 0.16;
	color *= vec3(0.97, 0.99, 1.025);

	COLOR = vec4(color, base.a);
}

