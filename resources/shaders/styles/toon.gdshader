// Cartoon candidate (art direction board): light in flat bands, cross-hatching in
// the shadow band, a hard rim — the comic-book look of a cel-shaded shooter.
shader_type spatial;
render_mode cull_back;

uniform vec4 albedo : source_color = vec4(1.0);
uniform vec4 emission : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float bands = 3.0;
uniform float shadow_floor = 0.28;
uniform float hatch_spacing = 5.0;
uniform float rim_strength = 0.45;

void fragment() {
	ALBEDO = albedo.rgb;
	EMISSION = emission.rgb;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0) * ATTENUATION;
	float band = floor(ndl * bands + 0.35) / bands;
	float lit = max(band, shadow_floor);

	// Ink hatching, in screen space, only where the light has fallen to its floor.
	float stripe = step(0.55, fract((FRAGCOORD.x - FRAGCOORD.y) / hatch_spacing));
	// Only under the key light: fill lights are weak enough to sit in the dark band
	// everywhere, and would stripe the lit side too.
	lit *= band <= 0.0 && LIGHT_IS_DIRECTIONAL ? mix(1.0, 0.55, stripe) : 1.0;

	float rim = step(0.62, 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0)) * step(0.1, ndl);
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim * rim_strength) / PI;
}
