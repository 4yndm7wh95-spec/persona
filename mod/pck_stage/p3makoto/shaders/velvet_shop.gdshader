shader_type canvas_item;

uniform float hover_glow : hint_range(0.0, 1.0) = 0.0;
uniform float scene_time = 0.0;

void fragment() {
	vec4 base = texture(TEXTURE, UV);
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

