using System;
using System.IO;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class WavAudioLoader
    {
        public static AudioClip TryLoad(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                WavData wav = Parse(data);
                if (wav == null) return null;

                AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), wav.SampleCount, wav.Channels, wav.SampleRate, false);
                clip.SetData(wav.Samples, 0);
                return clip;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to load WAV: " + path + " - " + ex.Message);
                return null;
            }
        }

        private static WavData Parse(byte[] data)
        {
            if (data.Length < 44) throw new InvalidOperationException("WAV file is too small.");
            if (ReadAscii(data, 0, 4) != "RIFF" || ReadAscii(data, 8, 4) != "WAVE")
            {
                throw new InvalidOperationException("Not a RIFF/WAVE file.");
            }

            int offset = 12;
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int audioFormat = 0;
            int dataOffset = -1;
            int dataSize = 0;

            while (offset + 8 <= data.Length)
            {
                string chunkId = ReadAscii(data, offset, 4);
                int chunkSize = BitConverter.ToInt32(data, offset + 4);
                int chunkDataOffset = offset + 8;

                if (chunkId == "fmt ")
                {
                    audioFormat = BitConverter.ToInt16(data, chunkDataOffset);
                    channels = BitConverter.ToInt16(data, chunkDataOffset + 2);
                    sampleRate = BitConverter.ToInt32(data, chunkDataOffset + 4);
                    bitsPerSample = BitConverter.ToInt16(data, chunkDataOffset + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkDataOffset;
                    dataSize = chunkSize;
                    break;
                }

                offset = chunkDataOffset + chunkSize;
                if ((offset & 1) == 1) offset++;
            }

            if (audioFormat != 1) throw new InvalidOperationException("Only PCM WAV is supported.");
            if (channels <= 0 || sampleRate <= 0) throw new InvalidOperationException("Invalid WAV format.");
            if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 32)
            {
                throw new InvalidOperationException("Only 8/16/32-bit PCM WAV is supported.");
            }
            if (dataOffset < 0 || dataSize <= 0) throw new InvalidOperationException("WAV data chunk not found.");

            int bytesPerSample = bitsPerSample / 8;
            int totalSamples = dataSize / bytesPerSample;
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                int sampleOffset = dataOffset + i * bytesPerSample;
                if (bitsPerSample == 8)
                {
                    samples[i] = (data[sampleOffset] - 128) / 128f;
                }
                else if (bitsPerSample == 16)
                {
                    samples[i] = BitConverter.ToInt16(data, sampleOffset) / 32768f;
                }
                else
                {
                    samples[i] = BitConverter.ToInt32(data, sampleOffset) / 2147483648f;
                }
            }

            WavData result = new WavData();
            result.Channels = channels;
            result.SampleRate = sampleRate;
            result.SampleCount = totalSamples / channels;
            result.Samples = samples;
            return result;
        }

        private static string ReadAscii(byte[] data, int offset, int count)
        {
            return System.Text.Encoding.ASCII.GetString(data, offset, count);
        }

        private sealed class WavData
        {
            public int Channels;
            public int SampleRate;
            public int SampleCount;
            public float[] Samples;
        }
    }
}
