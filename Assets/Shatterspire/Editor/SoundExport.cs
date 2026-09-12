using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Schreibt jeden Klang aus <see cref="ProceduralSound"/> als WAV heraus. Im Editor ueber
    /// SHATTERSPIRE › Export Sounds, ohne Editor per
    /// Unity.exe -batchmode -quit -executeMethod Shatterspire.Editor.SoundExport.ExportAll
    ///
    /// Warum es das gibt: Klaenge entstehen hier aus Rechnung, und Rechnung laesst sich nicht
    /// anhoeren, indem man sie liest. Der Export macht jeden Klang anhoerbar und zusaetzlich als
    /// Wellenform sichtbar, bevor jemand das Spiel startet.
    /// </summary>
    public static class SoundExport
    {
        private const string OutputDirectory = "Builds/Sounds";

        [MenuItem("SHATTERSPIRE/Export Sounds")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(OutputDirectory);
            var report = new StringBuilder();
            foreach (Sound sound in System.Enum.GetValues(typeof(Sound)))
            {
                var clip = ProceduralSound.For(sound);
                if (!clip)
                {
                    Debug.LogError($"SHATTERSPIRE Sound-Export: {sound} liefert keinen Clip.");
                    continue;
                }
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                var path = Path.Combine(OutputDirectory, sound + ".wav");
                File.WriteAllBytes(path, ToWav(samples, clip.frequency));

                var peak = 0f;
                var energy = 0.0;
                var peakIndex = 0;
                for (var i = 0; i < samples.Length; i++)
                {
                    var magnitude = Mathf.Abs(samples[i]);
                    if (magnitude > peak)
                    {
                        peak = magnitude;
                        peakIndex = i;
                    }
                    energy += samples[i] * (double)samples[i];
                }
                var rms = Mathf.Sqrt((float)(energy / Mathf.Max(1, samples.Length)));
                report.Append(sound).Append(" len=").Append((clip.samples / (float)clip.frequency).ToString("0.000"))
                    .Append("s peak=").Append(peak.ToString("0.000"))
                    .Append(" rms=").Append(rms.ToString("0.0000"))
                    .Append(" peakAt=").Append((peakIndex / (float)samples.Length).ToString("0.00"))
                    .Append('\n');
            }
            Debug.Log("SHATTERSPIRE Sound-Export nach " + OutputDirectory + ":\n" + report);
        }

        /// <summary>Kanoniales 16-Bit-PCM-WAV, mono. Reicht fuer Anhoeren und zum Zeichnen.</summary>
        private static byte[] ToWav(float[] samples, int frequency)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // mono
            writer.Write(frequency);
            writer.Write(frequency * 2);   // Bytes pro Sekunde
            writer.Write((short)2);        // Bytes pro Rahmen
            writer.Write((short)16);       // Bits je Wert
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (var sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
