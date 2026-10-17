namespace Drakepg
{
    /// <summary>Synthesized sounds by name (src/ui/sfx.ts). Placeholder until the port of the synth lands.</summary>
    public static partial class Sfx
    {
        public static void Play(string name) => PlayImpl(name);

        static partial void PlayImpl(string name);
    }
}
