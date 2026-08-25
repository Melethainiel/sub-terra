shader_type canvas_item;

// The same trick floor_stone.gdshader uses on the temple's slabs, borrowed for the
// HUD's own parchment: a per-pixel hash darkens each fragment a little so a card's
// flat fill never reads as one dead-flat colour. Shared by every parchment Control —
// PanelContainer, Button, whatever draws the fill — since the shader only ever sees
// its own node's already-rasterized shape, it never bleeds past a rounded corner or
// a torn-edge clip onto the cave behind it.

uniform float grain_strength : hint_range(0.0, 1.0) = 0.09;

float hash1(vec2 p) {
	return fract(sin(dot(p, vec2(41.7, 289.1))) * 43758.5453);
}

void fragment() {
	// COLOR already carries the node's own texture/stylebox fill — darken it in
	// place rather than resample, so text and icons drawn by the same node (there
	// are none here; every grainy Control is background-only) would be spared too.
	float grain = hash1(floor(FRAGCOORD.xy));
	COLOR.rgb *= 1.0 - grain_strength * grain;
}
