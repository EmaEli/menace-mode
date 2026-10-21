using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace SquaddieFieldPromotions;

// Imported native AudioClips. Never tries the unsupported AudioClip.Create/PCM
// callback path; Unity Editor builds the bundle on the authoring machine.
// The runtime does NOT need Unity Editor or the source WAV files.
internal sealed class RuntimeVoiceBundle
{
    internal const string BundleFile = "sfp_john_jane_voice.sfpbundle";
    internal const string ReceiptFile = "sfp_john_jane_voice.build.json";
    private readonly object _bundle;
    internal IReadOnlyDictionary<string, AudioClip> Clips { get; }

    private RuntimeVoiceBundle(object bundle, IReadOnlyDictionary<string, AudioClip> clips)
    {
        _bundle = bundle;
        Clips = clips;
    }

    internal static RuntimeVoiceBundle Load(string modFolder, ICollection<string> requested)
    {
        var folder = Path.Combine(modFolder, "runtime_assets", "voice");
        var bundlePath = Path.Combine(folder, BundleFile);
        var receiptPath = Path.Combine(folder, ReceiptFile);
        if (!File.Exists(bundlePath) || !File.Exists(receiptPath))
            throw new FileNotFoundException("SFP Unity voice bundle is missing. Run Build-Voice-Bundle.cmd before deploy.", bundlePath);
        var receipt = JsonSerializer.Deserialize<BuildReceipt>(File.ReadAllText(receiptPath));
        if (receipt == null || receipt.schemaVersion != 2 ||
            receipt.bundleFile != BundleFile || receipt.buildTarget != "StandaloneWindows64" ||
            receipt.clips == null || receipt.clips.Count < requested.Count)
            throw new InvalidDataException("Incompatible or incomplete SFP voice receipt.");
        if (!string.Equals(receipt.unityVersion, Application.unityVersion, StringComparison.Ordinal))
            throw new InvalidDataException("Voice bundle Unity " + receipt.unityVersion +
                " does not match MENACE " + Application.unityVersion);
        if (!string.Equals(Sha256(bundlePath), receipt.bundleSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SFP voice bundle SHA-256 does not match receipt.");
        var index = new Dictionary<string, ClipReceipt>(StringComparer.Ordinal);
        foreach (var clip in receipt.clips)
        {
            if (clip?.assetName == null || !index.TryAdd(clip.assetName, clip))
                throw new InvalidDataException("Voice bundle receipt has a missing or duplicate asset name.");
        }
        foreach (var name in requested)
        {
            if (!index.TryGetValue(name, out var meta) || meta.frames <= 0 ||
                meta.channels <= 0 || meta.sampleRate <= 0 || meta.editorVerifiedPeak <= 0)
                throw new InvalidDataException("Voice bundle receipt missing or corrupt asset: " + name);
        }

        // Legacy Generic Leaders uses UnityEngine.Il2CppAssetBundleManager, NOT
        // the older assumed MelonLoader namespace. It is an optional MelonLoader
        // utility assembly, not a dependency we impose on SFP players.
        // If unavailable, use the game's IL2CPP-generated native AssetBundle
        // proxy from UnityEngine.AssetBundleModule (already in MENACE).
        LegacyBundleBridge bridge = null;
        AssetBundle nativeBundle = null;
        string bridgeFailure = null;
        try
        {
            bridge = TryOpenLegacyBridge(bundlePath, receipt.crc);
        }
        catch (Exception exception)
        {
            bridgeFailure = exception.GetType().Name + ": " + exception.Message;
        }
        if (bridge == null)
        {
            try
            {
                nativeBundle = AssetBundle.LoadFromFile(bundlePath, receipt.crc);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Unity IL2CPP AssetBundle.LoadFromFile failed: " + exception.GetType().Name +
                    ": " + exception.Message + (bridgeFailure == null ? "" :
                    "; optional bridge also failed: " + bridgeFailure), exception);
            }
            if (nativeBundle == null)
                throw new InvalidOperationException(
                    "MENACE Unity AssetBundle.LoadFromFile returned null (CRC or Unity format mismatch)." +
                    (bridgeFailure == null ? "" : " Optional bridge: " + bridgeFailure));
        }
        try
        {
            var clipType = Il2CppType.Of<AudioClip>();
            var clipTypePtr = IL2CPP.Il2CppObjectBaseToPtr(clipType);
            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var name in requested)
            {
                IntPtr pointer;
                if (bridge != null)
                {
                    pointer = bridge.LoadAudioClip(name, clipTypePtr);
                }
                else
                {
                    var nativeAsset = nativeBundle.LoadAsset(name, clipType);
                    pointer = nativeAsset == null ? IntPtr.Zero : IL2CPP.Il2CppObjectBaseToPtr(nativeAsset);
                }
                if (pointer == IntPtr.Zero)
                    throw new InvalidDataException("Native SFP voice bundle has no AudioClip " + name);
                var clip = new AudioClip(pointer);
                var expected = index[name];
                if (clip.samples != expected.frames || clip.channels != expected.channels ||
                    clip.frequency != expected.sampleRate)
                    throw new InvalidDataException("Voice AudioClip dimensions differ: " + name);
                if (clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();
                if (clip.loadState != AudioDataLoadState.Loaded)
                    throw new InvalidOperationException("Unity failed to load audio: " + name);
                clip.name = name;
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                clips.Add(name, clip);
            }
            GC.KeepAlive(clipType);
            return new RuntimeVoiceBundle((object)bridge ?? nativeBundle, clips);
        }
        catch
        {
            // No voice bank has been installed yet; free only a failed load.
            try
            {
                if (bridge != null) bridge.Unload();
                else nativeBundle?.Unload(true);
            }
            catch { }
            throw;
        }
    }

    private sealed class LegacyBundleBridge
    {
        private readonly object _bundle;
        private readonly MethodInfo _load;
        private readonly MethodInfo _unload;

        internal LegacyBundleBridge(object bundle, MethodInfo load, MethodInfo unload)
        {
            _bundle = bundle;
            _load = load;
            _unload = unload;
        }

        internal IntPtr LoadAudioClip(string name, IntPtr clipType)
        {
            var raw = _load.Invoke(_bundle, new object[] { name, clipType });
            return raw is IntPtr pointer ? pointer : IntPtr.Zero;
        }

        internal void Unload() => _unload.Invoke(_bundle, new object[] { true });
    }

    private static LegacyBundleBridge TryOpenLegacyBridge(string path, uint crc)
    {
        Type manager = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            manager = assembly.GetType("UnityEngine.Il2CppAssetBundleManager", false);
            if (manager != null) break;
        }
        if (manager == null)
        {
            try
            {
                var assembly = Assembly.Load("UnityEngine.Il2CppAssetBundleManager");
                manager = assembly.GetType("UnityEngine.Il2CppAssetBundleManager", false);
            }
            catch (System.IO.FileNotFoundException) { }
            catch (System.IO.FileLoadException) { }
            catch (BadImageFormatException) { }
        }
        if (manager == null)
            return null; // Normal on Jiangyu-only installs; use Unity native path.

        var open = manager.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(uint) }, null);
        if (open == null)
            return null;
        var bundle = open.Invoke(null, new object[] { path, crc });
        if (bundle == null)
            return null;
        var type = bundle.GetType();
        var load = type.GetMethod("LoadAsset", BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(IntPtr) }, null);
        var unload = type.GetMethod("Unload", BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);
        if (load == null || unload == null)
            throw new MissingMemberException("Optional UnityEngine.Il2CppAssetBundleManager has incompatible bundle API");
        return new LegacyBundleBridge(bundle, load, unload);
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private sealed class BuildReceipt
    {
        public int schemaVersion { get; set; }
        public string unityVersion { get; set; }
        public string buildTarget { get; set; }
        public string bundleFile { get; set; }
        public string bundleSha256 { get; set; }
        public uint crc { get; set; }
        public List<ClipReceipt> clips { get; set; }
    }
    private sealed class ClipReceipt
    {
        public string assetName { get; set; }
        public int frames { get; set; }
        public int channels { get; set; }
        public int sampleRate { get; set; }
        public float editorVerifiedPeak { get; set; }
    }
}
