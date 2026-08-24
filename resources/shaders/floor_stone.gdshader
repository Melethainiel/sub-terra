shader_type spatial;
render_mode cull_back;

// The flagged path laid through the temple's galleries, to go with rock.gdshader:
// same three flat bands of light, same trick of sampling world position so one
// material tiles across a whole floor without UV work. Slabs are cut by a cellular
// field, and their joints can be let glow — that's how a Lava tile shows the heat
// coming up between the stones without needing a second material.

uniform vec3 stone_dark : source_color = vec3(0.055, 0.052, 0.058);
uniform vec3 stone_light : source_color = vec3(0.20, 0.19, 0.20);
uniform float slab_scale = 1.5;
uniform float slab_variety : hint_range(0.0, 1.0) = 0.55;
uniform float joint_width : hint_range(0.0, 0.3) = 0.055;
uniform vec3 joint_color : source_color = vec3(0.015, 0.013, 0.016);

uniform vec3 seam_color : source_color = vec3(1.0, 0.35, 0.08);
uniform float seam_strength : hint_range(0.0, 4.0) = 0.0;

uniform float grain_scale = 7.0;
uniform float grain_strength : hint_range(0.0, 1.0) = 0.25;

uniform float band_shadow : hint_range(0.0, 1.0) = 0.2;
uniform float band_mid : hint_range(0.0, 1.0) = 0.5;
uniform float band_lit : hint_range(0.0, 2.0) = 1.1;

varying vec3 world_pos;

vec2 hash2(vec2 p) {
	vec3 q = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
	q += dot(q, q.yzx + 33.33);
	return fract((q.xx + q.yz) * q.zy);
}

float hash1(vec2 p) {
	return fract(sin(dot(p, vec2(41.7, 289.1))) * 43758.5453);
}

// Nearest slab centre and how close this point is to the line between the two
// nearest ones — the joint. Returns (cell id x, cell id y, edge distance).
vec3 slab(vec2 p) {
	vec2 cell = floor(p);
	vec2 local = fract(p);

	vec2 best_cell = cell;
	vec2 best_offset = vec2(0.0);
	float first = 8.0;
	float second = 8.0;

	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 neighbour = vec2(float(x), float(y));
			// Jittered, but not to the corners: slabs stay roughly slab-shaped.
			vec2 point = neighbour + 0.25 + 0.5 * hash2(cell + neighbour);
			float distance = length(point - local);

			if (distance < first) {
				second = first;
				first = distance;
				best_cell = cell + neighbour;
				best_offset = point - local;
			} else if (distance < second) {
				second = distance;
			}
		}
	}

	return vec3(best_cell, second - first);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 cut = slab(world_pos.xz * slab_scale);
	float joint = 1.0 - smoothstep(0.0, joint_width, cut.z);

	// Each slab a slightly different stone, then a grain over the whole floor so a
	// single slab isn't one flat colour.
	float tone = mix(0.35, 1.0, hash1(cut.xy)) * slab_variety + (1.0 - slab_variety) * 0.6;
	float grain = fract(sin(dot(floor(world_pos.xz * grain_scale), vec2(12.99, 78.23))) * 43758.5453);
	tone = clamp(tone + (grain - 0.5) * grain_strength, 0.0, 1.0);

	ALBEDO = mix(mix(stone_dark, stone_light, tone), joint_color, joint);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	EMISSION = seam_color * joint * seam_strength;
}

void light() {
	float ndotl = dot(NORMAL, LIGHT);
	float lit = ndotl > 0.55 ? band_lit : ndotl > 0.08 ? band_mid : band_shadow;

	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * lit;
}
