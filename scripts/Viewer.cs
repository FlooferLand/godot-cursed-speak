using Godot;
using System;
using System.Collections.Generic;

namespace Project;

/** Main node handling animation and viewing and stuff */
public partial class Viewer : Node {
    [ExportGroup("Head")]
    [Export] public Node3D Head;
    [Export] public Node3D UpperSkull;
    [Export] public Node3D Eyes;
    [Export] public Node3D Jaw;
    [Export] public OmniLight3D JawLight;
    
    [ExportGroup("Audio & animation")]
    [Export] public AudioStreamPlayer Speech;
    [Export] public AudioStreamPlayer Music;
    [Export] public Camera3D Camera;
    [Export] public AnimationPlayer AnimPlayer;
    
    [ExportGroup("Speech")]
    [Export] public SpeechClipsResource SpeechClips;
    [Export] public Label Subtitles;
    [Export] public Timer SpeechStartTimer;

    [ExportGroup("Lights")]
    [Export] public Node LightSet1;
    [Export] public Node LightSet2;
    
    private AudioEffectSpectrumAnalyzerInstance speechAnalyser;
    private int currentSpeechIndex = 0;
    private double processTime = 0;
    private double physicsTime = 0;
    private double headBobEnergy = 0;
    private string[] subtitleSplit;
    
    public const float MinDb = 30;
    public const float FreqMax = 11050;

    public override void _Ready() {
        int speechBus = AudioServer.GetBusIndex("Speech");
        speechAnalyser = (AudioEffectSpectrumAnalyzerInstance) AudioServer.GetBusEffectInstance(speechBus, 0);
        
        // Word split
        subtitleSplit = SpeechClips.Words.Split('\n');
        if (subtitleSplit.Length != SpeechClips.Clips.Count) {
            GD.PushError("Subtitle split and speech clips aren't the same size");
        }
        
        // Speech stuff
        SpeechStartTimer.Start();
        SpeechStartTimer.Timeout += () => AdvanceSpeech();
        Speech.Finished += () => {
            if (Random.Shared.Next(0, 5) == 2) {
                AnimPlayer.Play("silliness");
                return;
            }
            AdvanceSpeech();
        };
        AnimPlayer.AnimationFinished += (anim) => {
            if (anim == "silliness") {
                if (currentSpeechIndex >= SpeechClips.Clips.Count) {
                    GetTree().Quit();  // The whole speech ended
                    return;
                }
                AdvanceSpeech();
            }
        };
    }

    /** Starts the next speech segment */
    public void AdvanceSpeech() {
        if (currentSpeechIndex == SpeechClips.Clips.Count) {
            AnimPlayer.Play("silliness");  // Final silly animation
            return;
        }
        Speech.Stream = SpeechClips.Clips[currentSpeechIndex];
        Speech.Play();
        Subtitles.Text = subtitleSplit[currentSpeechIndex];
        currentSpeechIndex += 1;
    }

    public override void _Process(double delta) {
        double bpm = Music.GetPlaybackPosition() % (60 / 120.0);
        foreach (var lightSet in new List<Node> { LightSet1, LightSet2 }) {
            bool inverted = (lightSet == LightSet2);
            foreach (var node in LightSet1.GetChildren()) {
                if (node is not Light3D light) continue;
                float energy = (float) bpm * (inverted ? 0f : 1f);
                light.LightEnergy = Mathf.Lerp(light.LightEnergy, energy * 6.0f, 15f * (float)delta);
            }
        }
        processTime += delta;
    }

    public override void _PhysicsProcess(double delta) {
        // Getting the magnitude and energy
        var mag = speechAnalyser.GetMagnitudeForFrequencyRange(0, FreqMax);
        float energy = Mathf.Clamp((MinDb + Mathf.LinearToDb(mag.Length())) / MinDb, 0f, 1f);
        if (energy < 0.3f) energy = 0f;
        
        // Balls in yo jaws
        Jaw.RotationDegrees = Jaw.RotationDegrees.Lerp(Vector3.Right * (energy * 30.0f), 20.0f * (float) delta);
        UpperSkull.RotationDegrees = UpperSkull.RotationDegrees.Lerp(Vector3.Left * (energy * 4.0f), 18.0f * (float) delta);
        JawLight.LightEnergy = Mathf.Lerp(JawLight.LightEnergy, energy * 30.0f, 8f * (float) delta);
        
        // Head bob
        headBobEnergy = Mathf.Lerp(headBobEnergy, GetHeadSine() * energy, 16.0f * delta);
        Head.RotationDegrees = Head.RotationDegrees.Lerp(
            Vector3.Right * (float) headBobEnergy,
            20.0f * (float)delta
        );

        // Advancing time
        physicsTime += delta;
    }

    public double GetHeadSine() {
        const double Freq = 12.0;
        const double Amp = 4.0;
        return Mathf.Sin(physicsTime * Freq) * Amp;
    }
}
