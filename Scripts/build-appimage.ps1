$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Gerando pacote .AppImage (Linux x86_64) diretamente no Windows..." -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (-not (Test-Path "dist\linux")) { New-Item -ItemType Directory -Path "dist\linux" | Out-Null }
if (-not (Test-Path "dist\release")) { New-Item -ItemType Directory -Path "dist\release" | Out-Null }

if (-not (Test-Path "dist\linux\AvaloniaMdiApp")) {
    Write-Host "Compilando binário Linux (linux-x64) antes de empacotar o AppImage..."
    dotnet publish -c Release -r linux-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o dist\linux
}

$csharpSource = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

public static class SquashFsAppImageBuilder
{
    private const uint BLOCK_SIZE = 131072; // 128 KB
    private const ushort BLOCK_LOG = 17;

    private class FsNode
    {
        public string Name = "";
        public bool IsDir;
        public ushort Mode;
        public byte[] Data = Array.Empty<byte>();
        public List<FsNode> Children = new List<FsNode>();
        public uint InodeNumber;
        public ushort InodeTableOffset;
        public uint DataStartBlock;
        public List<uint> BlockSizes = new List<uint>();
        public ushort DirTableOffset;
        public ushort DirFileSize;
    }

    private static byte[] CompressZlib(byte[] input, int offset, int count)
    {
        using (var ms = new MemoryStream())
        {
            // Zlib header (CMF=0x78, FLG=0x9C for default compression)
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);
            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
            {
                ds.Write(input, offset, count);
            }
            uint adler = ComputeAdler32(input, offset, count);
            ms.WriteByte((byte)((adler >> 24) & 0xFF));
            ms.WriteByte((byte)((adler >> 16) & 0xFF));
            ms.WriteByte((byte)((adler >> 8) & 0xFF));
            ms.WriteByte((byte)(adler & 0xFF));
            return ms.ToArray();
        }
    }

    private static uint ComputeAdler32(byte[] data, int offset, int count)
    {
        const uint MOD_ADLER = 65521;
        uint a = 1, b = 0;
        for (int i = 0; i < count; i++)
        {
            a = (a + data[offset + i]) % MOD_ADLER;
            b = (b + a) % MOD_ADLER;
        }
        return (b << 16) | a;
    }

    public static void Build(string linuxDistDir, string iconPath, string runtimeBinPath, string outputAppImagePath)
    {
        uint mkfsTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] iconBytes = File.ReadAllBytes(iconPath);

        string desktopContent =
            "[Desktop Entry]\n" +
            "Version=1.0\n" +
            "Type=Application\n" +
            "Name=Gerenciador MDI Portátil\n" +
            "GenericName=Workspace MDI e Fluxograma\n" +
            "Comment=Gerenciador de Janelas MDI, Planilhas, Fluxograma e Widgets\n" +
            "Exec=AvaloniaMdiApp %U\n" +
            "Icon=avaloniamdiapp\n" +
            "Terminal=false\n" +
            "Categories=Office;Utility;ProjectManagement;\n" +
            "StartupWMClass=AvaloniaMdiApp\n" +
            "StartupNotify=true\n";
        byte[] desktopBytes = Encoding.UTF8.GetBytes(desktopContent);

        string appRunContent =
            "#!/bin/bash\n" +
            "HERE=\"\$(dirname \"\$(readlink -f \"\${0}\")\")\"\n" +
            "export APPDIR=\"\${APPDIR:-\$HERE}\"\n" +
            "exec \"\$HERE/usr/bin/AvaloniaMdiApp\" \"\$@\"\n";
        byte[] appRunBytes = Encoding.UTF8.GetBytes(appRunContent);

        var root = new FsNode { Name = "", IsDir = true, Mode = 0x1FF }; // 0777
        root.Children.Add(new FsNode { Name = ".DirIcon", IsDir = false, Mode = 0x1A4, Data = iconBytes });
        root.Children.Add(new FsNode { Name = "AppRun", IsDir = false, Mode = 0x1ED, Data = appRunBytes });
        root.Children.Add(new FsNode { Name = "avaloniamdiapp.desktop", IsDir = false, Mode = 0x1A4, Data = desktopBytes });
        root.Children.Add(new FsNode { Name = "avaloniamdiapp.png", IsDir = false, Mode = 0x1A4, Data = iconBytes });

        var usrDir = new FsNode { Name = "usr", IsDir = true, Mode = 0x1ED };
        var binDir = new FsNode { Name = "bin", IsDir = true, Mode = 0x1ED };
        foreach (string filePath in Directory.GetFiles(linuxDistDir))
        {
            string fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isMainBin = string.Equals(fileName, "AvaloniaMdiApp", StringComparison.Ordinal);
            binDir.Children.Add(new FsNode
            {
                Name = fileName,
                IsDir = false,
                Mode = isMainBin ? (ushort)0x1ED : (ushort)0x1A4,
                Data = File.ReadAllBytes(filePath)
            });
        }

        var shareDir = new FsNode { Name = "share", IsDir = true, Mode = 0x1ED };
        var appsDir = new FsNode { Name = "applications", IsDir = true, Mode = 0x1ED };
        appsDir.Children.Add(new FsNode { Name = "avaloniamdiapp.desktop", IsDir = false, Mode = 0x1A4, Data = desktopBytes });

        var iconsDir = new FsNode { Name = "icons", IsDir = true, Mode = 0x1ED };
        var hicolorDir = new FsNode { Name = "hicolor", IsDir = true, Mode = 0x1ED };
        var sizeDir = new FsNode { Name = "256x256", IsDir = true, Mode = 0x1ED };
        var iconAppsDir = new FsNode { Name = "apps", IsDir = true, Mode = 0x1ED };
        iconAppsDir.Children.Add(new FsNode { Name = "avaloniamdiapp.png", IsDir = false, Mode = 0x1A4, Data = iconBytes });

        sizeDir.Children.Add(iconAppsDir);
        hicolorDir.Children.Add(sizeDir);
        iconsDir.Children.Add(hicolorDir);
        shareDir.Children.Add(appsDir);
        shareDir.Children.Add(iconsDir);

        usrDir.Children.Add(binDir);
        usrDir.Children.Add(shareDir);
        root.Children.Add(usrDir);

        // Sort children ASCIIbetically and assign post-order inode numbers (children before parents, root last)
        var allNodes = new List<FsNode>();
        uint nextInode = 1;
        void TraversePostOrder(FsNode node)
        {
            if (node.IsDir)
            {
                node.Children = node.Children.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
                foreach (var child in node.Children)
                {
                    TraversePostOrder(child);
                }
            }
            node.InodeNumber = nextInode++;
            allNodes.Add(node);
        }
        TraversePostOrder(root);

        using (var sqStream = new MemoryStream())
        using (var sqWriter = new BinaryWriter(sqStream))
        {
            // Reserve 96 bytes for Superblock
            sqWriter.Write(new byte[96]);

            // 1. Write compressed file data blocks
            foreach (var node in allNodes)
            {
                if (node.IsDir) continue;
                node.DataStartBlock = (uint)sqStream.Position;
                int remaining = node.Data.Length;
                int offset = 0;
                while (remaining > 0)
                {
                    int chunk = Math.Min((int)BLOCK_SIZE, remaining);
                    byte[] comp = CompressZlib(node.Data, offset, chunk);
                    sqWriter.Write(comp);
                    node.BlockSizes.Add((uint)comp.Length);
                    offset += chunk;
                    remaining -= chunk;
                }
            }

            // 2. Build uncompressed Inode Table and Directory Table in two passes so offsets are exact
            byte[] BuildInodeTable(Dictionary<FsNode, uint> parentMap)
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    foreach (var node in allNodes)
                    {
                        node.InodeTableOffset = (ushort)ms.Position;
                        if (node.IsDir)
                        {
                            uint nlink = (uint)(2 + node.Children.Count(c => c.IsDir));
                            uint parentInode = parentMap.TryGetValue(node, out uint p) ? p : (uint)(allNodes.Count + 1);
                            bw.Write((ushort)1); // Basic Directory Inode
                            bw.Write(node.Mode);
                            bw.Write((ushort)0); // uid idx
                            bw.Write((ushort)0); // gid idx
                            bw.Write(mkfsTime);
                            bw.Write(node.InodeNumber);
                            bw.Write((uint)0);   // start_block in dir table
                            bw.Write(nlink);
                            bw.Write(node.DirFileSize);
                            bw.Write(node.DirTableOffset);
                            bw.Write(parentInode);
                        }
                        else
                        {
                            bw.Write((ushort)2); // Basic File Inode
                            bw.Write(node.Mode);
                            bw.Write((ushort)0); // uid idx
                            bw.Write((ushort)0); // gid idx
                            bw.Write(mkfsTime);
                            bw.Write(node.InodeNumber);
                            bw.Write(node.DataStartBlock);
                            bw.Write(0xFFFFFFFFU); // no fragment
                            bw.Write((uint)0);     // offset
                            bw.Write((uint)node.Data.Length);
                            foreach (uint bsize in node.BlockSizes)
                            {
                                bw.Write(bsize);
                            }
                        }
                    }
                    return ms.ToArray();
                }
            }

            var parentLookup = new Dictionary<FsNode, uint>();
            void MapParents(FsNode dir)
            {
                foreach (var c in dir.Children)
                {
                    parentLookup[c] = dir.InodeNumber;
                    if (c.IsDir) MapParents(c);
                }
            }
            MapParents(root);

            BuildInodeTable(parentLookup);

            byte[] dirTableRaw;
            using (var dms = new MemoryStream())
            using (var dbw = new BinaryWriter(dms))
            {
                foreach (var node in allNodes)
                {
                    if (!node.IsDir) continue;
                    node.DirTableOffset = (ushort)dms.Position;
                    long startPos = dms.Position;

                    if (node.Children.Count > 0)
                    {
                        uint baseInode = node.Children.Min(c => c.InodeNumber);
                        dbw.Write((uint)(node.Children.Count - 1)); // count - 1
                        dbw.Write((uint)0); // start_block in inode table
                        dbw.Write(baseInode);

                        foreach (var child in node.Children)
                        {
                            byte[] nameBytes = Encoding.UTF8.GetBytes(child.Name);
                            dbw.Write(child.InodeTableOffset);
                            dbw.Write((short)(child.InodeNumber - baseInode));
                            dbw.Write((ushort)(child.IsDir ? 1 : 2));
                            dbw.Write((ushort)(nameBytes.Length - 1));
                            dbw.Write(nameBytes);
                        }
                    }

                    long bytesWritten = dms.Position - startPos;
                    node.DirFileSize = (ushort)(bytesWritten + 3);
                }
                dirTableRaw = dms.ToArray();
            }

            byte[] inodeTableRaw = BuildInodeTable(parentLookup);

            // Write Inode Table metadata block (compressed with Zlib)
            ulong inodeTableStart = (ulong)sqStream.Position;
            byte[] inodeTableComp = CompressZlib(inodeTableRaw, 0, inodeTableRaw.Length);
            sqWriter.Write((ushort)inodeTableComp.Length);
            sqWriter.Write(inodeTableComp);

            // Write Directory Table metadata block (compressed with Zlib)
            ulong dirTableStart = (ulong)sqStream.Position;
            byte[] dirTableComp = CompressZlib(dirTableRaw, 0, dirTableRaw.Length);
            sqWriter.Write((ushort)dirTableComp.Length);
            sqWriter.Write(dirTableComp);

            // Write ID Table (1 ID = 0 for root)
            ulong idMetaBlockPos = (ulong)sqStream.Position;
            byte[] idRaw = BitConverter.GetBytes((uint)0);
            byte[] idComp = CompressZlib(idRaw, 0, idRaw.Length);
            sqWriter.Write((ushort)idComp.Length);
            sqWriter.Write(idComp);

            ulong idTableStart = (ulong)sqStream.Position;
            sqWriter.Write(idMetaBlockPos);

            ulong bytesUsed = (ulong)sqStream.Position;

            // Pad SquashFS to 4096 bytes
            int pad = (int)((4096 - (sqStream.Position % 4096)) % 4096);
            if (pad > 0) sqWriter.Write(new byte[pad]);

            // Fill 96-byte Superblock at offset 0
            sqStream.Position = 0;
            sqWriter.Write((uint)0x73717368); // "hsqs"
            sqWriter.Write((uint)allNodes.Count);
            sqWriter.Write(mkfsTime);
            sqWriter.Write(BLOCK_SIZE);
            sqWriter.Write((uint)0); // fragments = 0
            sqWriter.Write((ushort)1); // ZLIB
            sqWriter.Write(BLOCK_LOG);
            sqWriter.Write((ushort)0x0090); // SQUASHFS_NO_FRAG | SQUASHFS_DUPLICATE
            sqWriter.Write((ushort)1); // no_ids = 1
            sqWriter.Write((ushort)4); // major = 4
            sqWriter.Write((ushort)0); // minor = 0
            ulong rootInodeRef = (ulong)root.InodeTableOffset;
            sqWriter.Write(rootInodeRef);
            sqWriter.Write(bytesUsed);
            sqWriter.Write(idTableStart);
            sqWriter.Write(0xFFFFFFFFFFFFFFFFUL); // xattr_id_table_start
            sqWriter.Write(inodeTableStart);
            sqWriter.Write(dirTableStart);
            sqWriter.Write(0xFFFFFFFFFFFFFFFFUL); // fragment_table_start
            sqWriter.Write(0xFFFFFFFFFFFFFFFFUL); // lookup_table_start

            byte[] runtimeBytes = File.ReadAllBytes(runtimeBinPath);
            using (var outFs = new FileStream(outputAppImagePath, FileMode.Create, FileAccess.Write))
            {
                outFs.Write(runtimeBytes, 0, runtimeBytes.Length);
                byte[] sqBytes = sqStream.ToArray();
                outFs.Write(sqBytes, 0, sqBytes.Length);
            }
        }
    }
}
"@

Add-Type -TypeDefinition $csharpSource -Language CSharp

$outRelease = Join-Path (Get-Location) "dist\release\GerenciadorMdiPortatil-x86_64.AppImage"
$outRoot = Join-Path (Get-Location) "GerenciadorMdiPortatil-x86_64.AppImage"
$linuxDir = Join-Path (Get-Location) "dist\linux"
$iconFile = Join-Path (Get-Location) "Assets\app-icon.png"
$runtimeFile = Join-Path (Get-Location) "Scripts\appimage-runtime-x86_64.bin"

[SquashFsAppImageBuilder]::Build($linuxDir, $iconFile, $runtimeFile, $outRelease)
Copy-Item -Path $outRelease -Destination $outRoot -Force

Write-Host "==========================================" -ForegroundColor Green
Write-Host "AppImage gerado com sucesso no Windows:" -ForegroundColor Green
Write-Host " -> $outRelease"
Write-Host " -> $outRoot"
Write-Host "==========================================" -ForegroundColor Green
