using System;
using System.IO;
using System.IO.Compression;

namespace PatchManager.Core.Cache
{
    /// <summary>
    /// Wrapper for <see cref="ZipArchive" /> for use with the caching system.
    /// </summary>
    /// <remarks>
    /// The archive lives in memory, and one <see cref="ZipArchive" /> is kept open over it for reads and one for
    /// writes, rather than a new one per call. Opening an archive parses its whole central directory, and closing
    /// one in update mode rewrites every entry, so a fresh archive per file made reading or writing a label of N
    /// files cost N squared. Switching between reading and writing closes the open one first, so the stream always
    /// holds a complete zip whenever the other mode or <see cref="Save" /> needs it.
    /// </remarks>
    public class Archive : IDisposable
    {
        private readonly string _path;
        private readonly MemoryStream _stream;
        private ZipArchive _reader;
        private ZipArchive _writer;
        private bool _isDisposed;

        /// <summary>
        /// Loads an archive from the given path into memory.
        /// </summary>
        /// <param name="path">Path to the archive file.</param>
        /// <param name="createNew">Whether to create a new archive if it does not exist.</param>
        public Archive(string path, bool createNew = false)
        {
            _path = path;
            _stream = new MemoryStream();

            if (!createNew)
            {
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                fileStream.CopyTo(_stream);
            }

            _stream.Seek(0, SeekOrigin.Begin);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed)
                return;
            CloseReader();
            CloseWriter();
            _stream.Dispose();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Reads the content of the given file in the archive.
        /// </summary>
        /// <param name="filePath">Path of the file relative to the root of the archive.</param>
        /// <returns>The full text of the file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist in the archive.</exception>
        public string ReadFile(string filePath)
        {
            AssertNotDisposed();
            CloseWriter();

            _reader ??= new ZipArchive(_stream, ZipArchiveMode.Read, true);
            var entry = _reader.GetEntry(filePath);
            if (entry == null)
                throw new FileNotFoundException($"File {filePath} does not exist in archive {_path}!");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Adds a file to the archive.
        /// </summary>
        /// <param name="filePath">Path of the file relative to the root of the archive.</param>
        /// <param name="content">Content of the file.</param>
        /// <exception cref="ArgumentException">Thrown when the file already exists in the archive.</exception>
        public void AddFile(string filePath, string content)
        {
            AssertNotDisposed();
            CloseReader();

            _writer ??= new ZipArchive(_stream, ZipArchiveMode.Update, true);

            if (_writer.GetEntry(filePath) != null)
                throw new ArgumentException($"File {filePath} already exists in archive {_path}!");

            var entry = _writer.CreateEntry(filePath);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        /// <summary>
        /// Saves the archive to disk.
        /// </summary>
        public void Save()
        {
            AssertNotDisposed();
            CloseWriter();
            using var fileStream = new FileStream(_path, FileMode.Create);
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.CopyTo(fileStream);
        }

        private void CloseReader()
        {
            _reader?.Dispose();
            _reader = null;
        }

        private void CloseWriter()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private void AssertNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(Archive), "The Archive object has been disposed.");
        }
    }
}