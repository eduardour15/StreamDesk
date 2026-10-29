using System.Text;

namespace StreamDesk.Probe.Tests;

internal static class NativeVideoFixture
{
    // Small uncompressed AVI: 16:9, 2 fps, 60 seconds, changing gray frames. No binary asset or encoder dependency.
    // RIFF/AVI layout: https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference
    public static void Write(string path)
    {
        const int width = 32, height = 18, fps = 2, frames = 120, frameBytes = width * height * 3;
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        void Tag(string tag) => writer.Write(Encoding.ASCII.GetBytes(tag));
        void Values(params int[] values) { foreach (var value in values) writer.Write(value); }
        void Chunk(string tag, Action content)
        {
            Tag(tag); writer.Write(0); var start = file.Position;
            content(); var end = file.Position;
            file.Position = start - 4; writer.Write((int)(end - start)); file.Position = end;
            if ((end - start) % 2 != 0) writer.Write((byte)0);
        }
        Chunk("RIFF", () =>
        {
            Tag("AVI ");
            Chunk("LIST", () =>
            {
                Tag("hdrl");
                Chunk("avih", () => Values(1_000_000 / fps, frameBytes * fps, 0, 0x10, frames, 0, 1, frameBytes, width, height, 0, 0, 0, 0));
                Chunk("LIST", () =>
                {
                    Tag("strl");
                    Chunk("strh", () =>
                    {
                        Tag("vids"); Tag("DIB "); Values(0, 0, 0, 1, fps, 0, frames, frameBytes, -1, 0);
                        writer.Write((short)0); writer.Write((short)0); writer.Write((short)width); writer.Write((short)height);
                    });
                    Chunk("strf", () =>
                    {
                        Values(40, width, height); writer.Write((short)1); writer.Write((short)24);
                        Values(0, frameBytes, 0, 0, 0, 0);
                    });
                });
            });
            Chunk("LIST", () =>
            {
                Tag("movi");
                for (var frame = 0; frame < frames; frame++)
                    Chunk("00db", () => writer.Write(Enumerable.Repeat((byte)(178 + frame % 5), frameBytes).ToArray()));
            });
            Chunk("idx1", () =>
            {
                for (var frame = 0; frame < frames; frame++)
                { Tag("00db"); Values(0x10, 4 + frame * (frameBytes + 8), frameBytes); }
            });
        });
    }
}
