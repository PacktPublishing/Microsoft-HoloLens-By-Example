using System.IO;

public static class DeflateStreamExtension
{
    /// <summary>
    /// Polyfill for a method introduced in .NET 4 (+), copies the contents of a stream 
    /// into another stream. 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    public static void CopyTo(this Stream input, Stream output)
    {
        byte[] buffer = new byte[4 * 1024];
        int bytesRead;

        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            output.Write(buffer, 0, bytesRead);
        }
    }
}