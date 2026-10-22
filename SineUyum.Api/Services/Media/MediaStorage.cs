namespace SineUyum.Api.Services.Media
{
    // Where uploaded files are stored. Local disk for now (a persistent volume in
    // Docker); an object store such as S3/Blob can be added behind this interface later.
    public interface IMediaStorage
    {
        // Files are served under this root URL (e.g. "/media").
        string PublicPrefix { get; }

        Task SaveAsync(string relativePath, byte[] content, CancellationToken ct = default);

        // Deletes every file in the folder except "keep" (keep null → all of them).
        void DeleteAllExcept(string relativeDirectory, string? keepRelativePath);
    }

    public sealed class LocalMediaStorage : IMediaStorage
    {
        private readonly string _root;

        public LocalMediaStorage(string root)
        {
            _root = Path.GetFullPath(root);
            Directory.CreateDirectory(_root);
        }

        public string Root => _root;
        public string PublicPrefix => "/media";

        public async Task SaveAsync(string relativePath, byte[] content, CancellationToken ct = default)
        {
            var full = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            // Written to a temporary file first and then moved: a half-written file is
            // never served.
            var temp = full + ".tmp";
            await File.WriteAllBytesAsync(temp, content, ct);
            File.Move(temp, full, overwrite: true);
        }

        public void DeleteAllExcept(string relativeDirectory, string? keepRelativePath)
        {
            var dir = Resolve(relativeDirectory);
            if (!Directory.Exists(dir)) return;

            var keep = keepRelativePath == null ? null : Resolve(keepRelativePath);
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (!string.Equals(file, keep, StringComparison.Ordinal)) File.Delete(file);
            }
        }

        // Resolves a relative path under the root; rejects paths leaving the root (../).
        private string Resolve(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Invalid media path.");
            }
            return full;
        }
    }
}
