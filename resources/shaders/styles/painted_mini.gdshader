// Miniature candidate (art direction board): a figure painted the way a tabletop
// miniature is — lighter where the paint faces up (the painter's zenithal light),
// a dark wash settling wherever the surface turns away, a thin bright edge
// highlight on every silhouette, a matt varnish over it all.
shader_type spatial;

uniform vec4 albedo : source_color = vec4(1.0);
uniform float metallic = 0.0;
uniform float roughness = 0.75;
uniform vec4 emission : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float zenithal = 0.55;
uniform float wash = 0.55;
uniform float edge_highlight = 0.35;

varying vec3 world_normal;

void vertex() {
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float up = world_normal.y * 0.5 + 0.5;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);

	vec3 paint = albedo.rgb * mix(1.0 - zenithal, 1.0 + zenithal * 0.5, up);
	// The wash: darker as the surface turns from the eye, like pigment pooled in recesses.
	paint *= mix(1.0 - wash, 1.0, smoothstep(0.05, 0.55, facing));
	// The edge highlight: a thin line of lighter paint right at the silhouette.
	paint = mix(paint, albedo.rgb * 1.6 + 0.08, edge_highlight * smoothstep(0.32, 0.12, facing) * step(0.04, facing));

	ALBEDO = paint;
	METALLIC = metallic;
	ROUGHNESS = roughness;
	EMISSION = emission.rgb;
}
