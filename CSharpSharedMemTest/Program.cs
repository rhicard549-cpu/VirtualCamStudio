using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CSharpSharedMemTest
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct SharedMemHeader
        {
            public uint maxSize;
            public int width;
            public int height;
            public int stride;
            public int format;
            public int resizemode;
            public int mirrormode;
            public int timeout;
            public byte data;  // First byte of flexible array
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== C# Shared Memory Structure Test ===\n");

            unsafe
            {
                // Create a test header
                SharedMemHeader header = new SharedMemHeader
                {
                    maxSize = 66355200,
                    width = 1920,
                    height = 1080,
                    stride = 1920,
                    format = 0,
                    resizemode = 0,
                    mirrormode = 0,
                    timeout = 1000
                };

                byte* headerPtr = (byte*)&header;
                int headerSize = Marshal.SizeOf<SharedMemHeader>();

                Console.WriteLine($"SharedMemHeader size: {headerSize} bytes");
                Console.WriteLine($"Expected size: 33 bytes (8 fields * 4 bytes + 1 byte)");
                Console.WriteLine();

                Console.WriteLine("Header field values:");
                Console.WriteLine($"  maxSize:    {header.maxSize} (0x{header.maxSize:X8})");
                Console.WriteLine($"  width:      {header.width}");
                Console.WriteLine($"  height:     {header.height}");
                Console.WriteLine($"  stride:     {header.stride}");
                Console.WriteLine($"  format:     {header.format}");
                Console.WriteLine($"  resizemode: {header.resizemode}");
                Console.WriteLine($"  mirrormode: {header.mirrormode}");
                Console.WriteLine($"  timeout:    {header.timeout}");
                Console.WriteLine();

                Console.WriteLine("Header bytes (hex):");
                for (int i = 0; i < 36; i++)
                {
                    Console.Write($"{headerPtr[i]:X2} ");
                    if ((i + 1) % 16 == 0) Console.WriteLine();
                }
                Console.WriteLine();
                Console.WriteLine();

                Console.WriteLine("Data field offset:");
                Console.WriteLine($"  Offset from header start: {(byte*)&header.data - (byte*)&header} bytes");
                Console.WriteLine();

                // Test with actual frame data
                Console.WriteLine("Creating test frame buffer (first 64 bytes red)...");
                byte[] frameBuffer = new byte[1920 * 1080 * 4];
                for (int i = 0; i < frameBuffer.Length; i += 4)
                {
                    frameBuffer[i] = 255;     // R
                    frameBuffer[i + 1] = 0;   // G
                    frameBuffer[i + 2] = 0;   // B
                    frameBuffer[i + 3] = 255; // A
                }

                Console.WriteLine("Frame buffer first 64 bytes:");
                for (int i = 0; i < 64; i++)
                {
                    Console.Write($"{frameBuffer[i]:X2} ");
                    if ((i + 1) % 16 == 0) Console.WriteLine();
                }
                Console.WriteLine();

                // Write to file for comparison
                Directory.CreateDirectory(@"C:\Temp");
                using (var writer = new StreamWriter(@"C:\Temp\csharp_struct_test.txt"))
                {
                    writer.WriteLine("=== C# Shared Memory Structure Test ===");
                    writer.WriteLine();
                    writer.WriteLine($"SharedMemHeader size: {headerSize} bytes");
                    writer.WriteLine($"Expected size: 33 bytes");
                    writer.WriteLine();

                    writer.WriteLine("Header Bytes (hex):");
                    for (int i = 0; i < 36; i++)
                    {
                        writer.Write($"{headerPtr[i]:X2} ");
                        if ((i + 1) % 16 == 0) writer.WriteLine();
                    }
                    writer.WriteLine();

                    writer.WriteLine($"Data field offset: {(byte*)&header.data - (byte*)&header} bytes");
                    writer.WriteLine();

                    writer.WriteLine("Frame Buffer First 64 Bytes:");
                    for (int i = 0; i < 64; i++)
                    {
                        writer.Write($"{frameBuffer[i]:X2} ");
                        if ((i + 1) % 16 == 0) writer.WriteLine();
                    }
                    writer.WriteLine();
                }

                Console.WriteLine("Test output written to C:\\Temp\\csharp_struct_test.txt");
            }
        }
    }
}
