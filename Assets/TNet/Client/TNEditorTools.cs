using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TNet
{
	/// <summary>
	/// Tools that work in the Unity Editor.
	/// </summary>

	static public class EditorTools
	{
		/// <summary>
		/// Re-import the asset located at the specified path.
		/// </summary>

		static public void Reimport (string path)
		{
#if UNITY_EDITOR
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
#endif
		}

		/// <summary>
		/// Given the texture asset's path, return its pixels.
		/// </summary>

		static public Color[] GetPixels (string path, out int width, out int height)
		{
			width = 0;
			height = 0;
#if UNITY_EDITOR
			if (string.IsNullOrEmpty(path)) return null;
			var ti = AssetImporter.GetAtPath(path) as TextureImporter;
			if (ti == null) return null;

			var settings = new TextureImporterSettings();
			ti.ReadTextureSettings(settings);

			var readable = settings.readable;
			var npotScale = settings.npotScale;
			var platform = ti.GetDefaultPlatformTextureSettings();
			var textureFormat = platform.format;
			var textureType = ti.textureType;
			var compression = ti.textureCompression;

			if (!readable || textureType != TextureImporterType.Default || npotScale != TextureImporterNPOTScale.None || compression != TextureImporterCompression.Uncompressed)
			{
				ti.textureCompression = TextureImporterCompression.Uncompressed;
				platform.format = TextureImporterFormat.RGBA32;

				settings.readable = true;
				settings.npotScale = TextureImporterNPOTScale.None;
				settings.ApplyTextureType(TextureImporterType.Default);

				ti.SetTextureSettings(settings);
				Reimport(path);

				var tex = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
				var pixels = tex.GetPixels();
				width = tex.width;
				height = tex.height;

				settings.readable = readable;
				settings.npotScale = npotScale;
				settings.ApplyTextureType(textureType);

				ti.textureCompression = compression;
				platform.format = textureFormat;

				ti.SetTextureSettings(settings);
				Reimport(path);
				return pixels;
			}

			var t = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
			width = t.width;
			height = t.height;
			return (t != null) ? t.GetPixels() : null;
#else
			return null;
#endif
		}

		/// <summary>
		/// Given the texture asset's path, return its pixels.
		/// </summary>

		static public Color32[] GetPixels32 (string path, out int width, out int height)
		{
			width = 0;
			height = 0;
#if UNITY_EDITOR
			if (string.IsNullOrEmpty(path)) return null;
			var ti = AssetImporter.GetAtPath(path) as TextureImporter;
			if (ti == null) return null;

			var settings = new TextureImporterSettings();
			ti.ReadTextureSettings(settings);

			var readable = settings.readable;
			var npotScale = settings.npotScale;
			var platform = ti.GetDefaultPlatformTextureSettings();
			var textureFormat = platform.format;
			var textureType = ti.textureType;
			var compression = ti.textureCompression;

			if (!readable || textureType != TextureImporterType.Default || npotScale != TextureImporterNPOTScale.None || compression != TextureImporterCompression.Uncompressed)
			{
				ti.textureCompression = TextureImporterCompression.Uncompressed;
				platform.format = TextureImporterFormat.RGBA32;

				settings.readable = true;
				settings.npotScale = TextureImporterNPOTScale.None;
				settings.ApplyTextureType(TextureImporterType.Default);

				ti.SetTextureSettings(settings);
				Reimport(path);

				var tex = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
				var pixels = tex.GetPixels32();
				width = tex.width;
				height = tex.height;

				settings.readable = readable;
				settings.npotScale = npotScale;
				settings.ApplyTextureType(textureType);

				ti.textureCompression = compression;
				platform.format = textureFormat;

				ti.SetTextureSettings(settings);
				Reimport(path);
				return pixels;
			}

			var t = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
			width = t.width;
			height = t.height;
			return (t != null) ? t.GetPixels32() : null;
#else
			return null;
#endif
		}

		/// <summary>
		/// Get the specified texture's pixels. If used in the Unity Editor, it will force the texture to be readable and re-import it as necessary.
		/// </summary>

		static public Color[] GetPixels (this Texture2D tex, out int width, out int height)
		{
			width = 0;
			height = 0;
			if (!tex) return null;
#if UNITY_EDITOR
			var path = AssetDatabase.GetAssetPath(tex);
			if (string.IsNullOrEmpty(path)) return tex.GetPixels();
			return GetPixels(path, out width, out height);
#else
			width = tex.width;
			height = tex.height;
			return tex.GetPixels();
#endif
		}

		/// <summary>
		/// Get the specified texture's pixels. If used in the Unity Editor, it will force the texture to be readable and re-import it as necessary.
		/// </summary>

		static public Color32[] GetPixels32 (this Texture2D tex, out int width, out int height)
		{
			width = 0;
			height = 0;
			if (!tex) return null;
#if UNITY_EDITOR
			var path = AssetDatabase.GetAssetPath(tex);
			if (string.IsNullOrEmpty(path)) return tex.GetPixels32();
			return GetPixels32(path, out width, out height);
#else
			width = tex.width;
			height = tex.height;
			return tex.GetPixels32();
#endif
		}

		/// <summary>
		/// Determine if this is a linear texture. Only possible properly at edit time.
		/// </summary>

		static public bool IsLinear (this Texture2D tex)
		{
#if UNITY_EDITOR
			var path = AssetDatabase.GetAssetPath(tex);
			if (string.IsNullOrEmpty(path)) return false;
			var ti = AssetImporter.GetAtPath(path) as TextureImporter;
			if (ti == null) return false;
			var settings = new TextureImporterSettings();
			ti.ReadTextureSettings(settings);
			return !ti.sRGBTexture;
#else
			var name = tex.name;
			if (name.Contains("Mask") || name.Contains("Normal") || name.Contains("Linear")) return true;
			return false;
#endif
		}
	}
}