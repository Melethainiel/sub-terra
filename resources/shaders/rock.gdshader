shader_type spatial;
render_mode cull_back;

// A dark, cel-shaded volcanic rock, no texture and no UVs: everything is sampled
// from world position, so one shared material reads right on any tile, however it
// was turned, and two tiles side by side are never the same rock twice.
//
// The rock is broken into plates by a cellular field: each cell gets its own
// heading, so a wall catches the light plate by plate instead of as one smooth
// surface, and the joints between them are darkened and let the mountain's heat
// through. That is the part the mesh used to try to carry in geometry — 25 000
// triangles a tile, and it read as debris. It belongs here.

uniform vec3 albedo_dark : source_color = vec3(0.05, 0.05, 0.062);
uniform vec3 albedo_light : source_color = vec3(0.22, 0.225, 0.25);

// The plates. Scale is cells per metre: 3.0 gives blocks about a third of a metre
// across, which is what the reference art shows against a corridor this wide.
uniform float plate_scale = 3.0;
uniform float plate_variety : hint_range(0.0, 1.0) = 0.45;

// A cellular field draws its joints as straight segments — the borders between
// Voronoi cells are line segments, and rock has no straight edges. The lookup
// position is therefore dragged about by noise first, at two scales: the coarse
// one bends a joint along its length, the fine one frays it.
uniform float plate_warp : hint_range(0.0, 1.0) = 0.35;
uniform float plate_warp_scale = 1.6;
uniform float plate_fray : hint_range(0.0, 0.5) = 0.13;
uniform float plate_fray_scale = 7.0;
uniform float plate_relief : hint_range(0.0, 1.0) = 0.55;
uniform float plate_shade : hint_range(0.0, 1.0) = 0.35;

// The joints between plates: how wide they read, how dark they cut, and how much
// of the heat below shows through them.
uniform float joint_width : hint_range(0.0, 0.5) = 0.11;
uniform float joint_dark : hint_range(0.0, 1.0) = 0.75;
uniform float joint_glow : hint_range(0.0, 4.0) = 1.4;
uniform vec3 vein_color : source_color = vec3(1.0, 0.35, 0.08);

// Where that heat reaches. It pools at the foot of the walls and only breaks out
// higher up in patches — a joint that glows everywhere turns the gallery into a
// net of lava, which is not what a cave looks like.
uniform float heat_height : hint_range(0.0, 2.0) = 0.55;
uniform float heat_patch_scale = 1.1;
uniform float heat_patch : hint_range(0.0, 1.0) = 0.12;

// Grain over the plates, so a single plate isn't one flat facet.
uniform float grain_scale = 9.0;
uniform float grain_strength : hint_range(0.0, 1.0) = 0.25;

uniform vec3 rim_color : source_color = vec3(1.0, 0.42, 0.12);
uniform float rim_strength : hint_range(0.0, 3.0) = 0.15;
uniform float rim_power : hint_range(1.0, 8.0) = 5.0;

// Cel bands: how a surface reads once a light catches it, in three flat steps
// instead of a smooth gradient.
uniform float band_shadow : hint_range(0.0, 1.0) = 0.2;
uniform float band_mid : hint_range(0.0, 1.0) = 0.5;
uniform float band_lit : hint_range(0.0, 2.0) = 1.1;

varying vec3 world_pos;
varying vec3 world_normal;

vec3 hash3(vec3 p) {
	p = vec3(dot(p, vec3(127.1, 311.7, 74.7)),
			 dot(p, vec3(269.5, 183.3, 246.1)),
			 dot(p, vec3(113.5, 271.9, 124.6)));
	return fract(sin(p) * 43758.5453);
}

float hash1(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float value_noise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);

	return mix(
		mix(mix(hash1(i + vec3(0.0, 0.0, 0.0)), hash1(i + vec3(1.0, 0.0, 0.0)), f.x),
			mix(hash1(i + vec3(0.0, 1.0, 0.0)), hash1(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
		mix(mix(hash1(i + vec3(0.0, 0.0, 1.0)), hash1(i + vec3(1.0, 0.0, 1.0)), f.x),
			mix(hash1(i + vec3(0.0, 1.0, 1.0)), hash1(i + vec3(1.0, 1.0, 1.0)), f.x), f.y),
		f.z);
}

vec3 warp(vec3 p, float scale, float strength) {
	return p + (vec3(
		value_noise(p * scale + vec3(11.3, 0.0, 0.0)),
		value_noise(p * scale + vec3(0.0, 27.1, 0.0)),
		value_noise(p * scale + vec3(0.0, 0.0, 43.7))) * 2.0 - 1.0) * strength;
}

float fbm(vec3 p) {
	float total = 0.0;
	float amplitude = 0.55;

	for (int i = 0; i < 3; i++) {
		total += amplitude * value_noise(p);
		p *= 2.03;
		amplitude *= 0.5;
	}

	return total;
}

// Nearest plate centre and the joint. Returns the cell's own seed in xyz, the gap
// between the two nearest centres in w — small where a joint runs.
vec4 plates(vec3 p) {
	vec3 cell = floor(p);
	vec3 local = fract(p);

	vec3 nearest = vec3(0.0);
	float first = 9.0;
	float second = 9.0;

	for (int z = -1; z <= 1; z++) {
		for (int y = -1; y <= 1; y++) {
			for (int x = -1; x <= 1; x++) {
				vec3 side = vec3(float(x), float(y), float(z));
				// Jittered, but not all the way to a corner: plates keep a
				// blocky spread rather than collapsing into splinters.
				vec3 centre = side + 0.2 + 0.6 * hash3(cell + side);
				float away = length(centre - local);

				if (away < first) {
					second = first;
					first = away;
					nearest = cell + side;
				} else if (away < second) {
					second = away;
				}
			}
		}
	}

	return vec4(nearest, second - first);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 n = normalize(world_normal);

	// Plates run bigger in some stretches of a gallery and finer in others; one
	// size everywhere reads as crazed pottery rather than as rock.
	float coarseness = 1.0 + plate_variety * (fbm(world_pos * 0.55) * 2.0 - 1.0);
	vec3 broken = warp(warp(world_pos, plate_warp_scale, plate_warp), plate_fray_scale, plate_fray);
	vec4 cut = plates(broken * plate_scale * coarseness);
	float joint = 1.0 - smoothstep(0.0, joint_width, cut.w);

	// Every plate tipped its own way. Only the part of the tip that lies along the
	// surface is kept, so a plate leans without ever facing into the rock.
	vec3 tip = hash3(cut.xyz) * 2.0 - 1.0;
	tip -= n * dot(tip, n);

	// Grain, as a gradient of a finer field, bending the normal a little more.
	vec3 grain_at = world_pos * grain_scale;
	float here = fbm(grain_at);
	float step_by = 0.4;
	vec3 gradient = vec3(
		fbm(grain_at + vec3(step_by, 0.0, 0.0)) - here,
		fbm(grain_at + vec3(0.0, step_by, 0.0)) - here,
		fbm(grain_at + vec3(0.0, 0.0, step_by)) - here) / step_by;
	gradient -= n * dot(gradient, n);

	vec3 bent = normalize(n + tip * plate_relief + gradient * grain_strength);

	// A joint reads as a crease, not as a painted line: the normal dives into it.
	bent = normalize(mix(bent, normalize(bent - n * 0.9), joint * 0.6));

	// Each plate its own stone, then the grain over it, then the joints cut dark.
	float tone = mix(0.25, 1.0, hash1(cut.xyz + 3.7));
	tone = clamp(tone * (1.0 - plate_shade * 0.5) + here * grain_strength, 0.0, 1.0);

	ALBEDO = mix(albedo_dark, albedo_light, tone) * (1.0 - joint * joint_dark);
	NORMAL = normalize((VIEW_MATRIX * vec4(bent, 0.0)).xyz);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;

	// The heat below, leaking up between the plates, and the same heat catching a
	// silhouette from behind — the cartoon way a cave reads against its own glow.
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), rim_power);

	float from_below = 1.0 - smoothstep(0.0, heat_height, world_pos.y);
	float breaks_out = smoothstep(1.0 - heat_patch, 1.0, fbm(world_pos * heat_patch_scale));
	float heat = clamp(from_below + breaks_out, 0.0, 1.0);

	EMISSION = vein_color * joint * joint_glow * heat + rim_color * fresnel * rim_strength;
}

void light() {
	float ndotl = dot(NORMAL, LIGHT);
	float lit = ndotl > 0.55 ? band_lit : ndotl > 0.08 ? band_mid : band_shadow;

	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * lit;
}
