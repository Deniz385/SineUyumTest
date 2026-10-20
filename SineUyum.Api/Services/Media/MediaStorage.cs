namespace SineUyum.Api.Services.Media
{
    // Yüklenen dosyaların saklandığı yer. Şimdilik yerel disk (Docker'da kalıcı
    // volume); ileride S3/Blob gibi bir nesne deposu bu arayüzle eklenebilir.
    public interface IMediaStorage
    {
        // Dosyalar bu kök URL altında sunulur (ör. "/media").
        string PublicPrefix { get; }

        Task SaveAsync(string relativePath, byte[] content, CancellationToken ct = default);

        // Klasördeki "keep" dışındaki tüm dosyaları siler (keep null → hepsi).
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

            // Önce geçici dosyaya yazıp sonra taşınır: yarım yazılmış dosya
            // hiçbir zaman sunulmaz.
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

        // Göreli yolu kök altında çözer; kök dışına çıkan (../) yolları reddeder.
        private string Resolve(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Geçersiz medya yolu.");
            }
            return full;
        }
    }
}
