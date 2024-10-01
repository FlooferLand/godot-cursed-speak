using Godot;
using Godot.Collections;

namespace Project;

[GlobalClass]
public partial class SpeechClipsResource : Resource {
    [Export] public Array<AudioStream> Clips = new();
    [Export(PropertyHint.MultilineText)] public string Words = string.Empty;
    [Export] public float StartDelay = 0f;
}
