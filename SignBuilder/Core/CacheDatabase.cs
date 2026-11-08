using System;
using System.IO;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Common;
using UnityEngine;
using SignBuilderSA.Geometry;
using SignBuilderSA.Assemblies;
using SignBuilderSA.Tools;

namespace SignBuilderSA.Core
{
    /// <summary>
    /// A mod-owned asset database rooted at ModsData/SignBuilderSA/Cache, so generated meshes and
    /// materials never land in the player's ImportedData folder. Mirrors how the game defines its
    /// own "User" database (a FileSystemDataSource behind an IAssetDatabaseDescriptor).
    /// </summary>
    public readonly struct SignBuilderCache : IAssetDatabaseDescriptor<SignBuilderCache>, IEquatable<SignBuilderCache>
    {
        public static string RootPath => Path.Combine(Application.persistentDataPath, "ModsData", "SignBuilderSA", "Cache");

        public DlcId dlcId => DlcId.Invalid;
        public bool canWriteSettings => false;
        public string name => "SignBuilderSACache";
        public IAssetFactory assetFactory => DefaultAssetFactory.instance;

        public IDataSourceProvider dataSourceProvider
        {
            get
            {
                Directory.CreateDirectory(RootPath);
                return new FileSystemDataSource(name, RootPath, assetFactory, 0L);
            }
        }

        public bool Equals(SignBuilderCache other) => true;
        public override bool Equals(object obj) => obj is SignBuilderCache;
        public override int GetHashCode() => GetType().GetHashCode();
    }
}
