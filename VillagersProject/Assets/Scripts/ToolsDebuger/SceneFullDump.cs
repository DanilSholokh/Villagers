using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SceneFullDump
{
    [MenuItem("Tools/Scene Debug/Dump Full Scene (to file)")]
    public static void DumpFullSceneToFile()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[SceneDump] Active scene is not valid.");
            return;
        }

        var sb = new StringBuilder(1024 * 256);
        sb.AppendLine("=== FULL SCENE DUMP ===");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Scene: {scene.name}");
        sb.AppendLine($"Path: {scene.path}");
        sb.AppendLine($"Loaded: {scene.isLoaded}");
        sb.AppendLine();

        var roots = scene.GetRootGameObjects();
        sb.AppendLine($"Root objects: {roots.Length}");
        sb.AppendLine();

        for (int i = 0; i < roots.Length; i++)
        {
            DumpTransformRecursive(roots[i].transform, sb, 0);
            sb.AppendLine();
        }

        WriteToFile(sb.ToString(), $"scene_dump_{scene.name}");
    }

    private static void DumpTransformRecursive(Transform t, StringBuilder sb, int indent)
    {
        var pad = new string(' ', indent * 2);
        var go = t.gameObject;

        sb.Append(pad).Append("- ").Append(go.name)
          .Append($" (activeSelf={go.activeSelf}, activeInHierarchy={go.activeInHierarchy})");

        // Tag/Layer
        sb.Append($" [tag={go.tag}, layer={LayerMask.LayerToName(go.layer)}]");

        // Transform базові
        sb.AppendLine();
        sb.AppendLine($"{pad}  Transform: pos={t.localPosition} rot={t.localRotation.eulerAngles} scale={t.localScale}");

        // RectTransform (якщо є)
        if (t is RectTransform rt)
        {
            sb.AppendLine($"{pad}  RectTransform: anchMin={rt.anchorMin} anchMax={rt.anchorMax} pivot={rt.pivot}");
            sb.AppendLine($"{pad}               anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} offsetMin={rt.offsetMin} offsetMax={rt.offsetMax}");

            // Runtime rect size може бути 0 в Edit, але в Play зазвичай корисно
            sb.AppendLine($"{pad}               rect(w,h)=({rt.rect.width:0.###},{rt.rect.height:0.###})");
        }

        // Перелік компонентів + ключові параметри
        DumpComponents(go, sb, indent);

        // Рекурсія
        for (int i = 0; i < t.childCount; i++)
            DumpTransformRecursive(t.GetChild(i), sb, indent + 1);
    }

    private static void DumpComponents(GameObject go, StringBuilder sb, int indent)
    {
        var pad = new string(' ', (indent * 2) + 2);
        var comps = go.GetComponents<Component>();

        sb.AppendLine($"{pad}Components ({comps.Length}):");

        foreach (var c in comps)
        {
            if (c == null)
            {
                sb.AppendLine($"{pad}- <Missing Script>");
                continue;
            }

            var type = c.GetType();
            sb.Append($"{pad}- {type.Name}");

            // Найкорисніші спец-виводи
            if (c is Canvas canvas)
            {
                sb.Append($" (enabled={canvas.enabled}, renderMode={canvas.renderMode}, sortOrder={canvas.sortingOrder}, overrideSort={canvas.overrideSorting})");
            }
            else if (c is CanvasScaler scaler)
            {
                sb.Append($" (mode={scaler.uiScaleMode}, refRes={scaler.referenceResolution}, match={scaler.matchWidthOrHeight:0.##})");
            }
            else if (c is GraphicRaycaster gr)
            {
                sb.Append($" (enabled={gr.enabled}, ignoreReversed={gr.ignoreReversedGraphics})");
            }
            else if (c is ScrollRect sr)
            {
                sb.Append($" (H={sr.horizontal}, V={sr.vertical}, move={sr.movementType}, inertia={sr.inertia}, sens={sr.scrollSensitivity:0.##})");
                sb.Append($" viewport={(sr.viewport ? GetPath(sr.viewport) : "null")}");
                sb.Append($" content={(sr.content ? GetPath(sr.content) : "null")}");
            }
            else if (c is RectMask2D rm)
            {
                sb.Append($" (padding={rm.padding}, softness={rm.softness})");
            }
            else if (c is Mask mask)
            {
                sb.Append($" (showMaskGraphic={mask.showMaskGraphic})");
            }
            else if (c is VerticalLayoutGroup vlg)
            {
                sb.Append($" (pad=({vlg.padding.left},{vlg.padding.right},{vlg.padding.top},{vlg.padding.bottom})");
                sb.Append($" spacing={vlg.spacing:0.##} align={vlg.childAlignment}");
                sb.Append($" ctrlW={vlg.childControlWidth} ctrlH={vlg.childControlHeight}");
                sb.Append($" expW={vlg.childForceExpandWidth} expH={vlg.childForceExpandHeight})");
            }
            else if (c is HorizontalLayoutGroup hlg)
            {
                sb.Append($" (pad=({hlg.padding.left},{hlg.padding.right},{hlg.padding.top},{hlg.padding.bottom})");
                sb.Append($" spacing={hlg.spacing:0.##} align={hlg.childAlignment}");
                sb.Append($" ctrlW={hlg.childControlWidth} ctrlH={hlg.childControlHeight}");
                sb.Append($" expW={hlg.childForceExpandWidth} expH={hlg.childForceExpandHeight})");
            }
            else if (c is ContentSizeFitter csf)
            {
                sb.Append($" (horiz={csf.horizontalFit}, vert={csf.verticalFit})");
            }
            else if (c is LayoutElement le)
            {
                sb.Append($" (min=({le.minWidth:0.##},{le.minHeight:0.##}) pref=({le.preferredWidth:0.##},{le.preferredHeight:0.##}) flex=({le.flexibleWidth:0.##},{le.flexibleHeight:0.##}))");
            }
            else if (c is Image img)
            {
                sb.Append($" (raycast={img.raycastTarget}, type={img.type}, color={img.color})");
            }
            else if (c is Button btn)
            {
                sb.Append($" (interactable={btn.interactable}, transition={btn.transition})");
            }
            else if (c is Toggle tog)
            {
                sb.Append($" (isOn={tog.isOn}, interactable={tog.interactable})");
            }
            else if (c is TextMeshProUGUI tmpui)
            {
                sb.Append($" (TMP_UI size={tmpui.fontSize:0.##} autoSize={tmpui.enableAutoSizing} wrap={tmpui.enableWordWrapping} raycast={tmpui.raycastTarget})");
                sb.Append($" text=\"{Trim(tmpui.text)}\"");
                sb.Append($" pref(w,h)=({tmpui.preferredWidth:0.##},{tmpui.preferredHeight:0.##})");
            }
            else if (c is TextMeshPro tmp3d)
            {
                sb.Append($" (TMP_3D size={tmp3d.fontSize:0.##}) text=\"{Trim(tmp3d.text)}\"");
            }

            sb.AppendLine();
        }
    }

    private static void WriteToFile(string text, string prefix)
    {
        var folder = Path.Combine(Application.dataPath, "../_SceneDumps");
        Directory.CreateDirectory(folder);

        var file = Path.Combine(folder, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(file, text, Encoding.UTF8);

        Debug.Log($"[SceneDump] Saved: {file}");
        EditorUtility.RevealInFinder(file);
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new StringBuilder();
        while (t != null)
        {
            if (sb.Length == 0) sb.Insert(0, t.name);
            else sb.Insert(0, t.name + "/");
            t = t.parent;
        }
        return sb.ToString();
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", "\\n").Replace("\r", "");
        return s.Length > 60 ? s.Substring(0, 60) + "..." : s;
    }
}
