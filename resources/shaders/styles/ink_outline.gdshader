// The thick ink line of the cartoon candidate: the hull, pushed out along its
// normals and drawn from the inside, so only its silhouette shows around the model.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;

uniform vec4 ink : source_color = vec4(0.04, 0.03, 0.03, 1.0);
uniform float thickness = 0.03;

void vertex() {
	VERTEX += NORMAL * thickness;
}

void fragment() {
	ALBEDO = ink.rgb;
}
